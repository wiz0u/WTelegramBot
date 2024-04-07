using System.Text;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using TL;
using KeyboardButton = Telegram.Bot.Types.ReplyMarkups.KeyboardButton;
using ReplyKeyboardMarkup = Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup;

namespace Telegram.Bot;

public partial class TelegramBotClient
{
	private async Task<ReplyMarkup?> MakeReplyMarkup(IReplyMarkup? replyMarkup) => replyMarkup switch
	{
		ReplyKeyboardRemove rkr => new ReplyKeyboardHide { flags = rkr.Selective == true ? ReplyKeyboardHide.Flags.selective : 0 },
		ForceReplyMarkup frm => new ReplyKeyboardForceReply
		{
			flags = (frm.Selective == true ? ReplyKeyboardForceReply.Flags.selective : 0) | (frm.InputFieldPlaceholder != null ? ReplyKeyboardForceReply.Flags.has_placeholder : 0),
			placeholder = frm.InputFieldPlaceholder
		},
		ReplyKeyboardMarkup rkm => new TL.ReplyKeyboardMarkup
		{
			flags = (rkm.Selective == true ? TL.ReplyKeyboardMarkup.Flags.selective : 0)
				| (rkm.InputFieldPlaceholder != null ? TL.ReplyKeyboardMarkup.Flags.has_placeholder : 0)
				| (rkm.ResizeKeyboard == true ? TL.ReplyKeyboardMarkup.Flags.resize : 0)
				| (rkm.OneTimeKeyboard == true ? TL.ReplyKeyboardMarkup.Flags.single_use : 0),
			placeholder = rkm.InputFieldPlaceholder,
			rows = rkm.Keyboard.Select(row => new KeyboardButtonRow { buttons = row.Select(MakeKeyboardButton).ToArray() }).ToArray()
		},
		InlineKeyboardMarkup ikm => new ReplyInlineMarkup
		{
			rows = await Task.WhenAll(ikm.InlineKeyboard.Select(
				async row => new KeyboardButtonRow { buttons = await Task.WhenAll(row.Select(MakeKeyboardButton)) }))
		} is { rows.Length: not 0 } rim ? rim : null,
		_ => null,
	};

	private static KeyboardButtonBase MakeKeyboardButton(KeyboardButton btn)
	{
		if (btn.RequestContact == true) return new KeyboardButtonRequestPhone { text = btn.Text };
		if (btn.RequestLocation == true) return new KeyboardButtonRequestGeoLocation { text = btn.Text };
		if (btn.RequestPoll != null) return new KeyboardButtonRequestPoll
		{
			text = btn.Text,
			quiz = btn.RequestPoll.Type is "quiz",
			flags = btn.RequestPoll.Type is "quiz" or "regular" ? KeyboardButtonRequestPoll.Flags.has_quiz : 0
		};
		if (btn.WebApp != null) return new KeyboardButtonSimpleWebView { text = btn.Text, url = btn.WebApp.Url };
		return new TL.KeyboardButton { text = btn.Text };
	}

	private async Task<KeyboardButtonBase> MakeKeyboardButton(InlineKeyboardButton btn)
	{
		if (btn.Url != null) return new KeyboardButtonUrl { text = btn.Text, url = btn.Url };
		if (btn.CallbackData != null) return new KeyboardButtonCallback { text = btn.Text, data = Encoding.UTF8.GetBytes(btn.CallbackData) };
		if (btn.CallbackGame != null) return new KeyboardButtonGame { text = btn.Text };
		if (btn.Pay == true) return new KeyboardButtonBuy { text = btn.Text };
		if (btn.SwitchInlineQuery != null) return new KeyboardButtonSwitchInline { text = btn.Text, query = btn.SwitchInlineQuery };
		if (btn.SwitchInlineQueryCurrentChat != null) return new KeyboardButtonSwitchInline { text = btn.Text, query = btn.SwitchInlineQuery, flags = KeyboardButtonSwitchInline.Flags.same_peer };
		if (btn.LoginUrl != null) return new InputKeyboardButtonUrlAuth
		{
			text = btn.Text,
			url = btn.LoginUrl.Url,
			fwd_text = btn.LoginUrl.ForwardText,
			flags = (btn.LoginUrl.ForwardText != null ? InputKeyboardButtonUrlAuth.Flags.has_fwd_text : 0)
				| (btn.LoginUrl.RequestWriteAccess == true ? InputKeyboardButtonUrlAuth.Flags.request_write_access : 0),
			bot = btn.LoginUrl.BotUsername != null ? await InputUser(btn.LoginUrl.BotUsername) : Client.User
		};
		if (btn.WebApp != null) return new KeyboardButtonWebView { text = btn.Text, url = btn.WebApp.Url };
		return new TL.KeyboardButton { text = btn.Text };
	}

