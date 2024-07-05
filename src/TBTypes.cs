using System;
using System.Collections.Generic;
using System.Text;

namespace WTelegram.Types
{
	/// <summary>Chat type for WTelegram.Bot with Client API infos</summary>
	public class Chat : Telegram.Bot.Types.Chat
	{
		/// <summary>The corresponding Client API chat structure. Real type can be TL.User, TL.Chat, TL.Channel...</summary>
		public TL.IObject? TLInfo;

		/// <summary>Client API access_hash of the chat</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		public static implicit operator TL.InputPeer(Chat chat) => chat.Type switch
		{
			ChatType.Private => new TL.InputPeerUser(chat.Id, chat.AccessHash),
			ChatType.Group => new TL.InputPeerChat(-chat.Id),
			_ => new TL.InputPeerChannel(-1000000000000 - chat.Id, chat.AccessHash),
		};
	}

	/// <summary>ChatFullInfo type for WTelegram.Bot with Client API infos</summary>
	public class ChatFullInfo : Telegram.Bot.Types.ChatFullInfo
	{
		/// <summary>The corresponding Client API chat full structure. Real type can be TL.Users_UserFull, TL.Messages_ChatFull)</summary>
		public TL.IObject? TLInfo;

		/// <summary>Client API access_hash of the chat</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		public static implicit operator TL.InputPeer(ChatFullInfo chat) => chat.Type switch
		{
			ChatType.Private => new TL.InputPeerUser(chat.Id, chat.AccessHash),
			ChatType.Group => new TL.InputPeerChat(-chat.Id),
			_ => new TL.InputPeerChannel(-1000000000000 - chat.Id, chat.AccessHash),
		};
	}

	/// <summary>User type for WTelegram.Bot with Client API infos</summary>
	public partial class User : Telegram.Bot.Types.User
	{
		/// <summary>The corresponding Client API user structure</summary>
		public TL.User? TLUser;
		/// <summary>Client API access_hash of the user</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		[return: NotNullIfNotNull(nameof(user))]
		public static implicit operator TL.InputPeerUser?(User? user) => user == null ? null : new(user.Id, user.AccessHash);
		/// <summary>Useful operator for Client API calls</summary>
		[return: NotNullIfNotNull(nameof(user))]
		public static implicit operator TL.InputUser?(User? user) => user == null ? null : new(user.Id, user.AccessHash);

		/// <inheritdoc/>
		public override string ToString() =>
			$"{(Username is null ? $"{FirstName}{LastName?.Insert(0, " ")}" : $"@{Username}")} ({Id})";
	}

	/// <summary>Update type for WTelegram.Bot with Client API infos</summary>
	public partial class Update : Telegram.Bot.Types.Update
	{
		/// <summary>The corresponding Client API update structure</summary>
		public TL.Update? TLUpdate;
	}

	/// <summary>Message type for WTelegram.Bot with Client API infos</summary>
	public partial class Message : Telegram.Bot.Types.Message
	{
		/// <summary>The corresponding Client API message structure</summary>
		public TL.MessageBase? TLMessage;
	}
}
