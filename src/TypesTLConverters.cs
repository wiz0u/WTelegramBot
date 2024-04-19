using System.Diagnostics.CodeAnalysis;
using System.Text;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TL;

namespace Telegram.Bot.Types;

/// <summary>Extension methods for converting between Client API and Bot API</summary>
public static class TypesTLConverters
{
	const long ZERO_CHANNEL_ID = -1000000000000;

	/// <summary>
	/// Optional. For <see cref="MessageEntityTextUrl"/> only, url that will be opened after user taps on the text
	/// </summary>
	public static string? Url(this MessageEntity entity)
		=> (entity as MessageEntityTextUrl)?.url;

	/// <summary>
	/// Optional. For <see cref="MessageEntityMentionName"/> only, the mentioned user id
	/// </summary>
	/// <remarks>Use <see cref="TelegramBotClient.User(long)"/> to obtain user details from this id</remarks>
	public static long? UserId(this MessageEntity entity)
		=> (entity as MessageEntityMentionName)?.user_id ?? (entity as InputMessageEntityMentionName)?.user_id.UserId;

	/// <summary>
	/// Optional. For <see cref="MessageEntityType.TextMention"/> only, the mentioned user
	/// </summary>
	public static User? User(this MessageEntity entity, TelegramBotClient botClient)
		=> entity.UserId() is long userId ? botClient.User(userId) ?? new User { Id = userId, FirstName = "" } : null;

	/// <summary>
	/// Optional. For <see cref="MessageEntityType.Pre"/> only, the programming language of the entity text
	/// </summary>
	public static string? Language(this MessageEntity entity)
		=> (entity as MessageEntityPre)?.language;

	/// <summary>
	/// Optional. For <see cref="MessageEntityType.CustomEmoji"/> only, unique identifier of the custom emoji.
	/// Use <see cref="Requests.GetCustomEmojiStickersRequest"/> to get full information about the sticker
	/// </summary>
	public static string? CustomEmojiId(this MessageEntity entity)
		=> (entity as MessageEntityCustomEmoji)?.document_id.ToString();

	/// <summary>Convert TL.User to Bot Types.User</summary>
	[return: NotNullIfNotNull(nameof(user))]
	public static User? User(this TL.User? user)
	{
		if (user == null) return null;
		var result = new User
		{
			Id = user.id,
			IsBot = user.IsBot,
			FirstName = user.first_name,
			LastName = user.last_name,
			Username = user.MainUsername,
			LanguageCode = user.lang_code,
			IsPremium = user.flags.HasFlag(TL.User.Flags.premium),
			AddedToAttachmentMenu = user.flags.HasFlag(TL.User.Flags.attach_menu_enabled),
			AccessHash = user.access_hash
		};
		if (user.IsBot)
		{
			result.CanJoinGroups = !user.flags.HasFlag(TL.User.Flags.bot_nochats);
			result.CanReadAllGroupMessages = user.flags.HasFlag(TL.User.Flags.bot_chat_history);
			result.SupportsInlineQueries = user.flags.HasFlag(TL.User.Flags.has_bot_inline_placeholder);
			result.CanConnectToBusiness = user.flags2.HasFlag(TL.User.Flags2.bot_business);
		}
		return result;
	}

	/// <summary>Convert TL.Chat to Bot Types.Chat</summary>
	[return: NotNullIfNotNull(nameof(chat))]
	public static Chat? Chat(this ChatBase? chat)
	{
		var channel = chat as Channel;
		return chat == null ? null : new Chat
		{
			Id = (channel == null ? 0 : ZERO_CHANNEL_ID) - chat.ID,
			Type = channel == null ? ChatType.Group : channel.IsChannel ? ChatType.Channel : ChatType.Supergroup,
			Title = chat.Title,
			Username = channel?.MainUsername,
			IsForum = channel?.flags.HasFlag(Channel.Flags.forum),
			AccessHash = channel?.access_hash ?? 0
		};
	}

	/// <summary>Convert TL.User to Bot Types.Chat</summary>
	[return: NotNullIfNotNull(nameof(user))]
	public static Chat? Chat(this TL.User? user) => user == null ? null : new Chat
	{
		Id = user.id,
		Type = ChatType.Private,
		Username = user.MainUsername,
		FirstName = user.first_name,
		LastName = user.last_name,
		AccessHash = user.access_hash
	};