	protected async Task<Message?> GetReplyToMessage(InputPeer peer, int? replyToMessageId, bool? allowSendingWithoutReply)
	{
		if (replyToMessageId > 0)
		{
			var msg = await GetMessage(peer, replyToMessageId.Value);
			if (msg == null)
				return allowSendingWithoutReply == true ? null : throw new ApiRequestException("message to reply not found", 400);
			return await MakeMessage(msg);
		}
		return null;
	}

	protected InputReplyTo? MakeReplyTo(int? replyToMessageId, int? messageThreadId)
	{
		if (replyToMessageId > 0)
			return new InputReplyToMessage
			{
				reply_to_msg_id = replyToMessageId.Value,
				top_msg_id = messageThreadId ?? 0,
				flags = messageThreadId.HasValue ? InputReplyToMessage.Flags.has_top_msg_id : 0
			};
		else if (messageThreadId > 0)
			return new InputReplyToMessage { top_msg_id = messageThreadId.Value, flags = InputReplyToMessage.Flags.has_top_msg_id };
		return null;
	}

	//TODO: replace Client.GetMessages everywhere by this method?
	protected async Task<MessageBase?> GetMessage(InputPeer peer, int messageId)
	{
		if (peer == null || messageId == 0) return null;
		//TODO: check in cache first
		var msgs = await Client.GetMessages(peer, messageId);
		msgs.UserOrChat(_collector);
		return msgs.Messages.FirstOrDefault();
	}

	protected string? ApplyParse(ParseMode? parseMode, string? text, ref MessageEntity[]? entities)
	{
		if (parseMode == null) return text;
		IEnumerable<MessageEntity>? entities_ = entities;
		ApplyParse(parseMode, ref text, ref entities_);
		entities = (MessageEntity[]?)entities_;
		return text;
	}

	protected void ApplyParse(ParseMode? parseMode, ref string? text, ref IEnumerable<MessageEntity>? entities)
	{
		if (entities != null || text == null) return;
		switch (parseMode)
		{
			case ParseMode.Markdown:
			case ParseMode.MarkdownV2:
				entities = Client.MarkdownToEntities(ref text, false, _collector);
				break;
			case ParseMode.Html:
				entities = Client.HtmlToEntities(ref text, false, _collector);
				break;
		}
	}

	private async Task<TL.BotCommandScope?> BotCommandScope(BotCommandScope? scope)
	=> scope switch
	{
		BotCommandScopeAllPrivateChats => new BotCommandScopeUsers(),
		BotCommandScopeAllGroupChats => new BotCommandScopeChats(),
		BotCommandScopeAllChatAdministrators => new BotCommandScopeChatAdmins(),
		BotCommandScopeChat bcsc => new BotCommandScopePeer { peer = await InputPeerChat(bcsc.ChatId) },
		BotCommandScopeChatAdministrators bcsca => new BotCommandScopePeerAdmins { peer = await InputPeerChat(bcsca.ChatId) },
		BotCommandScopeChatMember bcscm => new BotCommandScopePeerUser { peer = await InputPeerChat(bcscm.ChatId), user_id = InputUser(bcscm.UserId) },
		_ => null
	};

