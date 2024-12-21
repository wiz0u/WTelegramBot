using System.Data.Common;
using System.Reflection;
using TL;
using Chat = WTelegram.Types.Chat;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

/// <summary>A client offering the Telegram Bot API via the more powerful Client API</summary>
public partial class Bot : IDisposable
{
	/// <summary>This gives you access to the underlying Client API</summary>
	public Client Client { get; }
	/// <summary>The underlying UpdateManager (can be useful as Peer resolver for Client API calls)</summary>
	public readonly UpdateManager Manager;
	/// <inheritdoc/>
	public long BotId { get; }

	private Func<Update, Task>? _onUpdate;
	private Func<Message, UpdateType, Task>? _onMessage;
	CancellationTokenSource? _receivingEvents;
	/// <summary>Handler to be called when there is an incoming update</summary>
	public event Func<Update, Task>? OnUpdate { add { _onUpdate += value; StartEventReceiving(); } remove { _onUpdate -= value; StopEventReceiving(); } }
	/// <summary>Handler to be called when there is an incoming message or edited message</summary>
	public event Func<Message, UpdateType, Task>? OnMessage { add { _onMessage += value; StartEventReceiving(); } remove { _onMessage -= value; StopEventReceiving(); } }
	/// <summary>Handler to be called when there was a polling error or an exception in your handlers</summary>
	public event Func<Exception, Telegram.Bot.Polling.HandleErrorSource, Task>? OnError;

	/// <summary>Generate Unknown Updates for all raw TL Updates that usually would have been silently ignored by Bot API (see <see cref="Update.TLUpdate"/>)</summary>
	public bool WantUnknownTLUpdates { get; set; }

	internal const long ZERO_CHANNEL_ID = -1000000000000;
	static readonly User GroupAnonymousBot = new() { Id = 1087968824, Username = "GroupAnonymousBot", FirstName = "Group", IsBot = true };
	static readonly User ServiceNotification = new() { Id = 777000, FirstName = "Telegram" };

	/// <summary>Task launched from constructor</summary>
	protected readonly Task<Task> _initTask;
	private readonly Database _database;
	internal readonly Database.CachedTable<Chat> _chats;
	internal readonly Database.CachedTable<User> _users;
	private readonly BotCollectorPeer _collector;
	private readonly SemaphoreSlim _pendingCounter = new(0);
	/// <summary>Cache StickerSet ID => Name</summary>
	protected Dictionary<long, string> StickerSetNames = [];
	/// <summary>Cache used by <see cref="GetMessage"/></summary>
	protected Dictionary<(long peerId, int msgId), Message?> CachedMessages = [];
	/// <summary>The HttpClient used for HTTP mode, or null for TCP mode.</summary>
	protected readonly HttpClient? _httpClient;
	private const int DefaultAllowedUpdates = 0b111_1110_0101_1111_1111_1110; /// all <see cref="UpdateType"/> except Unknown=0, ChatMember=13, MessageReaction=15, MessageReactionCount=16
	private bool NotAllowed(UpdateType updateType) => (_state.AllowedUpdates & (1 << (int)updateType)) == 0;
	private readonly State _state = new();
	internal class State
	{
		public List<Update> PendingUpdates = [];
		public byte[]? SessionData;
		public int LastUpdateId;
		public int AllowedUpdates = -1; // if GetUpdates is never called, OnMessage/OnUpdate should receive all updates
	}

	/// <summary>Create a new <see cref="Bot"/> instance in TCP mode.</summary>
	/// <param name="botToken">The bot token</param>
	/// <param name="apiId">API id (see https://my.telegram.org/apps)</param>
	/// <param name="apiHash">API hash (see https://my.telegram.org/apps)</param>
	/// <param name="dbConnection">DB connection for storage and later resume</param>
	/// <param name="sqlCommands">Template for SQL strings (auto-detect by default)</param>
	public Bot(string botToken, int apiId, string apiHash, DbConnection dbConnection, SqlCommands sqlCommands = SqlCommands.Detect)
		: this(botToken, apiId, apiHash, dbConnection, null, sqlCommands) { }

