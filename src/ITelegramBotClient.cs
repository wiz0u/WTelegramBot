using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;

namespace Telegram.Bot;

/// <summary>
/// A client interface to use the Telegram Bot API
/// </summary>
public interface ITelegramBotClient : IDisposable
{
    /// <summary>Access to the underlying WTelegram.Bot instance (cleaner/simpler Bot API)</summary>
    WTelegram.Bot Bot { get; }
    /// <summary>Access to the underlying WTelegram.Client instance (Client API)</summary>
    WTelegram.Client Client { get; }
    /// <summary>Bot token</summary>
    string Token { get; }

    /// <summary>
    /// <see langword="true"/> when the bot is using local Bot API server
    /// </summary>
    bool LocalBotServer { get; }

    /// <summary>
    /// Unique identifier for the bot from bot token. For example, for the bot token
    /// "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy", the bot id is "1234567".
    /// Token format is not public API so this property is optional and may stop working
    /// in the future if Telegram changes it's token format.
    /// </summary>
    long BotId { get; }

    /// <summary>
    /// Timeout for requests
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    /// Send a request to Bot API
    /// </summary>
    /// <param name="request">API request object</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Result of the API request</returns>
    Task<Update[]> MakeRequestAsync(
        Requests.GetUpdatesRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Test the API token
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns><see langword="true"/> if token is valid</returns>
    Task<bool> TestApiAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Use this method to download a file. Get <paramref name="filePath"/> by calling
    /// <see cref="TelegramBotClientExtensions.GetFileAsync(ITelegramBotClient, string, CancellationToken)"/>
    /// </summary>
    /// <param name="filePath">Path to file on server</param>
    /// <param name="destination">Destination stream to write file to</param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used by other objects or threads to receive notice of cancellation.
    /// </param>
    /// <exception cref="ArgumentException">filePath is <c>null</c>, empty or too short</exception>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c></exception>
    Task DownloadFileAsync(
        string filePath,
        Stream destination,
        CancellationToken cancellationToken = default
    );
}