	private async Task<InputChatPhotoBase> InputChatPhoto(InputFileStream photo)
	{
		switch (photo.FileType)
		{
			case FileType.Id when photo is InputTelegramFile itf:
				if (itf.FileId!.ParseFileId().location is InputPhotoFileLocation ipfl)
					return new InputChatPhoto { id = new InputPhoto { id = ipfl.id, access_hash = ipfl.access_hash, file_reference = ipfl.file_reference } };
				break;
			case FileType.Stream:
				var inputFile = await Client.UploadFileAsync(photo.Content, photo.FileName);
				return new InputChatUploadedPhoto { file = inputFile, flags = InputChatUploadedPhoto.Flags.has_file };
		}
		throw new ApiRequestException("Unrecognized InputFileStream type");
	}

	private static InputPhoto InputPhoto(string fileId)
	{
		var location = (InputPhotoFileLocation)fileId.ParseFileId().location;
		return new InputPhoto { id = location.id, access_hash = location.access_hash, file_reference = location.file_reference };
	}

	public async Task<TL.InputMedia> InputMediaPhoto(InputOnlineFile file, bool? hasSpoiler = false)
	{
		switch (file.FileType)
		{
			case FileType.Id:
				return new TL.InputMediaPhoto { id = InputPhoto(file.FileId!), flags = hasSpoiler == true ? TL.InputMediaPhoto.Flags.spoiler : 0 };
			case FileType.Url:
				return new InputMediaPhotoExternal { url = file.Url, flags = hasSpoiler == true ? InputMediaPhotoExternal.Flags.spoiler : 0 };
			default: //case FileType.Stream:
				var uploadedFile = await Client.UploadFileAsync(file.Content, file.FileName);
				return new InputMediaUploadedPhoto { file = uploadedFile, flags = hasSpoiler == true ? InputMediaUploadedPhoto.Flags.spoiler : 0 };
		}
	}

	private static InputDocument InputDocument(string fileId)
	{
		var location = (InputDocumentFileLocation)fileId.ParseFileId().location;
		return new InputDocument { id = location.id, access_hash = location.access_hash, file_reference = location.file_reference };
	}

	public async Task<TL.InputMedia> InputMediaDocument(InputFileStream file, bool? hasSpoiler = false, string? mimeType = null)
	{
		switch (file.FileType)
		{
			case FileType.Id:
				return new TL.InputMediaDocument { id = InputDocument(((InputTelegramFile)file).FileId!), flags = hasSpoiler == true ? TL.InputMediaDocument.Flags.spoiler : 0 };
			case FileType.Url:
				return new InputMediaDocumentExternal { url = ((InputOnlineFile)file).Url, flags = hasSpoiler == true ? InputMediaDocumentExternal.Flags.spoiler : 0 };
			default: //case FileType.Stream:
				var uploadedFile = await Client.UploadFileAsync(file.Content, file.FileName);
				if (mimeType == null)
				{
					string? fileExt = Path.GetExtension(file.FileName);
					fileExt ??= Path.GetExtension((file.Content as FileStream)?.Name);
					mimeType = string.IsNullOrEmpty(fileExt) ? null : Helpers.GetMimeType(fileExt);
				}
				return new InputMediaUploadedDocument(uploadedFile, mimeType) { flags = hasSpoiler == true ? InputMediaUploadedDocument.Flags.spoiler : 0 };
		}
	}

	private async Task<InputStickerSetItem> InputStickerSetItem(long userId, InputFileStream file, string emojis, MaskPosition? maskPosition, string? mimeType = null)
	{
		var peer = InputPeerUser(userId);
		var media = await InputMediaDocument(file, mimeType: mimeType);
		var messageMedia = await Client.Messages_UploadMedia(peer, media);
		if (messageMedia is not MessageMediaDocument { document: TL.Document doc })
			throw new ApiRequestException("Unexpected UploadMedia result");
		return new InputStickerSetItem
		{
			document = doc,
			emoji = emojis,
			mask_coords = maskPosition.MaskCoord(),
			flags = maskPosition != null ? TL.InputStickerSetItem.Flags.has_mask_coords : 0
		};
	}

