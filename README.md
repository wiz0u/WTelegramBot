# WTelegramBot

WTelegramBot is a full rewrite in pure C# of the Bot API server, presenting the same methods as library Telegram.Bot.

This way you can easily migrate your existing Telegram Bot code to this more powerful approach.

The library is built on [WTelegramClient](https://wiz0u.github.io/WTelegramClient) to connect directly to Telegram Client API and gives you additional control over your bot, updates and call methods normally impossible to use with Bot API.

## Migration of existing Telegram.Bot code
Here is what you should pay attention to when migrating existing code

### Changes needed in your code:
- `TelegramBotClient` is `IDisposable`, so you should call `.Dispose()` when you're done using it, otherwise it will stay actively connected to Telegram servers and won't save its state
- constructor or `TelegramBotClientOptions` requires you to provide ApiID/ApiHash (obtained from https://my.telegram.org/apps)
- `ApiRequestException` error messages/code can differ from the Bot API errors
- FileID/FileUniqueID are not compatible with official Bot API ones, they are to be used in WTelegramBot calls only.
- `ITelegramBotClient` can not be used. Use `TelegramBotClient` directly
If your code used it a lot, you might find it useful to add this line at the top of one of your file:
    ```csharp
    global using ITelegramBotClient = Telegram.Bot.TelegramBotClient;
    ```
- A few optional fields (when unset) may be assigned to the default non-null value (like false) instead of null
- There is no support for Webhook or HTTP, except DeleteWebhookAsync
- Calling `MakeRequestAsync` with API request structures is not supported (except GetUpdatesRequest)
  Use the direct *Async methods instead

### Changes about Text Entities:
- Text entities are of type `TL.MessageEntity` instead `Telegram.Bot.Types.MessageEntity`
- To access `entity.Url`, use `entity.Url()`
- To access `entity.User`, use `entity.User(botClient)` ; or `entity.UserId()` if you only need the ID
- `MessageEntityType` are constant strings instead of enum
but you can test `entity.Type == MessageEntityType.Something` in the same way as before
- `TL.MessageEntity` is not serializable to Json like other Telegram.Bot classes.
If you want to store a message text with entities, I recommend you [convert it to/from HTML/Markdown](https://wiz0u.github.io/WTelegramClient/EXAMPLES#markdown), using WTelegramClient helper classes `HtmlText` & `Markdown`
- Texts in Markdown (V1) will be parsed as MarkdownV2. some discrepancy or error may arise due to reserved characters

## Advantages of WTelegramBot
You get access to raw Updates information from Client API, in addition to the usual Bot API updates.
They contain much more information that the limited set of Bot API updates
Some examples:
- detect deletion of messages
- notification when your messages were read in a group
- get info on the "preview" part of a message

You can also call Client API methods that are not possible for bots but not accessible from Bot API
Some example:
- get message history of group/channel
- get members list
- read message from public group/channel your bot has not joined
- send/receive big files

There are still a lot of restrictions to bots, even via Client API, so don't expect to be able to do many fancy things

## How to access these advanced features?

On each Update you receive, there is an extra field named "RawUpdate" that contains the matching raw TL.Update, which may contain extra information not transcribed into the Bot API Update

Enable `TelegramBotClient.WantUnknownRawUpdates` to also receive TL.Update that usually would have been silently ignored by Bot API
(they will be posted as Update of type Unknown with the RawUpdate field filled)

Extended API calls not usually accessible to Bot API can be made via the `TelegramBotClient.Client` which is the underlying [WTelegramClient](https://wiz0u.github.io/WTelegramClient/) instance.  
You can read that [library's documentation](https://wiz0u.github.io/WTelegramClient/EXAMPLES) or search through the [official Client API documentation](https://corefork.telegram.org/methods), but make sure to look for the mention "**Bots can use this method**".  
Note that you need to add a `using TL;` on top of your code, and these calls might throw `TL.RpcException` instead of `ApiRequestException`

In the future, I might add some of these advanced methods directly as TelegramBotClient methods to make it more easy.