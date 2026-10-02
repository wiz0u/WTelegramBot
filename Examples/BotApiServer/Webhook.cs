using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotApiServer;

// This implementation does NOT support:
// - sending parallel updates to the bot
// - custom certificate for webhook
// - requests as part of response content
public class Webhook : WebhookInfo
{
	static readonly Dictionary<string, Webhook> Webhooks = [];
	static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
	internal long BotId;
	internal byte[]? Certificate;
	internal string? SecretToken;
	readonly int[] _retryDelays = [0, 1000, 2000, 5000, 10000, 30000, -1];
	// access to these 3 fields must be protected with lock(Updates):
	internal Queue<Update> Updates = [];
	private Task? _sendingTask;
	private CancellationTokenSource? _cts;

	internal static async Task<WebhookInfo> GetWebhookInfo(WTelegramBotClient bot, GetWebhookInfoRequest _, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		lock (Webhooks)
			if (!Webhooks.TryGetValue(bot.Token, out var webhook))
				return new WebhookInfo { Url = "" };
			else
			{
				webhook.HasCustomCertificate = webhook.Certificate != null;
				webhook.PendingUpdateCount = webhook.Updates.Count;
				return webhook;
			}
	}

	internal static async Task<bool> DeleteWebhook(WTelegramBotClient bot, DeleteWebhookRequest _, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		Webhook? webhook;
		lock (Webhooks)
			if (!Webhooks.Remove(bot.Token, out webhook))
				return false;
		bot.OnUpdate -= webhook.OnUpdate;
		webhook._cts?.Cancel();
		return true;
	}

	internal static async Task<bool> SetWebhook(WTelegramBotClient bot, SetWebhookRequest request, CancellationToken ct)
	{
		Webhook? webhook;
		lock (Webhooks)
			if (!Webhooks.TryGetValue(bot.Token, out webhook))
				Webhooks[bot.Token] = webhook = new Webhook();
		return await webhook.Set(bot, request, ct);
	}

	private async Task<bool> Set(WTelegramBotClient bot, SetWebhookRequest request, CancellationToken ct)
	{
		bot.OnUpdate -= OnUpdate;
		_cts?.Cancel();
		BotId = bot.BotId;
		Url = request.Url;
		IpAddress = request.IpAddress;
		MaxConnections = request.MaxConnections;
		if (request.AllowedUpdates != null) AllowedUpdates = [.. request.AllowedUpdates];
		SecretToken = request.SecretToken;
		if (request.Certificate != null)
		{
			using var ms = new MemoryStream();
			await request.Certificate.Content.CopyToAsync(ms, ct);
			Certificate = ms.ToArray();
		}
		else
			Certificate = null;
		if (request.DropPendingUpdates)
			lock (Updates)
				Updates.Clear();
		bot.OnUpdate += OnUpdate;
		return true;
	}

	private async Task OnUpdate(WTelegram.Types.Update update)
	{
		var updateType = update.Type;
		if (AllowedUpdates?.Length > 0 ? !AllowedUpdates.Contains(updateType) :
			updateType is UpdateType.ChatMember or UpdateType.MessageReaction or UpdateType.MessageReactionCount)
			return;
		Console.WriteLine($"WebH {BotId} {update.Id} {updateType}");
		lock (Updates)
		{
			Updates.Enqueue(update);
			if (_sendingTask is null)
			{
				_cts = new CancellationTokenSource();
				_sendingTask = Task.Run(SendUpdates);
			}
		}
	}

	private async Task SendUpdates()
	{
		using (_cts)
			while (!_cts!.IsCancellationRequested)
			{
				Update? update;
				lock (Updates)
					if (!Updates.TryPeek(out update))
					{
						_sendingTask = null;
						_cts = null;
						return;
					}

				try
				{
					var request = new HttpRequestMessage(HttpMethod.Post, Url);
					if (SecretToken != null) request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", SecretToken);
					request.Content = JsonContent.Create(update, options: JsonBotAPI.Options);
					foreach (int delay in _retryDelays) // retry a few times with increasing delay
					{
						if (delay < 0)
							lock (Updates)  // abandon, will resume on next update
							{
								_sendingTask = null;
								_cts = null;
								return;
							}
						await Task.Delay(delay);
						try
						{
							var response = await Http.SendAsync(request, _cts.Token);
							if (response.IsSuccessStatusCode) break;
						}
						catch (HttpRequestException ex) { Console.WriteLine($"Webhook POST failed: {ex.Message}"); }
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error in SendUpdates: {ex}");
				}
				lock (Updates)
					Updates.TryDequeue(out _);
			}
	}
}