	private static Sticker MakeSticker(TL.Document doc, DocumentAttributeSticker? sticker, Messages_StickerSet? mss)
	{
		var result = new Sticker
		{
			FileSize = doc.size,
			IsAnimated = doc.mime_type == "application/x-tgsticker",
			IsVideo = doc.mime_type == "video/webm",
			Thumb = doc.LargestThumbSize?.PhotoSize(doc.ToFileLocation(doc.LargestThumbSize), doc.dc_id),
			Emoji = sticker?.alt ?? mss?.packs.FirstOrDefault(sp => sp.documents.Contains(doc.id))?.emoticon,
			SetName = mss?.set.short_name,
			MaskPosition = sticker?.mask_coords == null ? null : new MaskPosition
			{
				Point = (MaskPositionPoint)(sticker.mask_coords.n + 1),
				XShift = (float)sticker.mask_coords.x,
				YShift = (float)sticker.mask_coords.y,
				Scale = (float)sticker.mask_coords.zoom
			}
		}.SetFileIds(doc.ToFileLocation(), doc.dc_id);
		if (doc.GetAttribute<DocumentAttributeImageSize>() is { } imageSize) { result.Width = imageSize.w; result.Height = imageSize.h; }
		else if (doc.GetAttribute<DocumentAttributeVideo>() is { } video) { result.Width = video.w; result.Height = video.h; }
		else if (result.IsAnimated) { result.Width = result.Height = doc.GetAttribute<DocumentAttributeCustomEmoji>() is null ? 512 : 100; }
		return result;
	}

	private async Task SetDocThumb(InputMediaUploadedDocument doc, InputMedia? thumb)
	{
		if (thumb?.FileType != FileType.Stream) return;
		doc.thumb = await Client.UploadFileAsync(thumb.Content, thumb.FileName);
		doc.flags |= InputMediaUploadedDocument.Flags.has_thumb;
	}

	private static InputMediaGeoLive MakeGeoLive(double latitude, double longitude, float? horizontalAccuracy, int? heading, int? proximityAlertRadius, int livePeriod = 0)
	=> new()
	{
		geo_point = new InputGeoPoint
		{
			lat = latitude,
			lon = longitude,
			accuracy_radius = (int)horizontalAccuracy.GetValueOrDefault(),
			flags = horizontalAccuracy.HasValue ? InputGeoPoint.Flags.has_accuracy_radius : 0
		},
		period = livePeriod,
		heading = heading.GetValueOrDefault(),
		proximity_notification_radius = proximityAlertRadius.GetValueOrDefault(),
		flags = (livePeriod > 0 ? InputMediaGeoLive.Flags.has_period : 0)
			| (heading.HasValue ? InputMediaGeoLive.Flags.has_heading : 0)
			| (proximityAlertRadius.HasValue ? InputMediaGeoLive.Flags.has_proximity_notification_radius : 0)
	};

	private async Task<InputBotInlineResultBase[]> InputBotInlineResults(IEnumerable<InlineQueryResult> results)
		=> await Task.WhenAll(results.Select(InputBotInlineResult));

