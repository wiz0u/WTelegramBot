using System.Text;
using TL;
using KeyboardButton = Telegram.Bot.Types.ReplyMarkups.KeyboardButton;
using ReplyKeyboardMarkup = Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup;

namespace WTelegram;

public partial class Bot
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
				| (rkm.IsPersistent == true ? TL.ReplyKeyboardMarkup.Flags.persistent : 0)
				| (rkm.ResizeKeyboard == true ? TL.ReplyKeyboardMarkup.Flags.resize : 0)
				| (rkm.OneTimeKeyboard == true ? TL.ReplyKeyboardMarkup.Flags.single_use : 0)
				| (rkm.InputFieldPlaceholder != null ? TL.ReplyKeyboardMarkup.Flags.has_placeholder : 0),
			placeholder = rkm.InputFieldPlaceholder,
			rows = rkm.Keyboard.Select(row => new KeyboardButtonRow { buttons = row.Select(MakeKeyboardButton).ToArray() }).ToArray()
		},
		InlineKeyboardMarkup ikm => new ReplyInlineMarkup
		{
			rows = await ikm.InlineKeyboard.Select(
				async row => new KeyboardButtonRow { buttons = await row.Select(MakeKeyboardButton).WhenAllSequential() }).WhenAllSequential()
		} is { rows.Length: not 0 } rim ? rim : null,
		_ => null,
	};

	private static KeyboardButtonBase MakeKeyboardButton(KeyboardButton btn)
	{
		if (btn.RequestUsers is { } rus) return new InputKeyboardButtonRequestPeer
		{
			text = btn.Text,
			button_id = rus.RequestId,
			max_quantity = rus.MaxQuantity ?? 1,
			peer_type = new RequestPeerTypeUser
			{
				bot = rus.UserIsBot == true,
				premium = rus.UserIsPremium == true,
				flags = (rus.UserIsBot == null ? 0 : RequestPeerTypeUser.Flags.has_bot) | (rus.UserIsPremium == null ? 0 : RequestPeerTypeUser.Flags.has_premium)
			},
			flags = (rus.RequestName == true ? InputKeyboardButtonRequestPeer.Flags.name_requested : 0)
				| (rus.RequestUsername == true ? InputKeyboardButtonRequestPeer.Flags.username_requested : 0)
				| (rus.RequestPhoto == true ? InputKeyboardButtonRequestPeer.Flags.photo_requested : 0)
		};
		if (btn.RequestChat is { } rc) return new InputKeyboardButtonRequestPeer
		{
			text = btn.Text,
			button_id = rc.RequestId,
			max_quantity = 1,
			peer_type = MakeRequestPeerType(rc),
			flags = (rc.RequestTitle == true ? InputKeyboardButtonRequestPeer.Flags.name_requested : 0)
				| (rc.RequestUsername == true ? InputKeyboardButtonRequestPeer.Flags.username_requested : 0)
				| (rc.RequestPhoto == true ? InputKeyboardButtonRequestPeer.Flags.photo_requested : 0)
		};
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

	private static RequestPeerType MakeRequestPeerType(KeyboardButtonRequestChat rc)
	{
		if (rc.ChatIsChannel)
		{
			var rptb = new RequestPeerTypeBroadcast { };
			return FillFields(rc, rptb, ref rptb.flags, ref rptb.has_username, ref rptb.user_admin_rights, ref rptb.bot_admin_rights);
		}
		else
		{
			var rptc = new RequestPeerTypeChat
			{
				forum = rc.ChatIsForum == true,
				flags = (rc.ChatIsForum != null ? RequestPeerTypeChat.Flags.has_forum : 0) | (rc.BotIsMember == true ? RequestPeerTypeChat.Flags.bot_participant : 0)
			};
			return FillFields(rc, rptc, ref rptc.flags, ref rptc.has_username, ref rptc.user_admin_rights, ref rptc.bot_admin_rights);
		}
		static T FillFields<T, F>(KeyboardButtonRequestChat rc, T obj, ref F flags, ref bool has_username, ref ChatAdminRights? user_admin_rights, ref ChatAdminRights? bot_admin_rights)
			where T : RequestPeerType where F : Enum, IConvertible
		{
			has_username = rc.ChatHasUsername == true;
			user_admin_rights = rc.UserAdministratorRights?.ChatAdminRights();
			bot_admin_rights = rc.BotAdministratorRights?.ChatAdminRights();
			var more_flags = (rc.ChatHasUsername != null ? RequestPeerTypeChat.Flags.has_has_username : 0)
				| (rc.UserAdministratorRights != null ? RequestPeerTypeChat.Flags.has_user_admin_rights : 0)
				| (rc.BotAdministratorRights != null ? RequestPeerTypeChat.Flags.has_bot_admin_rights : 0)
				| (rc.ChatIsCreated == true ? RequestPeerTypeChat.Flags.creator : 0);
			flags = (F)(object)(flags.ToUInt32(null) | (uint)more_flags);
			return obj;
		}
	}

	private async Task<KeyboardButtonBase> MakeKeyboardButton(InlineKeyboardButton btn)
	{
		if (btn.Url != null) return new KeyboardButtonUrl { text = btn.Text, url = btn.Url };
		if (btn.CallbackData != null) return new KeyboardButtonCallback { text = btn.Text, data = Encoding.UTF8.GetBytes(btn.CallbackData) };
		if (btn.CallbackGame != null) return new KeyboardButtonGame { text = btn.Text };
		if (btn.Pay == true) return new KeyboardButtonBuy { text = btn.Text };
		if (btn.SwitchInlineQuery != null) return new KeyboardButtonSwitchInline { text = btn.Text, query = btn.SwitchInlineQuery };
		if (btn.SwitchInlineQueryCurrentChat != null) return new KeyboardButtonSwitchInline { text = btn.Text, query = btn.SwitchInlineQueryCurrentChat, flags = KeyboardButtonSwitchInline.Flags.same_peer };
		if (btn.SwitchInlineQueryChosenChat != null) return new KeyboardButtonSwitchInline { text = btn.Text, query = btn.SwitchInlineQueryChosenChat.Query, peer_types = btn.SwitchInlineQueryChosenChat.InlineQueryPeerTypes(), flags = KeyboardButtonSwitchInline.Flags.has_peer_types };
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

	/// <summary>Fetch the message being replied-to if any</summary>
	protected async Task<Message?> GetReplyToMessage(InputPeer peer, ReplyParameters? replied)
	{
		if (replied?.MessageId > 0)
		{
			if (replied.ChatId is not null) peer = await InputPeerChat(replied.ChatId);
			var msg = await GetMessage(peer, replied.MessageId);
			if (msg == null && replied.AllowSendingWithoutReply != true) throw new WTException("Bad Request: message to reply not found");
			return msg;
		}
		return null;
	}

	/// <summary>Build the eventual InputReplyTo structure for sending a reply message</summary>
	protected async Task<InputReplyTo?> MakeReplyTo(ReplyParameters? replied, int messageThreadId, InputPeer? replyToPeer)
	{
		if (replied?.MessageId > 0)
		{
			if (replied.ChatId is not null) replyToPeer = await InputPeerChat(replied.ChatId);
			var quote = replied.Quote;
			var quoteEntities = ApplyParse(replied.QuoteParseMode, ref quote, replied.QuoteEntities);
			return new InputReplyToMessage
			{
				reply_to_msg_id = replied.MessageId,
				top_msg_id = messageThreadId,
				reply_to_peer_id = messageThreadId != 0 ? replyToPeer : null,
				quote_text = quote,
				quote_entities = quoteEntities,
				quote_offset = replied.QuotePosition ?? 0,
				flags = (messageThreadId != 0 ? InputReplyToMessage.Flags.has_top_msg_id | InputReplyToMessage.Flags.has_reply_to_peer_id : 0)
					| (replied.ChatId is not null ? InputReplyToMessage.Flags.has_reply_to_peer_id : 0)
					| (quote != null ? InputReplyToMessage.Flags.has_quote_text : 0)
					| (quoteEntities != null ? InputReplyToMessage.Flags.has_quote_entities : 0)
					| (replied.QuotePosition.HasValue ? InputReplyToMessage.Flags.has_quote_offset : 0)
			};
		}
		else if (messageThreadId != 0)
			return new InputReplyToMessage { reply_to_msg_id = messageThreadId };
		return null;
	}

	private async Task<Message?> GetMIMessage(InputPeer peer, int messageId, bool replyToo = false)
	{
		var msg = await GetMessage(peer, messageId, replyToo);
		if (msg != null && msg.Date != default) return msg;
		return new Message { Chat = await ChatFromPeer(peer), MessageId = messageId };
	}

	/// <summary>Fetch and build a Bot Message (cached)</summary>
	protected async Task<Message?> GetMessage(InputPeer? peer, int messageId, bool replyToo = false)
	{
		if (peer == null || messageId == 0) return null;
		lock (CachedMessages)
			if (CachedMessages.TryGetValue((peer.ID, messageId), out var cachedMsg))
				return cachedMsg;
		var msgs = await Client.GetMessages(peer, messageId);
		msgs.UserOrChat(_collector);
		var msgBase = msgs.Messages.FirstOrDefault();
		var msg = replyToo ? await MakeMessageAndReply(msgBase) : await MakeMessage(msgBase);
		lock (CachedMessages)
			return CachedMessages[(peer.ID, messageId)] = msg;
	}

	private MessageEntity[]? MakeEntities(TL.MessageEntity[]? entities) => entities?.Select(e => e switch
	{
		TL.MessageEntityMention => new MessageEntity { Type = MessageEntityType.Mention, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityHashtag => new MessageEntity { Type = MessageEntityType.Hashtag, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityBotCommand => new MessageEntity { Type = MessageEntityType.BotCommand, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityUrl => new MessageEntity { Type = MessageEntityType.Url, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityEmail => new MessageEntity { Type = MessageEntityType.Email, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityBold => new MessageEntity { Type = MessageEntityType.Bold, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityItalic => new MessageEntity { Type = MessageEntityType.Italic, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityCode => new MessageEntity { Type = MessageEntityType.Code, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityPre mep => new MessageEntity { Type = MessageEntityType.Pre, Offset = e.Offset, Length = e.Length, Language = mep.language },
		TL.MessageEntityTextUrl metu => new MessageEntity { Type = MessageEntityType.TextLink, Offset = e.Offset, Length = e.Length, Url = metu.url },
		TL.MessageEntityMentionName memn => new MessageEntity { Type = MessageEntityType.TextMention, Offset = e.Offset, Length = e.Length, User = User(memn.user_id) },
		TL.MessageEntityPhone => new MessageEntity { Type = MessageEntityType.PhoneNumber, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityCashtag => new MessageEntity { Type = MessageEntityType.Cashtag, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityUnderline => new MessageEntity { Type = MessageEntityType.Underline, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityStrike => new MessageEntity { Type = MessageEntityType.Strikethrough, Offset = e.Offset, Length = e.Length },
		TL.MessageEntitySpoiler => new MessageEntity { Type = MessageEntityType.Spoiler, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityCustomEmoji mece => new MessageEntity { Type = MessageEntityType.CustomEmoji, Offset = e.Offset, Length = e.Length, CustomEmojiId = mece.document_id.ToString() },
		TL.MessageEntityBlockquote mebq => mebq.flags.HasFlag(MessageEntityBlockquote.Flags.collapsed)
			? new MessageEntity { Type = MessageEntityType.ExpandableBlockquote, Offset = e.Offset, Length = e.Length }
			: new MessageEntity { Type = MessageEntityType.Blockquote, Offset = e.Offset, Length = e.Length },
		_ => null!,
	}).Where(e => e != null).ToArray();

	/// <summary>Apply ParseMode to text and entities</summary>
	protected TL.MessageEntity[]? ApplyParse(ParseMode parseMode, ref string? text, IEnumerable<MessageEntity>? entities)
	{
		if (entities != null)
			return entities.Select(e => e.Type switch
			{
				MessageEntityType.Bold => new TL.MessageEntityBold { offset = e.Offset, length = e.Length },
				MessageEntityType.Italic => new TL.MessageEntityItalic { offset = e.Offset, length = e.Length },
				MessageEntityType.Code => new TL.MessageEntityCode { offset = e.Offset, length = e.Length },
				MessageEntityType.Pre => new TL.MessageEntityPre { offset = e.Offset, length = e.Length, language = e.Language },
				MessageEntityType.TextLink => new TL.MessageEntityTextUrl { offset = e.Offset, length = e.Length, url = e.Url },
				MessageEntityType.TextMention => new TL.InputMessageEntityMentionName { offset = e.Offset, length = e.Length, user_id = InputUser(e.User!.Id) },
				MessageEntityType.Underline => new TL.MessageEntityUnderline { offset = e.Offset, length = e.Length },
				MessageEntityType.Strikethrough => new TL.MessageEntityStrike { offset = e.Offset, length = e.Length },
				MessageEntityType.Spoiler => new TL.MessageEntitySpoiler { offset = e.Offset, length = e.Length },
				MessageEntityType.CustomEmoji => new TL.MessageEntityCustomEmoji { offset = e.Offset, length = e.Length, document_id = long.Parse(e.CustomEmojiId!) },
				MessageEntityType.Blockquote => new TL.MessageEntityBlockquote { offset = e.Offset, length = e.Length },
				MessageEntityType.ExpandableBlockquote => new TL.MessageEntityBlockquote { offset = e.Offset, length = e.Length, flags = MessageEntityBlockquote.Flags.collapsed },
				_ => (TL.MessageEntity)null!
			}).Where(e => e != null).ToArray();
		else if (text == null)
			return null;
		return parseMode switch
		{
			ParseMode.Markdown or ParseMode.MarkdownV2 => Client.MarkdownToEntities(ref text, false, _collector),
			ParseMode.Html => Client.HtmlToEntities(ref text, false, _collector),
			_ => null,
		};
	}

	/// <summary>Apply ParseMode to text and entities</summary>
	protected string? ApplyParse(ParseMode parseMode, string? text, MessageEntity[]? entities, out TL.MessageEntity[]? tlEntities)
	{
		tlEntities = ApplyParse(parseMode, ref text, entities);
		return text;
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
			//case FileType.Id:
			//	if (((InputFileId)photo).Id.ParseFileId().location is InputPhotoFileLocation ipfl)
			//		return new InputChatPhoto { id = new InputPhoto { id = ipfl.id, access_hash = ipfl.access_hash, file_reference = ipfl.file_reference } };
			//	break;
			case FileType.Stream:
				var inputFile = await Client.UploadFileAsync(photo.Content, photo.FileName);
				return new InputChatUploadedPhoto { file = inputFile, flags = InputChatUploadedPhoto.Flags.has_file };
		}
		throw new WTException("Unrecognized InputFileStream type");
	}

	private static InputPhoto InputPhoto(string fileId)
	{
		var location = (InputPhotoFileLocation)fileId.ParseFileId().location;
		return new InputPhoto { id = location.id, access_hash = location.access_hash, file_reference = location.file_reference };
	}

	/// <summary>Return TL structure for the photo InputFile. Upload the file for InputFileStream</summary>
	public async Task<TL.InputMedia> InputMediaPhoto(InputFile file, bool hasSpoiler = false)
	{
		switch (file.FileType)
		{
			case FileType.Id:
				return new TL.InputMediaPhoto { id = InputPhoto(((InputFileId)file).Id), flags = hasSpoiler == true ? TL.InputMediaPhoto.Flags.spoiler : 0 };
			case FileType.Url:
				return new InputMediaPhotoExternal { url = ((InputFileUrl)file).Url.AbsoluteUri, flags = hasSpoiler == true ? InputMediaPhotoExternal.Flags.spoiler : 0 };
			default: //case FileType.Stream:
				var stream = (InputFileStream)file;
				var uploadedFile = await Client.UploadFileAsync(stream.Content, stream.FileName);
				return new InputMediaUploadedPhoto { file = uploadedFile, flags = hasSpoiler == true ? InputMediaUploadedPhoto.Flags.spoiler : 0 };
		}
	}

	private static InputDocument InputDocument(string fileId)
	{
		var location = (InputDocumentFileLocation)fileId.ParseFileId().location;
		return new InputDocument { id = location.id, access_hash = location.access_hash, file_reference = location.file_reference };
	}

	/// <summary>Return TL structure for the document InputFile. Upload the file for InputFileStream</summary>
	public async Task<TL.InputMedia> InputMediaDocument(InputFile file, bool hasSpoiler = false, string? mimeType = null, string? defaultFilename = null)
	{
		switch (file.FileType)
		{
			case FileType.Id:
				return new TL.InputMediaDocument { id = InputDocument(((InputFileId)file).Id), flags = hasSpoiler == true ? TL.InputMediaDocument.Flags.spoiler : 0 };
			case FileType.Url:
				return new InputMediaDocumentExternal { url = ((InputFileUrl)file).Url.AbsoluteUri, flags = hasSpoiler == true ? InputMediaDocumentExternal.Flags.spoiler : 0 };
			default: //case FileType.Stream:
				var stream = (InputFileStream)file;
				var uploadedFile = await Client.UploadFileAsync(stream.Content, stream.FileName ?? defaultFilename);
				if (mimeType == null)
				{
					string? fileExt = Path.GetExtension(stream.FileName); // ?? defaultFilename (if we want to behave exactly like Telegram.Bot)
					fileExt ??= Path.GetExtension((stream.Content as FileStream)?.Name);
					if (!string.IsNullOrEmpty(fileExt))
						BotHelpers.ExtToMimeType.TryGetValue(fileExt, out mimeType);
				}
				return new InputMediaUploadedDocument(uploadedFile, mimeType) { flags = hasSpoiler == true ? InputMediaUploadedDocument.Flags.spoiler : 0 };
		}
	}

	/// <summary>Return TL structure for the InputMedia and its caption. Upload the file/thumb for InputFileStream and add attributes</summary>
	public async Task<TL.InputSingleMedia> InputSingleMedia(InputMedia media)
	{
		var caption = media.Caption;
		var captionEntities = ApplyParse(media.ParseMode, ref caption, media.CaptionEntities);
		var tlMedia = media is Telegram.Bot.Types.InputMediaPhoto imp
			? await InputMediaPhoto(media.Media, imp.HasSpoiler)
			: await InputMediaDocument(media.Media,
				media switch { InputMediaVideo imv => imv.HasSpoiler, InputMediaAnimation ima => ima.HasSpoiler, _ => false });
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
					if (imd.DisableContentTypeDetection == true) doc.flags |= InputMediaUploadedDocument.Flags.force_file;
					break;
				case Telegram.Bot.Types.InputMediaVideo imv:
					doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
						duration = imv.Duration, h = imv.Height, w = imv.Width,
						flags = imv.SupportsStreaming == true ? DocumentAttributeVideo.Flags.supports_streaming : 0 }];
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

	private async Task<InputStickerSetItem> InputStickerSetItem(long userId, InputSticker sticker)
	{
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

	private async Task<TL.InputDocument> UploadMediaDocument(InputPeerUser peer, TL.InputMedia media)
	{
		if (media is TL.InputMediaDocument imd) return imd.id; // already on Telegram, no need to upload
		var messageMedia = await Client.Messages_UploadMedia(peer, media);
		if (messageMedia is not MessageMediaDocument { document: TL.Document doc })
			throw new WTException("Unexpected UploadMedia result");
		return doc;
	}

	private void CacheStickerSet(Messages_StickerSet mss)
	{
		lock (StickerSetNames)
			StickerSetNames[mss.set.id] = mss.set.short_name;
	}

	private async Task<Sticker> MakeSticker(TL.Document doc, DocumentAttributeSticker? sticker)
	{
		var customEmoji = doc.GetAttribute<DocumentAttributeCustomEmoji>();
		string? setName = null;
		switch (sticker?.stickerset ?? customEmoji?.stickerset)
		{
			case InputStickerSetID issi:
				lock (StickerSetNames)
					if (StickerSetNames.TryGetValue(issi.id, out setName)) break;
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
			result.PremiumAnimation = new Telegram.Bot.Types.File { FileSize = doc.size }.SetFileIds(premiumLocation, doc.dc_id, "f");
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
				doc.thumb = await Client.UploadFileAsync(stream.Content, stream.FileName);
				doc.flags |= InputMediaUploadedDocument.Flags.has_thumb;
				break;
			default: throw new WTException("Only InputFileStream is not supported for thumbnails");
		}

	}

	private static InputMediaGeoLive MakeGeoLive(double latitude, double longitude, int horizontalAccuracy, int heading, int proximityAlertRadius, int livePeriod = 0)
	=> new()
	{
		geo_point = new InputGeoPoint
		{
			lat = latitude,
			lon = longitude,
			accuracy_radius = horizontalAccuracy,
			flags = horizontalAccuracy > 0 ? InputGeoPoint.Flags.has_accuracy_radius : 0
		},
		period = livePeriod,
		heading = heading,
		proximity_notification_radius = proximityAlertRadius,
		flags = (livePeriod > 0 ? InputMediaGeoLive.Flags.has_period : 0)
			| (heading > 0 ? InputMediaGeoLive.Flags.has_heading : 0)
			| (proximityAlertRadius > 0 ? InputMediaGeoLive.Flags.has_proximity_notification_radius : 0)
	};

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
				r.Url, "text/html", url: r.HideUrl == true ? null : r.Url),
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
						(itmc.LinkPreviewOptions.PreferLargeMedia == true ? InputBotInlineMessageMediaWebPage.Flags.force_large_media : 0) |
						(itmc.LinkPreviewOptions.PreferSmallMedia == true ? InputBotInlineMessageMediaWebPage.Flags.force_small_media : 0) |
						(itmc.LinkPreviewOptions.ShowAboveText == true ? InputBotInlineMessageMediaWebPage.Flags.invert_media : 0)
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
			InputContactMessageContent icmc => new InputBotInlineMessageMediaContact
			{
				reply_markup = reply_markup,
				phone_number = icmc.PhoneNumber,
				first_name = icmc.FirstName,
				last_name = icmc.LastName,
				vcard = icmc.Vcard,
				flags = reply_markup != null ? InputBotInlineMessageMediaContact.Flags.has_reply_markup : 0
			},
			InputVenueMessageContent ivmc => new InputBotInlineMessageMediaVenue
			{
				reply_markup = reply_markup,
				geo_point = new InputGeoPoint { lat = ivmc.Latitude, lon = ivmc.Longitude },
				title = ivmc.Title,
				address = ivmc.Address,
				provider = ivmc.GooglePlaceId != null ? "gplaces" : "foursquare",
				venue_id = ivmc.GooglePlaceId ?? ivmc.FoursquareId,
				venue_type = ivmc.GooglePlaceType ?? ivmc.FoursquareType,
				flags = reply_markup != null ? InputBotInlineMessageMediaVenue.Flags.has_reply_markup : 0
			},
			InputLocationMessageContent ilmc => new InputBotInlineMessageMediaGeo
			{
				reply_markup = reply_markup,
				geo_point = new InputGeoPoint
				{
					lat = ilmc.Latitude,
					lon = ilmc.Longitude,
					accuracy_radius = (int)(ilmc.HorizontalAccuracy ?? 0),
					flags = ilmc.HorizontalAccuracy.HasValue ? InputGeoPoint.Flags.has_accuracy_radius : 0
				},
				heading = ilmc.Heading ?? 0,
				period = ilmc.LivePeriod ?? 0,
				proximity_notification_radius = ilmc.ProximityAlertRadius ?? 0,
				flags = (reply_markup != null ? InputBotInlineMessageMediaGeo.Flags.has_reply_markup : 0)
					| (ilmc.LivePeriod > 0 ? InputBotInlineMessageMediaGeo.Flags.has_period : 0)
					| (ilmc.Heading.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_heading : 0)
					| (ilmc.ProximityAlertRadius.HasValue ? InputBotInlineMessageMediaGeo.Flags.has_proximity_notification_radius : 0)
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
						| (iimc.NeedName == true ? TL.Invoice.Flags.name_requested : 0)
						| (iimc.NeedPhoneNumber == true ? TL.Invoice.Flags.phone_requested : 0)
						| (iimc.NeedEmail == true ? TL.Invoice.Flags.email_requested : 0)
						| (iimc.NeedShippingAddress == true ? TL.Invoice.Flags.shipping_address_requested : 0)
						| (iimc.SendPhoneNumberToProvider == true ? TL.Invoice.Flags.phone_to_provider : 0)
						| (iimc.SendEmailToProvider == true ? TL.Invoice.Flags.email_to_provider : 0)
						| (iimc.IsFlexible == true ? TL.Invoice.Flags.flexible : 0),
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
				InlineQueryResultContact iqrc => new InputBotInlineMessageMediaContact
				{
					reply_markup = reply_markup,
					phone_number = iqrc.PhoneNumber,
					first_name = iqrc.FirstName,
					last_name = iqrc.LastName,
					vcard = iqrc.Vcard,
					flags = reply_markup != null ? InputBotInlineMessageMediaContact.Flags.has_reply_markup : 0
				},
				InlineQueryResultLocation iqrl => new InputBotInlineMessageMediaGeo
				{
					reply_markup = reply_markup,
					geo_point = new InputGeoPoint
					{
						lat = iqrl.Latitude,
						lon = iqrl.Longitude,
						accuracy_radius = (int)(iqrl.HorizontalAccuracy ?? 0),
						flags = iqrl.HorizontalAccuracy.HasValue ? InputGeoPoint.Flags.has_accuracy_radius : 0
					},
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
					provider = iqrv.GooglePlaceId != null ? "gplaces" : "foursquare",
					venue_id = iqrv.GooglePlaceId ?? iqrv.FoursquareId,
					venue_type = iqrv.GooglePlaceType ?? iqrv.FoursquareType,
					flags = reply_markup != null ? InputBotInlineMessageMediaVenue.Flags.has_reply_markup : 0
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
		bool sendPhoneNumberToProvider, bool sendEmailToProvider, bool isFlexible) => new()
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
					| (needName == true ? TL.Invoice.Flags.name_requested : 0)
					| (needPhoneNumber == true ? TL.Invoice.Flags.phone_requested : 0)
					| (needEmail == true ? TL.Invoice.Flags.email_requested : 0)
					| (needShippingAddress == true ? TL.Invoice.Flags.shipping_address_requested : 0)
					| (sendPhoneNumberToProvider == true ? TL.Invoice.Flags.phone_to_provider : 0)
					| (sendEmailToProvider == true ? TL.Invoice.Flags.email_to_provider : 0)
					| (isFlexible == true ? TL.Invoice.Flags.flexible : 0),
				currency = currency,
				prices = prices.LabeledPrices(),
				max_tip_amount = maxTipAmount ?? 0,
				suggested_tip_amounts = suggestedTipAmounts?.Select(sta => (long)sta).ToArray(),
			},
			payload = Encoding.UTF8.GetBytes(payload),
			provider = providerToken,
			provider_data = new DataJSON { data = providerData ?? "null" },
			start_param = startParameter,
		};

	private async Task<ChatBoost> MakeBoost(Boost boost)
	{
		var cb = new ChatBoost
		{
			BoostId = boost.id,
			AddDate = boost.date,
			ExpirationDate = boost.expires,
		};
		if (boost.flags.HasFlag(Boost.Flags.giveaway))
			cb.Source = new ChatBoostSourceGiveaway
			{
				GiveawayMessageId = boost.giveaway_msg_id,
				User = boost.user_id == 0 ? null : await UserOrResolve(boost.user_id),
				IsUnclaimed = boost.flags.HasFlag(Boost.Flags.unclaimed)
			};
		else if (boost.flags.HasFlag(Boost.Flags.gift))
			cb.Source = new ChatBoostSourceGiftCode { User = await UserOrResolve(boost.user_id) };
		else
			cb.Source = new ChatBoostSourcePremium { User = await UserOrResolve(boost.user_id) };
		return cb;
	}

	Task<UpdatesBase> Messages_SendMessage(string? bConnId, InputPeer peer, string? message, long random_id,
		InputReplyTo? reply_to, ReplyMarkup? reply_markup, TL.MessageEntity[]? entities, bool silent, bool noforwards, long effect,
		bool invert_media = false, bool no_webpage = false)
	{
		var query = new TL.Methods.Messages_SendMessage
		{
			flags = (TL.Methods.Messages_SendMessage.Flags)((reply_to != null ? 0x1 : 0) | (reply_markup != null ? 0x4 : 0) | (entities != null ? 0x8 : 0) | (no_webpage ? 0x2 : 0) | (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)),
			peer = peer,
			reply_to = reply_to,
			message = message,
			random_id = random_id,
			reply_markup = reply_markup,
			entities = entities,
			effect = effect
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<UpdatesBase> Messages_SendMedia(string? bConnId, InputPeer peer, TL.InputMedia media, string? message, long random_id,
		InputReplyTo? reply_to, ReplyMarkup? reply_markup, TL.MessageEntity[]? entities, bool silent, bool noforwards, long effect,
		bool invert_media = false)
	{
		var query = new TL.Methods.Messages_SendMedia
		{
			flags = (TL.Methods.Messages_SendMedia.Flags)((reply_to != null ? 0x1 : 0) | (reply_markup != null ? 0x4 : 0) | (entities != null ? 0x8 : 0) | (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)),
			peer = peer,
			reply_to = reply_to,
			media = media,
			message = message,
			random_id = random_id,
			reply_markup = reply_markup,
			entities = entities,
			effect = effect
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<UpdatesBase> Messages_SendMultiMedia(string? bConnId, InputPeer peer, InputSingleMedia[] multi_media, 
		InputReplyTo? reply_to, bool silent, bool noforwards, long effect, bool invert_media = false)
	{
		var query = new TL.Methods.Messages_SendMultiMedia
		{
			flags = (TL.Methods.Messages_SendMultiMedia.Flags)((reply_to != null ? 0x1 : 0) | (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)),
			peer = peer,
			reply_to = reply_to,
			multi_media = multi_media,
			effect = effect
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<UpdatesBase> Messages_EditMessage(string? bConnId, InputPeer peer, int id, string? message = null, TL.InputMedia? media = null, TL.ReplyMarkup? reply_markup = null, TL.MessageEntity[]? entities = null, DateTime? schedule_date = null, int? quick_reply_shortcut_id = null, bool no_webpage = false, bool invert_media = false)
	{
		var query = new TL.Methods.Messages_EditMessage
		{
			flags = (TL.Methods.Messages_EditMessage.Flags)((message != null ? 0x800 : 0) | (media != null ? 0x4000 : 0) | (reply_markup != null ? 0x4 : 0) | (entities != null ? 0x8 : 0) | (schedule_date != null ? 0x8000 : 0) | (quick_reply_shortcut_id != null ? 0x20000 : 0) | (no_webpage ? 0x2 : 0) | (invert_media ? 0x10000 : 0)),
			peer = peer,
			id = id,
			message = message,
			media = media,
			reply_markup = reply_markup,
			entities = entities,
			schedule_date = schedule_date.GetValueOrDefault(),
			quick_reply_shortcut_id = quick_reply_shortcut_id.GetValueOrDefault(),
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<bool> Messages_EditInlineBotMessage(string? bConnId, InputBotInlineMessageIDBase id, string? message = null, TL.InputMedia? media = null, TL.ReplyMarkup? reply_markup = null, TL.MessageEntity[]? entities = null, bool no_webpage = false, bool invert_media = false)
	{
		var query = new TL.Methods.Messages_EditInlineBotMessage
		{
			flags = (TL.Methods.Messages_EditInlineBotMessage.Flags)((message != null ? 0x800 : 0) | (media != null ? 0x4000 : 0) | (reply_markup != null ? 0x4 : 0) | (entities != null ? 0x8 : 0) | (no_webpage ? 0x2 : 0) | (invert_media ? 0x10000 : 0)),
			id = id,
			message = message,
			media = media,
			reply_markup = reply_markup,
			entities = entities,
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	internal async Task<Telegram.Bot.Types.BusinessIntro?> MakeBusinessIntro(TL.BusinessIntro? intro) => intro == null ? null : new()
	{
		Title = intro.title,
		Message = intro.description,
		Sticker = intro.sticker is TL.Document doc ? await MakeSticker(doc, doc.GetAttribute<DocumentAttributeSticker>()) : null
	};

	private async Task<BusinessConnection> MakeBusinessConnection(BotBusinessConnection bbc) => new BusinessConnection
	{
		Id = bbc.connection_id,
		User = await UserOrResolve(bbc.user_id),
		UserChatId = bbc.user_id,
		Date = bbc.date,
		CanReply = bbc.flags.HasFlag(BotBusinessConnection.Flags.can_reply),
		IsEnabled = !bbc.flags.HasFlag(BotBusinessConnection.Flags.disabled)
	};

	internal StarTransaction MakeStarTransaction(TL.StarsTransaction transaction)
	{
		TransactionPartner partner = transaction.peer switch
		{
			StarsTransactionPeerFragment => new TransactionPartnerFragment { WithdrawalState = WithdrawalState() },
			StarsTransactionPeerAds => new TransactionPartnerTelegramAds(),
			StarsTransactionPeer { peer: PeerUser { user_id: var user_id } } => new TransactionPartnerUser
			{
				User = User(user_id)!,
				InvoicePayload = transaction.bot_payload == null ? null : Encoding.UTF8.GetString(transaction.bot_payload)
			},
			_ => new TransactionPartnerOther(),
		};
		return new StarTransaction
		{
			Id = transaction.id,
			Amount = checked((int)Math.Abs(transaction.stars)),
			Date = transaction.date,
			Source = transaction.stars > 0 ? partner : null,
			Receiver = transaction.stars <= 0 ? partner : null,
		};

		RevenueWithdrawalState? WithdrawalState()
		{
			if (transaction.transaction_date != default)
				return new RevenueWithdrawalStateSucceeded { Date = transaction.transaction_date, Url = transaction.transaction_url };
			if (transaction.flags.HasFlag(StarsTransaction.Flags.pending))
				return new RevenueWithdrawalStatePending();
			if (transaction.flags.HasFlag(StarsTransaction.Flags.failed))
				return new RevenueWithdrawalStateFailed();
			return null;
		}
	}
}
