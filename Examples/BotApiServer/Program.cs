using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

// Before running, set environment variables ApiId, ApiHash and optionally DbDir, LocalFilesRoot (for file:// URLs)

int apiId = int.Parse(Environment.GetEnvironmentVariable("ApiId")!);
var apiHash = Environment.GetEnvironmentVariable("ApiHash")!;
var dbDir = Environment.GetEnvironmentVariable("DbDir") ?? "bin";
Telegram.Bot.Serialization.InputFileConverter.AllowLocalFilesUnder = Environment.GetEnvironmentVariable("LocalFilesRoot");
WTelegram.Helpers.Log = (level, str) => System.Diagnostics.Debug.WriteLine(str);
Dictionary<string, WTelegramBotClient?> bots = [];

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureTelegramBot<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt => opt.SerializerOptions);
var app = builder.Build();
app.UseHttpsRedirection();

app.MapGet("/bot{token}/{method}", OnRequest);
app.MapPost("/bot{token}/{method}", OnRequest);
Console.WriteLine("BotApiServer running...");
app.Run();

async Task<IResult> OnRequest(string token, string method, HttpRequest request, CancellationToken ct)
{
	List<InputFileStream>? attachments = null;
	try
	{
		if (!bots.TryGetValue(token, out var bot)) // reuse existing bot client if token is already known, otherwise create a new one
		{
			int colon = token.IndexOf(':');
			if (colon == -1 || !long.TryParse(token[..colon], out var botId))
				throw new ApiRequestException("Not Found", 404);
			else
			{
				var dbConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbDir}/{botId}.sqlite");
				bot = new WTelegramBotClient(token, apiId, apiHash, dbConnection);
				if (!await bot.TestApi(ct)) { bot.Dispose(); bot = null; } // bad token ? mark token as incorrect (unauthorized)
				bots[token] = bot;
			}
		}
		if (bot == null) throw new ApiRequestException("Unauthorized", 401);

		Console.WriteLine($"{request.Method} {bot.BotId} {method}");
		IFormCollection? form = null;
		string? json = null;
		switch (request.Method) // build request json from body
		{
			case "GET": break;
			case "POST":
				if (request.ContentLength == 0) break;
				_ = MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType);
				switch (contentType?.MediaType)
				{
					case "application/json":
						using (var reader = new StreamReader(request.Body, contentType.CharSet == null ? null : Encoding.GetEncoding(contentType.CharSet)))
							json = await reader.ReadToEndAsync(ct);
						break;
					case "application/x-www-form-urlencoded":
					case "multipart/form-data":
						form = await request.ReadFormAsync(ct);
						json = JsonSerializer.Serialize(form.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[0]));
						if (form.Files.Count > 0)
						{
							attachments = Telegram.Bot.Serialization.InputFileConverter.Attachments.Value = [];
							foreach (var file in form.Files)
								attachments.Add(new InputFileStream(file.OpenReadStream(), $"attach://{file.Name}\n{file.FileName ?? "file"}"));
						}
						break;
					default:
						throw new ApiRequestException($"Unsupported content type: {request.ContentType}", 400);
				}
				break;
			default:
				throw new ApiRequestException($"Unsupported HTTP method: {request.Method}", 400);
		}
		json ??= "{}";
		if (!json.EndsWith('}')) throw new ApiRequestException("Invalid request structure", 400);
		if (request.Query?.Count > 0) // override request with query string parameters (append them to request json)
		{
			json = json[..^1].TrimEnd();
			var add = JsonSerializer.Serialize(request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[0]));
			json = json[^1] != '{' ? $"{json},{add[1..]}" : $"{json}{add[1..]}";
		}

		var result = await HandleRequest(bot, method, json, ct);
		return Results.Ok(new ApiResponse<object> { Ok = true, Result = result });

	}
	catch (ApiRequestException ex)
	{
		return Results.Json(new ApiResponse { ErrorCode = ex.ErrorCode, Description = ex.Message }, statusCode: ex.ErrorCode);
	}
	catch (Exception ex)
	{
		return Results.InternalServerError(new ApiResponse { ErrorCode = 500, Description = $"{ex.GetType().Name}: {ex.Message}" });
	}
	finally
	{
		if (attachments != null)
			foreach (var file in attachments)
				file.Content.Dispose();
	}
}

