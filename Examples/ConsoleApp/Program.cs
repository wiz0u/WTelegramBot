// ----------------------------------------------------------------------------------------------
// This example demonstrates a lot of things you cannot normally do with Telegram.Bot / Bot API
// ----------------------------------------------------------------------------------------------
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TL;

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

using var bot = new WTelegram.Bot(botToken, apiId, apiHash, connection);
//          use new TelegramBotClient(...) instead, if you want the compatibility layer for your existing code
var my = await bot.GetMe();
Console.WriteLine($"I am @{my.Username}");


// get details about a user via the public username (even if not in discussion with bot)
if (await bot.InputUser("@spotifysavebot") is { user_id: var userId })
{
	var userDetails = await bot.GetChat(userId);
	var full = (Users_UserFull)userDetails.RawInfo!;
	var tlUser = full.users[userId];
	var fullUser = full.full_user;
	if (tlUser.flags.HasFlag(TL.User.Flags.bot)) Console.WriteLine($"{tlUser} is a bot");
	if (tlUser.flags.HasFlag(TL.User.Flags.scam)) Console.WriteLine($"{tlUser} is reported as scam");
	if (tlUser.flags.HasFlag(TL.User.Flags.verified)) Console.WriteLine($"{tlUser} is verified");
	if (tlUser.flags.HasFlag(TL.User.Flags.restricted)) Console.WriteLine($"{tlUser} is restricted: {tlUser.restriction_reason?[0].reason}");
	if (fullUser.bot_info is { commands: { } botCommands })
	{
		Console.WriteLine($"{tlUser} has {botCommands.Length} bot commands:");
		foreach (var command in botCommands)
			Console.WriteLine($"  /{command.command,-20} {command.description}");
	}
}


// get details about a public chat (even if bot is not a member of that chat)
var chatDetails = await bot.GetChat("@tdlibchat");
if (chatDetails.RawInfo is Messages_ChatFull { full_chat: ChannelFull channelFull })
{
	Console.WriteLine($"@{chatDetails.Username} has {channelFull.participants_count} members, {channelFull.online_count} online");
	if (channelFull.slowmode_seconds > 0)
		Console.WriteLine($"@{chatDetails.Username} has slowmode enabled: {channelFull.slowmode_seconds} seconds");
	if (channelFull.available_reactions is ChatReactionsAll { flags: ChatReactionsAll.Flags.allow_custom })
		Console.WriteLine($"@{chatDetails.Username} allows custom emojis as reactions");
}

// get list of members (you can increase the limit but Telegram might also impose a limit anyway)
var members = await bot.GetChatMemberList(chatDetails.Id, limit: 1000);
Console.WriteLine($"I fetched the info of {members.Length} members");

// get a range of posted messages
var messages = await bot.GetMessagesById("@tginfoen", Enumerable.Range(1904, 5));
Console.WriteLine($"I fetched {messages.Count} messages from @tginfoen:");
foreach (var m in messages)
	Console.WriteLine($"  {m.MessageId}: {m.Type}");

// show some message info not accessible in Bot API
var msg = messages[0];
if (msg.RawMessage is TL.Message rawMsg)
	Console.WriteLine($"Info for message {rawMsg.id}: Views = {rawMsg.views}  Shares = {rawMsg.forwards}");

// convert message text to HTML
var html = bot.Client.EntitiesToHtml(msg.Text, msg.Entities, true);
Console.WriteLine("Text in HTML: " + html);

// convert message caption to Markdown
var markdown = bot.Client.EntitiesToMarkdown(msg.Text, msg.Entities, true);

Console.WriteLine("___________________________________________________\n");
Console.WriteLine("I'm listening now. Send me a command in private or in a group where I am... Or press Escape to exit");
for (int offset = 0; ;)
{
	var updates = await bot.GetUpdates(offset, 100, 1, WTelegram.Bot.AllUpdateTypes);
	if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) break;
	foreach (var update in updates)
	{
		try
		{
			if (update.Message is { Text: { Length: > 0 } text } message)
			{
				text = text.ToLower();
				// commands accepted:
				if (text == "hello")
				{
					await bot.SendTextMessage(message.Chat, $"Hello, {message.From}!", replyParameters: message.MessageId); // easy replyToMessage
				}
				else if (text == "react")
				{
					await bot.SetMessageReaction(message.Chat, message.MessageId, ["👍"]); // easily send reaction
				}
				else if (text == "pic")
				{
					await bot.SendPhoto(message.Chat, "https://picsum.photos/310/200.jpg"); // easily send file by URL or FileID
				}
				else if (text == "lastseen")
				{
					var tlUser = message.From?.RawUser; // show some user info not accessible in Bot API:
					await bot.SendTextMessage(message.Chat, $"Your last seen is: {tlUser?.status?.ToString()?[13..]}");
				}
				else if (text == "getchat")
				{
					var chat = await bot.GetChat(message.Chat);
					// serialize current chat details as Json (not using Newtonsoft), and post it in <pre> code
					var dump = System.Text.Json.JsonSerializer.Serialize(chat, WTelegram.BotHelpers.JsonOptions);
					dump = $"<pre>{TL.HtmlText.Escape(dump)}</pre>";
					await bot.SendTextMessage(message.Chat, dump, parseMode: ParseMode.Html);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("An error occured: " + ex.Message);
		}
		offset = updates[^1].Id + 1;
	}
}
Console.WriteLine("Exiting...");
