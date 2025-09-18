[![Bot API 9.2](https://img.shields.io/badge/Bot_API-9.2-blueviolet)](https://core.telegram.org/bots/api)
[![NuGet version](https://img.shields.io/nuget/v/WTelegramBot?color=00508F)](https://www.nuget.org/packages/WTelegramBot/)
[![NuGet prerelease](https://img.shields.io/nuget/vpre/WTelegramBot?color=C09030&label=dev+nuget)](https://www.nuget.org/packages/WTelegramBot/absoluteLatest)
[![Donate](https://img.shields.io/badge/Help_this_project:-Donate-ff4444)](https://www.buymeacoffee.com/wizou)

# Powerful Telegram Bot API library for .NET

WTelegramBot is a full rewrite in pure C# of Telegram Bot API server, presenting the same methods as the Telegram.Bot library for easy [migration](https://github.com/wiz0u/WTelegramBot/blob/master/CHANGES.md).

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
- Connect using a MTProxy
- Change the bot's profile picture
- Monitor progress of file uploads/downloads

You also get access to raw Updates information from Client API, in addition to the usual Bot API updates.
They contain more information than the limited set of Bot API updates!
Some examples:
- Detect deletion of messages _(not always immediate)_
- Get more info on message media _(like date of original media upload, sticker duration, ...)_
- Notification when your messages were read in a group

See the [Example app](https://github.com/wiz0u/WTelegramBot/tree/master/Examples/ConsoleApp) for a nice demonstration of features.

➡️ There are still a lot of restrictions to bots, even via Client API, so don't expect to be able to do many fancy things

<a name="migration"></a>
## Difference between classes `WTelegram.Bot` and `TelegramBotClient`

The library contains a compatibility layer as `Telegram.Bot.WTelegramBotClient` inheriting from WTelegram.Bot.  
[Click here to easily migrate](https://github.com/wiz0u/WTelegramBot/blob/master/CHANGES.md) your existing Telegram.Bot code.

If you're not migrating an existing codebase, it is recommended that you use `WTelegram.Bot` class directly.
Here are the main differences:
* The method names don't have the *Async suffix (even though they should still be invoked with `await`) so they are more close to official [Bot API method names](https://core.telegram.org/bots/api#available-methods).
* The optional parameters follow a more logical order for developers, with the more rarely used optional parameters near the end.
* There is no CancellationToken parameter because it doesn't make sense to abort an immediate TCP request to Client API.  
_(Even with HTTP Bot API, it didn't make much sense: You can use cancellationToken.ThrowIfCancellationRequested() at various points of your own code if you want it to be cancellable)_
* In case of an error, WTelegram.Bot will throw `WTelegram.WTException` like `TL.RpcException` showing the raw Telegram error, instead of an ApiRequestException
* `WTelegram.Bot` and `WTelegramBotClient` are `IDisposable`, so remember to call `.Dispose()`

## How to access the advanced features?

The [Example app](https://github.com/wiz0u/WTelegramBot/tree/master/Examples/ConsoleApp) demonstrates all of the features below.

On each Update/Message/User/Chat you receive, there is an extra field named "`TL...`" that contains the corresponding raw Client API structure, which may contain extra information not transcribed into the Bot API

You can also enable property `WantUnknownTLUpdates` to receive updates that usually would have been silently ignored by Bot API
(they will be posted as Update of type Unknown with the TLUpdate field filled)

Some extended API calls can be made via `WTelegram.Bot` special methods:
- `GetChatMemberList`: fetch a list of chat members
- `GetMessagesById`: fetch posted messages (or range of messages) based on their message IDs
- `GetChat`: can obtain details about any user/group/channel based on their public name
- `SetMyPhoto`: change the bot's profile picture

Other extended API calls not usually accessible to Bot API can be made via the `Bot.Client` property which is the underlying [WTelegramClient](https://wiz0u.github.io/WTelegramClient/) instance.  
* This way, you can use new features available only in Client API latest layers without waiting months for it to be available in Bot API

For more information about calling Client API methods, you can read that [library's documentation](https://wiz0u.github.io/WTelegramClient/EXAMPLES)
or search through the [official Client API documentation](https://corefork.telegram.org/methods),
but make sure to look for the mention "**Bots can use this method**" (other methods can't be called).  

> Note: If you want to experiment with these, you'll need to add a `using TL;` on top of your code, and these calls might throw `TL.RpcException` instead of `ApiRequestException`

Some other `WTelegram.Bot` methods (for example, beginning with Input*) and extension methods can help you convert Bot API ids or structure to/from Client API.


## Help with the library

This library is still quite new but I tested it extensively to make sure it covers all of the Bot API successfully.

If you have questions about the (official) Bot API methods from TelegramBotClient, you can ask them in [Telegram.Bot support chat](https://t.me/joinchat/B35YY0QbLfd034CFnvCtCA).

If your question is more specific to WTelegram.Bot, or an issue with library behaviour, you can ask them in [@WTelegramClient](https://t.me/WTelegramClient).

If you like this library, you can [buy me a coffee](https://www.buymeacoffee.com/wizou) ❤ This will help the project keep going.

© 2021-2025 Olivier Marcoux