	/// <summary>Create a new <see cref="Bot"/> instance.</summary>
	/// <param name="botToken">The bot token</param>
	/// <param name="apiId">API id (see https://my.telegram.org/apps)</param>
	/// <param name="apiHash">API hash (see https://my.telegram.org/apps)</param>
	/// <param name="dbConnection">DB connection for storage and later resume</param>
	/// <param name="httpClient">An <see cref="HttpClient"/>, or null for TCP mode</param>
	/// <param name="sqlCommands">Template for SQL strings (auto-detect by default)</param>
	public Bot(string botToken, int apiId, string apiHash, DbConnection dbConnection, HttpClient? httpClient, SqlCommands sqlCommands = SqlCommands.Detect)
		: this(what => what switch
		{
			"api_id" => apiId.ToString(),
			"api_hash" => apiHash,
			"bot_token" => botToken,
			"device_model" => "server",
			_ => null
		},
		dbConnection, sqlCommands == SqlCommands.Detect ? null : Database.DefaultSqlCommands[(int)sqlCommands], httpClient)
	{ }

	/// <summary>Create a new <see cref="Bot"/> instance.</summary>
	/// <param name="configProvider">Configuration callback ("MTProxy" can be used for connection)</param>
	/// <param name="dbConnection">DB connection for storage and later resume</param>
	/// <param name="sqlCommands">SQL queries for your specific DB engine (null for auto-detect)</param>
	/// <param name="httpClient">An <see cref="HttpClient"/>, or null for TCP mode</param>
	public Bot(Func<string, string?> configProvider, DbConnection dbConnection, string[]? sqlCommands = null, HttpClient? httpClient = null)
	{
		var botToken = configProvider("bot_token") ?? throw new ArgumentNullException(nameof(configProvider), "bot_token is unset");
		BotId = long.Parse(botToken[0..botToken.IndexOf(':')]);
		sqlCommands ??= Database.DefaultSqlCommands[(int)Database.DetectType(dbConnection)];
		_collector = new(this);
		var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		Helpers.Log(1, $"WTelegramBot {version} using {dbConnection.GetType().Name} {dbConnection.DataSource}");
		_database = new Database(dbConnection, sqlCommands, _state);
		_database.GetTables(out _users, out _chats);
		Client = new Client(configProvider, _database.LoadSessionState());
		if ((_httpClient = httpClient) != null)
			Client.HttpMode(httpClient);
		Client.MTProxyUrl = configProvider("MTProxy");
		Manager = Client.WithUpdateManager(OnTLUpdate, _database.LoadMBoxStates(), _collector);
		_initTask = new Task<Task>(() => InitLogin(botToken));
	}

	private async Task InitLogin(string botToken)
	{
		if (Client.UserId != BotId)
		{
			var me = await Client.LoginBotIfNeeded(botToken);
			_collector.Collect([me]);
		}
		else
			await Client.ConnectAsync(true);
		try
		{
			foreach (var (id, update) in _database.LoadTLUpdates().ToList())
			{
				var botUpdate = await MakeUpdate(update);
				botUpdate ??= new Update { TLUpdate = update };
				botUpdate.Id = id;
				_state.PendingUpdates.Add(botUpdate);
			}
			var bot = User(BotId)!;
			Manager.Log(1, $"Connected as @{bot.Username} ({bot.Id}) | LastUpdateId = {_state.LastUpdateId} | {_state.PendingUpdates.Count} pending updates");

			if (_state.PendingUpdates.Count != 0)
			{
				_state.LastUpdateId = Math.Max(_state.LastUpdateId, _state.PendingUpdates[^1].Id);
				_pendingCounter.Release();
			}
		}
		catch { } // we can't reconstruct the PendingUpdates, too bad ¯\_(ツ)_/¯
	}

	async Task InitComplete()
	{
		if (_initTask.Status == TaskStatus.Created)
			try { _initTask.Start(); }
			catch (InvalidOperationException) { } // already started
		await await _initTask;
	}

