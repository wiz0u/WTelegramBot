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
- `TelegramBotClient` is `IDisposable`, so you should call `.Dispose()` when you're done using it, otherwise it will stay actively connected to Telegram servers and won't save its state
- You will need to provide ApiID/ApiHash (obtained from https://my.telegram.org/apps)
- Error messages/code on `ApiRequestException` can differ from the Bot API errors
- FileID/FileUniqueID are not compatible with official Bot API ones, they are to be used in WTelegramBot calls only.
- `ITelegramBotClient` can not be used. Use `TelegramBotClient` directly
If your code used it a lot, you might find it useful to add this line at the top of one of your file:
    ```csharp
    global using ITelegramBotClient = Telegram.Bot.TelegramBotClient;
    ```
- Some nullable properties may be assigned to the default type value instead of `null` (like `false` for `bool?`)
- Calling `MakeRequestAsync` with API request structures is not supported _(except GetUpdatesRequest)_
  Use the direct async methods instead
- There is no support for Webhooks or HTTP
- Methods DeleteWebhookAsync & LogOutAsync are forwarded to the Cloud Bot API. Use method CloseAsync to logout locally.

### Changes about Text Entities:
- Text entities are of type `TL.MessageEntity` instead `Telegram.Bot.Types.MessageEntity`
- To access `entity.Url`, use `entity.Url()`
- To access `entity.User`, use `entity.User(botClient)` ; or `entity.UserId()` if you only need the ID
- `MessageEntityType` are constant strings instead of enum, but you can test `entity.Type == MessageEntityType.Something` in the same way as before
- `TL.MessageEntity` is not serializable to Json like other Telegram.Bot classes.
If you want to store a message text with entities, I recommend you [convert it to/from HTML/Markdown](https://wiz0u.github.io/WTelegramClient/EXAMPLES#markdown), using WTelegramClient helper classes `HtmlText` & `Markdown`
- Texts in Markdown (V1) will be parsed as MarkdownV2. some discrepancy or error may arise due to reserved characters

### Making the library more easy to use, backward-compatibility friendly

As versions goes, the Telegram.Bot library has tend to break existing code.  
I've tried to restore what use to make the Telegram.Bot library attractive

- Implicit/easiers constructors to simplify your code:
  - `int` instead of `ReplyParameters` when you just want to reply to a message
  - `int`/`string`/`Stream` for `InputFile`, instead of having to call complex construction methods
  - `InputFile` instead of `InputMedia*` when you don't need to associate caption or such
- no more enforcing `init;` properties, so you can adjust the content of fields as you wish or modify a structure returned by the API (before passing it to API)
- Removing some unjustified [Obsolete] tags
- Not pushing you towards silly Request-based constructors


## How to access the advanced features?

On each Update you receive, there is an extra field named "RawUpdate" that contains the matching raw TL.Update, which may contain extra information not transcribed into the Bot API Update

Enable `TelegramBotClient.WantUnknownRawUpdates` to also receive TL.Update that usually would have been silently ignored by Bot API
(they will be posted as Update of type Unknown with the RawUpdate field filled)

Extended API calls not usually accessible to Bot API can be made via the `TelegramBotClient.Client` which is the underlying [WTelegramClient](https://wiz0u.github.io/WTelegramClient/) instance.  
You can read that [library's documentation](https://wiz0u.github.io/WTelegramClient/EXAMPLES) or search through the [official Client API documentation](https://corefork.telegram.org/methods), but make sure to look for the mention "**Bots can use this method**".  
Note that you need to add a `using TL;` on top of your code, and these calls might throw `TL.RpcException` instead of `ApiRequestException`

In the future, I might add some of these advanced methods directly as TelegramBotClient methods to make it more easy.

