using System.Diagnostics.CodeAnalysis;
using System.Text;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TL;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Exceptions;

namespace Telegram.Bot.Types;

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
	/// Optional. For <see cref="MessageEntityMentionName"/> only, the mentioned user
	/// </summary>
	public static User? User(this MessageEntity entity, TelegramBotClient botClient)
		=> entity.UserId() is long userId ? botClient.FindUser(userId) ?? new User { Id = userId, FirstName = "" } : null;

	/// <summary>
	/// Optional. For <see cref="MessageEntityPre"/> only, the programming language of the entity text
	/// </summary>
	public static string? Language(this MessageEntity entity)
		=> (entity as MessageEntityPre)?.language;

	[return: NotNullIfNotNull(nameof(user))]
	public static User? User(this TL.User? user)
	{
		if (user == null) return null;
		var result = new User
		{
			Id = user.id,
			IsBot = user.IsBot, //IsPremium = user.flags.HasFlag(User.Flags.premium),
			FirstName = user.first_name,
			LastName = user.last_name,
			Username = user.MainUsername,
			LanguageCode = user.lang_code,
			//AddedToAttachmentMenu = user.flags.HasFlag(User.Flags.attach_menu_enabled),
			AccessHash = user.access_hash
		};
		if (user.IsBot)
		{
			result.CanJoinGroups = !user.flags.HasFlag(TL.User.Flags.bot_nochats);
			result.CanReadAllGroupMessages = user.flags.HasFlag(TL.User.Flags.bot_chat_history);
			result.SupportsInlineQueries = user.flags.HasFlag(TL.User.Flags.has_bot_inline_placeholder);
		}
		return result;
	}

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
			AccessHash = channel?.access_hash ?? 0
		};
	}

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


#pragma warning disable CS0618 // Type or member is obsolete
	internal static ChatMember ChatMember(this ChatParticipantBase? participant, User user)
		=> participant switch
		{
			ChatParticipantCreator => new ChatMemberOwner { User = user },
			ChatParticipantAdmin => new ChatMemberAdministrator
			{
				User = user,
				CanManageChat = true,
				CanChangeInfo = true,
				CanDeleteMessages = true,
				CanInviteUsers = true,
				CanRestrictMembers = true,
				CanPinMessages = true,
				CanManageVideoChats = true,
				CanManageVoiceChats = true
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
				IsAnonymous = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.anonymous),
				CanBeEdited = cpa.flags.HasFlag(ChannelParticipantAdmin.Flags.can_edit),
				CanManageChat = cpa.admin_rights.flags != 0,
				CanPostMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.post_messages),
				CanEditMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.edit_messages),
				CanDeleteMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.delete_messages),
				CanManageVoiceChats = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_call),
				CanManageVideoChats = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_call),
				CanRestrictMembers = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.ban_users),
				CanPromoteMembers = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.add_admins),
				CanChangeInfo = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.change_info),
				CanInviteUsers = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.invite_users),
				CanPinMessages = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.pin_messages),
				//CanManageTopics = cpa.admin_rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_topics),
			},
			ChannelParticipantBanned cpb =>
				cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.view_messages)
				? new ChatMemberBanned { User = user, UntilDate = UntilDate(cpb.banned_rights.until_date) }
				: new ChatMemberRestricted
				{
					User = user,
					UntilDate = UntilDate(cpb.banned_rights.until_date),
					CanChangeInfo = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.change_info),
					CanInviteUsers = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.invite_users),
					CanPinMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.pin_messages),
					CanSendMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_messages),
					CanSendMediaMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_media),
					CanSendPolls = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_polls),
					CanSendOtherMessages = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games),
					CanAddWebPagePreviews = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.embed_links),
					//CanManageTopics = !cpb.banned_rights.flags.HasFlag(ChatBannedRights.Flags.manage_topics),
				},
			_ /*ChannelParticipantLeft*/ => new ChatMemberLeft { User = user, },
		};