	/// <summary>You must call Dispose to properly save state, close connection and dispose resources</summary>
	public void Dispose()
	{
		_receivingEvents?.Cancel();
		Client.Dispose();
		SaveState();
		_database.Dispose();
		_receivingEvents?.Dispose();
		_receivingEvents = null;
		GC.SuppressFinalize(this);
	}

	/// <summary>Save current state to database</summary>
	public void SaveState()
	{
		_database.SaveMBoxStates(Manager.State);
		_database.SaveSessionState();
		lock (_state.PendingUpdates)
			_database.SaveTLUpdates(_state.PendingUpdates);
	}

	/// <summary>Will remove all received updates</summary>
	public async Task DropPendingUpdates()
	{
		await GetUpdates(int.MaxValue, 1, 0, null);
	}

	/// <summary>Use this method to receive incoming updates using <a href="https://en.wikipedia.org/wiki/Push_technology#Long_polling">long polling</a></summary>
	/// <param name="offset">Identifier of the first update to be returned, typically the Id of the last update you handled <u>plus one</u>. Negative values are offset from the end of the pending updates queue</param>
	/// <param name="limit">Limits the number of updates to be retrieved (1-100)</param>
	/// <param name="timeout">Timeout in seconds for long polling. 0 to return immediately. Recommended value: 25</param>
	/// <param name="allowedUpdates">A list of the <see cref="UpdateType"/> you want your bot to receive. Specify an empty list to receive
	/// all update types except <see cref="UpdateType.ChatMember"/>. If null, the previous setting will be used.</param>
	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation</param>
	/// <remarks>In order to avoid getting duplicate updates, recalculate <paramref name="offset"/> after each server response</remarks>
	/// <returns>An Array of <see cref="Update"/> objects is returned.</returns>
	public async Task<Update[]> GetUpdates(int offset = 0, int limit = 100, int timeout = 0, IEnumerable<UpdateType>? allowedUpdates = null, CancellationToken cancellationToken = default)
	{
		if (offset is -1 or int.MaxValue) // drop updates => stop eventual resync process
			await Manager.StopResync();
		if (allowedUpdates != null)
		{
			var bitset = allowedUpdates.Aggregate(0, (bs, ut) => bs | (1 << (int)ut));
			_state.AllowedUpdates = bitset == 0 ? DefaultAllowedUpdates : bitset < 0 ? -1 : bitset;
		}
		Update[] result;
		limit = limit < 1 ? 1 : limit > 100 ? 100 : limit;
		while (_httpClient != null && timeout > 25)
		{
			result = await GetUpdates(offset, limit, 25, null, cancellationToken);
			if (result.Length != 0) return result;
			timeout -= 25;
		}
		timeout *= 1000;
		bool endHttpWait = false;
		for (int maxWait = 0; ; maxWait = timeout)
		{
			if (_httpClient != null)
			{
				// - HttpWait is buggy atm, except the default null behaviour which waits up to 25 seconds.
				// - For other timeout we need to call it once with wait_after=0 to obtain existing unfetched updates, then wait_after=timeout to wait for more
				// - HttpWait may produce ClientAPI updates that do not translate to BotAPI updates, so StartHttpWait continues the loop
				// - HttpWait is not cancellable, so in case we obtain some update in-between (via delayed GetDiff), we mark to end the loop
				if (timeout == 0)
					await Client.HttpWait(new());
				else if (maxWait != 0)
					_ = StartHttpWait(maxWait);
			}
			if (await _pendingCounter.WaitAsync(maxWait, cancellationToken))
				try
				{
					if (_httpClient != null && maxWait != 0) endHttpWait = true;
					lock (_state.PendingUpdates)
					{
						if (offset < 0)
							_state.PendingUpdates.RemoveRange(0, Math.Max(_state.PendingUpdates.Count + offset, 0));
						else if (_state.PendingUpdates.FindIndex(u => u.Id >= offset) is >= 0 and int index)
							_state.PendingUpdates.RemoveRange(0, index);
						else
							_state.PendingUpdates.Clear();
						if (_state.PendingUpdates.Count != 0)
						{
							_pendingCounter.Release();
							result = new Update[Math.Min(limit, _state.PendingUpdates.Count)];
							_state.PendingUpdates.CopyTo(0, result, 0, result.Length);
							return result;
						}
					}
				}
				finally
				{
					SaveState();
				}
			if (maxWait == timeout) break;
		}
		return [];

		async Task? StartHttpWait(int milliseconds)
		{
			var endTime = DateTime.UtcNow.AddMilliseconds(milliseconds);
			while (!endHttpWait && milliseconds > 0)
			{
				await Client.HttpWait(milliseconds == 25000 ? null : new() { max_delay = 0, wait_after = milliseconds, max_wait = 0 });
				milliseconds = (int)(endTime - DateTime.UtcNow).TotalMilliseconds;
			}
		}
	}

