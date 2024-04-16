global using Telegram.Bot.Types;
global using ITelegramBotClient = Telegram.Bot.TelegramBotClient;
global using BotCommand = Telegram.Bot.Types.BotCommand;
global using BotCommandScope = Telegram.Bot.Types.BotCommandScope;
global using Chat = Telegram.Bot.Types.Chat;
global using ForumTopic = Telegram.Bot.Types.ForumTopic;
global using InputFile = Telegram.Bot.Types.InputFile;
global using InputMedia = Telegram.Bot.Types.InputMedia;
global using Message = Telegram.Bot.Types.Message;
global using LabeledPrice = Telegram.Bot.Types.Payments.LabeledPrice;
global using ShippingOption = Telegram.Bot.Types.Payments.ShippingOption;
global using Update = Telegram.Bot.Types.Update;
global using User = Telegram.Bot.Types.User;
global using MessageEntity = TL.MessageEntity;
using JetBrains.Annotations;
using System.Data.Common;
using System.Reflection;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using TL;

namespace Telegram.Bot;

/// <summary>
/// A client to use the Telegram Bot API
/// </summary>
[PublicAPI]
public partial class TelegramBotClient : IDisposable    // ITelegramBotClient
{
    readonly TelegramBotClientOptions _options;

	/// <summary>This gives you access to the underlying Client API</summary>
	public WTelegram.Client Client { get; }
	/// <summary>The underlying UpdateManager (can be useful as Peer resolver for Client API calls)</summary>
	public readonly WTelegram.UpdateManager Manager;

    /// <inheritdoc/>
    public long BotId => _options.BotId;

    /// <inheritdoc />
    public bool LocalBotServer => _options.LocalBotServer;

    /// <summary>
    /// Timeout for requests
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

	/// <summary>
	/// Generate Unknown Updates for all raw TL Updates that usually would have been silently ignored by Bot API (see <see cref="Update.RawUpdate"/>)
	/// </summary>
	public bool WantUnknownRawUpdates { get; private set; }

	const long ZERO_CHANNEL_ID = -1000000000000;
	static readonly User GroupAnonymousBot = new() { Id = 1087968824, Username = "GroupAnonymousBot", FirstName = "Group", IsBot = true };
	static readonly User ServiceNotification = new() { Id = 777000, FirstName = "Telegram" };

	private readonly Task<TL.User> _initTask;
	private readonly Database _database;
	private readonly Database.CachedTable<Chat> _chats;
	private readonly Database.CachedTable<User> _users;
	private readonly BotCollectorPeer _collector;
	private SemaphoreSlim _pendingCounter = new(0);
	/// <summary>Cache StickerSet ID => Name</summary>
	protected Dictionary<long, string> StickerSetNames = [];
	/// <summary>Cache StickerSet Name => MimeType</summary>
	protected Dictionary<string, string?> StickerSetMimeType = [];
	/// <summary>Cache used by <see cref="GetMessage"/></summary>
	protected Dictionary<(long peerId, int msgId), Message?> CachedMessages = [];

	internal class State
	{
		public List<Update> PendingUpdates = [];
		public byte[]? SessionData;
		public int LastUpdateId;
		public int AllowedUpdates = DefaultAllowedUpdates;
	}
	private const int DefaultAllowedUpdates = 0b0101_1111_1111_1110; /// all <see cref="UpdateType"/> except Unknown=0, ChatMember=13
	private bool NotAllowed(UpdateType updateType) => (_state.AllowedUpdates & (1 << (int)updateType)) == 0;
	private State _state = new();

    /// <summary>
    /// Create a new <see cref="TelegramBotClient"/> instance.
    /// </summary>
    /// <param name="options">Configuration for <see cref="TelegramBotClient" /></param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <c>null</c>
    /// </exception>
    public TelegramBotClient(
        TelegramBotClientOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_collector = new(this);
		var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		WTelegram.Helpers.Log(1, $"WTelegramBot {version} using {options.DbConnection.GetType().Name} {options.DbConnection.DataSource}");
		_database = new Database(options.DbConnection, options.SqlCommands, _state);
		_database.GetTables(out _users, out _chats);
		Client = new WTelegram.Client(_options.WTCConfig, _database.LoadSessionState());
		Manager = Client.WithUpdateManager(OnUpdate, _database.LoadMBoxStates(), _collector);
		_initTask = InitLogin(_options.Token);
		if (_options.WaitForLogin) _initTask.Wait(); //TODO: test on winforms
	}

	private async Task<TL.User> InitLogin(string token)
	{
		try
		{
			var me = await Client.LoginBotIfNeeded(_options.Token);
			try
			{
				foreach (var (id, update) in _database.LoadTLUpdates().ToList())
				{
					var botUpdate = await MakeUpdate(update);
					botUpdate ??= new Types.Update { RawUpdate = update };
					botUpdate.Id = id;
					_state.PendingUpdates.Add(botUpdate);
				}
				Manager.Log(1, $"Connected as @{me.username} ({me.id}) | LastUpdateId = {_state.LastUpdateId} | {_state.PendingUpdates.Count} pending updates");

				if (_state.PendingUpdates.Count != 0)
				{
					_state.LastUpdateId = Math.Max(_state.LastUpdateId, _state.PendingUpdates[^1].Id);
					_pendingCounter.Release();
				}
			}
			catch { } // we can't reconstruct the PendingUpdates, too bad ¯\_(ツ)_/¯
			return me;
		}
		catch (WTelegram.WTException ex) { throw ITelegramBotClient.MakeException(ex); }
	}

