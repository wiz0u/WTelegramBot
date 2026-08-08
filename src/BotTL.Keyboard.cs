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
				| (rkm.InputFieldPlaceholder != null ? TL.ReplyKeyboardMarkup.Flags.has_placeholder : 0),
			placeholder = rkm.InputFieldPlaceholder,
			rows = [.. rkm.Keyboard.Select(row => new KeyboardButtonRow { buttons = [.. row.Select(MakeKeyboardButton)] })]
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
		var style = btn.KeyboardButtonStyle();
		return btn switch
		{
			{ RequestUsers: { } rus } => new InputKeyboardButtonRequestPeer
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
				style = style,
				flags = (style == null ? 0 : InputKeyboardButtonRequestPeer.Flags.has_style)
					| (rus.RequestName ? InputKeyboardButtonRequestPeer.Flags.name_requested : 0)
					| (rus.RequestUsername ? InputKeyboardButtonRequestPeer.Flags.username_requested : 0)
					| (rus.RequestPhoto ? InputKeyboardButtonRequestPeer.Flags.photo_requested : 0)
			},
			{ RequestChat: { } rc } => new InputKeyboardButtonRequestPeer
			{
				text = btn.Text,
				button_id = rc.RequestId,
				max_quantity = 1,
				peer_type = MakeRequestPeerType(rc),
				style = style,
				flags = (style == null ? 0 : InputKeyboardButtonRequestPeer.Flags.has_style)
					| (rc.RequestTitle ? InputKeyboardButtonRequestPeer.Flags.name_requested : 0)
					| (rc.RequestUsername ? InputKeyboardButtonRequestPeer.Flags.username_requested : 0)
					| (rc.RequestPhoto ? InputKeyboardButtonRequestPeer.Flags.photo_requested : 0)
			},
			{ RequestContact: true } => new KeyboardButtonRequestPhone { text = btn.Text, style = style, flags = style == null ? 0 : KeyboardButtonRequestPhone.Flags.has_style },
			{ RequestLocation: true } => new KeyboardButtonRequestGeoLocation { text = btn.Text, style = style, flags = style == null ? 0 : KeyboardButtonRequestGeoLocation.Flags.has_style },
			{ RequestPoll: { } } => new KeyboardButtonRequestPoll
			{
				text = btn.Text,
				quiz = btn.RequestPoll.Type == PollType.Quiz,
				style = style,
				flags = (style == null ? 0 : KeyboardButtonRequestPoll.Flags.has_style)
					| (btn.RequestPoll.Type.HasValue ? KeyboardButtonRequestPoll.Flags.has_quiz : 0)
			},
			{ WebApp: { } } => new KeyboardButtonSimpleWebView { text = btn.Text, url = btn.WebApp.Url, style = style, flags = style == null ? 0 : KeyboardButtonSimpleWebView.Flags.has_style },
			{ RequestManagedBot: { } rmb } => new InputKeyboardButtonRequestPeer
			{
				text = btn.Text,
				button_id = rmb.RequestId,
				max_quantity = 1,
				peer_type = new RequestPeerTypeCreateBot
				{
					suggested_name = rmb.SuggestedName,
					suggested_username = rmb.SuggestedUsername,
					flags = RequestPeerTypeCreateBot.Flags.bot_managed
						| (rmb.SuggestedName != null ? RequestPeerTypeCreateBot.Flags.has_suggested_name : 0)
						| (rmb.SuggestedUsername != null ? RequestPeerTypeCreateBot.Flags.has_suggested_username : 0),
				},
				style = style, flags = style == null ? 0 : InputKeyboardButtonRequestPeer.Flags.has_style
			},
			_ => new TL.KeyboardButton { text = btn.Text, style = style, flags = style == null ? 0 : TL.KeyboardButton.Flags.has_style }
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

	private async Task<KeyboardButtonBase> MakeKeyboardButton(InlineKeyboardButton btn)
	{
		var style = btn.KeyboardButtonStyle();
		return btn switch
		{
			{ Url: { } } => btn.Url.StartsWith("tg://user?id=", StringComparison.OrdinalIgnoreCase) && long.TryParse(btn.Url[13..], out var userId)
				? new InputKeyboardButtonUserProfile { text = btn.Text, user_id = InputUser(userId), style = style, flags = style == null ? 0 : InputKeyboardButtonUserProfile.Flags.has_style }
				: new KeyboardButtonUrl { text = btn.Text, url = btn.Url, style = style, flags = style == null ? 0 : TL.KeyboardButton.Flags.has_style },
			{ CallbackData: { } } => new KeyboardButtonCallback { text = btn.Text, data = Encoding.UTF8.GetBytes(btn.CallbackData), style = style, flags = style == null ? 0 : KeyboardButtonCallback.Flags.has_style },
			{ CallbackGame: { } } => new KeyboardButtonGame { text = btn.Text, style = style, flags = style == null ? 0 : TL.KeyboardButton.Flags.has_style },
			{ Pay: true } => new KeyboardButtonBuy { text = btn.Text, style = style, flags = style == null ? 0 : TL.KeyboardButton.Flags.has_style },
			{ SwitchInlineQuery: { } } => new KeyboardButtonSwitchInline { text = btn.Text, query = btn.SwitchInlineQuery, style = style, flags = style == null ? 0 : KeyboardButtonSwitchInline.Flags.has_style },
			{ SwitchInlineQueryCurrentChat: { } } => new KeyboardButtonSwitchInline
			{
				text = btn.Text,
				query = btn.SwitchInlineQueryCurrentChat,
				style = style,
				flags = KeyboardButtonSwitchInline.Flags.same_peer | (style == null ? 0 : KeyboardButtonSwitchInline.Flags.has_style)
			},
			{ SwitchInlineQueryChosenChat: { } siqcc } => new KeyboardButtonSwitchInline
			{
				text = btn.Text,
				query = siqcc.Query,
				peer_types = siqcc.InlineQueryPeerTypes(),
				style = style,
				flags = KeyboardButtonSwitchInline.Flags.has_peer_types | (style == null ? 0 : KeyboardButtonSwitchInline.Flags.has_style)
			},
			{ CopyText: { } } => new KeyboardButtonCopy { text = btn.Text, copy_text = btn.CopyText.Text, style = style, flags = style == null ? 0 : KeyboardButtonCopy.Flags.has_style },
			{ LoginUrl: { } lu } => new InputKeyboardButtonUrlAuth
			{
				text = btn.Text,
				url = lu.Url,
				fwd_text = lu.ForwardText,
				bot = lu.BotUsername != null ? await InputUser(lu.BotUsername) : Client.User,
				style = style,
				flags = (style == null ? 0 : InputKeyboardButtonUrlAuth.Flags.has_style)
					| (lu.ForwardText != null ? InputKeyboardButtonUrlAuth.Flags.has_fwd_text : 0)
					| (lu.RequestWriteAccess ? InputKeyboardButtonUrlAuth.Flags.request_write_access : 0),
			},
			{ WebApp: { } } => new KeyboardButtonWebView { text = btn.Text, url = btn.WebApp.Url, style = style, flags = style == null ? 0 : KeyboardButtonWebView.Flags.has_style },
			_ => new TL.KeyboardButton { text = btn.Text, style = style, flags = style == null ? 0 : TL.KeyboardButton.Flags.has_style },
		};
	}
}