	private async Task OnTLUpdate(TL.Update update)
	{
		try { await InitComplete(); } catch { }
		var botUpdate = await MakeUpdate(update);
		if (botUpdate == null && WantUnknownTLUpdates)
			botUpdate = new Update { TLUpdate = update };
		if (botUpdate != null)
		{
			botUpdate.Id = ++_state.LastUpdateId;
			bool wasEmpty;
			lock (_state.PendingUpdates)
			{
				wasEmpty = _state.PendingUpdates.Count == 0;
				_state.PendingUpdates.Add(botUpdate);
			}
			if (wasEmpty) _pendingCounter.Release();
			if (_onUpdate != null || _onMessage != null)
			{
				try
				{
					var task = _onMessage == null ? _onUpdate?.Invoke(botUpdate) : botUpdate switch
					{
						{ Message: { } m } => _onMessage?.Invoke((Message)m, UpdateType.Message),
						{ EditedMessage: { } em } => _onMessage?.Invoke((Message)em, UpdateType.EditedMessage),
						{ ChannelPost: { } cp } => _onMessage?.Invoke((Message)cp, UpdateType.ChannelPost),
						{ EditedChannelPost: { } ecp } => _onMessage?.Invoke((Message)ecp, UpdateType.EditedChannelPost),
						{ BusinessMessage: { } bm } => _onMessage?.Invoke((Message)bm, UpdateType.BusinessMessage),
						{ EditedBusinessMessage: { } ebm } => _onMessage?.Invoke((Message)ebm, UpdateType.EditedBusinessMessage),
						_ => _onUpdate?.Invoke(botUpdate) // if OnMessage is set, we call OnUpdate only for non-message updates
					};
					if (task != null) await task.ConfigureAwait(true);
				}
				catch (Exception ex)
				{
					var task = OnError?.Invoke(ex, Telegram.Bot.Polling.HandleErrorSource.HandleUpdateError);
					if (task != null) await task.ConfigureAwait(true);
					else System.Diagnostics.Trace.WriteLine(ex); // fallback logging if OnError is unset
				}
				return;
			}
		}
	}

	/// <summary>Obtain a InputUser from username, or null if resolve failed</summary>
	public async Task<InputUser?> InputUser(string username)
	{
		username = username.TrimStart('@');
		lock (_users)
			if (_users.SearchCache(user => user.Username?.Equals(username, StringComparison.OrdinalIgnoreCase) == true) is User user)
				return user;
		try
		{
			var resolved = await Client.Contacts_ResolveUsername(username);
			if (resolved.User is { } resolvedUser)
				lock (_users)
					return _users[resolvedUser.id] = resolvedUser.User();
		}
		catch (RpcException) { }
		return null;
	}

	/// <summary>Obtain a InputUser for this user (useful with Client API calls)</summary>
	public InputUser InputUser(long userId) => User(userId) ?? new InputUser(userId, 0);
	/// <summary>Obtain a InputPeerUser for this user (useful with Client API calls)</summary>
	public InputPeerUser InputPeerUser(long userId) => User(userId) ?? new InputPeerUser(userId, 0);

	/// <summary>return User if found in known users (DB), or null</summary>
	public User? User(long userId)
	{
		if (userId == 0) return null;
		lock (_users)
			if (_users.TryGetValue(userId, out var user))
				return user;
		return null;
	}