#pragma warning restore CS0618 // Type or member is obsolete

	private static DateTime? UntilDate(DateTime until_date) => until_date == DateTime.MaxValue ? null : until_date;

	[return: NotNullIfNotNull(nameof(banned_rights))]
	internal static ChatPermissions? ChatPermissions(this ChatBannedRights? banned_rights) => banned_rights == null ? null : new ChatPermissions
	{
		CanSendMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_messages),
		CanSendMediaMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_media),
		CanSendPolls = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_polls),
		CanSendOtherMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games),
		CanAddWebPagePreviews = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.embed_links),
		CanChangeInfo = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.change_info),
		CanInviteUsers = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.invite_users),
		CanPinMessages = !banned_rights.flags.HasFlag(ChatBannedRights.Flags.pin_messages)
	};

	internal static ChatBannedRights ToChatBannedRights(this ChatPermissions permissions) => new()
	{
		flags = (permissions.CanSendMessages == false ? ChatBannedRights.Flags.send_messages : 0)
			| (permissions.CanSendMediaMessages == false ? ChatBannedRights.Flags.send_media : 0)
			| (permissions.CanSendPolls == false ? ChatBannedRights.Flags.send_polls : 0)
			| (permissions.CanSendOtherMessages == false ? ChatBannedRights.Flags.send_stickers | ChatBannedRights.Flags.send_gifs | ChatBannedRights.Flags.send_games : 0)
			| (permissions.CanAddWebPagePreviews == false ? ChatBannedRights.Flags.embed_links : 0)
			| (permissions.CanChangeInfo == false ? ChatBannedRights.Flags.change_info : 0)
			| (permissions.CanInviteUsers == false ? ChatBannedRights.Flags.invite_users : 0)
			| (permissions.CanPinMessages == false ? ChatBannedRights.Flags.pin_messages : 0)
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

	public static PhotoSize[]? PhotoSizes(this PhotoBase photoBase)
		=> (photoBase is not Photo photo) ? null : photo.sizes.Select(ps => ps.PhotoSize(photo.ToFileLocation(ps), photo.dc_id)).ToArray();

	public static PhotoSize PhotoSize(this PhotoSizeBase ps, InputFileLocationBase location, int dc_id)
		=> new PhotoSize()
		{
			Width = ps.Width,
			Height = ps.Height,
			FileSize = ps.FileSize,
		}.SetFileIds(location, dc_id, ps.Type);

	public static (File file, InputFileLocationBase location, int dc_id) ParseFileId(this string fileId, bool generateFile = false)
	{
		var idBytes = Convert.FromBase64String(fileId.Replace('_', '/').Replace('-', '+') + new string('=', 3 - ((fileId.Length + 3) % 4)));
		if (idBytes[^1] != 42) throw new ApiRequestException("Unsupported FileID format");
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
		// fileType = 0 for web, or FileTypeClass+1 (see D:\Repos\telegram-bot-api\td\td\telegram\files\FileType.cpp)
		// fileId
		// 0 for Type 'a', 1 for Type 'c', or Type char + 5
		//byte type = Type switch { "a" => 0, "c" => 1, _ => (byte)(Type[0] + 5) };
		//Convert.ToBase64String(BitConverter.GetBytes(photoId).Prepend(fileType).Append(type).EncodeZero().ToArray());
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
		var idBytes = Convert.FromBase64String(inlineMessageId.Replace('_', '/').Replace('-', '+') + new string('=', 3 - ((inlineMessageId.Length + 3) % 4)));
		using var memStream = new MemoryStream(idBytes);
		using var reader = new BinaryReader(memStream);
		return (InputBotInlineMessageIDBase)reader.ReadTLObject();
	}

	private static string ToBase64(this Span<byte> bytes)
		=> Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
			| (rights.CanChangeInfo == true ? TL.ChatAdminRights.Flags.change_info : 0)
			| (rights.CanPostMessages == true ? TL.ChatAdminRights.Flags.post_messages : 0)
			| (rights.CanEditMessages == true ? TL.ChatAdminRights.Flags.edit_messages : 0)
			| (rights.CanDeleteMessages == true ? TL.ChatAdminRights.Flags.delete_messages : 0)
			| (rights.CanRestrictMembers == true ? TL.ChatAdminRights.Flags.ban_users : 0)
			| (rights.CanInviteUsers == true ? TL.ChatAdminRights.Flags.invite_users : 0)
			| (rights.CanPinMessages == true ? TL.ChatAdminRights.Flags.pin_messages : 0)
			| (rights.CanPromoteMembers == true ? TL.ChatAdminRights.Flags.add_admins : 0)
			| (rights.CanManageVideoChats == true ? TL.ChatAdminRights.Flags.manage_call : 0)
			| (rights.CanManageChat == true ? TL.ChatAdminRights.Flags.other : 0)
			//| (rights.CanManageVideoChats == true ? TL.ChatAdminRights.Flags.manage_topics : 0)
		};

	internal static ChatAdministratorRights ChatAdministratorRights(this ChatAdminRights? rights)
		=> rights == null ? new() : new()
		{
			CanManageChat = rights.flags != 0,
			CanPostMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.post_messages),
			CanEditMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.edit_messages),
			CanDeleteMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.delete_messages),
			CanManageVideoChats = rights.flags.HasFlag(TL.ChatAdminRights.Flags.manage_call),
			CanRestrictMembers = rights.flags.HasFlag(TL.ChatAdminRights.Flags.ban_users),
			CanPromoteMembers = rights.flags.HasFlag(TL.ChatAdminRights.Flags.add_admins),
			CanChangeInfo = rights.flags.HasFlag(TL.ChatAdminRights.Flags.change_info),
			CanInviteUsers = rights.flags.HasFlag(TL.ChatAdminRights.Flags.invite_users),
			CanPinMessages = rights.flags.HasFlag(TL.ChatAdminRights.Flags.pin_messages)
		};

	[return: NotNullIfNotNull(nameof(maskPosition))]
	internal static MaskCoords? MaskCoord(this MaskPosition? maskPosition)
		=> maskPosition == null ? null : new MaskCoords
		{ n = (int)maskPosition.Point - 1, x = maskPosition.XShift, y = maskPosition.YShift, zoom = maskPosition.Scale };

	internal static TL.LabeledPrice[] LabeledPrices(this IEnumerable<Payments.LabeledPrice> prices)
		=> prices.Select(p => new TL.LabeledPrice { label = p.Label, amount = p.Amount }).ToArray();

	public static TL.BotCommand BotCommand(this BotCommand bc)
		=> new() { command = bc.Command.StartsWith('/') ? bc.Command[1..] : bc.Command, description = bc.Description };
	public static BotCommand BotCommand(this TL.BotCommand bc)
		=> new() { Command = bc.command, Description = bc.description };

	[return: NotNullIfNotNull(nameof(pa))]
	internal static ShippingAddress? ShippingAddress(this PostAddress? pa) => pa == null ? null : new ShippingAddress
	{
		CountryCode = pa.country_iso2,
		State = pa.state,
		City = pa.city,
		StreetLine1 = pa.street_line1,
		StreetLine2 = pa.street_line2,
		PostCode = pa.post_code
	};

	[return: NotNullIfNotNull(nameof(pri))]
	internal static OrderInfo? OrderInfo(this PaymentRequestedInfo? pri) => pri == null ? null : new OrderInfo
	{
		Name = pri.name,
		PhoneNumber = pri.phone,
		Email = pri.email,
		ShippingAddress = pri.shipping_address.ShippingAddress()
	};
}