    /// <summary>
    /// Create a new <see cref="TelegramBotClient"/> instance.
    /// </summary>
    /// <param name="token">The bot token</param>
    /// <param name="apiId">API id (see https://my.telegram.org/apps)</param>
    /// <param name="apiHash">API hash (see https://my.telegram.org/apps)</param>
    /// <param name="dbConnection">DB connection for storage and later resume</param>
    public TelegramBotClient(string token, int apiId, string apiHash, DbConnection dbConnection) :
        this(new TelegramBotClientOptions(token, apiId, apiHash, dbConnection))
    { }

	/// <summary>You must call Dispose to properly save state, close connection and dispose resources</summary>
	public void Dispose()
	{
		Client.Dispose();
		SaveState();
		_database.Dispose();
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

	/// <inheritdoc />
	public virtual Task<Update[]> MakeRequestAsync(
		GetUpdatesRequest request,
		CancellationToken cancellationToken = default)
	{
		if (request is null) { throw new ArgumentNullException(nameof(request)); }
		return GetUpdates(request.Offset ?? 0, request.Limit ?? 100, request.Timeout ?? 0, request.AllowedUpdates, cancellationToken);
	}

	/// <summary>Use this method to receive incoming updates using <a href="https://en.wikipedia.org/wiki/Push_technology#Long_polling">long polling</a></summary>
	/// <param name="offset">Identifier of the first update to be returned, typically the Id of the last update you handled <u>plus one</u>. Negative values are offset from the end of the pending updates queue</param>
	/// <param name="limit">Limits the number of updates to be retrieved (1-100)</param>
	/// <param name="timeout">Timeout in seconds for long polling. 0 to return immediately</param>
	/// <param name="allowedUpdates">A list of the <see cref="UpdateType"/> you want your bot to receive. Specify an empty list to receive
	/// all update types except <see cref="UpdateType.ChatMember"/>. If null, the previous setting will be used.</param>
	/// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation</param>
	/// <remarks>In order to avoid getting duplicate updates, recalculate <paramref name="offset"/> after each server response</remarks>
	/// <returns>An Array of <see cref="Update"/> objects is returned.</returns>
	public async Task<Update[]> GetUpdates(int offset = 0, int limit = 100, int timeout = 0, IEnumerable<UpdateType>? allowedUpdates = null, CancellationToken cancellationToken = default)
	{
		if (allowedUpdates != null)
		{
			var bitset = allowedUpdates.Aggregate(0, (bs, ut) => bs | (1 << (int)ut));
			_state.AllowedUpdates = bitset != 0 ? bitset : DefaultAllowedUpdates;
		}
		Update[] result;
		limit = Math.Clamp(limit, 1, 100);
		timeout *= 1000;
		for (int maxWait = 0; ; maxWait = timeout)
		{
			if (await _pendingCounter.WaitAsync(maxWait, cancellationToken))
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
			if (maxWait == timeout) break;
		}
		return [];
	}

	private async Task OnUpdate(TL.Update update)
	{
		try { await _initTask; } catch { }
		var botUpdate = await MakeUpdate(update);
		if (botUpdate == null && WantUnknownRawUpdates)
			botUpdate = new Types.Update { RawUpdate = update };
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
		}
	}

	/// <returns>User or null</returns>
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

	/// <summary>return User if found in known users, or null</summary>
	public User? User(long userId)
	{
		lock (_users)
			if (_users.TryGetValue(userId, out var user))
				return user;
		return null;
	}

	/// <summary>Obtain a InputChannel for this chat (useful with Client API calls)</summary><remarks>May throw exception if chat is unknown</remarks>
	public async Task<InputChannel> InputChannel(ChatId chatId) => chatId.Identifier is not long id || id < ZERO_CHANNEL_ID
		? (InputPeerChannel)await InputPeerChat(chatId) : throw new ApiRequestException("Bad Request: method is available for supergroup and channel chats only", 400);

	/// <summary>return Chat if found in known chats, or null</summary>
	public Chat? Chat(long chatId)
	{
		lock (_chats)
			if (_chats.TryGetValue(chatId, out var chat))
				return chat;
		return null;
	}

	/// <summary>Obtain a InputPeerChat for this chat (useful with Client API calls)</summary><remarks>May throw exception if chat is unknown</remarks>
	public async Task<InputPeer> InputPeerChat(ChatId chatId)
	{
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
				throw new ApiRequestException($"Chat {chatId} is unknown");
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
			throw new ApiRequestException($"Chat {chatId} is unknown");
		}
	}

    /// <summary>
    /// Test the API token
    /// </summary>
    /// <returns><see langword="true"/> if token is valid</returns>
    public async Task<bool> TestApiAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _initTask;
            return true;
        }
        catch (ApiRequestException e)
            when (e.ErrorCode is 400 or 401)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DownloadFileAsync(
        string filePath,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        int slash = filePath.IndexOf('/');
        await GetInfoAndDownloadFileAsync(slash < 0 ? filePath : filePath[..slash], destination, cancellationToken);
    }

	/// <summary>Free up some memory by clearing internal caches that can be reconstructed automatically<para>Call this periodically for heavily used bots if you feel too much memory is used by TelegramBotClient</para></summary>
	public void ClearCaches()
	{
		lock (_users) _users.ClearCache();
		lock (_chats) _chats.ClearCache();
		lock (StickerSetNames) StickerSetNames.Clear();
		lock (StickerSetMimeType) StickerSetMimeType.Clear();
		lock (CachedMessages) CachedMessages.Clear();
	}
}
//TODO clean SLN before release
