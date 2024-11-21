using System.Data.Common;
using WTelegram;

namespace Telegram.Bot;

/// <summary>This class is used to provide configuration for <see cref="WTelegramBotClient"/></summary>
public class WTelegramBotClientOptions : TelegramBotClientOptions
{
    /// <summary>Your api_id, obtained at https://my.telegram.org/apps</summary>
    public int ApiId { get; }
    /// <summary>Your api_hash, obtained at https://my.telegram.org/apps</summary>
    public string ApiHash { get; }
    /// <summary>Connection to Database for loading/storing the bot state</summary>
    public DbConnection DbConnection { get; }
    /// <summary>You can set the SQL queries for your specific DB engine</summary>
    public string[] SqlCommands { get; set; }

    /// <summary>Create a new <see cref="WTelegramBotClientOptions"/> instance.</summary>
    /// <param name="token">API token</param>
    /// <param name="apiId">API id (see https://my.telegram.org/apps)</param>
    /// <param name="apiHash">API hash (see https://my.telegram.org/apps)</param>
    /// <param name="dbConnection">DB connection for storage and later resume</param>
    /// <param name="sqlCommands">Template for SQL strings</param>
    /// <param name="useTestEnvironment">Indicates that test environment will be used</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="token"/> format is invalid</exception>
    public WTelegramBotClientOptions(string token, int apiId, string apiHash, DbConnection dbConnection, SqlCommands sqlCommands = WTelegram.SqlCommands.Detect, bool useTestEnvironment = false)
        : base(token, useTestEnvironment: useTestEnvironment)
    {
        ApiId = apiId;
        ApiHash = apiHash;
        DbConnection = dbConnection;
        if (sqlCommands == WTelegram.SqlCommands.Detect) sqlCommands = Database.DetectType(dbConnection);
        SqlCommands = Database.DefaultSqlCommands[(int)sqlCommands];
    }

    /// <summary>The Config callback used by WTelegramClient</summary>
    public virtual string? WTCConfig(string what) => what switch
    {
        "api_id" => ApiId.ToString(),
        "api_hash" => ApiHash,
        "bot_token" => Token,
        "device_model" => "server",
        "server_address" => UseTestEnvironment ? "2>149.154.167.40:443" : "2>149.154.167.50:443",
        _ => null
    };
}