	private async Task<InputBotInlineResultBase> InputBotInlineResult(InlineQueryResult result)
	{
		if (result is InlineQueryResultGame game)
			return new InputBotInlineResultGame
			{
				id = result.Id, short_name = game.GameShortName, send_message = new InputBotInlineMessageGame
				{
					reply_markup = await MakeReplyMarkup(game.ReplyMarkup),
					flags = game.ReplyMarkup != null ? InputBotInlineMessageGame.Flags.has_reply_markup : 0 }
			};
		if (result is InlineQueryResultCachedPhoto cachedPhoto)
			return new InputBotInlineResultPhoto
			{
				id = result.Id, type = "photo", photo = InputPhoto(cachedPhoto.PhotoFileId),
				send_message = await InputBotInlineMessage(result, cachedPhoto.InputMessageContent, cachedPhoto.Caption, cachedPhoto.ParseMode, cachedPhoto.CaptionEntities)
			};
		InputBotInlineResultDocument? cached = result switch
		{
			InlineQueryResultCachedAudio c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.AudioFileId },
			InlineQueryResultCachedDocument c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.DocumentFileId, title = c.Title, description = c.Description, type = "file" },
			InlineQueryResultCachedGif c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.GifFileId, title = c.Title },
			InlineQueryResultCachedMpeg4Gif c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.Mpeg4FileId, title = c.Title, type = "gif" },
			InlineQueryResultCachedSticker c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent), id = c.StickerFileId },
			InlineQueryResultCachedVideo c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.VideoFileId, title = c.Title, description = c.Description },
			InlineQueryResultCachedVoice c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.VoiceFileId, title = c.Title },
			_ => null
		};
		if (cached != null)
		{
			cached.type ??= result.Type.ToString().ToLower();
			cached.document = InputDocument(cached.id); // above, we used the id to store the fileId
			cached.id = result.Id;
			cached.flags = (cached.title != null ? InputBotInlineResultDocument.Flags.has_title : 0)
				| (cached.description != null ? InputBotInlineResultDocument.Flags.has_description : 0);
			return cached;
		}

		return await (result switch
		{
			InlineQueryResultArticle r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, null, null, null,
				r.ThumbUrl, "image/jpeg", r.ThumbWidth, r.ThumbHeight,
				r.Url, "text/html", url: r.HideUrl == true ? null : r.Url),
			InlineQueryResultAudio r => MakeIbir(r, r.Title, r.Performer, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				null, null, 0, 0,
				r.AudioUrl, "audio/mpeg", new DocumentAttributeAudio { duration = r.AudioDuration ?? 0, title = r.Title, performer = r.Performer, flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.has_performer }),
			InlineQueryResultContact r => MakeIbir(r, r.LastName == null ? r.FirstName : $"{r.FirstName} {r.LastName}", r.PhoneNumber, r.InputMessageContent, null, null, null,
				r.ThumbUrl, "image/jpeg", r.ThumbWidth, r.ThumbHeight),
			InlineQueryResultDocument r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				r.ThumbUrl, "image/jpeg", r.ThumbWidth, r.ThumbHeight,
				r.DocumentUrl, r.MimeType, null, "file"),
			InlineQueryResultGif r => MakeIbir(r, r.Title, null, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				r.ThumbUrl, r.ThumbMimeType, 0, 0,
				r.GifUrl, "image/gif", r.GifWidth + r.GifHeight > 0 ? new DocumentAttributeImageSize { w = r.GifWidth ?? 0, h = r.GifHeight ?? 0 } : null),
			InlineQueryResultLocation r => MakeIbir(r, r.Title, $"{r.Latitude} {r.Longitude}", r.InputMessageContent, null, null, null,
				r.ThumbUrl, "image/jpeg", r.ThumbWidth, r.ThumbHeight, type: "geo"),
			InlineQueryResultMpeg4Gif r => MakeIbir(r, r.Title, null, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				r.ThumbUrl, r.ThumbMimeType ?? "image/jpeg", 0, 0,
				r.Mpeg4Url, "video/mp4", r.Mpeg4Width + r.Mpeg4Height > 0 ? new DocumentAttributeVideo { w = r.Mpeg4Width ?? 0, h = r.Mpeg4Height ?? 0, duration = r.Mpeg4Duration ?? 0 } : null, "gif"),
			InlineQueryResultPhoto r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				r.ThumbUrl, "image/jpeg", 0, 0,
				r.PhotoUrl, "image/jpeg", r.PhotoWidth + r.PhotoHeight > 0 ? new DocumentAttributeImageSize { w = r.PhotoWidth ?? 0, h = r.PhotoHeight ?? 0 } : null),
			InlineQueryResultVenue r => MakeIbir(r, r.Title, r.Address, r.InputMessageContent, null, null, null,
				r.ThumbUrl, "image/jpeg", r.ThumbWidth, r.ThumbHeight),
			InlineQueryResultVideo r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				r.ThumbUrl, "image/jpeg", 0, 0,
				r.VideoUrl, r.MimeType, r.VideoWidth + r.VideoHeight > 0 ? new DocumentAttributeVideo { w = r.VideoWidth ?? 0, h = r.VideoHeight ?? 0, duration = r.VideoDuration ?? 0 } : null),
			InlineQueryResultVoice r => MakeIbir(r, r.Title, null, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities,
				null, null, 0, 0,
				r.VoiceUrl, "audio/ogg", new DocumentAttributeAudio { duration = r.VoiceDuration ?? 0, flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.voice }),
			_ => throw new NotSupportedException()
		});
	}

	private async Task<InputBotInlineResult> MakeIbir(InlineQueryResult r, string? title, string? description, 
		InputMessageContent? imc, string? caption, ParseMode? parseMode, MessageEntity[]? captionEntities,
		string? thumbnail_url = null, string? thumbnail_type = null, int? thumbWidth = 0, int? thumbHeight = 0, 
		string? content_url = null, string? content_type = null, DocumentAttribute? attribute = null, 
		string? type = null, string? url = null)
	{
		InputWebDocument? thumb = null, content = null;
		if (content_url != null)
		{
			content = new InputWebDocument { url = content_url, mime_type = content_type };
			var filename = new DocumentAttributeFilename { file_name = Path.GetFileName(content_url) };
			content.attributes = attribute != null ? [attribute, filename] : [filename];
		}
		if (thumbnail_url != null)
		{
			thumb = new InputWebDocument { url = thumbnail_url, mime_type = thumbnail_type };
			if (thumbWidth > 0 & thumbHeight > 0) thumb.attributes = [new DocumentAttributeImageSize { w = thumbWidth ?? 0, h = thumbHeight ?? 0 }];
		}
		return new()
		{
			id = r.Id, title = title, description = description, url = url, thumb = thumb, content = content,
			type = type ?? r.Type.ToString().ToLower(),
			send_message = await InputBotInlineMessage(r, imc, caption, parseMode, captionEntities),
			flags = (title != null ? TL.InputBotInlineResult.Flags.has_title : 0)
			| (description != null ? TL.InputBotInlineResult.Flags.has_description : 0)
			| (url != null ? TL.InputBotInlineResult.Flags.has_url : 0)
			| (thumb != null ? TL.InputBotInlineResult.Flags.has_thumb : 0)
			| (content != null ? TL.InputBotInlineResult.Flags.has_content : 0)
		};
	}

	private async Task<InputBotInlineMessage> InputBotInlineMessage(InlineQueryResult iqr,
		InputMessageContent? message, string? caption = null, ParseMode? parseMode = null, MessageEntity[]? captionEntities = null)
	{
		if (message != null) captionEntities = null;
		var reply_markup = await MakeReplyMarkup(iqr.ReplyMarkup);
		return message switch
		{
			InputTextMessageContent itmc => new InputBotInlineMessageText
			{ reply_markup = reply_markup,
				message = ApplyParse(itmc.ParseMode, itmc.MessageText, ref captionEntities), entities = itmc.Entities ?? captionEntities, 
				flags = (reply_markup != null ? InputBotInlineMessageText.Flags.has_reply_markup : 0) |
						((itmc.Entities ?? captionEntities) != null ? InputBotInlineMessageText.Flags.has_entities : 0) |
						(itmc.DisableWebPagePreview == true ? InputBotInlineMessageText.Flags.no_webpage : 0)},
			InputContactMessageContent icmc => new InputBotInlineMessageMediaContact
			{ reply_markup = reply_markup,
				phone_number = icmc.PhoneNumber, first_name = icmc.FirstName, last_name = icmc.LastName, vcard = icmc.Vcard,
				flags = reply_markup != null ? InputBotInlineMessageMediaContact.Flags.has_reply_markup : 0 },
			InputVenueMessageContent ivmc => new InputBotInlineMessageMediaVenue
			{ reply_markup = reply_markup,
				geo_point = new InputGeoPoint { lat = ivmc.Latitude, lon = ivmc.Longitude },
				title = ivmc.Title, address = ivmc.Address, provider = ivmc.GooglePlaceId != null ? "gplaces" : "foursquare",
				venue_id = ivmc.GooglePlaceId ?? ivmc.FoursquareId, venue_type = ivmc.GooglePlaceType ?? ivmc.FoursquareType,
				flags = reply_markup != null ? InputBotInlineMessageMediaVenue.Flags.has_reply_markup : 0 },
			InputLocationMessageContent ilmc => new InputBotInlineMessageMediaGeo
			{ reply_markup = reply_markup,
				geo_point = new InputGeoPoint
				{ lat = ilmc.Latitude, lon = ilmc.Longitude, accuracy_radius = (int)(ilmc.HorizontalAccuracy ?? 0), 
					flags = ilmc.HorizontalAccuracy.HasValue ? InputGeoPoint.Flags.has_accuracy_radius : 0 },
				heading = ilmc.Heading ?? 0, period = ilmc.LivePeriod ?? 0, proximity_notification_radius = ilmc.ProximityAlertRadius ?? 0,
				flags = (reply_markup != null ? InputBotInlineMessageMediaGeo.Flags.has_reply_markup : 0)
					| (ilmc.LivePeriod > 0 ? InputBotInlineMessageMediaGeo.Flags.has_period : 0)
					| (ilmc.Heading.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_heading : 0)
					| (ilmc.ProximityAlertRadius.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_proximity_notification_radius : 0) },
			InputInvoiceMessageContent iimc => new InputBotInlineMessageMediaInvoice
			{ reply_markup = reply_markup,
				title = iimc.Title, description = iimc.Description,
				photo = new InputWebDocument
				{ url = iimc.PhotoUrl, size = iimc.PhotoSize ?? 0, mime_type = "image/jpeg",
				attributes = iimc.PhotoWidth + iimc.PhotoHeight > 0 ? [new TL.DocumentAttributeImageSize { w = iimc.PhotoWidth ?? 0, h = iimc.PhotoHeight ?? 0 }] : null },
				invoice = new Invoice
				{
					flags = (iimc.MaxTipAmount.HasValue ? Invoice.Flags.has_max_tip_amount : 0)
						| (iimc.NeedName == true ? Invoice.Flags.name_requested : 0)
						| (iimc.NeedPhoneNumber == true ? Invoice.Flags.phone_requested : 0)
						| (iimc.NeedEmail == true ? Invoice.Flags.email_requested : 0)
						| (iimc.NeedShippingAddress == true ? Invoice.Flags.shipping_address_requested : 0)
						| (iimc.SendPhoneNumberToProvider == true ? Invoice.Flags.phone_to_provider : 0)
						| (iimc.SendEmailToProvider == true ? Invoice.Flags.email_to_provider : 0)
						| (iimc.IsFlexible == true ? Invoice.Flags.flexible : 0),
					currency = iimc.Currency,
					prices = iimc.Prices.LabeledPrices(),
					max_tip_amount = iimc.MaxTipAmount ?? 0,
					suggested_tip_amounts = iimc.SuggestedTipAmounts?.Select(sta => (long)sta).ToArray(),
				},
				payload = Encoding.UTF8.GetBytes(iimc.Payload), provider = iimc.ProviderToken, 
				provider_data = new DataJSON { data = iimc.ProviderData ?? "null" },
				flags = reply_markup != null ? InputBotInlineMessageMediaInvoice.Flags.has_reply_markup : 0 },
			null => iqr switch
			{
				InlineQueryResultContact iqrc => new InputBotInlineMessageMediaContact
				{ reply_markup = reply_markup,
					phone_number = iqrc.PhoneNumber, first_name = iqrc.FirstName, last_name = iqrc.LastName, vcard = iqrc.Vcard,
					flags = reply_markup != null ? InputBotInlineMessageMediaContact.Flags.has_reply_markup : 0 },
				InlineQueryResultLocation iqrl => new InputBotInlineMessageMediaGeo
				{ reply_markup = reply_markup,
					geo_point = new InputGeoPoint
					{ lat = iqrl.Latitude, lon = iqrl.Longitude, accuracy_radius = (int)(iqrl.HorizontalAccuracy ?? 0), 
						flags = iqrl.HorizontalAccuracy.HasValue ? InputGeoPoint.Flags.has_accuracy_radius : 0 },
					heading = iqrl.Heading ?? 0, period = iqrl.LivePeriod ?? 0, proximity_notification_radius = iqrl.ProximityAlertRadius ?? 0,
					flags = (reply_markup != null ? InputBotInlineMessageMediaGeo.Flags.has_reply_markup : 0)
						| (iqrl.LivePeriod > 0 ? InputBotInlineMessageMediaGeo.Flags.has_period : 0)
						| (iqrl.Heading.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_heading : 0)
						| (iqrl.ProximityAlertRadius.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_proximity_notification_radius : 0) },
				InlineQueryResultVenue iqrv => new InputBotInlineMessageMediaVenue
				{ reply_markup = reply_markup,
					geo_point = new InputGeoPoint { lat = iqrv.Latitude, lon = iqrv.Longitude },
					title = iqrv.Title, address = iqrv.Address, provider = iqrv.GooglePlaceId != null ? "gplaces" : "foursquare",
					venue_id = iqrv.GooglePlaceId ?? iqrv.FoursquareId, venue_type = iqrv.GooglePlaceType ?? iqrv.FoursquareType,
					flags = reply_markup != null ? InputBotInlineMessageMediaVenue.Flags.has_reply_markup : 0 },
				_ => new InputBotInlineMessageMediaAuto
				{ reply_markup = reply_markup,
					message = ApplyParse(parseMode, caption, ref captionEntities), entities = captionEntities, 
					flags = (reply_markup != null ? InputBotInlineMessageMediaAuto.Flags.has_reply_markup : 0) |
							(captionEntities != null ? InputBotInlineMessageMediaAuto.Flags.has_entities: 0) },
			},
			_ => throw new NotImplementedException()
		};
	}

	public ApiRequestException MakeException(WTelegram.WTException ex)
	{
		if (ex is not RpcException rpcEx) return new ApiRequestException(ex.Message, ex);
		var msg = ex.Message switch
		{
			"MESSAGE_NOT_MODIFIED" => "message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message",
			"WC_CONVERT_URL_INVALID" or "EXTERNAL_URL_INVALID" => "Wrong HTTP URL specified",
			"WEBPAGE_CURL_FAILED" => "Failed to get HTTP URL content",
			"WEBPAGE_MEDIA_EMPTY" => "Wrong type of the web page content",
			"MEDIA_GROUPED_INVALID" => "Can't use the media of the specified type in the album",
			"REPLY_MARKUP_TOO_LONG" => "reply markup is too long",
			"INPUT_USER_DEACTIVATED" => "user is deactivated", // 403
			"USER_IS_BLOCKED" => "bot was blocked by the user", // 403
			"USER_ADMIN_INVALID" => "user is an administrator of the chat",
			"File generation failed" => "can't upload file by URL",
			"CHAT_ABOUT_NOT_MODIFIED" => "chat description is not modified",
			"PACK_SHORT_NAME_INVALID" => "invalid sticker set name is specified",
			"PACK_SHORT_NAME_OCCUPIED" => "sticker set name is already occupied",
			"STICKER_EMOJI_INVALID" => "invalid sticker emojis",
			"QUERY_ID_INVALID" => "query is too old and response timeout expired or query ID is invalid",
			"MESSAGE_DELETE_FORBIDDEN" => "message can't be deleted",
			_ => ex.Message,
		};
		msg = rpcEx.Code switch
		{
			401 => "Unauthorized: " + msg,
			403 => "Forbidden: " + msg,
			500 => "Internal Server Error: " + msg,
			_ => "Bad Request: " + msg,
		};
		return new ApiRequestException(msg, rpcEx.Code, ex);
	}
}
