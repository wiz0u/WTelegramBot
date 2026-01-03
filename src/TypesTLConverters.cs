using System.Text;
using TL;
using WTelegram;

namespace Telegram.Bot.Types;

/// <summary>Extension methods for converting between Client API and Bot API</summary>
public static class TypesTLConverters
{
	const long ZERO_CHANNEL_ID = -1000000000000;

	/// <summary>The corresponding Client API chat structure. Real type can be TL.User, TL.Chat, TL.Channel...</summary>
	public static TL.IObject? TLInfo(this Chat chat) => (chat as WTelegram.Types.Chat)?.TLInfo;
	/// <summary>The corresponding Client API chat full structure. Real type can be TL.Users_UserFull, TL.Messages_ChatFull)</summary>
	public static TL.IObject? TLInfo(this ChatFullInfo chat) => (chat as WTelegram.Types.ChatFullInfo)?.TLInfo;
	/// <summary>The corresponding Client API user structure</summary>
	public static TL.User? TLUser(this User user) => (user as WTelegram.Types.User)?.TLUser;
	/// <summary>The corresponding Client API update structure</summary>
	public static TL.Update? TLUpdate(this Update update) => (update as WTelegram.Types.Update)?.TLUpdate;
	/// <summary>The corresponding Client API message structure</summary>
	public static TL.MessageBase? TLMessage(this Message message) => (message as WTelegram.Types.Message)?.TLMessage;

	/// <summary>Convert TL.User to Bot Types.User</summary>
	[return: NotNullIfNotNull(nameof(tlUser))]
	public static WTelegram.Types.User? User(this TL.User? tlUser)
	{
		if (tlUser == null) return null;
		var user = new WTelegram.Types.User
		{
			TLUser = tlUser,
			Id = tlUser.id,
			IsBot = tlUser.IsBot,
			FirstName = tlUser.first_name,
			LastName = tlUser.last_name,
			Username = tlUser.MainUsername,
			LanguageCode = tlUser.lang_code,
			IsPremium = tlUser.flags.HasFlag(TL.User.Flags.premium),
			AddedToAttachmentMenu = tlUser.flags.HasFlag(TL.User.Flags.attach_menu_enabled),
			AccessHash = tlUser.access_hash
		};
		if (tlUser.IsBot) user.LoadBotFlags(tlUser);
		return user;
	}

	internal static void LoadBotFlags(this WTelegram.Types.User user, TL.User tlUser)
	{
		//   AddedToAttachmentMenu is not updated from a min user
		user.CanJoinGroups = !tlUser.flags.HasFlag(TL.User.Flags.bot_nochats);
		user.CanReadAllGroupMessages = tlUser.flags.HasFlag(TL.User.Flags.bot_chat_history);
		user.SupportsInlineQueries = tlUser.flags.HasFlag(TL.User.Flags.has_bot_inline_placeholder);
		user.CanConnectToBusiness = tlUser.flags2.HasFlag(TL.User.Flags2.bot_business);
		user.HasMainWebApp = tlUser.flags2.HasFlag(TL.User.Flags2.bot_has_main_app);
		user.HasTopicsEnabled = tlUser.flags2.HasFlag(TL.User.Flags2.bot_forum_view);
	}

	/// <summary>Convert TL.Chat to Bot Types.Chat</summary>
	[return: NotNullIfNotNull(nameof(chat))]
	public static WTelegram.Types.Chat? Chat(this ChatBase? chat) => chat switch
	{
		null => null,
		Channel channel => new()
		{
			TLInfo = chat,
			Id = ZERO_CHANNEL_ID - chat.ID,
			Type = channel.IsChannel ? ChatType.Channel : ChatType.Supergroup,
			Title = chat.Title,
			Username = channel.MainUsername,
			IsForum = channel.flags.HasFlag(Channel.Flags.forum),
			IsDirectMessages = channel.flags2.HasFlag(Channel.Flags2.monoforum),
			AccessHash = channel.access_hash
		},
		ChannelForbidden chForbidden => new()
		{
			TLInfo = chat,
			Id = ZERO_CHANNEL_ID - chat.ID,
			Type = chForbidden.IsChannel ? ChatType.Channel : ChatType.Supergroup,
			Title = chat.Title,
			AccessHash = chForbidden.access_hash
		},
		_ => new() { TLInfo = chat, Id = -chat.ID, Type = ChatType.Group, Title = chat.Title }
	};

	/// <summary>Convert TL.User to Bot Types.Chat</summary>
	[return: NotNullIfNotNull(nameof(user))]
	public static WTelegram.Types.Chat? Chat(this TL.User? user) => user == null ? null : new()
	{
		TLInfo = user,
		Id = user.id,
		Type = ChatType.Private,
		Username = user.MainUsername,
		FirstName = user.first_name,
		LastName = user.last_name,
		AccessHash = user.access_hash
	};

	/// <summary>Convert Bot Types.User to Bot Types.Chat</summary>
	[return: NotNullIfNotNull(nameof(user))]
	public static WTelegram.Types.Chat? Chat(this WTelegram.Types.User? user) => user == null ? null : new()
	{
		TLInfo = user.TLUser,
		Id = user.Id,
		Type = ChatType.Private,
		Username = user.Username,
		FirstName = user.FirstName,
		LastName = user.LastName,
		AccessHash = user.AccessHash
	};


	/// <summary>Convert TL.ChatParticipantBase to Types.ChatMember</summary>
	public static ChatMember ChatMember(this ChatParticipantBase? participant, User user)
		=> participant switch
		{
			ChatParticipantCreator => new ChatMemberOwner { User = user },
			ChatParticipantAdmin => new ChatMemberAdministrator
			{
				User = user,
				CanManageChat = true,
				CanChangeInfo = true,
				//CanPostMessages, CanEditMessages: set only for channels
				CanDeleteMessages = true,
				CanInviteUsers = true,
				CanRestrictMembers = true,
				CanPinMessages = true,
				//CanManageTopics: set only for supergroups
				CanPromoteMembers = false,
				CanManageVideoChats = true,
				//CanPostStories, CanEditStories, CanDeleteStories, CanManageDirectMessages: set only for channels
				IsAnonymous = false,
			},
			ChatParticipant => new ChatMemberMember { User = user },
			_ => new ChatMemberLeft { User = user }
		};