	/// <summary>Convert Bot Types.User to Bot Types.Chat</summary>
	[return: NotNullIfNotNull(nameof(user))]
	public static Chat? Chat(this User? user) => user == null ? null : new Chat
	{
		Id = user.Id,
		Type = ChatType.Private,
		Username = user.Username,
		FirstName = user.FirstName,
		LastName = user.LastName,
		AccessHash = user.AccessHash
	};


	internal static ChatMember ChatMember(this ChatParticipantBase? participant, User user)
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
				//CanPostStories, CanEditStories, CanDeleteStories: set only for channels
				IsAnonymous = false,
			},
			ChatParticipant => new ChatMemberMember { User = user },
			_ => new ChatMemberLeft { User = user }
		};

	internal static ChatMember ChatMember(this ChannelParticipantBase? participant, User user)
		=> participant switch
		{
			ChannelParticipantSelf or ChannelParticipant => new ChatMemberMember { User = user },
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

	internal static void LegacyMode(this ChatPermissions permissions)
	{
		if (permissions.CanSendPolls == true) permissions.CanSendMessages = true;
		if (permissions.CanSendOtherMessages == true || permissions.CanAddWebPagePreviews == true)
			permissions.CanSendAudios = permissions.CanSendDocuments = permissions.CanSendPhotos = permissions.CanSendVideos =
				permissions.CanSendVideoNotes = permissions.CanSendVoiceNotes = permissions.CanSendMessages = true;
	}

	internal static ChatBannedRights ToChatBannedRights(this ChatPermissions permissions, DateTime? untilDate = default) => new()
	{
		until_date = untilDate ?? default,
		flags = (permissions.CanSendMessages == true ? 0 : ChatBannedRights.Flags.send_messages)
			| (permissions.CanSendAudios == true ? 0 : ChatBannedRights.Flags.send_audios)
			| (permissions.CanSendDocuments == true ? 0 : ChatBannedRights.Flags.send_docs)
			| (permissions.CanSendPhotos == true ? 0 : ChatBannedRights.Flags.send_photos)
			| (permissions.CanSendVideos == true ? 0 : ChatBannedRights.Flags.send_videos)
			| (permissions.CanSendVideoNotes == true ? 0 : ChatBannedRights.Flags.send_roundvideos)
			| (permissions.CanSendVoiceNotes == true ? 0 : ChatBannedRights.Flags.send_voices)
			| (permissions.CanSendPolls == true ? 0 : ChatBannedRights.Flags.send_polls)
			| (permissions.CanSendOtherMessages == true ? 0 : ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games | ChatBannedRights.Flags.send_inline)
			| (permissions.CanAddWebPagePreviews == true ? 0 : ChatBannedRights.Flags.embed_links)
			| (permissions.CanChangeInfo == true ? 0 : ChatBannedRights.Flags.change_info)
			| (permissions.CanInviteUsers == true ? 0 : ChatBannedRights.Flags.invite_users)
			| (permissions.CanPinMessages == true ? 0 : ChatBannedRights.Flags.pin_messages)
			| (permissions.CanManageTopics == true ? 0 : ChatBannedRights.Flags.manage_topics)
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

	internal static InlineKeyboardMarkup? InlineKeyboardMarkup(this ReplyMarkup? reply_markup) => reply_markup is not ReplyInlineMarkup rim ? null :
		new InlineKeyboardMarkup(rim.rows.Select(row => row.buttons.Select(btn => btn switch
		{
			KeyboardButtonUrl kbu => InlineKeyboardButton.WithUrl(kbu.text, kbu.url),
			KeyboardButtonCallback kbc => InlineKeyboardButton.WithCallbackData(kbc.text, Encoding.UTF8.GetString(kbc.data)),
			KeyboardButtonGame kbg => InlineKeyboardButton.WithCallBackGame(kbg.text),
			KeyboardButtonBuy kbb => InlineKeyboardButton.WithPayment(kbb.text),
			KeyboardButtonSwitchInline kbsi => kbsi.flags.HasFlag(KeyboardButtonSwitchInline.Flags.same_peer) ?
				InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(kbsi.text, kbsi.query) :
				InlineKeyboardButton.WithSwitchInlineQuery(kbsi.text, kbsi.query),
			KeyboardButtonUrlAuth kbua => InlineKeyboardButton.WithLoginUrl(kbua.text, new LoginUrl
			{
				Url = kbua.url,
				ForwardText = kbua.fwd_text,
			}),
			KeyboardButtonWebView kbwv => InlineKeyboardButton.WithWebApp(kbwv.text, new WebAppInfo { Url = kbwv.url }),
			_ => new InlineKeyboardButton(btn.Text),
		})));

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
			writer.Write((int)(file.FileSize ?? 0));
			writer.Write((byte)42);
		}
		var bytes = memStream.ToArray();
		file.FileId = ToBase64(bytes);
		bytes[12] = (byte)(type?[0] ?? 0);
		file.FileUniqueId = ToBase64(bytes.AsSpan(3, 10));
		return file;
	}

	/// <summary>Decode FileId into TL.InputFileLocation</summary>
	public static (File file, InputFileLocationBase location, int dc_id) ParseFileId(this string fileId, bool generateFile = false)
	{
		var idBytes = Convert.FromBase64String(fileId.Replace('_', '/').Replace('-', '+') + new string('=', (2147483644 - fileId.Length) % 4));
		if (idBytes[^1] != 42) throw new ApiRequestException("Unsupported file_id format");
		using var memStream = new MemoryStream(idBytes);
		using var reader = new BinaryReader(memStream);
		var location = (InputFileLocationBase)reader.ReadTLObject();
		byte dc_id = reader.ReadByte();
		int fileSize = reader.ReadInt32();
		if (!generateFile) return (null!, location, dc_id);

		idBytes[12] = idBytes[^8]; // patch byte following id with InputPhotoFileLocation.thumb_size
		var fileUniqueId = ToBase64(idBytes.AsSpan(3, 10));
		var filename = location.GetType().Name;
		if (filename.StartsWith("Input")) filename = filename[5..];
		if (filename.EndsWith("FileLocation")) filename = filename[..^12];
		filename = $"{filename}_{fileUniqueId}";
		if (filename.Contains("Photo")) filename += ".jpg";
		return (new File
		{
			FilePath = $"{fileId}/{filename}",
			FileId = fileId,
			FileUniqueId = ToBase64(idBytes.AsSpan(3, 10)),
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
		writer.Write(big.FileSize);
		writer.Write((byte)42);
		var bytes = memStream.ToArray();
		var chatPhoto = new ChatPhoto()
		{ BigFileId = ToBase64(bytes) };
		bytes[12] = (byte)big.Type[0]; // patch byte following id with thumb_size
		chatPhoto.BigFileUniqueId = ToBase64(bytes.AsSpan(3, 10));
		memStream.SetLength(0);
		writer.WriteTLObject(photo.ToFileLocation(small));
		writer.Write((byte)photo.dc_id);
		writer.Write(small.FileSize);
		writer.Write((byte)42);
		bytes = memStream.ToArray();
		chatPhoto.SmallFileId = ToBase64(bytes);
		bytes[12] = (byte)small.Type[0]; // patch byte following id with thumb_size
		chatPhoto.SmallFileUniqueId = ToBase64(bytes.AsSpan(3, 10));
		return chatPhoto;
	}

	internal static string? InlineMessageId(this InputBotInlineMessageIDBase? msg_id)
	{
		if (msg_id == null) return "";
		using var memStream = new MemoryStream(128);
		using (var writer = new BinaryWriter(memStream))
			msg_id.WriteTL(writer);
		var bytes = memStream.ToArray();
		return ToBase64(bytes);
	}

	internal static InputBotInlineMessageIDBase ParseInlineMsgID(this string inlineMessageId)
	{
		var idBytes = Convert.FromBase64String(inlineMessageId.Replace('_', '/').Replace('-', '+') + new string('=', (2147483644 - inlineMessageId.Length) % 4));
		using var memStream = new MemoryStream(idBytes);
		using var reader = new BinaryReader(memStream);
		return (InputBotInlineMessageIDBase)reader.ReadTLObject();
	}

	private static string ToBase64(this byte[] bytes) => ToBase64(bytes.AsSpan());
	private static string ToBase64(this Span<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
			_ => throw new ApiRequestException("MenuButton has unsupported type")
		};

	internal static MenuButton MenuButton(this BotMenuButtonBase? menuButton)
		=> menuButton switch
		{
			null => new MenuButtonDefault(),
			BotMenuButtonCommands => new MenuButtonCommands(),
			BotMenuButton bmb => new MenuButtonWebApp { Text = bmb.text, WebApp = new WebAppInfo { Url = bmb.url } },
			_ => throw new ApiRequestException("Unrecognized BotMenuButtonBase")
		};

	internal static ChatAdminRights ChatAdminRights(this ChatAdministratorRights? rights)
		=> rights == null ? new() : new()
		{
			flags = (rights.IsAnonymous == true ? TL.ChatAdminRights.Flags.anonymous : 0)
			| (rights.CanManageChat == true ? TL.ChatAdminRights.Flags.other : 0)
			| (rights.CanDeleteMessages == true ? TL.ChatAdminRights.Flags.delete_messages : 0)
			| (rights.CanManageVideoChats == true ? TL.ChatAdminRights.Flags.manage_call : 0)
			| (rights.CanRestrictMembers == true ? TL.ChatAdminRights.Flags.ban_users : 0)
			| (rights.CanPromoteMembers == true ? TL.ChatAdminRights.Flags.add_admins : 0)
			| (rights.CanChangeInfo == true ? TL.ChatAdminRights.Flags.change_info : 0)
			| (rights.CanInviteUsers == true ? TL.ChatAdminRights.Flags.invite_users : 0)
			| (rights.CanPostMessages == true ? TL.ChatAdminRights.Flags.post_messages : 0)
			| (rights.CanEditMessages == true ? TL.ChatAdminRights.Flags.edit_messages : 0)
			| (rights.CanPinMessages == true ? TL.ChatAdminRights.Flags.pin_messages : 0)
			| (rights.CanManageTopics == true ? TL.ChatAdminRights.Flags.manage_topics : 0)
			| (rights.CanPostStories == true ? TL.ChatAdminRights.Flags.post_stories : 0)
			| (rights.CanEditStories == true ? TL.ChatAdminRights.Flags.edit_stories : 0)
			| (rights.CanDeleteStories == true ? TL.ChatAdminRights.Flags.delete_stories : 0)
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
		};

	[return: NotNullIfNotNull(nameof(maskPosition))]
	internal static MaskCoords? MaskCoord(this MaskPosition? maskPosition)
		=> maskPosition == null ? null : new MaskCoords
		{ n = (int)maskPosition.Point - 1, x = maskPosition.XShift, y = maskPosition.YShift, zoom = maskPosition.Scale };

	internal static TL.LabeledPrice[] LabeledPrices(this IEnumerable<Payments.LabeledPrice> prices)
		=> prices.Select(p => new TL.LabeledPrice { label = p.Label, amount = p.Amount }).ToArray();

	internal static TL.BotCommand BotCommand(this BotCommand bc)
		=> new() { command = bc.Command.StartsWith('/') ? bc.Command[1..] : bc.Command, description = bc.Description };
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
	{
		var result = new List<InlineQueryPeerType>();
		if (swiqcc.AllowUserChats == true) result.Add(InlineQueryPeerType.PM);
		if (swiqcc.AllowBotChats == true) result.Add(InlineQueryPeerType.BotPM);
		if (swiqcc.AllowGroupChats == true) { result.Add(InlineQueryPeerType.Chat); result.Add(InlineQueryPeerType.Megagroup); }
		if (swiqcc.AllowChannelChats == true) result.Add(InlineQueryPeerType.Broadcast);
		return [.. result];
	}

	internal static Passport.PassportData PassportData(this MessageActionSecureValuesSentMe masvsm) => new()
	{
		Data = masvsm.values.Select(EncryptedPassportElement).ToArray(),
		Credentials = new Passport.EncryptedCredentials { Data = masvsm.credentials.data.ToBase64(), Hash = masvsm.credentials.hash.ToBase64(), Secret = masvsm.credentials.secret.ToBase64() }
	};

	internal static Passport.EncryptedPassportElement EncryptedPassportElement(this SecureValue sv) => new()
	{
		Type = sv.type switch
		{
			SecureValueType.PersonalDetails => Passport.EncryptedPassportElementType.PersonalDetails,
			SecureValueType.Passport => Passport.EncryptedPassportElementType.Passport,
			SecureValueType.DriverLicense => Passport.EncryptedPassportElementType.DriverLicence,
			SecureValueType.IdentityCard => Passport.EncryptedPassportElementType.IdentityCard,
			SecureValueType.InternalPassport => Passport.EncryptedPassportElementType.InternalPassport,
			SecureValueType.Address => Passport.EncryptedPassportElementType.Address,
			SecureValueType.UtilityBill => Passport.EncryptedPassportElementType.UtilityBill,
			SecureValueType.BankStatement => Passport.EncryptedPassportElementType.BankStatement,
			SecureValueType.RentalAgreement => Passport.EncryptedPassportElementType.RentalAgreement,
			SecureValueType.PassportRegistration => Passport.EncryptedPassportElementType.PassportRegistration,
			SecureValueType.TemporaryRegistration => Passport.EncryptedPassportElementType.TemporaryRegistration,
			SecureValueType.Phone => Passport.EncryptedPassportElementType.PhoneNumber,
			SecureValueType.Email => Passport.EncryptedPassportElementType.Email,
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

	private static Passport.PassportFile PassportFile(this SecureFile file)
		=> new Passport.PassportFile { FileSize = file.size, FileDate = file.date }.SetFileIds(file.ToFileLocation(), file.dc_id);

	internal static SharedUser ToSharedUser(this RequestedPeer peer) => peer is not RequestedPeerUser rpu ? null! :
		new SharedUser { UserId = rpu.user_id, FirstName = rpu.first_name, LastName = rpu.last_name, Username = rpu.username, Photo = rpu.photo.PhotoSizes() };

	internal static ChatShared? ToSharedChat(this RequestedPeer peer, int requestId) => peer switch
	{
		RequestedPeerChat rpc => new ChatShared { RequestId = requestId, ChatId = rpc.chat_id, Title = rpc.title, Photo = rpc.photo.PhotoSizes() },
		RequestedPeerChannel rpch => new ChatShared { RequestId = requestId, ChatId = rpch.channel_id, Title = rpch.title, Username = rpch.username, Photo = rpch.photo.PhotoSizes() },
		_ => null
	};

	internal static Reaction Reaction(this ReactionType reaction) => reaction switch
	{
		ReactionTypeEmoji rte => new ReactionEmoji { emoticon = rte.Emoji },
		ReactionTypeCustomEmoji rtce => new ReactionCustomEmoji { document_id = long.Parse(rtce.CustomEmojiId) },
		_ => throw new ApiRequestException("Unrecognized ReactionType")
	};
	internal static ReactionType ReactionType(this Reaction reaction) => reaction switch
	{
		ReactionEmoji rte => new ReactionTypeEmoji { Emoji = rte.emoticon },
		ReactionCustomEmoji rce => new ReactionTypeCustomEmoji { CustomEmojiId = rce.document_id.ToString() },
		_ => throw new ApiRequestException("Unrecognized Reaction")
	};

	internal static InputMediaWebPage? InputMediaWebPage(this LinkPreviewOptions? lpo) => lpo?.Url == null ? null : new InputMediaWebPage
	{
		url = lpo.Url,
		flags = TL.InputMediaWebPage.Flags.optional
			| (lpo.PreferLargeMedia == true ? TL.InputMediaWebPage.Flags.force_large_media : 0)
			| (lpo.PreferSmallMedia == true ? TL.InputMediaWebPage.Flags.force_small_media : 0)
	};

	internal static LinkPreviewOptions? LinkPreviewOptions(this MessageMedia? mm, bool invert_media)
		=> mm == null ? new LinkPreviewOptions { IsDisabled = true } : mm is not MessageMediaWebPage mmwp ? null : new LinkPreviewOptions
		{
			Url = mmwp.webpage.Url,
			PreferLargeMedia = mmwp.flags.HasFlag(MessageMediaWebPage.Flags.force_large_media),
			PreferSmallMedia = mmwp.flags.HasFlag(MessageMediaWebPage.Flags.force_small_media),
			ShowAboveText = invert_media
		};

	internal static Birthday? Birthday(this TL.Birthday? birthday) => birthday == null ? null : new Birthday
	{
		Day = birthday.day,
		Month = birthday.month,
		Year = birthday.flags.HasFlag(TL.Birthday.Flags.has_year) ? birthday.year : null
	};

	internal static BusinessLocation? BusinessLocation(this TL.BusinessLocation? loc) => loc == null ? null : new BusinessLocation
	{
		Address = loc.address,
		Location = loc.geo_point.Location()
	};

	internal static BusinessOpeningHours? BusinessOpeningHours(this TL.BusinessWorkHours? hours) => hours == null ? null : new BusinessOpeningHours
	{
		TimeZoneName = hours.timezone_id,
		OpeningHours = hours.weekly_open.Select(wo =>
			new BusinessOpeningHoursInterval { OpeningMinute = wo.start_minute, ClosingMinute = wo.end_minute}).ToArray()
	};

}