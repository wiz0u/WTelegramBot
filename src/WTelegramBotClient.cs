﻿using System.Data.Common;

namespace Telegram.Bot;

/// <summary>
/// A client to use the Telegram Bot API
/// </summary>
public partial class WTelegramBotClient : WTelegram.Bot, ITelegramBotClient
{
    readonly WTelegramBotClientOptions _options;

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
    /// Global cancellation token
    /// </summary>
    public CancellationToken GlobalCancelToken { get; }

	/// <inheritdoc />
	public Exceptions.IExceptionParser ExceptionsParser { get; set; } = new Exceptions.DefaultExceptionParser();
	[Obsolete("Not supported by WTelegramBot")]
	event AsyncEventHandler<Args.ApiRequestEventArgs>? ITelegramBotClient.OnMakingApiRequest { add { } remove { } }
	[Obsolete("Not supported by WTelegramBot")]
	event AsyncEventHandler<Args.ApiResponseEventArgs>? ITelegramBotClient.OnApiResponseReceived { add { } remove { } }

	/// <summary>
	/// Create a new <see cref="WTelegramBotClient"/> instance.
	/// </summary>
	/// <param name="options">Configuration for <see cref="WTelegramBotClient" /></param>
	/// <param name="cancellationToken">Global cancellation token</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="options"/> is <c>null</c>
	/// </exception>
	public WTelegramBotClient(WTelegramBotClientOptions options, CancellationToken cancellationToken = default)
        : base(options.WTCConfig, options.DbConnection, options.SqlCommands, waitForLogin: false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        GlobalCancelToken = cancellationToken;
        if (options.WaitForLogin)
            try
            {
                _initTask.Wait(cancellationToken);
            }
            catch (WTelegram.WTException ex) { throw MakeException(ex); }
    }

    /// <summary>
    /// Create a new <see cref="WTelegramBotClient"/> instance.
    /// </summary>
    /// <param name="token">The bot token</param>
    /// <param name="apiId">API id (see https://my.telegram.org/apps)</param>
    /// <param name="apiHash">API hash (see https://my.telegram.org/apps)</param>
    /// <param name="dbConnection">DB connection for storage and later resume</param>
    /// <param name="cancellationToken">Global cancellation token</param>
    public WTelegramBotClient(string token, int apiId, string apiHash, DbConnection dbConnection, CancellationToken cancellationToken = default) :
        this(new WTelegramBotClientOptions(token, apiId, apiHash, dbConnection), cancellationToken)
    { }

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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(GlobalCancelToken, cancellationToken);
        await DownloadFile(filePath, destination, cts.Token);
    }

	/// <summary>
	/// Use this method to get basic info about a file download it. For the moment, bots can download files
	/// of up to 20MB in size.
	/// </summary>
	/// <param name="fileId">File identifier to get info about</param>
	/// <param name="destination">Destination stream to write file to</param>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used by other objects or threads to receive notice of cancellation
	/// </param>
	/// <returns>On success, a <see cref="File"/> object is returned.</returns>
	public async Task<Types.File> GetInfoAndDownloadFileAsync(
		string fileId,
		Stream destination,
		CancellationToken cancellationToken = default
	) =>
		await ThrowIfCancelled(cancellationToken).GetInfoAndDownloadFile(fileId, destination, cancellationToken).ThrowAsApi(this);

	/// <summary>Convert WTelegram Exception into ApiRequestException</summary>
	internal Exceptions.ApiRequestException MakeException(WTelegram.WTException ex)
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
		return ExceptionsParser.Parse(new() { Description = msg, ErrorCode = rpcEx.Code });
    }
}
