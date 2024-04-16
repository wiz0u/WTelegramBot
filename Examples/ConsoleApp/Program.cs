using System.Text;
using Telegram.Bot;

// This code needs these 3 variables in Project Properties > Debug > Launch Profiles > Environment variables
// Get your Api Id/Hash from https://my.telegram.org/apps
int apiId = int.Parse(Environment.GetEnvironmentVariable("ApiId")!);
string apiHash = Environment.GetEnvironmentVariable("ApiHash")!;
string botToken = Environment.GetEnvironmentVariable("BotToken")!;

StreamWriter WTelegramLogs = new StreamWriter("WTelegramBot.log", true, Encoding.UTF8) { AutoFlush = true };
WTelegram.Helpers.Log = (lvl, str) => WTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");

// Using SQLite DB for storage. Other DBs below (remember to add/uncomment the adequate PackageReference in .csproj)
using var connection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
//SQL Server:	using var connection = new Microsoft.Data.SqlClient.SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=PATH_TO.mdf;Integrated Security=True;Connect Timeout=60");
//MySQL:		using var connection = new MySql.Data.MySqlClient.MySqlConnection(@"Data Source=...");
//PosgreSQL:	using var connection = new Npgsql.NpgsqlConnection(@"Data Source=...");

using var botClient = new TelegramBotClient(botToken, apiId, apiHash, connection);
var my = await botClient.GetMeAsync();
Console.WriteLine($"I am @{my.Username}. Press Escape to stop the program");

for (int offset = 0; ;)
{
	var updates = await botClient.GetUpdates(offset, timeout: 1);
	if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) break;
	foreach (var update in updates)
	{
		if (update.Message is { } msg)
			await botClient.SendTextMessageAsync(msg.Chat, $"Hello, {msg.From}!");
		offset = updates[^1].Id + 1;
	}
}
