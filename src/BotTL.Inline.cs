using System.Text;
using TL;
using MessageEntity = Telegram.Bot.Types.MessageEntity;

namespace WTelegram;

public partial class Bot
{
	private async Task<InputBotInlineResultBase[]> InputBotInlineResults(IEnumerable<InlineQueryResult> results)
		=> await results.Select(InputBotInlineResult).WhenAllSequential();

	private async Task<InputBotInlineResultBase> InputBotInlineResult(InlineQueryResult result)
	{
		if (result is InlineQueryResultGame game)
			return new InputBotInlineResultGame
			{
				id = result.Id,
				short_name = game.GameShortName,
				send_message = new InputBotInlineMessageGame
				{
					reply_markup = await MakeReplyMarkup(game.ReplyMarkup),
					flags = game.ReplyMarkup != null ? InputBotInlineMessageGame.Flags.has_reply_markup : 0
				}
			};
		if (result is InlineQueryResultCachedPhoto cp)
			return new InputBotInlineResultPhoto
			{
				id = result.Id,
				type = "photo",
				photo = InputPhoto(cp.PhotoFileId),
				send_message = await InputBotInlineMessage(result, cp.InputMessageContent, cp.Caption, cp.ParseMode, cp.CaptionEntities, cp.ShowCaptionAboveMedia)
			};
		InputBotInlineResultDocument? cached = result switch
		{
			InlineQueryResultCachedAudio c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.AudioFileId },
			InlineQueryResultCachedDocument c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities), id = c.DocumentFileId, title = c.Title, description = c.Description, type = "file" },
			InlineQueryResultCachedGif c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities, c.ShowCaptionAboveMedia), id = c.GifFileId, title = c.Title },
			InlineQueryResultCachedMpeg4Gif c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities, c.ShowCaptionAboveMedia), id = c.Mpeg4FileId, title = c.Title, type = "gif" },
			InlineQueryResultCachedSticker c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent), id = c.StickerFileId },
			InlineQueryResultCachedVideo c => new()
			{ send_message = await InputBotInlineMessage(c, c.InputMessageContent, c.Caption, c.ParseMode, c.CaptionEntities, c.ShowCaptionAboveMedia), id = c.VideoFileId, title = c.Title, description = c.Description },
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
			InlineQueryResultArticle r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, null, default, null, false,
				r.ThumbnailUrl, "image/jpeg", r.ThumbnailWidth, r.ThumbnailHeight,
				r.Url, "text/html", url: r.Url),
			InlineQueryResultAudio r => MakeIbir(r, r.Title, r.Performer, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, false,
				null, null, 0, 0,
				r.AudioUrl, "audio/mpeg", new DocumentAttributeAudio { duration = r.AudioDuration ?? 0, title = r.Title, performer = r.Performer, flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.has_performer }),
			InlineQueryResultContact r => MakeIbir(r, r.LastName == null ? r.FirstName : $"{r.FirstName} {r.LastName}", r.PhoneNumber, r.InputMessageContent, null, default, null, false,
				r.ThumbnailUrl, "image/jpeg", r.ThumbnailWidth, r.ThumbnailHeight),
			InlineQueryResultDocument r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, false,
				r.ThumbnailUrl, "image/jpeg", r.ThumbnailWidth, r.ThumbnailHeight,
				r.DocumentUrl, r.MimeType, null, "file"),
			InlineQueryResultGif r => MakeIbir(r, r.Title, null, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, r.ShowCaptionAboveMedia,
				r.ThumbnailUrl, r.ThumbnailMimeType, 0, 0,
				r.GifUrl, "image/gif", r.GifWidth + r.GifHeight > 0 ? new DocumentAttributeImageSize { w = r.GifWidth ?? 0, h = r.GifHeight ?? 0 } : null),
			InlineQueryResultLocation r => MakeIbir(r, r.Title, $"{r.Latitude} {r.Longitude}", r.InputMessageContent, null, default, null, false,
				r.ThumbnailUrl, "image/jpeg", r.ThumbnailWidth, r.ThumbnailHeight, type: "geo"),
			InlineQueryResultMpeg4Gif r => MakeIbir(r, r.Title, null, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, r.ShowCaptionAboveMedia,
				r.ThumbnailUrl, r.ThumbnailMimeType ?? "image/jpeg", 0, 0,
				r.Mpeg4Url, "video/mp4", r.Mpeg4Width + r.Mpeg4Height > 0 ? new DocumentAttributeVideo { w = r.Mpeg4Width ?? 0, h = r.Mpeg4Height ?? 0, duration = r.Mpeg4Duration ?? 0 } : null, "gif"),
			InlineQueryResultPhoto r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, r.ShowCaptionAboveMedia,
				r.ThumbnailUrl, "image/jpeg", 0, 0,
				r.PhotoUrl, "image/jpeg", r.PhotoWidth + r.PhotoHeight > 0 ? new DocumentAttributeImageSize { w = r.PhotoWidth ?? 0, h = r.PhotoHeight ?? 0 } : null),
			InlineQueryResultVenue r => MakeIbir(r, r.Title, r.Address, r.InputMessageContent, null, default, null, false,
				r.ThumbnailUrl, "image/jpeg", r.ThumbnailWidth, r.ThumbnailHeight),
			InlineQueryResultVideo r => MakeIbir(r, r.Title, r.Description, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, r.ShowCaptionAboveMedia,
				r.ThumbnailUrl, "image/jpeg", 0, 0,
				r.VideoUrl, r.MimeType, r.VideoWidth + r.VideoHeight > 0 ? new DocumentAttributeVideo { w = r.VideoWidth ?? 0, h = r.VideoHeight ?? 0, duration = r.VideoDuration ?? 0 } : null),
			InlineQueryResultVoice r => MakeIbir(r, r.Title, null, r.InputMessageContent, r.Caption, r.ParseMode, r.CaptionEntities, false,
				null, null, 0, 0,
				r.VoiceUrl, "audio/ogg", new DocumentAttributeAudio { duration = r.VoiceDuration ?? 0, flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.voice }),
			_ => throw new NotSupportedException()
		});
	}

	private async Task<InputBotInlineResult> MakeIbir(InlineQueryResult r, string? title, string? description,
		InputMessageContent? imc, string? caption, ParseMode parseMode, MessageEntity[]? captionEntities, bool invert_media = false,
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
			id = r.Id,
			title = title,
			description = description,
			url = url,
			thumb = thumb,
			content = content,
			type = type ?? r.Type.ToString().ToLower(),
			send_message = await InputBotInlineMessage(r, imc, caption, parseMode, captionEntities, invert_media),
			flags = (title != null ? TL.InputBotInlineResult.Flags.has_title : 0)
			| (description != null ? TL.InputBotInlineResult.Flags.has_description : 0)
			| (url != null ? TL.InputBotInlineResult.Flags.has_url : 0)
			| (thumb != null ? TL.InputBotInlineResult.Flags.has_thumb : 0)
			| (content != null ? TL.InputBotInlineResult.Flags.has_content : 0)
		};
	}

	private async Task<InputBotInlineMessage> InputBotInlineMessage(InlineQueryResult iqr, InputMessageContent? message,
		string? caption = null, ParseMode parseMode = default, MessageEntity[]? captionEntities = null, bool invert_media = false)
	{
		var reply_markup = await MakeReplyMarkup(iqr.ReplyMarkup);
		return message switch
		{
			InputTextMessageContent itmc when itmc.LinkPreviewOptions?.Url != null => new InputBotInlineMessageMediaWebPage
			{
				reply_markup = reply_markup,
				message = ApplyParse(itmc.ParseMode, itmc.MessageText, itmc.Entities, out var entities),
				entities = entities,
				url = itmc.LinkPreviewOptions.Url,
				flags = (reply_markup != null ? InputBotInlineMessageMediaWebPage.Flags.has_reply_markup : 0) |
						(entities != null ? InputBotInlineMessageMediaWebPage.Flags.has_entities : 0) |
						(itmc.LinkPreviewOptions.PreferLargeMedia ? InputBotInlineMessageMediaWebPage.Flags.force_large_media : 0) |
						(itmc.LinkPreviewOptions.PreferSmallMedia ? InputBotInlineMessageMediaWebPage.Flags.force_small_media : 0) |
						(itmc.LinkPreviewOptions.ShowAboveText ? InputBotInlineMessageMediaWebPage.Flags.invert_media : 0)
			},
			InputTextMessageContent itmc => new InputBotInlineMessageText
			{
				reply_markup = reply_markup,
				message = ApplyParse(itmc.ParseMode, itmc.MessageText, itmc.Entities, out var entities),
				entities = entities,
				flags = (reply_markup != null ? InputBotInlineMessageText.Flags.has_reply_markup : 0) |
						(entities != null ? InputBotInlineMessageText.Flags.has_entities : 0) |
						(itmc.LinkPreviewOptions?.IsDisabled == true ? InputBotInlineMessageText.Flags.no_webpage : 0) |
						(itmc.LinkPreviewOptions?.ShowAboveText == true ? InputBotInlineMessageText.Flags.invert_media : 0)
			},
			InputRichMessageContent irmc => new InputBotInlineMessageRichMessage
			{
				reply_markup = reply_markup,
				rich_message = await InputRichMessage(irmc.RichMessage),
				flags = reply_markup != null ? InputBotInlineMessageRichMessage.Flags.has_reply_markup : 0
			},
			InputLocationMessageContent ilmc => new InputBotInlineMessageMediaGeo
			{
				reply_markup = reply_markup,
				geo_point = MakeGeoPoint(ilmc.Latitude, ilmc.Longitude, ilmc.HorizontalAccuracy),
				heading = ilmc.Heading ?? 0,
				period = ilmc.LivePeriod ?? 0,
				proximity_notification_radius = ilmc.ProximityAlertRadius ?? 0,
				flags = (reply_markup != null ? InputBotInlineMessageMediaGeo.Flags.has_reply_markup : 0)
					| (ilmc.LivePeriod > 0 ? InputBotInlineMessageMediaGeo.Flags.has_period : 0)
					| (ilmc.Heading.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_heading : 0)
					| (ilmc.ProximityAlertRadius.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_proximity_notification_radius : 0)
			},
			InputVenueMessageContent ivmc => new InputBotInlineMessageMediaVenue
			{
				reply_markup = reply_markup,
				geo_point = new InputGeoPoint { lat = ivmc.Latitude, lon = ivmc.Longitude },
				title = ivmc.Title,
				address = ivmc.Address,
				provider = ivmc.GooglePlaceId != null ? "gplaces" : ivmc.FoursquareId != null ? "foursquare" : null,
				venue_id = ivmc.GooglePlaceId ?? ivmc.FoursquareId,
				venue_type = ivmc.GooglePlaceType ?? ivmc.FoursquareType,
				flags = reply_markup != null ? InputBotInlineMessageMediaVenue.Flags.has_reply_markup : 0
			},
			InputContactMessageContent icmc => new InputBotInlineMessageMediaContact
			{
				reply_markup = reply_markup,
				phone_number = icmc.PhoneNumber,
				first_name = icmc.FirstName,
				last_name = icmc.LastName,
				vcard = icmc.Vcard,
				flags = reply_markup != null ? InputBotInlineMessageMediaContact.Flags.has_reply_markup : 0
			},
			InputInvoiceMessageContent iimc => new InputBotInlineMessageMediaInvoice
			{
				reply_markup = reply_markup,
				title = iimc.Title,
				description = iimc.Description,
				photo = new InputWebDocument
				{
					url = iimc.PhotoUrl,
					size = iimc.PhotoSize ?? 0,
					mime_type = "image/jpeg",
					attributes = iimc.PhotoWidth + iimc.PhotoHeight > 0 ? [new TL.DocumentAttributeImageSize { w = iimc.PhotoWidth ?? 0, h = iimc.PhotoHeight ?? 0 }] : null
				},
				invoice = new TL.Invoice
				{
					flags = (iimc.MaxTipAmount.HasValue ? TL.Invoice.Flags.has_max_tip_amount : 0)
						| (iimc.NeedName ? TL.Invoice.Flags.name_requested : 0)
						| (iimc.NeedPhoneNumber ? TL.Invoice.Flags.phone_requested : 0)
						| (iimc.NeedEmail ? TL.Invoice.Flags.email_requested : 0)
						| (iimc.NeedShippingAddress ? TL.Invoice.Flags.shipping_address_requested : 0)
						| (iimc.SendPhoneNumberToProvider ? TL.Invoice.Flags.phone_to_provider : 0)
						| (iimc.SendEmailToProvider ? TL.Invoice.Flags.email_to_provider : 0)
						| (iimc.IsFlexible ? TL.Invoice.Flags.flexible : 0),
					currency = iimc.Currency,
					prices = iimc.Prices.LabeledPrices(),
					max_tip_amount = iimc.MaxTipAmount ?? 0,
					suggested_tip_amounts = iimc.SuggestedTipAmounts?.Select(sta => (long)sta).ToArray(),
				},
				payload = Encoding.UTF8.GetBytes(iimc.Payload),
				provider = iimc.ProviderToken,
				provider_data = new DataJSON { data = iimc.ProviderData ?? "null" },
				flags = reply_markup != null ? InputBotInlineMessageMediaInvoice.Flags.has_reply_markup : 0
			},
			null => iqr switch
			{
				InlineQueryResultLocation iqrl => new InputBotInlineMessageMediaGeo
				{
					reply_markup = reply_markup,
					geo_point = MakeGeoPoint(iqrl.Latitude, iqrl.Longitude, iqrl.HorizontalAccuracy),
					heading = iqrl.Heading ?? 0,
					period = iqrl.LivePeriod ?? 0,
					proximity_notification_radius = iqrl.ProximityAlertRadius ?? 0,
					flags = (reply_markup != null ? InputBotInlineMessageMediaGeo.Flags.has_reply_markup : 0)
						| (iqrl.LivePeriod > 0 ? InputBotInlineMessageMediaGeo.Flags.has_period : 0)
						| (iqrl.Heading.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_heading : 0)
						| (iqrl.ProximityAlertRadius.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_proximity_notification_radius : 0)
				},
				InlineQueryResultVenue iqrv => new InputBotInlineMessageMediaVenue
				{
					reply_markup = reply_markup,
					geo_point = new InputGeoPoint { lat = iqrv.Latitude, lon = iqrv.Longitude },
					title = iqrv.Title,
					address = iqrv.Address,
					provider = iqrv.GooglePlaceId != null ? "gplaces" : iqrv.FoursquareId != null ? "foursquare" : null,
					venue_id = iqrv.GooglePlaceId ?? iqrv.FoursquareId,
					venue_type = iqrv.GooglePlaceType ?? iqrv.FoursquareType,
					flags = reply_markup != null ? InputBotInlineMessageMediaVenue.Flags.has_reply_markup : 0
				},
				InlineQueryResultContact iqrc => new InputBotInlineMessageMediaContact
				{
					reply_markup = reply_markup,
					phone_number = iqrc.PhoneNumber,
					first_name = iqrc.FirstName,
					last_name = iqrc.LastName,
					vcard = iqrc.Vcard,
					flags = reply_markup != null ? InputBotInlineMessageMediaContact.Flags.has_reply_markup : 0
				},
				_ => new InputBotInlineMessageMediaAuto
				{
					reply_markup = reply_markup,
					message = ApplyParse(parseMode, caption, captionEntities, out var entities),
					entities = entities,
					flags = (reply_markup != null ? InputBotInlineMessageMediaAuto.Flags.has_reply_markup : 0) |
							(entities != null ? InputBotInlineMessageMediaAuto.Flags.has_entities : 0) |
							(invert_media ? InputBotInlineMessageMediaAuto.Flags.invert_media : 0)
				},
			},
			_ => throw new NotImplementedException()
		};
	}
}