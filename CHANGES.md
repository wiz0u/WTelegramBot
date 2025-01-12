# Migration from Telegram.Bot to WTelegramBot

> [!NOTE]  
> If you're writing new code, you don't need to read this document, and you should use the `WTelegram.Bot` class directly.

WTelegramBot library incorporates `Telegram.Bot` namespaces and types, and provides both classic `TelegramBotClient` and the new `WTelegramBotClient`, offering the same services, with the latter being more advanced and based on WTelegram.Bot.  
Migration effort for existing code should be minimal.  

### Changes needed in your code:
- Change the nuget package dependency from Telegram.Bot to [WTelegramBot](https://www.nuget.org/packages/WTelegramBot),
  and eventually add a database package.
- Use class `WTelegramBotClient` instead of `TelegramBotClient`
- Provide an ApiId and ApiHash _([obtained here](https://my.telegram.org/apps))_
  as well as a DbConnection, typically SqliteConnection _(MySQL, PosgreSQL, SQLServer, and others are also supported)_
- `WTelegramBotClient` is `IDisposable`, so you should call `.Dispose()` when you're done using it.

Example of changes:
```csharp
=== In your .csproj ===
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.4" />
    <PackageReference Include="WTelegramBot" Version="7.7.1"/>

=== In your code ===
global using TelegramBotClient = Telegram.Bot.WTelegramBotClient;
...
var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=WTelegramBot.sqlite");
Bot = new WTelegramBotClient(BotToken, ApiId, ApiHash, dbConnection);
...
Bot.Dispose();
```


Other points to note:
- Error messages on `ApiRequestException` may sometimes differ from the usual Bot API errors
- FileID/FileUniqueID/InlineMessageId strings are not compatible with official Bot API ones, they are to be used with this library only.
- There is no native support for Webhooks (but see [support for ASP.NET apps](#support-for-aspnet-apps))
- Methods DeleteWebhookAsync & LogOutAsync are forwarded to the Cloud Bot API. Use method CloseAsync to logout locally.
- Texts in Markdown (V1) will be parsed as MarkdownV2. some discrepancy or error may arise due to reserved characters

## Support for ASP.NET apps

If you can't establish a permanent TCP connection to Telegram server, `WTelegramBotClient` now supports using a `HttpClient` on the constructor (like TelegramBotClient).
This is however not recommended as it is less efficient and does not work well in parallel calls.

The recommended code for client instantiation would be something like:

```csharp
services.AddSingleton<ITelegramBotClient>(sp =>
{
    BotConfiguration? config = sp.GetService<IOptions<BotConfiguration>>()?.Value;
    var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=WTelegramBot.sqlite");
    return new WTelegramBotClient(config.BotToken, config.ApiId, config.ApiHash, dbConnection);
});
```
> [!TIP]  
> _For better performance, you can remove the `<ITelegramBotClient>` so the singleton is typed `WTelegramBotClient`, but make sure your dependency-injection code use `WTelegramBotClient` everywhere_

Webhooks are not natively supported, but you can use a background service for polling instead:
- Add this line to your Web App services configuration:  
  `builder.Services.AddHostedService<PollingService>();`
- Add the following PollingService.cs class:
```csharp
using Telegram.Bot;
using Telegram.Bot.Polling;

public class PollingService(ITelegramBotClient bot, UpdateHandler updateHandler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bot.ReceiveAsync(updateHandler, new ReceiverOptions { AllowedUpdates = [], DropPendingUpdates = true }, stoppingToken);
    }
}
```
- Make sure to implement your UpdateHandler class as deriving from `Telegram.Bot.Polling.IUpdateHandler`
- Remember to `DeleteWebhookAsync` from Telegram Bot API when switching to WTelegramBot
- You should also make sure your hosting service won't stop/recycle your app after some HTTP inactivity timeout.  
_(some host providers have an "always on" option, or alternatively you can ping your service with an HTTP request every 5 min to keep it alive)_

