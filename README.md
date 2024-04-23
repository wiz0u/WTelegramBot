# WTelegramBot

WTelegramBot is a full rewrite in pure C# of the Bot API server, presenting the same methods as library Telegram.Bot for easy [migration](#migration).

The library is built on top of [WTelegramClient](https://wiz0u.github.io/WTelegramClient) to connect directly to Telegram Client API and gives you additional control over your bot, updates and call methods normally impossible to use with Bot API.

## Advantages of WTelegramBot
You can also call Client API methods that are possible for bots but not accessible from Bot API
Some examples:
- get past messages of group/channel
- get group/channel members list
- resolve user/chat usernames
- get full details of users/chats
- send/receive big files

You also get access to raw Updates information from Client API, in addition to the usual Bot API updates.
They contain much more information than the limited set of Bot API updates
Some examples:
- detect deletion of messages _(not always immediate)_
- get info on the "preview" part of a message
- notification when your messages were read in a group

There are still a lot of restrictions to bots, even via Client API, so don't expect to be able to do many fancy things

<a name="migration"></a>
## Migration of existing Telegram.Bot code
After changing the dependency on Telegram.Bot nuget package to WTelegramBot, here is what you should pay attention to when migrating existing code:

### Changes needed in your code:
- On `TelegramBotClient` constructor (or options), you will need to provide ApiID and ApiHash _(obtained from https://my.telegram.org/apps)_
  as well as a DbConnection, typically SqliteConnection:
    ```csharp
    // requires Nuget package: Microsoft.Data.Sqlite
    var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
    ```
    _MySQL, PosgreSQL, SQLServer are also supported_
- `TelegramBotClient` is `IDisposable`, so you should call `.Dispose()` when you're done using it, otherwise it will stay actively connected to Telegram servers and might not save its latest state
    ℹ️ Remember to close/dispose the dbConnection as well
- Error messages (and sometimes code) on `ApiRequestException` can differ from the usual Bot API errors
- FileID/FileUniqueID are not compatible with official Bot API ones, they are to be used with this library only.
- `ITelegramBotClient` can not be used. Use `TelegramBotClient` directly
  If your existing code uses the interface a lot, you might find it useful to add this line at the top of one of your file:
    ```csharp
    global using ITelegramBotClient = Telegram.Bot.TelegramBotClient;
    ```
- Calling `MakeRequestAsync` with API request structures is not supported _(except GetUpdatesRequest)_
  Use the direct async methods instead
- There is no support for Webhooks or HTTP (see [support for ASP.NET apps])
- Methods DeleteWebhookAsync & LogOutAsync are forwarded to the Cloud Bot API. Use method CloseAsync to logout locally.
- Serialization via Newtonsoft.Json is not supported, but you can use System.Text.Json serialization instead with `BotHelpers.JsonOptions`

### Changes about Text Entities:
- Text entities are of type `TL.MessageEntity` _(and derived classes)_ instead of `Telegram.Bot.Types.MessageEntity`
  If your existing code used MessageEntity a lot, you might find it useful to add this line at the top of one of your file:
    ```csharp
    global using MessageEntity = TL.MessageEntity;
    ```
- To access `entity.Url`, use `entity.Url()`
- To access `entity.User`, use `entity.User(botClient)` ; or `entity.UserId()` if you only need the ID
- `MessageEntityType` are constant strings instead of enum, but you can test `entity.Type == MessageEntityType.Something` in the same way as before
- WTelegramClient includes two helper classes to [convert entities to/from HTML/Markdown](https://wiz0u.github.io/WTelegramClient/EXAMPLES#markdown): `HtmlText` & `Markdown`
- Texts in Markdown (V1) will be parsed as MarkdownV2. some discrepancy or error may arise due to reserved characters

### Making the library more easy to use, backward-compatibility friendly

As versions goes, the Telegram.Bot library has tend to break existing code.  
I believe backward-compatibility is very important to gain the trust of users of my library.  

So I've tried to restore what got broken over time and used to make the Telegram.Bot library simple and attractive to use, like helpers or implicit constructors for parameters:

- `ReplyParameters`: just pass an `int` when you just want to reply to a message
_(so the new replyParameters: parameter behaves the same as the old replyToMessageId: parameter)_
- `LinkPreviewOptions`: just pass a `bool` (true) to disable link preview
_(so the new linkPreviewOptions: parameter behaves the same as the old disableWebPagePreview: parameter)_
- `InputFile`: just pass a `string`/`Stream` for file_id/url/stream content (as was possible in previous version of Telegram.Bot)
- `InputMedia*`: just pass an `InputFile` when you don't need to associate caption or such
- `MessageId`: auto-converts to/from `int` (and also from `Message`)
- `ReactionType`: just pass a `string` when you want to send an emoji
- `ReactionType`: just pass a `long` when you want to send a custom emoji (id)
- No more enforcing `init;` properties, so you can adjust the content of fields as you wish or modify a structure returned by the API (before passing it to API)
- Not using `MaybeInaccessibleMessage`, you would just get a `Message` of type Unknown with default Date if inaccessible
- Removed some unjustified [Obsolete] tags
- Turned most `bool?` into simple `bool`, as null already meant false
- Not pushing you towards using silly Request-based constructors (seriously!?)

These should make migration from previous versions of Telegram.Bot more easy

Additional helpers:
- TelegramBotClient.AllUpdateTypes to make your bot accept all available updates.

## How to access the advanced features?

On each Update you receive, there is an extra field named "RawUpdate" that contains the matching raw TL.Update, which may contain extra information not transcribed into the Bot API Update

Enable `TelegramBotClient.WantUnknownRawUpdates` to also receive TL.Update that usually would have been silently ignored by Bot API
(they will be posted as Update of type Unknown with the RawUpdate field filled)

Extended API calls not usually accessible to Bot API can be made via the `TelegramBotClient.Client` which is the underlying [WTelegramClient](https://wiz0u.github.io/WTelegramClient/) instance.  
You can read that [library's documentation](https://wiz0u.github.io/WTelegramClient/EXAMPLES) or search through the [official Client API documentation](https://corefork.telegram.org/methods), but make sure to look for the mention "**Bots can use this method**".  
Note that you need to add a `using TL;` on top of your code, and these calls might throw `TL.RpcException` instead of `ApiRequestException`

In the future, I might add some of these advanced methods directly as TelegramBotClient methods to make it more easy.


## Support for ASP.NET apps

If your app is written as an ASP.NET app using webhooks, you can still use this library using background Polling:

```csharp
// instead of calling SetWebhookAsync, run the following code once your app starts:
BotClient.StartReceiving(HandleUpdate, HandlePollingError);
```

You should make sure your hosting service won't stop/recycle your app after some HTTP inactivity timeout.

(some host providers have an "always on" option, or alternatively you can ping your service with an HTTP request every 5 min to keep it alive)