static T Parse<T>(string json) where T : IRequest => JsonSerializer.Deserialize<T>(json, JsonBotAPI.Options)!;
static bool IsInlineRequest(string json) => json.IndexOf("\"inline_message_id\"") is > 0 and int index && json[index - 1] != '\\';

static async Task<object> HandleRequest(WTelegramBotClient bot, string method, string json, CancellationToken ct) => method.ToLowerInvariant() switch
{
	"getupdates" => await bot.SendRequest(Parse<GetUpdatesRequest>(json), ct),
	"setwebhook" => await bot.SendRequest(Parse<SetWebhookRequest>(json), ct),
	"deletewebhook" => await bot.SendRequest(Parse<DeleteWebhookRequest>(json), ct),
	"getwebhookinfo" => await bot.SendRequest(Parse<GetWebhookInfoRequest>(json), ct),
	"getme" => await bot.SendRequest(Parse<GetMeRequest>(json), ct),
	"logout" => await bot.SendRequest(Parse<LogOutRequest>(json), ct),
	"close" => await bot.SendRequest(Parse<CloseRequest>(json), ct),
	"sendmessage" => await bot.SendRequest(Parse<SendMessageRequest>(json), ct),
	"forwardmessage" => await bot.SendRequest(Parse<ForwardMessageRequest>(json), ct),
	"forwardmessages" => await bot.SendRequest(Parse<ForwardMessagesRequest>(json), ct),
	"copymessage" => await bot.SendRequest(Parse<CopyMessageRequest>(json), ct),
	"copymessages" => await bot.SendRequest(Parse<CopyMessagesRequest>(json), ct),
	"sendphoto" => await bot.SendRequest(Parse<SendPhotoRequest>(json), ct),
	"sendaudio" => await bot.SendRequest(Parse<SendAudioRequest>(json), ct),
	"senddocument" => await bot.SendRequest(Parse<SendDocumentRequest>(json), ct),
	"sendvideo" => await bot.SendRequest(Parse<SendVideoRequest>(json), ct),
	"sendanimation" => await bot.SendRequest(Parse<SendAnimationRequest>(json), ct),
	"sendvoice" => await bot.SendRequest(Parse<SendVoiceRequest>(json), ct),
	"sendvideonote" => await bot.SendRequest(Parse<SendVideoNoteRequest>(json), ct),
	"sendpaidmedia" => await bot.SendRequest(Parse<SendPaidMediaRequest>(json), ct),
	"sendmediagroup" => await bot.SendRequest(Parse<SendMediaGroupRequest>(json), ct),
	"sendlocation" => await bot.SendRequest(Parse<SendLocationRequest>(json), ct),
	"sendvenue" => await bot.SendRequest(Parse<SendVenueRequest>(json), ct),
	"sendcontact" => await bot.SendRequest(Parse<SendContactRequest>(json), ct),
	"sendpoll" => await bot.SendRequest(Parse<SendPollRequest>(json), ct),
	"sendchecklist" => await bot.SendRequest(Parse<SendChecklistRequest>(json), ct),
	"senddice" => await bot.SendRequest(Parse<SendDiceRequest>(json), ct),
	"sendmessagedraft" => await bot.SendRequest(Parse<SendMessageDraftRequest>(json), ct),
	"sendchataction" => await bot.SendRequest(Parse<SendChatActionRequest>(json), ct),
	"setmessagereaction" => await bot.SendRequest(Parse<SetMessageReactionRequest>(json), ct),
	"getuserprofilephotos" => await bot.SendRequest(Parse<GetUserProfilePhotosRequest>(json), ct),
	"getuserprofileaudios" => await bot.SendRequest(Parse<GetUserProfileAudiosRequest>(json), ct),
	"setuseremojistatus" => await bot.SendRequest(Parse<SetUserEmojiStatusRequest>(json), ct),
	"getfile" => await bot.SendRequest(Parse<GetFileRequest>(json), ct),
	"banchatmember" => await bot.SendRequest(Parse<BanChatMemberRequest>(json), ct),
	"unbanchatmember" => await bot.SendRequest(Parse<UnbanChatMemberRequest>(json), ct),
	"restrictchatmember" => await bot.SendRequest(Parse<RestrictChatMemberRequest>(json), ct),
	"promotechatmember" => await bot.SendRequest(Parse<PromoteChatMemberRequest>(json), ct),
	"setchatadministratorcustomtitle" => await bot.SendRequest(Parse<SetChatAdministratorCustomTitleRequest>(json), ct),
	"setchatmembertag" => await bot.SendRequest(Parse<SetChatMemberTagRequest>(json), ct),
	"banchatsenderchat" => await bot.SendRequest(Parse<BanChatSenderChatRequest>(json), ct),
	"unbanchatsenderchat" => await bot.SendRequest(Parse<UnbanChatSenderChatRequest>(json), ct),
	"setchatpermissions" => await bot.SendRequest(Parse<SetChatPermissionsRequest>(json), ct),
	"exportchatinvitelink" => await bot.SendRequest(Parse<ExportChatInviteLinkRequest>(json), ct),
	"createchatinvitelink" => await bot.SendRequest(Parse<CreateChatInviteLinkRequest>(json), ct),
	"editchatinvitelink" => await bot.SendRequest(Parse<EditChatInviteLinkRequest>(json), ct),
	"createchatsubscriptioninvitelink" => await bot.SendRequest(Parse<CreateChatSubscriptionInviteLinkRequest>(json), ct),
	"editchatsubscriptioninvitelink" => await bot.SendRequest(Parse<EditChatSubscriptionInviteLinkRequest>(json), ct),
	"revokechatinvitelink" => await bot.SendRequest(Parse<RevokeChatInviteLinkRequest>(json), ct),
	"approvechatjoinrequest" => await bot.SendRequest(Parse<ApproveChatJoinRequestRequest>(json), ct),
	"declinechatjoinrequest" => await bot.SendRequest(Parse<DeclineChatJoinRequestRequest>(json), ct),
	"setchatphoto" => await bot.SendRequest(Parse<SetChatPhotoRequest>(json), ct),
	"deletechatphoto" => await bot.SendRequest(Parse<DeleteChatPhotoRequest>(json), ct),
	"setchattitle" => await bot.SendRequest(Parse<SetChatTitleRequest>(json), ct),
	"setchatdescription" => await bot.SendRequest(Parse<SetChatDescriptionRequest>(json), ct),
	"pinchatmessage" => await bot.SendRequest(Parse<PinChatMessageRequest>(json), ct),
	"unpinchatmessage" => await bot.SendRequest(Parse<UnpinChatMessageRequest>(json), ct),
	"unpinallchatmessages" => await bot.SendRequest(Parse<UnpinAllChatMessagesRequest>(json), ct),
	"leavechat" => await bot.SendRequest(Parse<LeaveChatRequest>(json), ct),
	"getchat" => await bot.SendRequest(Parse<GetChatRequest>(json), ct),
	"getchatadministrators" => await bot.SendRequest(Parse<GetChatAdministratorsRequest>(json), ct),
	"getchatmembercount" => await bot.SendRequest(Parse<GetChatMemberCountRequest>(json), ct),
	"getchatmember" => await bot.SendRequest(Parse<GetChatMemberRequest>(json), ct),
	"setchatstickerset" => await bot.SendRequest(Parse<SetChatStickerSetRequest>(json), ct),
	"deletechatstickerset" => await bot.SendRequest(Parse<DeleteChatStickerSetRequest>(json), ct),
	"getforumtopiciconstickers" => await bot.SendRequest(Parse<GetForumTopicIconStickersRequest>(json), ct),
	"createforumtopic" => await bot.SendRequest(Parse<CreateForumTopicRequest>(json), ct),
	"editforumtopic" => await bot.SendRequest(Parse<EditForumTopicRequest>(json), ct),
	"closeforumtopic" => await bot.SendRequest(Parse<CloseForumTopicRequest>(json), ct),
	"reopenforumtopic" => await bot.SendRequest(Parse<ReopenForumTopicRequest>(json), ct),
	"deleteforumtopic" => await bot.SendRequest(Parse<DeleteForumTopicRequest>(json), ct),
	"unpinallforumtopicmessages" => await bot.SendRequest(Parse<UnpinAllForumTopicMessagesRequest>(json), ct),
	"editgeneralforumtopic" => await bot.SendRequest(Parse<EditGeneralForumTopicRequest>(json), ct),
	"closegeneralforumtopic" => await bot.SendRequest(Parse<CloseGeneralForumTopicRequest>(json), ct),
	"reopengeneralforumtopic" => await bot.SendRequest(Parse<ReopenGeneralForumTopicRequest>(json), ct),
	"hidegeneralforumtopic" => await bot.SendRequest(Parse<HideGeneralForumTopicRequest>(json), ct),
	"unhidegeneralforumtopic" => await bot.SendRequest(Parse<UnhideGeneralForumTopicRequest>(json), ct),
	"unpinallgeneralforumtopicmessages" => await bot.SendRequest(Parse<UnpinAllGeneralForumTopicMessagesRequest>(json), ct),
	"answercallbackquery" => await bot.SendRequest(Parse<AnswerCallbackQueryRequest>(json), ct),
	"getuserchatboosts" => await bot.SendRequest(Parse<GetUserChatBoostsRequest>(json), ct),
	"getbusinessconnection" => await bot.SendRequest(Parse<GetBusinessConnectionRequest>(json), ct),
	"getmanagedbottoken" => await bot.SendRequest(Parse<GetManagedBotTokenRequest>(json), ct),
	"replacemanagedbottoken" => await bot.SendRequest(Parse<ReplaceManagedBotTokenRequest>(json), ct),
	"setmycommands" => await bot.SendRequest(Parse<SetMyCommandsRequest>(json), ct),
	"deletemycommands" => await bot.SendRequest(Parse<DeleteMyCommandsRequest>(json), ct),
	"getmycommands" => await bot.SendRequest(Parse<GetMyCommandsRequest>(json), ct),
	"setmyname" => await bot.SendRequest(Parse<SetMyNameRequest>(json), ct),
	"getmyname" => await bot.SendRequest(Parse<GetMyNameRequest>(json), ct),
	"setmydescription" => await bot.SendRequest(Parse<SetMyDescriptionRequest>(json), ct),
	"getmydescription" => await bot.SendRequest(Parse<GetMyDescriptionRequest>(json), ct),
	"setmyshortdescription" => await bot.SendRequest(Parse<SetMyShortDescriptionRequest>(json), ct),
	"getmyshortdescription" => await bot.SendRequest(Parse<GetMyShortDescriptionRequest>(json), ct),
	"setmyprofilephoto" => await bot.SendRequest(Parse<SetMyProfilePhotoRequest>(json), ct),
	"removemyprofilephoto" => await bot.SendRequest(Parse<RemoveMyProfilePhotoRequest>(json), ct),
	"setchatmenubutton" => await bot.SendRequest(Parse<SetChatMenuButtonRequest>(json), ct),
	"getchatmenubutton" => await bot.SendRequest(Parse<GetChatMenuButtonRequest>(json), ct),
	"setmydefaultadministratorrights" => await bot.SendRequest(Parse<SetMyDefaultAdministratorRightsRequest>(json), ct),
	"getmydefaultadministratorrights" => await bot.SendRequest(Parse<GetMyDefaultAdministratorRightsRequest>(json), ct),
	"getavailablegifts" => await bot.SendRequest(Parse<GetAvailableGiftsRequest>(json), ct),
	"sendgift" => await bot.SendRequest(Parse<SendGiftRequest>(json), ct),
	"giftpremiumsubscription" => await bot.SendRequest(Parse<GiftPremiumSubscriptionRequest>(json), ct),
	"verifyuser" => await bot.SendRequest(Parse<VerifyUserRequest>(json), ct),
	"verifychat" => await bot.SendRequest(Parse<VerifyChatRequest>(json), ct),
	"removeuserverification" => await bot.SendRequest(Parse<RemoveUserVerificationRequest>(json), ct),
	"removechatverification" => await bot.SendRequest(Parse<RemoveChatVerificationRequest>(json), ct),
	"readbusinessmessage" => await bot.SendRequest(Parse<ReadBusinessMessageRequest>(json), ct),
	"deletebusinessmessages" => await bot.SendRequest(Parse<DeleteBusinessMessagesRequest>(json), ct),
	"setbusinessaccountname" => await bot.SendRequest(Parse<SetBusinessAccountNameRequest>(json), ct),
	"setbusinessaccountusername" => await bot.SendRequest(Parse<SetBusinessAccountUsernameRequest>(json), ct),
	"setbusinessaccountbio" => await bot.SendRequest(Parse<SetBusinessAccountBioRequest>(json), ct),
	"setbusinessaccountprofilephoto" => await bot.SendRequest(Parse<SetBusinessAccountProfilePhotoRequest>(json), ct),
	"removebusinessaccountprofilephoto" => await bot.SendRequest(Parse<RemoveBusinessAccountProfilePhotoRequest>(json), ct),
	"setbusinessaccountgiftsettings" => await bot.SendRequest(Parse<SetBusinessAccountGiftSettingsRequest>(json), ct),
	"getbusinessaccountstarbalance" => await bot.SendRequest(Parse<GetBusinessAccountStarBalanceRequest>(json), ct),
	"transferbusinessaccountstars" => await bot.SendRequest(Parse<TransferBusinessAccountStarsRequest>(json), ct),
	"getbusinessaccountgifts" => await bot.SendRequest(Parse<GetBusinessAccountGiftsRequest>(json), ct),
	"getusergifts" => await bot.SendRequest(Parse<GetUserGiftsRequest>(json), ct),
	"getchatgifts" => await bot.SendRequest(Parse<GetChatGiftsRequest>(json), ct),
	"convertgifttostars" => await bot.SendRequest(Parse<ConvertGiftToStarsRequest>(json), ct),
	"upgradegift" => await bot.SendRequest(Parse<UpgradeGiftRequest>(json), ct),
	"transfergift" => await bot.SendRequest(Parse<TransferGiftRequest>(json), ct),
	"poststory" => await bot.SendRequest(Parse<PostStoryRequest>(json), ct),
	"repoststory" => await bot.SendRequest(Parse<RepostStoryRequest>(json), ct),
	"editstory" => await bot.SendRequest(Parse<EditStoryRequest>(json), ct),
	"deletestory" => await bot.SendRequest(Parse<DeleteStoryRequest>(json), ct),
	"answerwebappquery" => await bot.SendRequest(Parse<AnswerWebAppQueryRequest>(json), ct),
	"savepreparedinlinemessage" => await bot.SendRequest(Parse<SavePreparedInlineMessageRequest>(json), ct),
	"savepreparedkeyboardbutton" => await bot.SendRequest(Parse<SavePreparedKeyboardButtonRequest>(json), ct),
	"editmessagetext" => IsInlineRequest(json) ? await bot.SendRequest(Parse<EditInlineMessageTextRequest>(json), ct) : await bot.SendRequest(Parse<EditMessageTextRequest>(json), ct),
	"editmessagecaption" => IsInlineRequest(json) ? await bot.SendRequest(Parse<EditInlineMessageCaptionRequest>(json), ct) : await bot.SendRequest(Parse<EditMessageCaptionRequest>(json), ct),
	"editmessagemedia" => IsInlineRequest(json) ? await bot.SendRequest(Parse<EditInlineMessageMediaRequest>(json), ct) : await bot.SendRequest(Parse<EditMessageMediaRequest>(json), ct),
	"editmessagelivelocation" => IsInlineRequest(json) ? await bot.SendRequest(Parse<EditInlineMessageLiveLocationRequest>(json), ct) : await bot.SendRequest(Parse<EditMessageLiveLocationRequest>(json), ct),
	"stopmessagelivelocation" => IsInlineRequest(json) ? await bot.SendRequest(Parse<StopInlineMessageLiveLocationRequest>(json), ct) : await bot.SendRequest(Parse<StopMessageLiveLocationRequest>(json), ct),
	"editmessagechecklist" => await bot.SendRequest(Parse<EditMessageChecklistRequest>(json), ct),
	"editmessagereplymarkup" => IsInlineRequest(json) ? await bot.SendRequest(Parse<EditInlineMessageReplyMarkupRequest>(json), ct) : await bot.SendRequest(Parse<EditMessageReplyMarkupRequest>(json), ct),
	"stoppoll" => await bot.SendRequest(Parse<StopPollRequest>(json), ct),
	"approvesuggestedpost" => await bot.SendRequest(Parse<ApproveSuggestedPostRequest>(json), ct),
	"declinesuggestedpost" => await bot.SendRequest(Parse<DeclineSuggestedPostRequest>(json), ct),
	"deletemessage" => await bot.SendRequest(Parse<DeleteMessageRequest>(json), ct),
	"deletemessages" => await bot.SendRequest(Parse<DeleteMessagesRequest>(json), ct),
	"sendsticker" => await bot.SendRequest(Parse<SendStickerRequest>(json), ct),
	"getstickerset" => await bot.SendRequest(Parse<GetStickerSetRequest>(json), ct),
	"getcustomemojistickers" => await bot.SendRequest(Parse<GetCustomEmojiStickersRequest>(json), ct),
	"uploadstickerfile" => await bot.SendRequest(Parse<UploadStickerFileRequest>(json), ct),
	"createnewstickerset" => await bot.SendRequest(Parse<CreateNewStickerSetRequest>(json), ct),
	"addstickertoset" => await bot.SendRequest(Parse<AddStickerToSetRequest>(json), ct),
	"setstickerpositioninset" => await bot.SendRequest(Parse<SetStickerPositionInSetRequest>(json), ct),
	"deletestickerfromset" => await bot.SendRequest(Parse<DeleteStickerFromSetRequest>(json), ct),
	"replacestickerinset" => await bot.SendRequest(Parse<ReplaceStickerInSetRequest>(json), ct),
	"setstickeremojilist" => await bot.SendRequest(Parse<SetStickerEmojiListRequest>(json), ct),
	"setstickerkeywords" => await bot.SendRequest(Parse<SetStickerKeywordsRequest>(json), ct),
	"setstickermaskposition" => await bot.SendRequest(Parse<SetStickerMaskPositionRequest>(json), ct),
	"setstickersettitle" => await bot.SendRequest(Parse<SetStickerSetTitleRequest>(json), ct),
	"setstickersetthumbnail" => await bot.SendRequest(Parse<SetStickerSetThumbnailRequest>(json), ct),
	"setcustomemojistickersetthumbnail" => await bot.SendRequest(Parse<SetCustomEmojiStickerSetThumbnailRequest>(json), ct),
	"deletestickerset" => await bot.SendRequest(Parse<DeleteStickerSetRequest>(json), ct),
	"answerinlinequery" => await bot.SendRequest(Parse<AnswerInlineQueryRequest>(json), ct),
	"sendinvoice" => await bot.SendRequest(Parse<SendInvoiceRequest>(json), ct),
	"createinvoicelink" => await bot.SendRequest(Parse<CreateInvoiceLinkRequest>(json), ct),
	"answershippingquery" => await bot.SendRequest(Parse<AnswerShippingQueryRequest>(json), ct),
	"answerprecheckoutquery" => await bot.SendRequest(Parse<AnswerPreCheckoutQueryRequest>(json), ct),
	"getmystarbalance" => await bot.SendRequest(Parse<GetMyStarBalanceRequest>(json), ct),
	"getstartransactions" => await bot.SendRequest(Parse<GetStarTransactionsRequest>(json), ct),
	"refundstarpayment" => await bot.SendRequest(Parse<RefundStarPaymentRequest>(json), ct),
	"edituserstarsubscription" => await bot.SendRequest(Parse<EditUserStarSubscriptionRequest>(json), ct),
	"setpassportdataerrors" => await bot.SendRequest(Parse<SetPassportDataErrorsRequest>(json), ct),
	"sendgame" => await bot.SendRequest(Parse<SendGameRequest>(json), ct),
	"setgamescore" => IsInlineRequest(json) ? await bot.SendRequest(Parse<SetInlineGameScoreRequest>(json), ct) : await bot.SendRequest(Parse<SetGameScoreRequest>(json), ct),
	"getgamehighscores" => IsInlineRequest(json) ? await bot.SendRequest(Parse<GetInlineGameHighScoresRequest>(json), ct) : await bot.SendRequest(Parse<GetGameHighScoresRequest>(json), ct),
	_ => throw new ApiRequestException("Not Found")
};