	internal static ChatMember ChatMember(this ChannelParticipantBase? participant, User user)
		=> participant switch
		{
			ChannelParticipantSelf cps => new ChatMemberMember { User = user, UntilDate = cps.subscription_until_date.NullIfDefault() },
			ChannelParticipant cp => new ChatMemberMember { User = user, UntilDate = cp.subscription_until_date.NullIfDefault() },
			ChannelParticipantCreator cpc => new ChatMemberOwner { User = user, CustomTitle = cpc.rank, IsAnonymous = cpc.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.anonymous) },
			ChannelParticipantAdmin cpa => new ChatMemberAdministrator
			{
				User = user,
				CustomTitle = cpa.rank,
				CanBeEdited = cpa.flags.HasFlag(ChannelParticipantAdmin.Flags.can_edit),
				IsAnonymous = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.anonymous),
				CanManageChat = cpa.admin_rights.flags != 0,
				CanPostMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.post_messages),
				CanEditMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.edit_messages),
				CanDeleteMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.delete_messages),
				CanManageVideoChats = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_call),
				CanRestrictMembers = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.ban_users),
				CanPromoteMembers = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.add_admins),
				CanChangeInfo = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.change_info),
				CanInviteUsers = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.invite_users),
				CanPinMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.pin_messages),
				CanManageTopics = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_topics),
				CanPostStories = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.post_stories),
				CanEditStories = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.edit_stories),
				CanDeleteStories = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.delete_stories),
				CanManageDirectMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_direct_messages),
			},
			ChannelParticipantBanned cpb =>
				cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.view_messages)
				? new ChatMemberBanned { User = user, UntilDate = UntilDate(cpb.banned_rights.until_date) }
				: new ChatMemberRestricted
				{
					User = user,
					IsMember = !cpb.flags.HasFlag(ChannelParticipantBanned.Flags.left),
					UntilDate = UntilDate(cpb.banned_rights.until_date),
					CanChangeInfo = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.change_info),
					CanInviteUsers = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.invite_users),
					CanPinMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.pin_messages),
					CanSendMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_messages),
					CanSendAudios = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_audios),
					CanSendDocuments = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_docs),
					CanSendPhotos = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_photos),
					CanSendVideos = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_videos),
					CanSendVideoNotes = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_roundvideos),
					CanSendVoiceNotes = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_voices),
					CanSendPolls = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_polls),
					CanSendOtherMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games),
					CanAddWebPagePreviews = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.embed_links),
					CanManageTopics = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.manage_topics),
				},
			_ /*ChannelParticipantLeft*/ => new ChatMemberLeft { User = user, },
		};

	private static DateTime? UntilDate(DateTime until_date) => until_date == DateTime.MaxValue ? null : until_date;

	[return: NotNullIfNotNull(nameof(banned_rights))]
	internal static ChatPermissions? ChatPermissions(this ChatBannedRights? banned_rights) => banned_rights == null ? null : new ChatPermissions
	{
		CanSendMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_messages),
		CanSendAudios = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_audios),
		CanSendDocuments = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_docs),
		CanSendPhotos = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_photos),
		CanSendVideos = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_videos),
		CanSendVideoNotes = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_roundvideos),
		CanSendVoiceNotes = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_voices),
		CanSendPolls = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_polls),
		CanSendOtherMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games),
		CanAddWebPagePreviews = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.embed_links),
		CanChangeInfo = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.change_info),
		CanInviteUsers = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.invite_users),
		CanPinMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.pin_messages),
		CanManageTopics = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.manage_topics)
	};

	internal static ChatPermissions LegacyMode(this ChatPermissions permissions, bool? useIndependentChatPermissions)
	{
		if (useIndependentChatPermissions != true)
		{
			if (permissions.CanSendPolls) permissions.CanSendMessages = true;
			if (permissions.CanSendOtherMessages || permissions.CanAddWebPagePreviews)
				permissions.CanSendAudios = permissions.CanSendDocuments = permissions.CanSendPhotos = permissions.CanSendVideos =
					permissions.CanSendVideoNotes = permissions.CanSendVoiceNotes = permissions.CanSendMessages = true;
		}
		return permissions;
	}

	internal static ChatBannedRights ToChatBannedRights(this ChatPermissions permissions, DateTime? untilDate = default) => new()
	{
		until_date = untilDate ?? default,
		flags = (permissions.CanSendMessages ? 0 : ChatBannedRights.Flags.send_messages)
			| (permissions.CanSendAudios ? 0 : ChatBannedRights.Flags.send_audios)
			| (permissions.CanSendDocuments ? 0 : ChatBannedRights.Flags.send_docs)
			| (permissions.CanSendPhotos ? 0 : ChatBannedRights.Flags.send_photos)
			| (permissions.CanSendVideos ? 0 : ChatBannedRights.Flags.send_videos)
			| (permissions.CanSendVideoNotes ? 0 : ChatBannedRights.Flags.send_roundvideos)
			| (permissions.CanSendVoiceNotes ? 0 : ChatBannedRights.Flags.send_voices)
			| (permissions.CanSendPolls ? 0 : ChatBannedRights.Flags.send_polls)
			| (permissions.CanSendOtherMessages ? 0 : ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games | ChatBannedRights.Flags.send_inline)
			| (permissions.CanAddWebPagePreviews ? 0 : ChatBannedRights.Flags.embed_links)
			| (permissions.CanChangeInfo ? 0 : ChatBannedRights.Flags.change_info)
			| (permissions.CanInviteUsers ? 0 : ChatBannedRights.Flags.invite_users)
			| (permissions.CanPinMessages ? 0 : ChatBannedRights.Flags.pin_messages)
			| (permissions.CanManageTopics ? 0 : ChatBannedRights.Flags.manage_topics)
	};

	[return: NotNullIfNotNull(nameof(location))]
	internal static ChatLocation? ChatLocation(this ChannelLocation? location)
		=> location == null ? null : new ChatLocation { Location = Location(location.geo_point), Address = location.address };

	[return: NotNullIfNotNull(nameof(geo))]
	internal static Location? Location(this GeoPoint? geo)
		=> geo == null ? null : new Location
		{
			Longitude = geo.lon,
			Latitude = geo.lat,
			HorizontalAccuracy = geo.flags.HasFlag(GeoPoint.Flags.has_accuracy_radius) ? geo.accuracy_radius : null
		};

	internal static InlineKeyboardMarkup? InlineKeyboardMarkup(this TL.ReplyMarkup? reply_markup) => reply_markup is not ReplyInlineMarkup rim ? null :
		new InlineKeyboardMarkup(rim.rows.Select(row => row.buttons.Select(btn => btn switch
		{
			KeyboardButtonUrl kbu => InlineKeyboardButton.WithUrl(kbu.text, kbu.url),
			KeyboardButtonCallback kbc => InlineKeyboardButton.WithCallbackData(kbc.text, Encoding.UTF8.GetString(kbc.data)),
			KeyboardButtonGame kbg => InlineKeyboardButton.WithCallbackGame(kbg.text),
			KeyboardButtonBuy kbb => InlineKeyboardButton.WithPay(kbb.text),
			KeyboardButtonSwitchInline kbsi => kbsi.flags.HasFlag(KeyboardButtonSwitchInline.Flags.same_peer) ?
				InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(kbsi.text, kbsi.query) :
				InlineKeyboardButton.WithSwitchInlineQuery(kbsi.text, kbsi.query),
			KeyboardButtonCopy kbco => InlineKeyboardButton.WithCopyText(kbco.text, kbco.copy_text),
			KeyboardButtonUrlAuth kbua => InlineKeyboardButton.WithLoginUrl(kbua.text, new LoginUrl
			{
				Url = kbua.url,
				ForwardText = kbua.fwd_text,
			}),
			KeyboardButtonWebView kbwv => InlineKeyboardButton.WithWebApp(kbwv.text, new WebAppInfo { Url = kbwv.url }),
			_ => new InlineKeyboardButton(btn.Text),
		})));

	internal static Video Video(this TL.Document document, MessageMediaDocument mmd)
	{
		var thumb = document.LargestThumbSize;
		var video = document.GetAttribute<DocumentAttributeVideo>();
		return new Video
		{
			FileSize = document.size,
			Width = video?.w ?? 0,
			Height = video?.h ?? 0,
			Duration = (int)(video?.duration + 0.5 ?? 0.0),
			Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id),
			FileName = document.Filename,
			MimeType = document.mime_type,
			Cover = mmd.video_cover?.PhotoSizes(),
			StartTimestamp = mmd.video_timestamp.NullIfZero(),
		}.SetFileIds(document.ToFileLocation(), document.dc_id);
	}

	/// <summary>Convert TL.Photo into Bot Types.PhotoSize[]</summary>
	public static PhotoSize[]? PhotoSizes(this PhotoBase photoBase)
		=> (photoBase is not Photo photo) ? null : photo.sizes.Select(ps => ps.PhotoSize(photo.ToFileLocation(ps), photo.dc_id)).ToArray();

	/// <summary>Convert TL.PhotoSize into Bot Types.PhotoSize</summary>
	public static PhotoSize PhotoSize(this PhotoSizeBase ps, InputFileLocationBase location, int dc_id)
		=> new PhotoSize()
		{
			Width = ps.Width,
			Height = ps.Height,
			FileSize = ps.FileSize,
		}.SetFileIds(location, dc_id, ps.Type);

	/// <summary>Encode TL.InputFileLocation as FileId/FileUniqueId strings into a Bot File structure</summary>
	public static T SetFileIds<T>(this T file, InputFileLocationBase location, int dc_id, string? type = null) where T : FileBase
	{
		using var memStream = new MemoryStream(128);
		using (var writer = new BinaryWriter(memStream))
		{
			writer.WriteTLObject(location);
			writer.Write((byte)dc_id);
			writer.Write((uint)(file.FileSize ?? 0));
			writer.Write((byte)42);
		}
		var bytes = memStream.ToArray();
		file.FileId = ToBase64(bytes);
		bytes[12] = (byte)(type?[0] ?? 0);
		file.FileUniqueId = ToBase64(bytes, 3, 10);
		return file;
	}

	/// <summary>Decode FileId into TL.InputFileLocation</summary>
	public static (TGFile file, InputFileLocationBase location, int dc_id) ParseFileId(this string fileId, bool generateFile = false)
	{
		var idBytes = fileId.FromBase64();
		if (idBytes[^1] != 42) throw new WTException("Unsupported file_id format");
		using var memStream = new MemoryStream(idBytes);
		using var reader = new BinaryReader(memStream);
		var location = (InputFileLocationBase)reader.ReadTLObject();
		byte dc_id = reader.ReadByte();
		long fileSize = reader.ReadUInt32();
		if (!generateFile) return (null!, location, dc_id);

		idBytes[12] = idBytes[^8]; // patch byte following id with InputPhotoFileLocation.thumb_size
		var fileUniqueId = ToBase64(idBytes, 3, 10);
		var filename = location.GetType().Name;
		if (filename.StartsWith("Input")) filename = filename[5..];
		if (filename.EndsWith("FileLocation")) filename = filename[..^12];
		filename = $"{filename}_{fileUniqueId}";
		if (filename.Contains("Photo")) filename += ".jpg";
		return (new TGFile
		{
			FilePath = $"{fileId}/{filename}",
			FileId = fileId,
			FileUniqueId = ToBase64(idBytes, 3, 10),
			FileSize = fileSize
		}, location, dc_id);
	}

	internal static ChatPhoto? ChatPhoto(this PhotoBase? photoBase)
	{
		if (photoBase is not Photo photo) return null;
		var small = photo.sizes?.FirstOrDefault(ps => ps.Type == "a");
		var big = photo.sizes?.FirstOrDefault(ps => ps.Type == "c");
		small ??= photo.sizes?.Aggregate((agg, next) => next.Width > 0 && (long)next.Width * next.Height < (long)agg.Width * agg.Height ? next : agg)!;
		big ??= photo.LargestPhotoSize;
		using var memStream = new MemoryStream(128);
		using var writer = new BinaryWriter(memStream);
		writer.WriteTLObject(photo.ToFileLocation(big));
		writer.Write((byte)photo.dc_id);
		writer.Write((uint)big.FileSize);
		writer.Write((byte)42);
		var bytes = memStream.ToArray();
		var chatPhoto = new ChatPhoto()
		{ BigFileId = ToBase64(bytes) };
		bytes[12] = (byte)big.Type[0]; // patch byte following id with thumb_size
		chatPhoto.BigFileUniqueId = ToBase64(bytes, 3, 10);
		memStream.SetLength(0);
		writer.WriteTLObject(photo.ToFileLocation(small));
		writer.Write((byte)photo.dc_id);
		writer.Write((uint)small.FileSize);
		writer.Write((byte)42);
		bytes = memStream.ToArray();
		chatPhoto.SmallFileId = ToBase64(bytes);
		bytes[12] = (byte)small.Type[0]; // patch byte following id with thumb_size
		chatPhoto.SmallFileUniqueId = ToBase64(bytes, 3, 10);
		return chatPhoto;
	}

	internal static string? InlineMessageId(this InputBotInlineMessageIDBase? msg_id)
	{
		if (msg_id == null) return null;
		using var memStream = new MemoryStream(128);
		using (var writer = new BinaryWriter(memStream))
			msg_id.WriteTL(writer);
		var bytes = memStream.ToArray();
		return ToBase64(bytes);
	}

	internal static InputBotInlineMessageIDBase ParseInlineMsgID(this string inlineMessageId)
	{
		var idBytes = inlineMessageId.FromBase64();
		using var memStream = new MemoryStream(idBytes);
		using var reader = new BinaryReader(memStream);
		return (InputBotInlineMessageIDBase)reader.ReadTLObject();
	}

	private static byte[] FromBase64(this string str) => Convert.FromBase64String(str.Replace('_', '/').Replace('-', '+') + new string('=', (2147483644 - str.Length) % 4));
	private static string ToBase64(this byte[] bytes) => ToBase64(bytes, 0, bytes.Length);
	private static string ToBase64(this byte[] bytes, int offset, int length) => Convert.ToBase64String(bytes, offset, length).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	internal static SendMessageAction ChatAction(this ChatAction action) => action switch
	{
		Enums.ChatAction.Typing => new SendMessageTypingAction(),
		Enums.ChatAction.UploadPhoto => new SendMessageUploadPhotoAction(),
		Enums.ChatAction.RecordVideo => new SendMessageRecordVideoAction(),
		Enums.ChatAction.UploadVideo => new SendMessageUploadVideoAction(),
		Enums.ChatAction.RecordVoice => new SendMessageRecordAudioAction(),
		Enums.ChatAction.UploadVoice => new SendMessageUploadAudioAction(),
		Enums.ChatAction.UploadDocument => new SendMessageUploadDocumentAction(),
		Enums.ChatAction.FindLocation => new SendMessageGeoLocationAction(),
		Enums.ChatAction.RecordVideoNote => new SendMessageRecordRoundAction(),
		Enums.ChatAction.UploadVideoNote => new SendMessageUploadRoundAction(),
		Enums.ChatAction.ChooseSticker => new SendMessageChooseStickerAction(),
		_ => throw new RpcException(400, "Wrong parameter action in request")
	};

	internal static BotMenuButtonBase? BotMenuButton(this MenuButton? menuButton)
		=> menuButton switch
		{
			MenuButtonDefault => null,
			MenuButtonCommands => new BotMenuButtonCommands(),
			MenuButtonWebApp mbwa => new BotMenuButton { text = mbwa.Text, url = mbwa.WebApp.Url },
			_ => throw new WTException("MenuButton has unsupported type")
		};

	internal static MenuButton MenuButton(this BotMenuButtonBase? menuButton)
		=> menuButton switch
		{
			null => new MenuButtonDefault(),
			BotMenuButtonCommands => new MenuButtonCommands(),
			BotMenuButton bmb => new MenuButtonWebApp { Text = bmb.text, WebApp = new WebAppInfo { Url = bmb.url } },
			_ => throw new WTException("Unrecognized BotMenuButtonBase")
		};

	internal static ChatAdminRights ChatAdminRights(this ChatAdministratorRights? rights)
		=> rights == null ? new() : new()
		{
			flags = (rights.IsAnonymous ? TL.ChatAdminRights.Flags.anonymous : 0)
			| (rights.CanManageChat ? TL.ChatAdminRights.Flags.other : 0)
			| (rights.CanDeleteMessages ? TL.ChatAdminRights.Flags.delete_messages : 0)
			| (rights.CanManageVideoChats ? TL.ChatAdminRights.Flags.manage_call : 0)
			| (rights.CanRestrictMembers ? TL.ChatAdminRights.Flags.ban_users : 0)
			| (rights.CanPromoteMembers ? TL.ChatAdminRights.Flags.add_admins : 0)
			| (rights.CanChangeInfo ? TL.ChatAdminRights.Flags.change_info : 0)
			| (rights.CanInviteUsers ? TL.ChatAdminRights.Flags.invite_users : 0)
			| (rights.CanPostMessages ? TL.ChatAdminRights.Flags.post_messages : 0)
			| (rights.CanEditMessages ? TL.ChatAdminRights.Flags.edit_messages : 0)
			| (rights.CanPinMessages ? TL.ChatAdminRights.Flags.pin_messages : 0)
			| (rights.CanManageTopics ? TL.ChatAdminRights.Flags.manage_topics : 0)
			| (rights.CanPostStories ? TL.ChatAdminRights.Flags.post_stories : 0)
			| (rights.CanEditStories ? TL.ChatAdminRights.Flags.edit_stories : 0)
			| (rights.CanDeleteStories ? TL.ChatAdminRights.Flags.delete_stories : 0)
			| (rights.CanManageDirectMessages ? TL.ChatAdminRights.Flags.manage_direct_messages : 0)
		};

	internal static ChatAdministratorRights ChatAdministratorRights(this ChatAdminRights? rights)
		=> rights == null ? new() : new()
		{
			IsAnonymous = rights.flags.HasFlag(TL.ChatAdminRights.Flags.anonymous),
			CanManageChat = rights.flags.HasFlag(TL.ChatAdminRights.Flags.other),
			CanDeleteMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.delete_messages),
			CanManageVideoChats = rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_call),
			CanRestrictMembers = rights.flags.HasFlag(TL.ChatAdminRights.Flags.ban_users),
			CanPromoteMembers = rights.flags.HasFlag(TL.ChatAdminRights.Flags.add_admins),
			CanChangeInfo = rights.flags.HasFlag(TL.ChatAdminRights.Flags.change_info),
			CanInviteUsers = rights.flags.HasFlag(TL.ChatAdminRights.Flags.invite_users),
			CanPostMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.post_messages),
			CanEditMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.edit_messages),
			CanPinMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.pin_messages),
			CanManageTopics = rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_topics),
			CanPostStories = rights.flags.HasFlag(TL.ChatAdminRights.Flags.post_stories),
			CanEditStories = rights.flags.HasFlag(TL.ChatAdminRights.Flags.edit_stories),
			CanDeleteStories = rights.flags.HasFlag(TL.ChatAdminRights.Flags.delete_stories),
			CanManageDirectMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_direct_messages),
		};

	[return: NotNullIfNotNull(nameof(maskPosition))]
	internal static MaskCoords? MaskCoord(this MaskPosition? maskPosition)
		=> maskPosition == null ? null : new MaskCoords
		{ n = (int)maskPosition.Point - 1, x = maskPosition.XShift, y = maskPosition.YShift, zoom = maskPosition.Scale };

	internal static TL.LabeledPrice[] LabeledPrices(this IEnumerable<Payments.LabeledPrice> prices)
		=> [.. prices.Select(p => new TL.LabeledPrice { label = p.Label, amount = p.Amount })];

	internal static TL.BotCommand BotCommand(this BotCommand bc)
		=> new() { command = bc.Command.StartsWith("/") ? bc.Command[1..] : bc.Command, description = bc.Description };
	internal static BotCommand BotCommand(this TL.BotCommand bc)
		=> new() { Command = bc.command, Description = bc.description };

	[return: NotNullIfNotNull(nameof(pa))]
	internal static Payments.ShippingAddress? ShippingAddress(this PostAddress? pa) => pa == null ? null : new()
	{
		CountryCode = pa.country_iso2,
		State = pa.state,
		City = pa.city,
		StreetLine1 = pa.street_line1,
		StreetLine2 = pa.street_line2,
		PostCode = pa.post_code
	};

	[return: NotNullIfNotNull(nameof(pri))]
	internal static Payments.OrderInfo? OrderInfo(this PaymentRequestedInfo? pri) => pri == null ? null : new()
	{
		Name = pri.name,
		PhoneNumber = pri.phone,
		Email = pri.email,
		ShippingAddress = pri.shipping_address.ShippingAddress()
	};

	internal static InlineQueryPeerType[] InlineQueryPeerTypes(this SwitchInlineQueryChosenChat swiqcc)
		=> InlineQueryPeerTypes(swiqcc.AllowUserChats, swiqcc.AllowBotChats, swiqcc.AllowGroupChats, swiqcc.AllowChannelChats);

	internal static InlineQueryPeerType[] InlineQueryPeerTypes(bool allowUserChats, bool allowBotChats, bool allowGroupChats, bool allowChannelChats)
	{
		var result = new List<InlineQueryPeerType>();
		if (allowUserChats) result.Add(InlineQueryPeerType.PM);
		if (allowBotChats) result.Add(InlineQueryPeerType.BotPM);
		if (allowGroupChats) { result.Add(InlineQueryPeerType.Chat); result.Add(InlineQueryPeerType.Megagroup); }
		if (allowChannelChats) result.Add(InlineQueryPeerType.Broadcast);
		return [.. result];
	}

	internal static SecureValueErrorBase SecureValueError(PassportElementError error)
	{
		var type = error.Type.ToSecureValueType();
		var text = error.Message;
		return error switch
		{
			PassportElementErrorDataField e => new SecureValueErrorData { type = type, text = text, data_hash = e.DataHash.FromBase64(), field = e.FieldName },
			PassportElementErrorFrontSide e => new SecureValueErrorFrontSide { type = type, text = text, file_hash = e.FileHash.FromBase64() },
			PassportElementErrorReverseSide e => new SecureValueErrorReverseSide { type = type, text = text, file_hash = e.FileHash.FromBase64() },
			PassportElementErrorSelfie e => new SecureValueErrorSelfie { type = type, text = text, file_hash = e.FileHash.FromBase64() },
			PassportElementErrorFile e => new SecureValueErrorFile { type = type, text = text, file_hash = e.FileHash.FromBase64() },
			PassportElementErrorFiles e => new SecureValueErrorFiles { type = type, text = text, file_hash = [.. e.FileHashes.Select(FromBase64)] },
			PassportElementErrorTranslationFile e => new SecureValueErrorTranslationFile { type = type, text = text, file_hash = e.FileHash.FromBase64() },
			PassportElementErrorTranslationFiles e => new SecureValueErrorTranslationFiles { type = type, text = text, file_hash = [.. e.FileHashes.Select(FromBase64)] },
			PassportElementErrorUnspecified e => new SecureValueError { type = type, text = text, hash = e.ElementHash.FromBase64() },
			_ => throw new WTException("Unrecognized PassportElementError")
		};
	}

	internal static SecureValueType ToSecureValueType(this EncryptedPassportElementType type) => type switch
	{
		EncryptedPassportElementType.PersonalDetails => SecureValueType.PersonalDetails,
		EncryptedPassportElementType.Passport => SecureValueType.Passport,
		EncryptedPassportElementType.DriverLicense => SecureValueType.DriverLicense,
		EncryptedPassportElementType.IdentityCard => SecureValueType.IdentityCard,
		EncryptedPassportElementType.InternalPassport => SecureValueType.InternalPassport,
		EncryptedPassportElementType.Address => SecureValueType.Address,
		EncryptedPassportElementType.UtilityBill => SecureValueType.UtilityBill,
		EncryptedPassportElementType.BankStatement => SecureValueType.BankStatement,
		EncryptedPassportElementType.RentalAgreement => SecureValueType.RentalAgreement,
		EncryptedPassportElementType.PassportRegistration => SecureValueType.PassportRegistration,
		EncryptedPassportElementType.TemporaryRegistration => SecureValueType.TemporaryRegistration,
		EncryptedPassportElementType.PhoneNumber => SecureValueType.Phone,
		EncryptedPassportElementType.Email => SecureValueType.Email,
		_ => throw new WTException("Unrecognized EncryptedPassportElementType")
	};

	internal static EncryptedPassportElement EncryptedPassportElement(this TL.SecureValue sv) => new()
	{
		Type = sv.type switch
		{
			SecureValueType.PersonalDetails => EncryptedPassportElementType.PersonalDetails,
			SecureValueType.Passport => EncryptedPassportElementType.Passport,
			SecureValueType.DriverLicense => EncryptedPassportElementType.DriverLicense,
			SecureValueType.IdentityCard => EncryptedPassportElementType.IdentityCard,
			SecureValueType.InternalPassport => EncryptedPassportElementType.InternalPassport,
			SecureValueType.Address => EncryptedPassportElementType.Address,
			SecureValueType.UtilityBill => EncryptedPassportElementType.UtilityBill,
			SecureValueType.BankStatement => EncryptedPassportElementType.BankStatement,
			SecureValueType.RentalAgreement => EncryptedPassportElementType.RentalAgreement,
			SecureValueType.PassportRegistration => EncryptedPassportElementType.PassportRegistration,
			SecureValueType.TemporaryRegistration => EncryptedPassportElementType.TemporaryRegistration,
			SecureValueType.Phone => EncryptedPassportElementType.PhoneNumber,
			SecureValueType.Email => EncryptedPassportElementType.Email,
			_ => 0,
		},
		Data = sv.data?.data.ToBase64(),
		PhoneNumber = sv.plain_data is SecurePlainPhone spp ? spp.phone : null,
		Email = sv.plain_data is SecurePlainEmail spe ? spe.email : null,
		Files = sv.files?.Select(PassportFile).ToArray(),
		FrontSide = sv.front_side?.PassportFile(),
		ReverseSide = sv.reverse_side?.PassportFile(),
		Selfie = sv.selfie?.PassportFile(),
		Translation = sv.translation?.Select(PassportFile).ToArray(),
		Hash = sv.hash.ToBase64()
	};

	internal static PassportData PassportData(this MessageActionSecureValuesSentMe masvsm) => new()
	{
		Data = [.. masvsm.values.Select(EncryptedPassportElement)],
		Credentials = new EncryptedCredentials { Data = masvsm.credentials.data.ToBase64(), Hash = masvsm.credentials.hash.ToBase64(), Secret = masvsm.credentials.secret.ToBase64() }
	};

	private static PassportFile PassportFile(this SecureFile file)
		=> new PassportFile { FileSize = file.size, FileDate = file.date }.SetFileIds(file.ToFileLocation(), file.dc_id);

	internal static SharedUser ToSharedUser(this RequestedPeer peer) => peer is not RequestedPeerUser rpu ? null! :
		new SharedUser { UserId = rpu.user_id, FirstName = rpu.first_name, LastName = rpu.last_name, Username = rpu.username, Photo = rpu.photo.PhotoSizes() };

	internal static ChatShared? ToSharedChat(this RequestedPeer peer, int requestId) => peer switch
	{
		RequestedPeerChat rpc => new ChatShared { RequestId = requestId, ChatId = -rpc.chat_id, Title = rpc.title, Photo = rpc.photo.PhotoSizes() },
		RequestedPeerChannel rpch => new ChatShared { RequestId = requestId, ChatId = ZERO_CHANNEL_ID - rpch.channel_id, Title = rpch.title, Username = rpch.username, Photo = rpch.photo.PhotoSizes() },
		_ => null
	};

	internal static TL.Reaction Reaction(this ReactionType reaction) => reaction switch
	{
		ReactionTypeEmoji rte => new TL.ReactionEmoji { emoticon = rte.Emoji },
		ReactionTypeCustomEmoji rtce => new TL.ReactionCustomEmoji { document_id = long.Parse(rtce.CustomEmojiId) },
		ReactionTypePaid => new TL.ReactionPaid { },
		_ => throw new WTException("Unrecognized ReactionType")
	};
	internal static ReactionType ReactionType(this TL.Reaction reaction) => reaction switch
	{
		TL.ReactionEmoji rte => new ReactionTypeEmoji { Emoji = rte.emoticon },
		TL.ReactionCustomEmoji rce => new ReactionTypeCustomEmoji { CustomEmojiId = rce.document_id.ToString() },
		TL.ReactionPaid => new ReactionTypePaid { },
		_ => throw new WTException("Unrecognized Reaction")
	};

	internal static InputMediaWebPage? InputMediaWebPage(this LinkPreviewOptions? lpo) => lpo?.Url == null ? null : new InputMediaWebPage
	{
		url = lpo.Url,
		flags = TL.InputMediaWebPage.Flags.optional
			| (lpo.PreferLargeMedia ? TL.InputMediaWebPage.Flags.force_large_media : 0)
			| (lpo.PreferSmallMedia ? TL.InputMediaWebPage.Flags.force_small_media : 0)
	};

	internal static LinkPreviewOptions LinkPreviewOptions(this MessageMediaWebPage mmwp, bool invert_media) => new()
	{
		Url = mmwp.webpage.Url,
		PreferLargeMedia = mmwp.flags.HasFlag(MessageMediaWebPage.Flags.force_large_media),
		PreferSmallMedia = mmwp.flags.HasFlag(MessageMediaWebPage.Flags.force_small_media),
		ShowAboveText = invert_media
	};

	internal static Birthdate? Birthdate(this TL.Birthday? birthday) => birthday == null ? null : new Birthdate
	{
		Day = birthday.day,
		Month = birthday.month,
		Year = birthday.flags.HasFlag(Birthday.Flags.has_year) ? birthday.year : null
	};

	internal static BusinessLocation? BusinessLocation(this TL.BusinessLocation? loc) => loc == null ? null : new BusinessLocation
	{
		Address = loc.address,
		Location = loc.geo_point.Location()
	};

	internal static BusinessOpeningHours? BusinessOpeningHours(this TL.BusinessWorkHours? hours) => hours == null ? null : new BusinessOpeningHours
	{
		TimeZoneName = hours.timezone_id,
		OpeningHours = [.. hours.weekly_open.Select(wo =>
			new BusinessOpeningHoursInterval { OpeningMinute = wo.start_minute, ClosingMinute = wo.end_minute })]
	};

	internal static Document? Document(this TL.DocumentBase document, TL.PhotoSizeBase? thumb = null)
		=> document is not TL.Document doc ? null : new Document
		{
			FileSize = doc.size,
			Thumbnail = thumb?.PhotoSize(doc.ToFileLocation(thumb), doc.dc_id),
			FileName = doc.Filename,
			MimeType = doc.mime_type
		}.SetFileIds(doc.ToFileLocation(), doc.dc_id);

	internal static BackgroundType BackgroundType(this WallPaperBase wallpaper) => wallpaper switch
	{
		WallPaperNoFile wpnf => wpnf.settings.emoticon != null
			? new BackgroundTypeChatTheme { ThemeName = wpnf.settings.emoticon }
			: new BackgroundTypeFill { Fill = wpnf.settings.BackgroundFill()!, DarkThemeDimming = wpnf.settings?.intensity ?? 0 },
		WallPaper wp => wp.flags.HasFlag(WallPaper.Flags.pattern)
			? new BackgroundTypePattern
			{
				Document = wp.document.Document()!,
				Fill = wp.settings.BackgroundFill()!,
				IsMoving = wp.settings?.flags.HasFlag(WallPaperSettings.Flags.motion) == true,
				Intensity = Math.Abs(wp.settings?.intensity ?? 0),
				IsInverted = wp.settings?.intensity < 0
			}
			: new BackgroundTypeWallpaper
			{
				Document = wp.document.Document()!,
				DarkThemeDimming = wp.settings?.intensity ?? 0,
				IsBlurred = wp.settings?.flags.HasFlag(WallPaperSettings.Flags.blur) == true,
				IsMoving = wp.settings?.flags.HasFlag(WallPaperSettings.Flags.motion) == true,
			},
		_ => throw new WTException("Unrecognized WallPaperBase")
	};

	private static BackgroundFill? BackgroundFill(this WallPaperSettings? settings) => settings == null ? null :
		settings.flags.HasFlag(WallPaperSettings.Flags.has_third_background_color) ? new BackgroundFillFreeformGradient
		{
			Colors = settings.flags.HasFlag(WallPaperSettings.Flags.has_fourth_background_color)
				? [settings.background_color, settings.second_background_color, settings.third_background_color, settings.fourth_background_color]
				: [settings.background_color, settings.second_background_color, settings.third_background_color]
		}
		: settings.second_background_color == settings.background_color ? new BackgroundFillSolid { Color = settings.background_color }
		: new BackgroundFillGradient
		{
			TopColor = settings.background_color,
			BottomColor = settings.second_background_color,
			RotationAngle = settings.rotation
		};

	internal static PaidMedia PaidMedia(MessageExtendedMediaBase memb) => memb switch
	{
		MessageExtendedMediaPreview memp => new PaidMediaPreview { Width = memp.w, Height = memp.h, Duration = memp.video_duration },
		MessageExtendedMedia mem => PaidMedia(mem.media),
		_ => throw new WTException("Unrecognized MessageExtendedMediaBase")
	};

	internal static PaidMedia PaidMedia(TL.MessageMedia media) => media switch
	{
		MessageMediaPhoto mmp => new PaidMediaPhoto { Photo = mmp.photo.PhotoSizes()! },
		MessageMediaDocument { document: TL.Document document } mmd when mmd.flags.HasFlag(MessageMediaDocument.Flags.video) =>
			new PaidMediaVideo { Video = document.Video(mmd) },
		_ => throw new WTException("Unrecognized Paid MessageMedia")
	};

	internal static bool IsPositive(this StarsAmountBase amount)
		=> amount.Amount > 0 || (amount.Amount == 0 && amount is StarsAmount { nanos: > 0 });

	internal static StarAmount StarAmount(this TL.StarsAmountBase sab) => sab is TL.StarsAmount sa
		? new StarAmount { Amount = (int)sa.amount, NanostarAmount = sa.nanos }
		: throw new WTException($"Unsupported {sab}");
	internal static string Currency(this TL.StarsAmountBase sab) => sab switch
	{
		TL.StarsAmount => "XTR",
		TL.StarsTonAmount => "TON",
		_ => throw new WTException("Unknown StarsAmountBase currency")
	};

	internal static TL.StarsAmountBase? StarAmount(this SuggestedPostPrice spp) => spp.Currency switch
	{
		"XTR" => new TL.StarsAmount { amount = spp.Amount },
		"TON" => new TL.StarsTonAmount { amount = spp.Amount }, // must be a multiple of 10000000
		_ => throw new WTException("Invalid SuggestedPostPrice currency")
	};
	[return: NotNullIfNotNull(nameof(sab))]
	internal static SuggestedPostPrice? SuggestedPostPrice(this TL.StarsAmountBase? sab) => sab is null ? null :
		new SuggestedPostPrice() { Currency = sab.Currency(), Amount = sab.Amount };

	internal static long ToChatId(this Peer peer)
		=> peer switch { PeerChat pc => -pc.chat_id, PeerChannel pch => ZERO_CHANNEL_ID - pch.channel_id, _ => peer.ID };

	internal static BusinessBotRights BusinessBotRights(this TL.BusinessBotRights rights) => new()
	{
		CanReply = rights.flags.HasFlag(TL.BusinessBotRights.Flags.reply),
		CanReadMessages = rights.flags.HasFlag(TL.BusinessBotRights.Flags.read_messages),
		CanDeleteSentMessages = rights.flags.HasFlag(TL.BusinessBotRights.Flags.delete_sent_messages),
		CanDeleteAllMessages = rights.flags.HasFlag(TL.BusinessBotRights.Flags.delete_received_messages),
		CanEditName = rights.flags.HasFlag(TL.BusinessBotRights.Flags.edit_name),
		CanEditBio = rights.flags.HasFlag(TL.BusinessBotRights.Flags.edit_bio),
		CanEditProfilePhoto = rights.flags.HasFlag(TL.BusinessBotRights.Flags.edit_profile_photo),
		CanEditUsername = rights.flags.HasFlag(TL.BusinessBotRights.Flags.edit_username),
		CanChangeGiftSettings = rights.flags.HasFlag(TL.BusinessBotRights.Flags.change_gift_settings),
		CanViewGiftsAndStars = rights.flags.HasFlag(TL.BusinessBotRights.Flags.view_gifts),
		CanConvertGiftsToStars = rights.flags.HasFlag(TL.BusinessBotRights.Flags.sell_gifts),
		CanTransferAndUpgradeGifts = rights.flags.HasFlag(TL.BusinessBotRights.Flags.transfer_and_upgrade_gifts),
		CanTransferStars = rights.flags.HasFlag(TL.BusinessBotRights.Flags.transfer_stars),
		CanManageStories = rights.flags.HasFlag(TL.BusinessBotRights.Flags.manage_stories),
	};

	internal static AcceptedGiftTypes AcceptedGiftTypes(this DisallowedGiftsSettings.Flags flags) => new()
	{
		UnlimitedGifts = !flags.HasFlag(DisallowedGiftsSettings.Flags.disallow_unlimited_stargifts),
		LimitedGifts = !flags.HasFlag(DisallowedGiftsSettings.Flags.disallow_limited_stargifts),
		UniqueGifts = !flags.HasFlag(DisallowedGiftsSettings.Flags.disallow_unique_stargifts),
		PremiumSubscription = !flags.HasFlag(DisallowedGiftsSettings.Flags.disallow_premium_gifts),
		GiftsFromChannels = !flags.HasFlag(DisallowedGiftsSettings.Flags.disallow_stargifts_from_channels),
	};

	internal static MediaArea MediaArea(this StoryArea area)
	{
		return area.Type switch
		{
			StoryAreaTypeLocation satl => new MediaAreaGeoPoint
			{
				coordinates = area.Position.Coordinates(),
				geo = new() { lat = satl.Latitude, lon = satl.Longitude },
				address = satl.Address.GeoPointAddress(),
				flags = satl.Address != null ? MediaAreaGeoPoint.Flags.has_address : 0
			},
			StoryAreaTypeSuggestedReaction satsr => new MediaAreaSuggestedReaction
			{
				coordinates = area.Position.Coordinates(),
				reaction = satsr.ReactionType.Reaction(),
				flags = (satsr.IsDark ? MediaAreaSuggestedReaction.Flags.dark : 0) |
						(satsr.IsFlipped ? MediaAreaSuggestedReaction.Flags.flipped : 0)
			},
			StoryAreaTypeLink satl => new MediaAreaUrl
			{
				coordinates = area.Position.Coordinates(),
				url = satl.Url
			},
			StoryAreaTypeWeather satw => new MediaAreaWeather
			{
				coordinates = area.Position.Coordinates(),
				emoji = satw.Emoji,
				temperature_c = satw.Temperature,
				color = satw.BackgroundColor
			},
			StoryAreaTypeUniqueGift satug => new MediaAreaStarGift
			{
				coordinates = area.Position.Coordinates(),
				slug = satug.Name
			},
			_ => throw new WTException("Unsupported " + area.Type),
		};
	}

	internal static MediaAreaCoordinates Coordinates(this StoryAreaPosition pos) => new()
	{
		x = pos.XPercentage,
		y = pos.YPercentage,
		w = pos.WidthPercentage,
		h = pos.HeightPercentage,
		rotation = pos.RotationAngle,
		radius = pos.CornerRadiusPercentage,
		flags = pos.CornerRadiusPercentage > 0 ? MediaAreaCoordinates.Flags.has_radius : 0
	};

	internal static GeoPointAddress? GeoPointAddress(this LocationAddress? addr) => addr == null ? null : new()
	{
		country_iso2 = addr.CountryCode,
		state = addr.State,
		city = addr.City,
		street = addr.Street,
		flags = (addr.State != null ? TL.GeoPointAddress.Flags.has_state : 0) |
				(addr.City != null ? TL.GeoPointAddress.Flags.has_city : 0) |
				(addr.Street != null ? TL.GeoPointAddress.Flags.has_street : 0)
	};

	internal static TL.SuggestedPost? SuggestedPost(this SuggestedPostParameters? spp) => spp == null ? null : new()
	{
		schedule_date = spp.SendDate ?? default,
		price = spp.Price?.StarAmount(),
		flags = (spp.SendDate != null ? TL.SuggestedPost.Flags.has_schedule_date : 0) |
				(spp.Price != null ? TL.SuggestedPost.Flags.has_price : 0)
	};

	internal static SuggestedPostInfo? SuggestedPostInfo(this TL.SuggestedPost? sp) => sp == null ? null : new()
	{
		SendDate = sp.schedule_date,
		Price = sp.price.SuggestedPostPrice(),
		State = sp.flags.HasFlag(TL.SuggestedPost.Flags.accepted) ? "approved" :
				sp.flags.HasFlag(TL.SuggestedPost.Flags.rejected) ? "declined" : "pending"
	};

	internal static string Origin(this MessageActionStarGiftUnique masgu)
	{
		if (masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.from_offer)) return "offer";
		if (masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.assigned)) return "blockchain";
		if (masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.prepaid_upgrade)) return "gifted_upgrade";
		if (masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.has_resale_amount)) return "resale";
		if (masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.upgrade)) return "upgrade";
		return "transfer";
	}

	internal static GiftBackground GiftBackground(this TL.StarGiftBackground bg)
		=> new() { CenterColor = bg.center_color, EdgeColor = bg.edge_color, TextColor = bg.text_color };

	internal static UniqueGiftColors UniqueGiftColors(this PeerColorCollectible pcc) => new()
	{
		ModelCustomEmojiId = pcc.gift_emoji_id.ToString(),
		SymbolCustomEmojiId = pcc.background_emoji_id.ToString(),
		LightThemeMainColor = pcc.accent_color,
		LightThemeOtherColors = pcc.colors,
		DarkThemeMainColor = pcc.flags.HasFlag(PeerColorCollectible.Flags.has_dark_accent_color) ? pcc.dark_accent_color : pcc.accent_color,
		DarkThemeOtherColors = pcc.flags.HasFlag(PeerColorCollectible.Flags.has_dark_colors) ? pcc.dark_colors : pcc.colors,
	};
}
