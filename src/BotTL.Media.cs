using System.Text;
using TL;

namespace WTelegram;

public partial class Bot
{
	private async Task<InputChatPhotoBase> InputChatPhoto(InputFileStream photo)
	{
		switch (photo.FileType)
		{
			//case FileType.Id:
			//	if (((InputFileId)photo).Id.ParseFileId().location is InputPhotoFileLocation ipfl)
			//		return new InputChatPhoto { id = new InputPhoto { id = ipfl.id, access_hash = ipfl.access_hash, file_reference = ipfl.file_reference } };
			//	break;
			case FileType.Stream:
				var inputFile = await Client.UploadFileAsync(photo.Content, photo.FileName, ProgressCallback(photo));
				return new InputChatUploadedPhoto { file = inputFile, flags = InputChatUploadedPhoto.Flags.has_file };
		}
		throw new WTException("Unrecognized InputFileStream type");
	}

	internal static InputDocument InputDocument(string fileId)
	{
		var location = (InputDocumentFileLocation)fileId.ParseFileId().location;
		return new InputDocument { id = location.id, access_hash = location.access_hash, file_reference = location.file_reference };
	}

	internal static InputPhoto InputPhoto(string fileId)
	{
		var location = (InputPhotoFileLocation)fileId.ParseFileId().location;
		return new InputPhoto { id = location.id, access_hash = location.access_hash, file_reference = location.file_reference };
	}

	private Client.ProgressCallback? ProgressCallback(InputFileStream stream) => OnFileProgress == null ? null
		: (progress, total) => OnFileProgress?.Invoke(stream, progress, total);

	/// <summary>Return TL structure for the photo InputFile. Upload the file for InputFileStream</summary>
	public async Task<TL.InputMedia> InputMediaPhoto(InputFile file, bool hasSpoiler = false)
	{
		switch (file.FileType)
		{
			case FileType.Id:
				return new TL.InputMediaPhoto { id = InputPhoto(((InputFileId)file).Id), flags = hasSpoiler ? TL.InputMediaPhoto.Flags.spoiler : 0 };
			case FileType.Url:
				return new InputMediaPhotoExternal { url = ((InputFileUrl)file).Url.AbsoluteUri, flags = hasSpoiler ? InputMediaPhotoExternal.Flags.spoiler : 0 };
			default: //case FileType.Stream:
				var stream = (InputFileStream)file;
				var uploadedFile = await Client.UploadFileAsync(stream.Content, stream.FileName, ProgressCallback(stream));
				return new InputMediaUploadedPhoto { file = uploadedFile, flags = hasSpoiler ? InputMediaUploadedPhoto.Flags.spoiler : 0 };
		}
	}

	private async Task<TL.InputDocument> UploadMediaDocument(InputPeerUser peer, TL.InputMedia media)
	{
		if (media is TL.InputMediaDocument imd) return imd.id; // already on Telegram, no need to upload
		var messageMedia = await Client.Messages_UploadMedia(peer, media);
		if (messageMedia is not MessageMediaDocument { document: TL.Document doc })
			throw new WTException("Unexpected UploadMedia result");
		return doc;
	}

	private async Task<TL.InputPhoto?> UploadMediaPhoto(InputPeer peer, InputFile? cover)
	{
		if (cover == null) return null;
		var media = await InputMediaPhoto(cover);
		if (media is TL.InputMediaPhoto imp) return imp.id; // already on Telegram, no need to upload
		var messageMedia = await Client.Messages_UploadMedia(peer, media);
		if (messageMedia is not MessageMediaPhoto { photo: TL.Photo photo })
			throw new WTException("Unexpected UploadMedia result");
		return photo;
	}

