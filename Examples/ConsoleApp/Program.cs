// ----------------------------------------------------------------------------------------------
// This example demonstrates a lot of things you cannot normally do with Telegram.Bot / Bot API
// ----------------------------------------------------------------------------------------------
using ConsoleApp;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// This code needs these 3 variables in Project Properties > Debug > Launch Profiles > Environment variables
// Get your Api Id/Hash from https://my.telegram.org/apps
int apiId = 24607747;
string apiHash = "1a49eb4b8935d780fa7c54c85e786838";
string botToken = "7788578706:AAF2OYkxbpKV_0ccmeqbU5rsghpbNPLyT_k";

StreamWriter WTelegramLogs = new StreamWriter("WTelegramBot.log", true, Encoding.UTF8) { AutoFlush = true };
WTelegram.Helpers.Log = (lvl, str) => WTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");

// Using SQLite DB for storage. Other DBs below (remember to add/uncomment the adequate PackageReference in .csproj)
using var connection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
//SQL Server:	using var connection = new Microsoft.Data.SqlClient.SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=PATH_TO.mdf;Integrated Security=True;Connect Timeout=60");
//MySQL:    	using var connection = new MySql.Data.MySqlClient.MySqlConnection(@"Data Source=...");
//PosgreSQL:	using var connection = new Npgsql.NpgsqlConnection(@"Data Source=...");
//Local File Storage: var storage = new FileBotStorage(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WTelegramBot", "ConsoleAppExample"));

using var bot = new WTelegram.Bot(botToken, apiId, apiHash, connection);
//          use new WTelegramBotClient(...) instead, if you want the power of WTelegram with Telegram.Bot compatibility for existing code
//          use new TelegramBotClient(...)  instead, if you just want Telegram.Bot classic code
var my = await bot.GetMe();
Console.WriteLine($"I am @{my.Username}");

// get details about a user via the public username (even if not in discussion with bot)
if (await bot.InputUser("@spotifysavebot") is { user_id: var userId })
{
	var userDetails = await bot.GetChat(userId);
	var full = (TL.Users_UserFull)userDetails.TLInfo!;
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

//---------------------------------------------------------------------------------------
// get details about a public chat (even if bot is not a member of that chat)
var chatDetails = await bot.GetChat("@tdlibchat");
if (chatDetails.TLInfo is TL.Messages_ChatFull { full_chat: TL.ChannelFull channelFull })
{
	Console.WriteLine($"@{chatDetails.Username} has {channelFull.participants_count} members, {channelFull.online_count} online");
	if (channelFull.slowmode_seconds > 0)
		Console.WriteLine($"@{chatDetails.Username} has slowmode enabled: {channelFull.slowmode_seconds} seconds");
	if (channelFull.available_reactions is TL.ChatReactionsAll { flags: TL.ChatReactionsAll.Flags.allow_custom })
		Console.WriteLine($"@{chatDetails.Username} allows custom emojis as reactions");
}

//---------------------------------------------------------------------------------------
// get list of members (you can increase the limit but Telegram might also impose a limit anyway)
var members = await bot.GetChatMemberList(chatDetails.Id, limit: 1000);
Console.WriteLine($"I fetched the info of {members.Length} members");

//---------------------------------------------------------------------------------------
// get a range of posted messages
var messages = await bot.GetMessagesById("@tginfoen", Enumerable.Range(1904, 5));
Console.WriteLine($"I fetched {messages.Count} messages from @tginfoen:");
foreach (var m in messages)
	Console.WriteLine($"  {m.MessageId}: {m.Type}");

//---------------------------------------------------------------------------------------
// show some message info not accessible in Bot API
var msg = messages[0];
var tlMsg = msg.TLMessage() as TL.Message;
Console.WriteLine($"Info for message {tlMsg.id}: Views = {tlMsg.views}  Shares = {tlMsg.forwards}  Pinned = {tlMsg.flags.HasFlag(TL.Message.Flags.pinned)}");

Console.WriteLine("___________________________________________________\n");
Console.WriteLine("I'm listening now. Send me a command in private or in a group where I am... Or press Escape to exit");
await bot.DropPendingUpdates();
bot.WantUnknownTLUpdates = true;
bot.OnError += (e, s) => Console.Error.WriteLineAsync(e.ToString());
bot.OnMessage += OnMessage;
bot.OnUpdate += OnUpdate;
while (Console.ReadKey(true).Key != ConsoleKey.Escape) { }
Console.WriteLine("Exiting...");


async Task OnMessage(WTelegram.Types.Message msg, UpdateType type)
{
	if (msg.Text == null) return;
	var text = msg.Text.ToLower();
	// commands accepted:
	if (text == "/start")
	{
		//---> It's easy to reply to a message by giving its id to replyParameters: (was broken in Telegram.Bot v20.0.0)
		await bot.SendMessage(msg.Chat, $"Hello, {msg.From}!", replyParameters: msg);
	}
	else if (text == "/wtb")
	{
		//---> It's easy to disableWebPreview like before by passing true to linkPreviewOptions: (was broken in Telegram.Bot v20.0.0)
		await bot.SendMessage(msg.Chat, $"Enjoy https://www.nuget.org/packages/WTelegramBot", linkPreviewOptions: true);
	}
	else if (text == "/pic")
	{
		//---> It's easy to send a file by id or by url by just passing the string: (was broken in Telegram.Bot v19.0.0)
		await bot.SendPhoto(msg.Chat, "https://picsum.photos/310/200.jpg"); // easily send file by URL or FileID
	}
	else if (text == "/react")
	{
		//---> It's easy to send reaction emojis by just giving the emoji string or id
		await bot.SetMessageReaction(msg.Chat, msg.MessageId, ["👍"]);
	}
	else if (text == "/lastseen")
	{
		//---> Show more user info that is normally not accessible in Bot API:
		var tlUser = msg.From?.TLUser();
		await bot.SendMessage(msg.Chat, $"Your last seen is: {tlUser?.status?.ToString()?[13..]}");
	}
	else if (text == "/getchat")
	{
		var chat = await bot.GetChat(msg.Chat);
		//---> Demonstrate how to serialize structure to Json, and post it in <pre> code
		var dump = System.Text.Json.JsonSerializer.Serialize(chat, JsonBotAPI.Options);
		dump = $"<pre>{TL.HtmlText.Escape(dump)}</pre>";
		await bot.SendMessage(msg.Chat, dump, parseMode: ParseMode.Html);
	}
}

Task OnUpdate(WTelegram.Types.Update update)
{
	if (update.Type == UpdateType.Unknown)
	{
		//---> Show some update types that are unsupported by Bot API but can be handled via TLUpdate
		if (update.TLUpdate is TL.UpdateDeleteChannelMessages udcm)
			Console.WriteLine($"{udcm.messages.Length} message(s) deleted in {bot.Chat(udcm.channel_id)?.Title}");
		else if (update.TLUpdate is TL.UpdateDeleteMessages udm)
			Console.WriteLine($"{udm.messages.Length} message(s) deleted in user chat or small private group");
		else if (update.TLUpdate is TL.UpdateReadChannelOutbox urco)
			Console.WriteLine($"Someone read {bot.Chat(urco.channel_id)?.Title} up to message {urco.max_id}");
	}
	return Task.CompletedTask;
}
