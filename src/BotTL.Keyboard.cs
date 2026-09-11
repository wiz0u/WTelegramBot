using System.Text;
using TL;
using KeyboardButton = Telegram.Bot.Types.ReplyMarkups.KeyboardButton;
using ReplyKeyboardMarkup = Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup;

namespace WTelegram;

public partial class Bot
{
	private async Task<TL.ReplyMarkup?> MakeReplyMarkup(ReplyMarkup? replyMarkup) => replyMarkup switch
	{
		ReplyKeyboardRemove rkr => new ReplyKeyboardHide { flags = rkr.Selective ? ReplyKeyboardHide.Flags.selective : 0 },
		ForceReplyMarkup frm => new ReplyKeyboardForceReply
		{
			flags = (frm.Selective ? ReplyKeyboardForceReply.Flags.selective : 0) | (frm.InputFieldPlaceholder != null ? ReplyKeyboardForceReply.Flags.has_placeholder : 0),
			placeholder = frm.InputFieldPlaceholder
		},
		ReplyKeyboardMarkup rkm => new TL.ReplyKeyboardMarkup
		{
			flags = (rkm.Selective ? TL.ReplyKeyboardMarkup.Flags.selective : 0)
				| (rkm.IsPersistent ? TL.ReplyKeyboardMarkup.Flags.persistent : 0)
				| (rkm.ResizeKeyboard ? TL.ReplyKeyboardMarkup.Flags.resize : 0)
				| (rkm.OneTimeKeyboard ? TL.ReplyKeyboardMarkup.Flags.single_use : 0)
				| (rkm.InputFieldPlaceholder != null ? TL.ReplyKeyboardMarkup.Flags.has_placeholder : 0)
				| (rkm.ForceReply ? TL.ReplyKeyboardMarkup.Flags.force_reply : 0),
			placeholder = rkm.InputFieldPlaceholder,
			rows = [.. rkm.Keyboard.Select(row => new KeyboardButtonRow { buttons = [.. row.Select(MakeKeyboardButton)] })]
		},
		InlineKeyboardMarkup ikm => new ReplyInlineMarkup
		{
			flags = ikm.ForceReply ? TL.ReplyInlineMarkup.Flags.force_reply : 0,
			rows = [.. ikm.InlineKeyboard.Select(row => new KeyboardInlineButtonRow { buttons = [.. row.Select(MakeKeyboardButton)] })]
		} is { rows.Length: not 0 } rim ? rim : null,
		_ => null,
	};