	/// <summary>Return TL structure for the live photo + static photo. Upload the file for InputFileStream</summary>
	public async Task<TL.InputMedia> InputMediaLivePhoto(InputFile file, InputFile photo, InputPeer peer, bool hasSpoiler = false)
	{
		if (file.FileType == FileType.Url || photo.FileType == FileType.Url)
			throw new WTException("Sending live photos by URL is not supported");
		var im = await InputMediaPhoto(photo, hasSpoiler);
		if (file is InputFileId ifi && im is TL.InputMediaPhoto imp)
		{
			imp.flags |= TL.InputMediaPhoto.Flags.live_photo;
			imp.video = InputDocument(ifi.Id);
		}
		else if (file is InputFileStream ifs && im is TL.InputMediaUploadedPhoto imup)
		{
			var uploadedFile = await Client.UploadFileAsync(ifs.Content, ifs.FileName, ProgressCallback(ifs));
			var media = new InputMediaUploadedDocument(uploadedFile, "video/mp4") { flags = hasSpoiler ? TL.InputMediaUploadedDocument.Flags.spoiler : 0 };
			var messageMedia = await Client.Messages_UploadMedia(peer, media);
			if (messageMedia is not MessageMediaDocument { document: TL.Document doc })
				throw new WTException("Unexpected UploadMedia result");
			imup.flags |= TL.InputMediaUploadedPhoto.Flags.live_photo;
			imup.video = doc;
		}
		else
			throw new WTException("photo/livePhoto file type mismatch");
		return im;
	}

	/// <summary>Return TL structure for the document InputFile. Upload the file for InputFileStream</summary>
	public async Task<TL.InputMedia> InputMediaDocument(InputFile file, TL.InputPhoto? video_cover = default, int? video_timestamp = default, bool hasSpoiler = false, string? mimeType = null, string? defaultFilename = null)
	{
		switch (file.FileType)
		{
			case FileType.Id:
				return new TL.InputMediaDocument
				{
					id = InputDocument(((InputFileId)file).Id),
					video_cover = video_cover,
					video_timestamp = video_timestamp ?? 0,
					flags = (hasSpoiler ? TL.InputMediaDocument.Flags.spoiler : 0)
						| (video_timestamp.HasValue ? TL.InputMediaDocument.Flags.has_video_timestamp : 0)
						| (video_cover != null ? TL.InputMediaDocument.Flags.has_video_cover : 0)
				};
			case FileType.Url:
				return new InputMediaDocumentExternal
				{
					url = ((InputFileUrl)file).Url.AbsoluteUri,
					video_cover = video_cover,
					video_timestamp = video_timestamp ?? 0,
					flags = (hasSpoiler ? TL.InputMediaDocumentExternal.Flags.spoiler : 0)
						| (video_timestamp.HasValue ? TL.InputMediaDocumentExternal.Flags.has_video_timestamp : 0)
						| (video_cover != null ? TL.InputMediaDocumentExternal.Flags.has_video_cover : 0)
				};
			default: //case FileType.Stream:
				var stream = (InputFileStream)file;
				var uploadedFile = await Client.UploadFileAsync(stream.Content, stream.FileName ?? defaultFilename, ProgressCallback(stream));
				if (mimeType == null)
				{
					string? fileExt = Path.GetExtension(stream.FileName); // ?? defaultFilename (if we want to behave exactly like Telegram.Bot)
					fileExt ??= Path.GetExtension((stream.Content as FileStream)?.Name);
					if (!string.IsNullOrEmpty(fileExt))
						BotHelpers.ExtToMimeType.TryGetValue(fileExt, out mimeType);
				}
				return new InputMediaUploadedDocument(uploadedFile, mimeType)
				{
					video_cover = video_cover,
					video_timestamp = video_timestamp ?? 0,
					flags = (hasSpoiler ? TL.InputMediaUploadedDocument.Flags.spoiler : 0)
						| (video_timestamp.HasValue ? TL.InputMediaUploadedDocument.Flags.has_video_timestamp : 0)
						| (video_cover != null ? TL.InputMediaUploadedDocument.Flags.has_video_cover : 0)
				};
		}
	}

