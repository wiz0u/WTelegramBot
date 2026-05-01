# BotApiServer

This is an example Local Bot API server, based on WTelegramBot

It is an ASP.NET webservice that converts Bot API HTTP requests to WTelegramBot method calls
and serializes the response back to JSON.

It can be used as a replacement for the official Telegram Bot API server, in order to bypass some cloud Bot API limitations like files size.

## Features

Like the official Bot API server, it supports:
- All Bot API methods
- Multiple bot tokens in parallel
- Fields provided via query parameters and/or POST body with content-types:
	- application/json
	- application/x-www-form-urlencoded
	- multipart/form-data _(including file attachments)_

## Caveats

- It does not support webhooks yet
- It is a simple example implementation, not designed for high performance, and maybe not recommended for production use

## Usage

- Set the required environment variables _(typically in Properties/launchSettings.json)_:  
  `ApiId`, `ApiHash` and optionally `DbDir`, `LocalFilesRoot` _(for file:// URLs)_
- Run the project  
  _(by defaults it listen on http://localhost:5000 and https://localhost:5001)_
- Configure your bot to use this Bot API server, and run your bot  
  ```
  var bot = new TelegramBotClient(new TelegramBotClientOptions(botToken, "http://localhost:5000"));
  ```
  