	private static TL.KeyboardButton MakeKeyboardButton(KeyboardButton btn)
	{
		var style = btn.KeyboardButtonStyle();
		TL.ButtonType type = btn switch
		{
			{ RequestUsers: { } rus } => new InputButtonTypeRequestPeer
			{
				button_id = rus.RequestId,
				max_quantity = rus.MaxQuantity ?? 1,
				peer_type = new RequestPeerTypeUser
				{
					bot = rus.UserIsBot == true,
					premium = rus.UserIsPremium == true,
					flags = (rus.UserIsBot == null ? 0 : RequestPeerTypeUser.Flags.has_bot) | (rus.UserIsPremium == null ? 0 : RequestPeerTypeUser.Flags.has_premium)
				},
				flags = (rus.RequestName ? InputButtonTypeRequestPeer.Flags.name_requested : 0)
					| (rus.RequestUsername ? InputButtonTypeRequestPeer.Flags.username_requested : 0)
					| (rus.RequestPhoto ? InputButtonTypeRequestPeer.Flags.photo_requested : 0)
			},
			{ RequestChat: { } rc } => new InputButtonTypeRequestPeer
			{
				button_id = rc.RequestId,
				max_quantity = 1,
				peer_type = MakeRequestPeerType(rc),
				flags = (rc.RequestTitle ? InputButtonTypeRequestPeer.Flags.name_requested : 0)
					| (rc.RequestUsername ? InputButtonTypeRequestPeer.Flags.username_requested : 0)
					| (rc.RequestPhoto ? InputButtonTypeRequestPeer.Flags.photo_requested : 0)
			},
			{ RequestContact: true } => new ButtonTypeRequestPhone(),
			{ RequestLocation: true } => new ButtonTypeRequestGeoLocation(),
			{ RequestPoll: { } } => new ButtonTypeRequestPoll
			{
				quiz = btn.RequestPoll.Type == PollType.Quiz,
				flags = (btn.RequestPoll.Type.HasValue ? ButtonTypeRequestPoll.Flags.has_quiz : 0)
			},
			{ WebApp: { } } => new ButtonTypeSimpleWebView { url = btn.WebApp.Url },
			{ RequestManagedBot: { } rmb } => new InputButtonTypeRequestPeer
			{
				button_id = rmb.RequestId,
				max_quantity = 1,
				peer_type = new RequestPeerTypeCreateBot
				{
					suggested_name = rmb.SuggestedName,
					suggested_username = rmb.SuggestedUsername,
					flags = RequestPeerTypeCreateBot.Flags.bot_managed
						| (rmb.SuggestedName != null ? RequestPeerTypeCreateBot.Flags.has_suggested_name : 0)
						| (rmb.SuggestedUsername != null ? RequestPeerTypeCreateBot.Flags.has_suggested_username : 0),
				}
			},
			_ => throw new NotImplementedException($"Unsupported button type: {btn?.GetType().Name}"),
		};
		return new TL.KeyboardButton
		{
			flags = style == null ? 0 : TL.KeyboardButton.Flags.has_style,
			text = btn.Text,
			style = style,
			type = type,
		};
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
				flags = (rc.ChatIsForum != null ? RequestPeerTypeChat.Flags.has_forum : 0) | (rc.BotIsMember ? RequestPeerTypeChat.Flags.bot_participant : 0)
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
				| (rc.ChatIsCreated ? RequestPeerTypeChat.Flags.creator : 0);
			flags = (F)(object)(flags.ToUInt32(null) | (uint)more_flags);
			return obj;
		}
	}

	private TL.KeyboardInlineButton MakeKeyboardButton(InlineKeyboardButton btn)
	{
		var style = btn.KeyboardButtonStyle();
		TL.InlineButtonType type = btn switch
		{
			{ Url: { } } => btn.Url.StartsWith("tg://user?id=", StringComparison.OrdinalIgnoreCase) && long.TryParse(btn.Url[13..], out var userId)
				? new InputInlineButtonTypeUserProfile { user_id = InputUser(userId) }
				: new InlineButtonTypeUrl { url = btn.Url },
			{ CallbackData: { } } => new InlineButtonTypeCallback { data = Encoding.UTF8.GetBytes(btn.CallbackData) },
			{ CallbackGame: { } } => new InlineButtonTypeGame { },
			{ Pay: true } => new InlineButtonTypeBuy { },
			{ SwitchInlineQuery: { } } => new InlineButtonTypeSwitchInline { query = btn.SwitchInlineQuery },
			{ SwitchInlineQueryCurrentChat: { } } => new InlineButtonTypeSwitchInline
			{
				query = btn.SwitchInlineQueryCurrentChat,
				flags = InlineButtonTypeSwitchInline.Flags.same_peer
			},
			{ SwitchInlineQueryChosenChat: { } siqcc } => new InlineButtonTypeSwitchInline
			{
				query = siqcc.Query,
				peer_types = siqcc.InlineQueryPeerTypes(),
				flags = InlineButtonTypeSwitchInline.Flags.has_peer_types
			},
			{ CopyText: { } } => new InlineButtonTypeCopy { copy_text = btn.CopyText.Text },
			{ LoginUrl: { } lu } => new InputInlineButtonTypeUrlAuth
			{
				url = lu.Url,
				fwd_text = lu.ForwardText,
				bot = lu.BotUsername != null ? InputUser(lu.BotUsername) : Client.User,
				flags = (lu.ForwardText != null ? InputInlineButtonTypeUrlAuth.Flags.has_fwd_text : 0)
					| (lu.RequestWriteAccess ? InputInlineButtonTypeUrlAuth.Flags.request_write_access : 0),
			},
			{ WebApp: { } } => new InlineButtonTypeWebView { url = btn.WebApp.Url },
			{ Disabled: { } } => new InlineButtonTypeDisabled { },
			_ => throw new NotImplementedException($"Unsupported button type: {btn?.GetType().Name}"),
		};
		return new TL.KeyboardInlineButton
		{
			flags = style == null ? 0 : TL.KeyboardInlineButton.Flags.has_style,
			text = btn.Text,
			style = style,
			type = type,
		};
	}

	private TL.InlineButtonType MakeButtonType(RichMessageButton btn) => btn switch
	{
		{ Url: { } } => btn.Url.StartsWith("tg://user?id=", StringComparison.OrdinalIgnoreCase) && long.TryParse(btn.Url[13..], out var userId)
			? new InputInlineButtonTypeUserProfile { user_id = InputUser(userId) }
			: new InlineButtonTypeUrl { url = btn.Url },
		{ CallbackData: { } } => new InlineButtonTypeCallback { data = Encoding.UTF8.GetBytes(btn.CallbackData) },
		{ SwitchInlineQuery: { } } => new InlineButtonTypeSwitchInline { query = btn.SwitchInlineQuery },
		{ SwitchInlineQueryCurrentChat: { } } => new InlineButtonTypeSwitchInline
		{
			query = btn.SwitchInlineQueryCurrentChat,
			flags = InlineButtonTypeSwitchInline.Flags.same_peer
		},
		{ SwitchInlineQueryChosenChat: { } siqcc } => new InlineButtonTypeSwitchInline
		{
			query = siqcc.Query,
			peer_types = siqcc.InlineQueryPeerTypes(),
			flags = InlineButtonTypeSwitchInline.Flags.has_peer_types
		},
		{ CopyText: { } } => new InlineButtonTypeCopy { copy_text = btn.CopyText.Text },
		{ LoginUrl: { } lu } => new InputInlineButtonTypeUrlAuth
		{
			url = lu.Url,
			fwd_text = lu.ForwardText,
			bot = lu.BotUsername != null ? InputUser(lu.BotUsername) : Client.User,
			flags = (lu.ForwardText != null ? InputInlineButtonTypeUrlAuth.Flags.has_fwd_text : 0)
				| (lu.RequestWriteAccess ? InputInlineButtonTypeUrlAuth.Flags.request_write_access : 0),
		},
		{ WebApp: { } } => new InlineButtonTypeWebView { url = btn.WebApp.Url },
		{ Disabled: { } } => new InlineButtonTypeDisabled { },
		_ => throw new NotImplementedException($"Unsupported button type: {btn?.GetType().Name}"),
	};
}