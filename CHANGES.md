# Difference between Telegram.Bot library and WTelegramBot compatibility layer

> ⚠️ If you're writing new code, you don't need to read this document, and you should use the `WTelegramBot` class directly.

WTelegramBot library inherits `Telegram.Bot` namespaces and types, and we added a `WTelegramBotClient` class that offers a very good compatibility layer.

The amount of effort to migrate your existing code from Telegram.Bot to WTelegramBot library should be minimal.  

Basically, you just need to change the nuget package dependency from Telegram.Bot to [WTelegramBot](https://www.nuget.org/packages/WTelegramBot). Most of your code should already compile fine.

After that, here are the points you should pay attention to when migrating existing code:

### Changes needed in your code:
- Use `WTelegramBotClient` instead of `TelegramBotClient` (and eventually `WTelegramBotClientOptions`)
- You will need to provide an ApiID and ApiHash _(obtained from https://my.telegram.org/apps)_
  as well as a DbConnection, typically SqliteConnection:
    ```csharp
    // requires Nuget package: Microsoft.Data.Sqlite
    var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
    ```
    _MySQL, PosgreSQL, SQLServer, and any custom DB are also supported_
- `WTelegramBotClient` and `WTelegram.Bot` are `IDisposable`, so you should call `.Dispose()` when you're done using it,
  otherwise it will stay actively connected to Telegram servers and might not save its latest state.  
- Error messages on `ApiRequestException` may sometimes differ from the usual Bot API errors
- FileID/FileUniqueID/InlineMessageId strings are not compatible with official Bot API ones, they are to be used with this library only.
- There is no native support for Webhooks / HTTP / HttpClient (but see [support for ASP.NET apps](#support-for-aspnet-apps))
- Methods DeleteWebhookAsync & LogOutAsync are forwarded to the Cloud Bot API. Use method CloseAsync to logout locally.
- Texts in Markdown (V1) will be parsed as MarkdownV2. some discrepancy or error may arise due to reserved characters

## Support for ASP.NET apps

`WTelegramBotClient` doesn't support `HttpClient` or parallel instantiations,
so make sure you declare only one instance.  
For example, instead of `services.AddHttpClient().AddTypedClient<...` you would use:

```csharp
services.AddSingleton<ITelegramBotClient>(sp =>
{
    BotConfiguration? config = sp.GetService<IOptions<BotConfiguration>>()?.Value;
    var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=WTelegramBot.sqlite");
    return new WTelegramBotClient(config.BotToken, config.ApiId, config.ApiHash, dbConnection);
});
```
>_For better performance, you can remove the `<ITelegramBotClient>` so the singleton is typed `WTelegramBotClient`, but make sure your dependency-injection code use `WTelegramBotClient` everywhere_

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