	/// <summary>Return TL structure for the InputMedia and its caption. Upload the file/thumb for InputFileStream and add attributes</summary>
	public async Task<TL.InputSingleMedia> InputSingleMedia(InputPeer peer, InputMedia media)
	{
		var caption = media.Caption;
		var captionEntities = ApplyParse(media.ParseMode, ref caption, media.CaptionEntities);
		var tlMedia = media switch
		{
			Telegram.Bot.Types.InputMediaPhoto imp => await InputMediaPhoto(media.Media, imp.HasSpoiler),
			Telegram.Bot.Types.InputMediaLivePhoto imlp => await InputMediaLivePhoto(media.Media, imlp.Photo, peer, imlp.HasSpoiler),
			Telegram.Bot.Types.InputMediaVideo imv => await InputMediaDocument(media.Media, await UploadMediaPhoto(peer, imv.Cover), imv.StartTimestamp, imv.HasSpoiler),
			Telegram.Bot.Types.InputMediaAnimation ima => await InputMediaDocument(media.Media, hasSpoiler: ima.HasSpoiler),
			_ => await InputMediaDocument(media.Media)
		};
		if (tlMedia is TL.InputMediaUploadedDocument doc)
		{
			switch (media)
			{
				case Telegram.Bot.Types.InputMediaAudio ima:
					doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeAudio {
						duration = ima.Duration, performer = ima.Performer, title = ima.Title,
						flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.has_performer }];
					break;
				case Telegram.Bot.Types.InputMediaDocument imd:
					if (imd.DisableContentTypeDetection) doc.flags |= InputMediaUploadedDocument.Flags.force_file;
					break;
				case Telegram.Bot.Types.InputMediaVideo imv:
					doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
						duration = imv.Duration, h = imv.Height, w = imv.Width,
						flags = imv.SupportsStreaming ? DocumentAttributeVideo.Flags.supports_streaming : 0 }];
					break;
				case Telegram.Bot.Types.InputMediaAnimation ima:
					if (doc.mime_type == "video/mp4")
						doc.attributes = [.. doc.attributes, new DocumentAttributeVideo { duration = ima.Duration, w = ima.Width, h = ima.Height }];
					else if (ima.Width > 0 && ima.Height > 0)
					{
						if (doc.mime_type?.StartsWith("image/") != true) doc.mime_type = "image/gif";
						doc.attributes = [.. doc.attributes, new DocumentAttributeImageSize { w = ima.Width, h = ima.Height }];
					}
					break;
			}
			if (media is IInputMediaThumb { Thumbnail: { } thumbnail })
				await SetDocThumb(doc, thumbnail);
		}
		return new InputSingleMedia
		{
			flags = captionEntities != null ? TL.InputSingleMedia.Flags.has_entities : 0,
			media = tlMedia,
			message = caption,
			entities = captionEntities?.ToArray(),
		};
	}

	internal async Task<TL.InputMedia> InputPollMedia(InputPeer peer, InputMediaType type, object media)
	{
		switch (type)
		{
			case InputMediaType.Location:
				var location = (InputMediaLocation)media;
				return new InputMediaGeoPoint { geo_point = MakeGeoPoint(location.Latitude, location.Longitude, location.HorizontalAccuracy) };
			case InputMediaType.Sticker:
				var sticker = (InputMediaSticker)media;
				var imd = await InputMediaDocument(sticker.Media);
				if (imd is TL.InputMediaUploadedDocument doc)
					doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeSticker { alt = sticker.Emoji }];
				return imd;
			case InputMediaType.Venue:
				var venue = (Telegram.Bot.Types.InputMediaVenue)media;
				return new TL.InputMediaVenue
				{
					geo_point = new InputGeoPoint { lat = venue.Latitude, lon = venue.Longitude },
					title = venue.Title,
					address = venue.Address,
					provider = venue.GooglePlaceId != null ? "gplaces" : venue.FoursquareId != null ? "foursquare" : null,
					venue_id = venue.GooglePlaceId ?? venue.FoursquareId,
					venue_type = venue.GooglePlaceType ?? venue.FoursquareType,
				};
			case InputMediaType.Link:
				return new TL.InputMediaWebPage { url = ((InputMediaLink)media).Url };
			default:
				return (await InputSingleMedia(peer, (InputMedia)media)).media;
		}
	}

	private async Task<InputStickerSetItem> InputStickerSetItem(long userId, InputSticker sticker)
	{
		await InitComplete();
		var peer = InputPeerUser(userId);
		var media = await InputMediaDocument(sticker.Sticker, mimeType: MimeType(sticker.Format));
		var document = await UploadMediaDocument(peer, media);
		string keywords = sticker.Keywords == null ? "" : string.Join(",", sticker.Keywords);
		return new InputStickerSetItem
		{
			document = document,
			emoji = string.Concat(sticker.EmojiList),
			mask_coords = sticker.MaskPosition.MaskCoord(),
			keywords = keywords,
			flags = (sticker.MaskPosition != null ? TL.InputStickerSetItem.Flags.has_mask_coords : 0)
				| (keywords != "" ? TL.InputStickerSetItem.Flags.has_keywords : 0),
		};
	}

	private void CacheStickerSet(Messages_StickerSet mss)
	{
		lock (StickerSetNames)
			StickerSetNames[mss.set.id] = mss.set.short_name;
	}

	private Task<Sticker> MakeSticker(TL.Document doc) => MakeSticker(doc, doc.GetAttribute<DocumentAttributeSticker>());

	// sync = true: No async calls. Won't try to resolve missing SetName</param>
	private async Task<Sticker> MakeSticker(TL.Document doc, DocumentAttributeSticker? sticker, bool sync = false)
	{
		var customEmoji = doc.GetAttribute<DocumentAttributeCustomEmoji>();
		string? setName = null;
		switch (sticker?.stickerset ?? customEmoji?.stickerset)
		{
			case InputStickerSetID issi:
				lock (StickerSetNames)
					if (StickerSetNames.TryGetValue(issi.id, out setName)) break;
				if (sync) break;
				try
				{
					var mss = await Client.Messages_GetStickerSet(issi);
					CacheStickerSet(mss);
					setName = mss.set.short_name;
				}
				catch (Exception) { }
				break;
			case InputStickerSetShortName issn: setName = issn.short_name; break;
			default: Manager.Log(3, $"MakeSticker called with unexpected {sticker?.stickerset} stickerset"); break;
		}
		var result = new Sticker
		{
			FileSize = doc.size,
			IsAnimated = doc.mime_type == "application/x-tgsticker",
			IsVideo = doc.mime_type == "video/webm",
			Type = customEmoji != null ? StickerType.CustomEmoji :
				sticker?.flags.HasFlag(DocumentAttributeSticker.Flags.mask) == true ? StickerType.Mask : StickerType.Regular,
			Thumbnail = doc.LargestThumbSize?.PhotoSize(doc.ToFileLocation(doc.LargestThumbSize), doc.dc_id),
			Emoji = sticker?.alt ?? customEmoji?.alt, // ?? mss?.packs.FirstOrDefault(sp => sp.documents.Contains(doc.id))?.emoticon,
			SetName = setName,
			MaskPosition = sticker?.mask_coords == null ? null : new MaskPosition
			{
				Point = (MaskPositionPoint)(sticker.mask_coords.n + 1),
				XShift = (float)sticker.mask_coords.x,
				YShift = (float)sticker.mask_coords.y,
				Scale = (float)sticker.mask_coords.zoom
			},
			NeedsRepainting = customEmoji?.flags.HasFlag(DocumentAttributeCustomEmoji.Flags.text_color) ?? false,
			CustomEmojiId = customEmoji != null ? doc.id.ToString() : null
		}.SetFileIds(doc.ToFileLocation(), doc.dc_id);
		if (doc.video_thumbs?.OfType<VideoSize>().FirstOrDefault(vs => vs.type == "f") != null)
		{
			var premiumLocation = doc.ToFileLocation();
			premiumLocation.thumb_size = "f";
			result.PremiumAnimation = new TGFile { FileSize = doc.size }.SetFileIds(premiumLocation, doc.dc_id, "f");
			result.PremiumAnimation.FilePath = result.PremiumAnimation.FileId + "/Sticker_" + result.PremiumAnimation.FileUniqueId;
		}
		if (doc.GetAttribute<DocumentAttributeImageSize>() is { } imageSize) { result.Width = imageSize.w; result.Height = imageSize.h; }
		else if (doc.GetAttribute<DocumentAttributeVideo>() is { } video) { result.Width = video.w; result.Height = video.h; }
		else if (result.IsAnimated) { result.Width = result.Height = customEmoji is null ? 512 : 100; }
		return result;
	}

	private async Task SetDocThumb(InputMediaUploadedDocument doc, InputFile? thumb)
	{
		switch (thumb)
		{
			case null: break;
			case InputFileStream stream:
				doc.thumb = await Client.UploadFileAsync(stream.Content, stream.FileName, ProgressCallback(stream));
				doc.flags |= InputMediaUploadedDocument.Flags.has_thumb;
				break;
			default: throw new WTException("Only InputFileStream is not supported for thumbnails");
		}

	}

	private static string MimeType(StickerFormat stickerFormat) => stickerFormat switch
	{
		StickerFormat.Animated => "application/x-tgsticker",
		StickerFormat.Video => "video/webm",
		_ => "image/webp"
	};

	private static InputMediaInvoice InputMediaInvoice(string title, string description, string payload, string? providerToken,
		string currency, IEnumerable<LabeledPrice> prices, int? maxTipAmount, IEnumerable<int>? suggestedTipAmounts, string? startParameter,
		string? providerData, string? photoUrl, int? photoSize, int? photoWidth, int? photoHeight,
		bool needName, bool needPhoneNumber, bool needEmail, bool needShippingAddress,
		bool sendPhoneNumberToProvider, bool sendEmailToProvider, bool isFlexible, int? subscriptionPeriod) => new()
		{
			flags = (photoUrl != null ? TL.InputMediaInvoice.Flags.has_photo : 0) | (startParameter != null ? TL.InputMediaInvoice.Flags.has_start_param : 0)
				| (providerToken != null ? TL.InputMediaInvoice.Flags.has_provider : 0),
			title = title,
			description = description,
			photo = photoUrl == null ? null : new InputWebDocument
			{
				url = photoUrl,
				mime_type = "image/jpeg",
				size = photoSize ?? 0,
				attributes = photoWidth > 0 && photoHeight > 0 ? [new TL.DocumentAttributeImageSize { w = photoWidth.Value, h = photoHeight.Value }] : null
			},
			invoice = new TL.Invoice
			{
				flags = (maxTipAmount.HasValue ? TL.Invoice.Flags.has_max_tip_amount : 0)
					| (subscriptionPeriod.HasValue ? TL.Invoice.Flags.has_subscription_period : 0)
					| (needName ? TL.Invoice.Flags.name_requested : 0)
					| (needPhoneNumber ? TL.Invoice.Flags.phone_requested : 0)
					| (needEmail ? TL.Invoice.Flags.email_requested : 0)
					| (needShippingAddress ? TL.Invoice.Flags.shipping_address_requested : 0)
					| (sendPhoneNumberToProvider ? TL.Invoice.Flags.phone_to_provider : 0)
					| (sendEmailToProvider ? TL.Invoice.Flags.email_to_provider : 0)
					| (isFlexible ? TL.Invoice.Flags.flexible : 0),
				currency = currency,
				prices = prices.LabeledPrices(),
				max_tip_amount = maxTipAmount ?? 0,
				suggested_tip_amounts = suggestedTipAmounts?.Select(sta => (long)sta).ToArray(),
				subscription_period = subscriptionPeriod ?? 0,
			},
			payload = Encoding.UTF8.GetBytes(payload),
			provider = providerToken,
			provider_data = new DataJSON { data = providerData ?? "null" },
			start_param = startParameter,
		};

	private async Task<TL.InputMedia> GetStoryMedia(InputStoryContent content)
	{
		TL.InputMedia tlMedia;
		if (content is InputStoryContentPhoto iscp)
			tlMedia = await InputMediaPhoto(iscp.Photo);
		else if (content is InputStoryContentVideo iscv)
		{
			tlMedia = await InputMediaDocument(iscv.Video, mimeType: "video/mp4", defaultFilename: "story.mp4");
			if (tlMedia is TL.InputMediaUploadedDocument doc)
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
					duration = iscv.Duration, w = 720, h = 1280, video_start_ts = iscv.CoverFrameTimestamp ?? 0.0,
					flags = (iscv.IsAnimation ? DocumentAttributeVideo.Flags.nosound : 0)
						| (iscv.CoverFrameTimestamp.HasValue ? DocumentAttributeVideo.Flags.has_video_start_ts : 0)}];
		}
		else throw new RpcException(500, $"Unsupported {content}");
		return tlMedia;
	}
}