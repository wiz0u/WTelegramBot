global using Telegram.Bot.Types;
global using ITelegramBotClient = Telegram.Bot.TelegramBotClient;
global using BotCommand = Telegram.Bot.Types.BotCommand;
global using BotCommandScope = Telegram.Bot.Types.BotCommandScope;
global using Chat = Telegram.Bot.Types.Chat;
global using InputFile = Telegram.Bot.Types.InputFile;
global using InputMedia = Telegram.Bot.Types.InputMedia;
global using Message = Telegram.Bot.Types.Message;
global using LabeledPrice = Telegram.Bot.Types.Payments.LabeledPrice;
global using ShippingOption = Telegram.Bot.Types.Payments.ShippingOption;
global using Update = Telegram.Bot.Types.Update;
global using User = Telegram.Bot.Types.User;
global using MessageEntity = TL.MessageEntity;
using JetBrains.Annotations;
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

    public WTelegram.Client Client { get; }
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
    static readonly Dictionary<long, ChatBase> NoChats = [];

    private readonly Task<TL.User> _initTask;
    private readonly FileDatabase<Chat> _chats = new(Path.Combine("database", "chats"));
    private readonly FileDatabase<User> _users = new(Path.Combine("database", "users"));
    private readonly BotCollectorPeer _collector;
    private SemaphoreSlim _pendingCounter = new(0);
	protected Dictionary<long, string> StickerSetNames = [];        // cache id -> name
	protected Dictionary<string, string> StickerSetMimeType = [];   // cache name -> mimeType

	const int DefaultAllowedUpdates = 0b1111_1101_1111_1111_1111; /// all <see cref="UpdateType"/> except ChatMember=13
    private bool NotAllowed(UpdateType updateType) => (_state.AllowedUpdates & (1 << (int)updateType)) == 0;
    class State //TODO: save/restore this
    {
        public List<Update> PendingUpdates = null!;
        public int LastUpdateId;
        public int AllowedUpdates;
    }
    State _state;

    /// <summary>
    /// Create a new <see cref="TelegramBotClient"/> instance.
    /// </summary>
    /// <param name="options">Configuration for <see cref="TelegramBotClient" /></param>
    /// <param name="httpClient">A custom <see cref="HttpClient"/></param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <c>null</c>
    /// </exception>
    public TelegramBotClient(
        TelegramBotClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _collector = new(this);
        // TODO load from disk/DB, and if not found:
        _state = new() { AllowedUpdates = DefaultAllowedUpdates, PendingUpdates = [] };
        Client = new WTelegram.Client(_options.WTCConfig);
        Manager = Client.WithUpdateManager(OnUpdate, $"Updates-{BotId}.state", _collector);
        _initTask = DoLogin(_options.Token);
        _initTask.Wait(5000);
    }

	private async Task<TL.User> DoLogin(string token)
	{
		try
		{
            return await Client.LoginBotIfNeeded(_options.Token);
		}
		catch (WTelegram.WTException ex) { throw MakeException(ex); }
	}

	/// <summary>
	/// Create a new <see cref="TelegramBotClient"/> instance.
	/// </summary>
	/// <param name="token"></param>
	public TelegramBotClient(
        string token, int apiId, string apiHash,
        string? sessionPathname = null) :
        this(new TelegramBotClientOptions(token, apiId, apiHash, sessionPathname))
    { }

    public async Task Login() => await _initTask;

    /// <inheritdoc />
    public virtual async Task<Update[]> MakeRequestAsync(
        GetUpdatesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) { throw new ArgumentNullException(nameof(request)); }

        if (request.AllowedUpdates != null)
        {
            var bitset = request.AllowedUpdates.Aggregate(0, (bs, ut) => bs | (1 << (int)ut));
            _state.AllowedUpdates = bitset != 0 ? bitset : DefaultAllowedUpdates;
        }
        if (!await _pendingCounter.WaitAsync((request.Timeout ?? 0) * 1000, cancellationToken))
            return [];
        Update[] result;
        var limit = request.Limit ?? 0;
        if (limit is < 1 or > 100) limit = 100;
        lock (_state.PendingUpdates)
        {
            if (limit >= _state.PendingUpdates.Count && !(request.Offset < 0))
            {
                result = [.. _state.PendingUpdates];
                _state.PendingUpdates.Clear();
            }
            else
            {
                var index = request.Offset < 0 ? Math.Max(0, _state.PendingUpdates.Count + request.Offset.Value) : 0;
                result = [.. _state.PendingUpdates.GetRange(index, Math.Min(limit, _state.PendingUpdates.Count - index))];
                _state.PendingUpdates.RemoveRange(0, index + result.Length);
                if (_state.PendingUpdates.Count != 0) _pendingCounter.Release();
            }
        }
        return result;
    }

    public void Dispose()
    {
        Client.Dispose();
        SaveState();
        GC.SuppressFinalize(this);
    }

    public void SaveState()
    {
        Manager.SaveState($"Updates-{BotId}.state");
    }

    private async Task OnUpdate(TL.Update update)
    {
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

    public InputUser InputUser(long userId) => FindUser(userId) ?? new InputUser(userId, 0);
    public InputPeerUser InputPeerUser(long userId) => FindUser(userId) ?? new InputPeerUser(userId, 0);

	/// <summary>return User if found in known users, or null</summary>
	public User? FindUser(long userId)
    {
        lock (_users)
            if (_users.TryGetValue(userId, out var user))
                return user;
        return null;
    }

    public async Task<InputChannel> InputChannel(ChatId chatId) => chatId.Identifier is not long id || id < ZERO_CHANNEL_ID
        ? (InputPeerChannel)await InputPeerChat(chatId) : throw new ApiRequestException("Bad Request: method is available for supergroup and channel chats only");

    /// <summary>return Chat if found in known chats, or null</summary>
    public Chat? FindChat(long chatId)
    {
        lock (_chats)
            if (_chats.TryGetValue(chatId, out var chat))
                return chat;
        return null;
    }

    public async Task<InputPeer> InputPeerChat(ChatId chatId)
    {
        if (chatId.Identifier is long id)
            if (id >= 0)
                return InputPeerUser(id);
            else if (id > ZERO_CHANNEL_ID)
                return new InputPeerChat(-id);
            else
            {
                if (FindChat(id = ZERO_CHANNEL_ID - id) is { } chat)
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
}
//TODO clean SLN before release