	/// <summary>Obtain a InputChannel for this chat (useful with Client API calls)</summary><remarks>May throw exception if chat is unknown</remarks>
	public async Task<InputChannel> InputChannel(ChatId chatId)
	{
		await InitComplete();
		if (chatId.Identifier is long id && id >= ZERO_CHANNEL_ID)
			throw new WTException("Bad Request: method is available for supergroup and channel chats only");
		return (InputPeerChannel)await InputPeerChat(chatId);
	}

	/// <summary>return Chat if found in known chats (DB), or null</summary>
	public Chat? Chat(long chatId)
	{
		if (chatId < 0) chatId = chatId > ZERO_CHANNEL_ID ? -chatId : ZERO_CHANNEL_ID - chatId;
		lock (_chats)
			if (_chats.TryGetValue(chatId, out var chat))
				return chat;
		return null;
	}

	/// <summary>Obtain a InputPeerChat for this chat (useful with Client API calls)</summary><remarks>May throw exception if chat is unknown</remarks>
	public async Task<InputPeer> InputPeerChat(ChatId chatId)
	{
		await InitComplete();
		if (chatId.Identifier is long id)
			if (id >= 0)
				return InputPeerUser(id);
			else if (id > ZERO_CHANNEL_ID)
				return new InputPeerChat(-id);
			else
			{
				if (Chat(id = ZERO_CHANNEL_ID - id) is { } chat)
					return chat;
				var chats = await Client.Channels_GetChannels(new InputChannel(id, 0));
				if (chats.chats.TryGetValue(id, out var chatBase))
				{
					lock (_chats)
						_chats[id] = chatBase.Chat();
					return chatBase;
				}
				throw new WTException($"Bad Request: Chat not found {chatId}");
			}
		else
		{
			var username = chatId.Username?.TrimStart('@');
			lock (_chats)
				if (_chats.SearchCache(chat => chat.Username?.Equals(username, StringComparison.OrdinalIgnoreCase) == true) is Chat chat)
					return chat;
			var resolved = await Client.Contacts_ResolveUsername(username);
			if (resolved.Chat is { } chatBase)
				lock (_chats)
					return _chats[chatBase.ID] = chatBase.Chat();
			throw new WTException($"Bad Request: Chat not found {chatId}");
		}
	}

	private async Task<InputBotInlineMessageIDBase> ParseInlineMsgID(string inlineMessageId)
	{
		await InitComplete();
		return inlineMessageId.ParseInlineMsgID();
	}

	/// <summary>Free up some memory by clearing internal caches that can be reconstructed automatically<para>Call this periodically for heavily used bots if you feel too much memory is used by TelegramBotClient</para></summary>
	public void ClearCaches()
	{
		lock (_users) _users.ClearCache();
		lock (_chats) _chats.ClearCache();
		lock (StickerSetNames) StickerSetNames.Clear();
		lock (CachedMessages) CachedMessages.Clear();
	}

	private void StartEventReceiving()
	{
		if (_httpClient == null) return;
		lock (_initTask)
		{
			if (_receivingEvents != null) return;
			_receivingEvents = new CancellationTokenSource();
		}
		_ = StartReceiving();
	}

	private void StopEventReceiving()
	{
		lock (_initTask)
		{
			if (_receivingEvents == null || _onUpdate != null || _onMessage != null) return;
			_receivingEvents?.Cancel();
			_receivingEvents = null;
		}
	}

	private async Task StartReceiving() // HTTP mode: poll for updates (events are called in OnTLUpdate)
	{
		_state.AllowedUpdates = -1;
		int messageOffset = 0;
		while (!_receivingEvents!.IsCancellationRequested)
		{
			try
			{
				var updates = await GetUpdates(messageOffset, 100, 25, null, _receivingEvents.Token).ConfigureAwait(false);
				if (updates.Length > 0) messageOffset = updates[^1].Id + 1;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				var task = OnError?.Invoke(ex, Telegram.Bot.Polling.HandleErrorSource.PollingError);
				if (task != null) await task.ConfigureAwait(true);
				else System.Diagnostics.Trace.WriteLine(ex); // fallback logging if OnError is unset
			}
		}
	}
}
