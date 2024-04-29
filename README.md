[![Bot API Layer](https://img.shields.io/badge/Bot_API_Layer-7.2-blueviolet)](https://corefork.telegram.org/methods)
[![NuGet version](https://img.shields.io/nuget/v/WTelegramBot?color=00508F)](https://www.nuget.org/packages/WTelegramBot/)
[![NuGet prerelease](https://img.shields.io/nuget/vpre/WTelegramBot?color=C09030&label=dev+nuget)](https://www.nuget.org/packages/WTelegramBot/absoluteLatest)
[![Donate](https://img.shields.io/badge/Help_this_project:-Donate-ff4444)](https://www.buymeacoffee.com/wizou)

# Powerful Telegram Bot API library for .NET

WTelegramBot is a full rewrite in pure C# of Telegram Bot API server, presenting the same methods as the Telegram.Bot library for easy [migration](#migration).

The library is built on top of [WTelegramClient](https://wiz0u.github.io/WTelegramClient) to connect directly to Telegram Client API and gives you additional control over your bot, updates and call methods normally impossible to use with Bot API.


## Advantages of WTelegram.Bot

Using class `WTelegram.Bot` you have access to a clean set of developer-friendly methods to easily access the Bot API

You can also call Client API methods that are possible for bots but not accessible from Bot API!
Some examples:
- Fetch past messages of group/channel
- Get group/channel members list
- Resolve user/chat usernames
- Get full details of users/chats
- Send/receive big files

You also get access to raw Updates information from Client API, in addition to the usual Bot API updates.
They contain more information than the limited set of Bot API updates!
Some examples:
- Detect deletion of messages _(not always immediate)_
- Get more info on message media _(like date of original media upload, sticker duration, ...)_
- Notification when your messages were read in a group

See the [Example app](https://github.com/wiz0u/WTelegramBot/tree/master/Examples/ConsoleApp) for a nice demonstration of features.

➡️ There are still a lot of restrictions to bots, even via Client API, so don't expect to be able to do many fancy things

<a name="migration"></a>
## Migration of existing Telegram.Bot code
The library contains a compatibility layer as `Telegram.Bot.TelegramBotClient` inheriting from WTelegram.Bot.

Basically, you just need to change the nuget package dependency from Telegram.Bot to WTelegramBot.  
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
- FileID/FileUniqueID/InlineMessageId are not compatible with official Bot API ones, they are to be used with this library only.
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

### Making the library more easy to use, backward-compatibility friendly

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
- Turned some `ParseMode?` back into `ParseMode` (restoring the old `ParseMode.Default` which is the same as default/null)
- Restored some `MessageType` enum value that were removed (renamed) recently (easier compatibility)
- Not pushing you towards using silly Request-based constructors (seriously!?)

These should make migration from previous versions of Telegram.Bot more easy

Additional helpers:
- TelegramBotClient.AllUpdateTypes to make your bot accept all available updates.


## Difference between classes `WTelegram.Bot` and `TelegramBotClient`

If you're porting an existing codebase, you can continue to use TelegramBotClient as it has the same methods you're used to.  
But if you're creating a new bot rather than migrating existing code, it is recommended that you use WTelegram.Bot.

Here are the differences:
* The method names don't have the *Async suffix (even though they should still be invoked with `await`) so they are more close to official [Bot API method names](https://core.telegram.org/bots/api#available-methods).
* The orders of parameters can differ, presenting a more logical order for developers, with the more rarely used optional parameters near the end.
* There is no CancellationToken parameter because it doesn't make sense to abort an immediate TCP request to Client API.  
_(Even with HTTP Bot API, it didn't make much sense: You can use cancellationToken.ThrowIfCancellationRequested() at various points of your own code if you want it to be cancellable)_
* In case of an error, WTelegram.Bot will throw `TL.RpcException` showing the raw Telegram error, instead of an ApiRequestException

## How to access the advanced features?

The [Example app](https://github.com/wiz0u/WTelegramBot/tree/master/Examples/ConsoleApp) demonstrates all of the features below.

On each Update/Message/User/Chat you receive, there is an extra field named "`TL...`" that contains the corresponding raw Client API structure, which may contain extra information not transcribed into the Bot API

You can also enable `TelegramBotClient.WantUnknownTLUpdates` to receive updates that usually would have been silently ignored by Bot API
(they will be posted as Update of type Unknown with the TLUpdate field filled)

Some extended API calls can be made via `WTelegram.Bot` special methods:
- `GetChatMemberList`: fetch a list of chat members
- `GetMessagesById`: fetch posted messages (or range of messages) based on their message IDs
- `InputUser`: can resolve a username into a user ID that you can then use with GetChat
- `GetChat`: can obtain details about any group/channel based on their public name, or a user ID resolved by InputUser

Other extended API calls not usually accessible to Bot API can be made via the `Bot.Client` property which is the underlying [WTelegramClient](https://wiz0u.github.io/WTelegramClient/) instance.  
* This way, you can use new features available only in Client API latest layers without waiting months for it to be available in Bot API

For more information about calling Client API methods, you can read that [library's documentation](https://wiz0u.github.io/WTelegramClient/EXAMPLES)
or search through the [official Client API documentation](https://corefork.telegram.org/methods),
but make sure to look for the mention "**Bots can use this method**" (other methods can't be called).  

> Note: If you want to experiment with these, you'll need to add a `using TL;` on top of your code, and these calls might throw `TL.RpcException` instead of `ApiRequestException`

Some other `WTelegram.Bot` methods (for example, beginning with Input*) and extension methods can help you convert Bot API ids or structure to/from Client API.


## Support for ASP.NET apps

If your app is written as an ASP.NET app using webhooks, you can still use this library using background Polling:

```csharp
// instead of calling SetWebhookAsync, run the following code once your app starts:
BotClient.StartReceiving(HandleUpdate, HandlePollingError);
```

You should make sure your hosting service won't stop/recycle your app after some HTTP inactivity timeout.

(some host providers have an "always on" option, or alternatively you can ping your service with an HTTP request every 5 min to keep it alive)


## Help with the library

This library is still quite new but I tested it extensively to make sure it covers all of the Bot API successfully.

If you have questions about the (official) Bot API methods from TelegramBotClient, you can ask them in [Telegram.Bot support chat](https://t.me/joinchat/B35YY0QbLfd034CFnvCtCA).

If your question is more specific to WTelegram.Bot, or an issue with library behaviour, you can ask them in [@WTelegramClient](https://t.me/WTelegramClient).

If you like this library, you can [buy me a coffee](https://www.buymeacoffee.com/wizou) ❤ This will help the project keep going.

© 2024 Olivier Marcoux
