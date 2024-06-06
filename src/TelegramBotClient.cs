using System.Data.Common;
using Telegram.Bot.Requests;

namespace Telegram.Bot;

/// <summary>
/// A client to use the Telegram Bot API
/// </summary>
public class TelegramBotClient : WTelegram.Bot, ITelegramBotClient
{
    readonly TelegramBotClientOptions _options;

    /// <inheritdoc/>
    public WTelegram.Bot Bot => this;

    /// <inheritdoc/>
    public string Token => _options.Token;

    /// <inheritdoc />
    public bool LocalBotServer => _options.LocalBotServer;

    /// <summary>
    /// Timeout for requests
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Create a new <see cref="TelegramBotClient"/> instance.
    /// </summary>
    /// <param name="options">Configuration for <see cref="TelegramBotClient" /></param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <c>null</c>
    /// </exception>
    public TelegramBotClient(TelegramBotClientOptions options)
        : base(options.WTCConfig, options.DbConnection, options.SqlCommands, false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.WaitForLogin)
            try
            {
                _initTask.Wait();
            }
            catch (WTelegram.WTException ex) { throw MakeException(ex); }
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

    /// <inheritdoc />
    public virtual Task<Update[]> MakeRequestAsync(
        GetUpdatesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) { throw new ArgumentNullException(nameof(request)); }
        return GetUpdates(request.Offset ?? 0, request.Limit ?? 100, request.Timeout ?? 0, request.AllowedUpdates, cancellationToken);
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
        catch (TL.RpcException ex) when (ex.Code is 400 or 401) { return false; }
        catch (WTelegram.WTException ex) { throw MakeException(ex); }
    }

    /// <inheritdoc />
    public async Task DownloadFileAsync(
        string filePath,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        await DownloadFile(filePath, destination, cancellationToken);
    }

    /// <summary>Convert WTelegram Exception into ApiRequestException</summary>
    internal static Exceptions.ApiRequestException MakeException(WTelegram.WTException ex)
    {
        if (ex is not TL.RpcException rpcEx) return new Exceptions.ApiRequestException(ex.Message, 400, ex);
        var msg = ex.Message switch
        {
            "MESSAGE_NOT_MODIFIED" => "message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message",
            "WC_CONVERT_URL_INVALID" or "EXTERNAL_URL_INVALID" => "Wrong HTTP URL specified",
            "WEBPAGE_CURL_FAILED" => "Failed to get HTTP URL content",
            "WEBPAGE_MEDIA_EMPTY" => "Wrong type of the web page content",
            "MEDIA_GROUPED_INVALID" => "Can't use the media of the specified type in the album",
            "REPLY_MARKUP_TOO_LONG" => "reply markup is too long",
            "INPUT_USER_DEACTIVATED" => "user is deactivated", // force 403
            "USER_IS_BLOCKED" => "bot was blocked by the user", // force 403
            "USER_ADMIN_INVALID" => "user is an administrator of the chat",
            "File generation failed" => "can't upload file by URL",
            "CHAT_ABOUT_NOT_MODIFIED" => "chat description is not modified",
            "PACK_SHORT_NAME_INVALID" => "invalid sticker set name is specified",
            "PACK_SHORT_NAME_OCCUPIED" => "sticker set name is already occupied",
            "STICKER_EMOJI_INVALID" => "invalid sticker emojis",
            "QUERY_ID_INVALID" => "query is too old and response timeout expired or query ID is invalid",
            "MESSAGE_DELETE_FORBIDDEN" => "message can't be deleted",
            _ => ex.Message,
        };
        msg = rpcEx.Code switch
        {
            401 => "Unauthorized: " + msg,
            403 => "Forbidden: " + msg,
            500 => "Internal Server Error: " + msg,
            _ => "Bad Request: " + msg,
        };
        return new Exceptions.ApiRequestException(msg, rpcEx.Code, ex);
    }
}

public static partial class TelegramBotClientExtensions
{
	/// <summary>
	/// Use this method to get basic info about a file download it. For the moment, bots can download files
	/// of up to 20MB in size.
	/// </summary>
	/// <param name="botClient">An instance of <see cref="ITelegramBotClient"/></param>
	/// <param name="fileId">File identifier to get info about</param>
	/// <param name="destination">Destination stream to write file to</param>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used by other objects or threads to receive notice of cancellation
	/// </param>
	/// <returns>On success, a <see cref="File"/> object is returned.</returns>
	public static async Task<Types.File> GetInfoAndDownloadFileAsync(
		this ITelegramBotClient botClient,
		string fileId,
		Stream destination,
		CancellationToken cancellationToken = default
	) =>
		await botClient.Bot(cancellationToken).GetInfoAndDownloadFile(fileId, destination, cancellationToken).ThrowAsApi();

	internal static long LongOrDefault(this string? s) => s == null ? 0 : long.Parse(s);
}
