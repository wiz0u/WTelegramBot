using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Telegram.Bot;

/// <summary>
/// This class is used to provide configuration for <see cref="TelegramBotClient"/>
/// </summary>
[PublicAPI]
public class TelegramBotClientOptions
{
    /// <summary>
    /// API token
    /// </summary>
    public string Token { get; }

	/// <summary>Your api_id, obtained at https://my.telegram.org/apps</summary>
	public int ApiId { get; }
	/// <summary>Your api_hash, obtained at https://my.telegram.org/apps</summary>
	public string ApiHash { get; }
	/// <summary>Connection to Database for loading/storing the bot state</summary>
	public DbConnection DbConnection { get; }
	/// <summary>You can set the SQL queries for your specific DB engine</summary>
	public string[] SqlCommands { get; set; }
	/// <summary>Should the constructor wait synchronously for login to complete <i>(necessary before further API calls)</i>.<br/>Set to <see langword="false"/> and use <c>await botClient.GetMeAsync()</c> to wait for login asynchronously instead</summary>
	public bool WaitForLogin { get; set; } = true;

    /// <summary>
    /// Indicates that test environment will be used
    /// </summary>
    public bool UseTestEnvironment { get; }

    /// <summary>
    /// Unique identifier for the bot from bot token. For example, for the bot token
    /// "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy", the bot id is "1234567".
    /// Token format is not public API so this property is optional and may stop working
    /// in the future if Telegram changes it's token format.
    /// </summary>
    public long BotId { get; }

    /// <summary>
    /// Indicates that local bot server will be used
    /// </summary>
    public bool LocalBotServer => true;

    /// <summary>
    /// Use a custom Telegram server address (used only on first connection)
    /// </summary>
    public string BaseServerAddress { get; }

    /// <summary>
    /// Create a new <see cref="TelegramBotClientOptions"/> instance.
    /// </summary>
    /// <param name="token">API token</param>
    /// <param name="apiId">API id (see https://my.telegram.org/apps)</param>
    /// <param name="apiHash">API hash (see https://my.telegram.org/apps)</param>
    /// <param name="dbConnection">DB connection for storage and later resume</param>
    /// <param name="sqlCommands">Template for SQL strings</param>
    /// <param name="useTestEnvironment"></param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="token"/> format is invalid
    /// </exception>
    public TelegramBotClientOptions(string token, int apiId, string apiHash, DbConnection dbConnection, SqlCommands sqlCommands = Bot.SqlCommands.Detect, bool useTestEnvironment = false)
    {
        Token = token ?? throw new ArgumentNullException(nameof(token));
        ApiId = apiId;
        ApiHash = apiHash;
		DbConnection = dbConnection;
		if (sqlCommands == Bot.SqlCommands.Detect) sqlCommands = Database.DetectType(dbConnection);
		SqlCommands = Database.DefaultSqlCommands[(int)sqlCommands];
        UseTestEnvironment = useTestEnvironment;

        BotId = GetIdFromToken(token)
            ?? throw new ArgumentException("Can't parse bot ID from token");

        BaseServerAddress = useTestEnvironment
            ? "2>149.154.167.40:443" 
            : "2>149.154.167.50:443";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static long? GetIdFromToken(string token)
        {
#if NET6_0_OR_GREATER
            var span = token.AsSpan();
            var index = span.IndexOf(':');

            if (index is < 1 or > 16) { return null; }

            var botIdSpan = span[..index];
            if (!long.TryParse(botIdSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var botId)) { return null; }
#else
            var index = token.IndexOf(value: ':');

            if (index is < 1 or > 16) { return null; }

            var botIdSpan = token.Substring(startIndex: 0, length: index);
            if (!long.TryParse(botIdSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var botId)) { return null; }
#endif

            return botId;
        }
    }

	/// <summary>The Config callback used by WTelegramClient</summary>
	public virtual string? WTCConfig(string what) => what switch
	{
		"api_id" => ApiId.ToString(),
		"api_hash" => ApiHash,
		"device_model" => "server",
		"server_address" => BaseServerAddress,
		_ => null
	};
}
