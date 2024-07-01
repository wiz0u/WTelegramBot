# Difference between Telegram.Bot library and WTelegramBot compatibility layer

> ⚠️ If you're writing new code, you don't need to read this document, and you should use the `WTelegramBot` class directly.

WTelegramBot library include a copy of most types from the `Telegram.Bot` namespaces, and a `TelegramBotClient` class that offers a very good compatibility layer.

The amount of effort to migrate your existing code from Telegram.Bot to WTelegramBot library should be minimal.  

Basically, you just need to change the nuget package dependency from Telegram.Bot to [WTelegramBot](https://www.nuget.org/packages/WTelegramBot). Most of your code should already compile fine.

After that, here are the points you should pay attention to when migrating existing code:

### Changes needed in your code:
- On `TelegramBotClient` constructor (or options), you will need to provide an ApiID and ApiHash _(obtained from https://my.telegram.org/apps)_
  as well as a DbConnection, typically SqliteConnection:
    ```csharp
    // requires Nuget package: Microsoft.Data.Sqlite
    var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
    ```
    _MySQL, PosgreSQL, SQLServer, and any custom DB are also supported_
- `TelegramBotClient` and `WTelegram.Bot` are `IDisposable`, so you should call `.Dispose()` when you're done using it, otherwise it will stay actively connected to Telegram servers and might not save its latest state.  
  ⚠️ Remember to close/dispose the dbConnection as well
- Error messages on `ApiRequestException` may sometimes differ from the usual Bot API errors
- FileID/FileUniqueID/InlineMessageId strings are not compatible with official Bot API ones, they are to be used with this library only.
- Calling `MakeRequestAsync` with API request structures is not supported _(except GetUpdatesRequest)_  
  Use the direct async methods instead.
- There is no support for HTTP / Webhooks (see [support for ASP.NET apps](#support-for-aspnet-apps))
- Methods DeleteWebhookAsync & LogOutAsync are forwarded to the Cloud Bot API. Use method CloseAsync to logout locally.
- Serialization via Newtonsoft.Json is not supported, but you can use System.Text.Json serialization instead with `WTelegram.BotHelpers.JsonOptions`

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

### Improvements to make the library more easy to use, backward-compatibility friendly

As versions goes, the Telegram.Bot library has tend to break existing code.  
I believe backward-compatibility is very important to gain the trust of users of my library.  

So I've tried to restore what got broken over time and what used to make the Telegram.Bot library simple and attractive to use, like helpers or implicit constructors for parameters:

- `ReplyParameters`: just pass an `int` when you just want to reply to a message  
_(so the new replyParameters: parameter behaves the same as the old replyToMessageId: parameter)_
- `LinkPreviewOptions`: just pass a `bool` (true) to disable link preview  
_(so the new linkPreviewOptions: parameter behaves the same as the old disableWebPagePreview: parameter)_
- `InputFile`: just pass a `string`/`Stream` for file_id/url/stream content _(as was possible in previous versions of Telegram.Bot)_
- `InputMedia*`: just pass an `InputFile` when you don't need to associate caption or such
- `MessageId`: auto-converts to/from `int` (and also from `Message`)
- `ReactionType`: just pass a `string` when you want to send an emoji
- `ReactionType`: just pass a `long` when you want to send a custom emoji (id)
- Some other obvious implicit conversion operators for structures containing a single property
- No more enforcing `init;` properties, so you can adjust the content of fields as you wish or modify a structure returned by the API _(before passing it back to the API)_
- Not using the annoying `MaybeInaccessibleMessage`, you would just get a `Message` of type Unknown with default Date if inaccessible
- Removed many [Obsolete] tags for things that still simplify your code
- Turned many nullable (like `bool?`) into normal type (like `bool`) when `null` meant the same as the default value (like `false`)
- Turned some `ParseMode?` back into `ParseMode` (restoring the old `ParseMode.None` which is the same as default/null)
- Restored some `MessageType` enum value that were removed (renamed) recently (easier compatibility)
- Not pushing you towards using silly Request-based constructors (seriously!?)

These should make migration from previous versions of Telegram.Bot more easy

## Limited breaking changes

Starting with version 7.3, the Telegram.Bot classes are now automatically generated.  
This will allow us to provide you with the latest version of the Bot API methods more quickly

It introduced a few breaking changes that should be easy to fix in your code.

Removed:
- ReplyMarkupBase (property Selective is still available on subclasses)
- Some rarely used obsolete properties (no longer supported by Bot API)

Renamed:
- Birthday => Birthdate
- WithPayment => WithPay
- WithCallBackGame => WithCallbackGame
- KeyWords => Keywords

Other changes:
- InputSticker constructor arguments order
- InputVenueMessageContent constructor arguments order
- ForumTopic.Color is an int
