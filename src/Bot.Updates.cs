using System.Text;
using TL;
using Chat = WTelegram.Types.Chat;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

public partial class Bot
{
	/// <summary>Converts Client API TL.Update to Bot Telegram.Bot.Types.Update</summary>
	protected async Task<Update?> MakeUpdate(TL.Update update)
	{
		switch (update)
		{
			case UpdateNewMessage unm:
				if (unm.message is TL.Message msg && msg.flags.HasFlag(TL.Message.Flags.out_)) return null;
				bool isChannelPost = (await ChatFromPeer(unm.message.Peer))?.Type == ChatType.Channel;
				if (NotAllowed(isChannelPost ? UpdateType.ChannelPost : UpdateType.Message)) return null;
				var message = await MakeMessageAndReply(unm.message);
				if (message == null) return null;
				return isChannelPost ? new Update { ChannelPost = message, TLUpdate = update }
									: new Update { Message = message, TLUpdate = update };
			case UpdateEditMessage uem:
				if (uem.message is TL.Message emsg && emsg.flags.HasFlag(TL.Message.Flags.out_)) return null;
				isChannelPost = (await ChatFromPeer(uem.message.Peer))?.Type == ChatType.Channel;
				if (NotAllowed(isChannelPost ? UpdateType.ChannelPost : UpdateType.Message)) return null;
				return isChannelPost ? new Update { EditedChannelPost = await MakeMessageAndReply(uem.message), TLUpdate = update }
									: new Update { EditedMessage = await MakeMessageAndReply(uem.message), TLUpdate = update };
			case UpdateBotInlineQuery ubiq:
				if (NotAllowed(UpdateType.InlineQuery)) return null;
				return new Update
				{
					InlineQuery = new InlineQuery
					{
						Id = ubiq.query_id.ToString(),
						From = await UserOrResolve(ubiq.user_id),
						Query = ubiq.query,
						Offset = ubiq.offset,
						ChatType = ubiq.peer_type switch
						{
							InlineQueryPeerType.SameBotPM => ChatType.Sender,
							InlineQueryPeerType.PM or InlineQueryPeerType.BotPM => ChatType.Private,
							InlineQueryPeerType.Chat => ChatType.Group,
							InlineQueryPeerType.Megagroup => ChatType.Supergroup,
							InlineQueryPeerType.Broadcast => ChatType.Channel,
							_ => null,
						},
						Location = ubiq.geo.Location()
					},
					TLUpdate = update
				};
			case UpdateBotGuestChatQuery ubgcq:
				if (NotAllowed(UpdateType.GuestMessage)) return null;
				var reference_message = ubgcq.message.ReplyTo is MessageReplyHeader { reply_to_msg_id: { } rmid } ? ubgcq.reference_messages?.FirstOrDefault(m => m.ID == rmid) : null;
				message = await MakeMessageAndReply(ubgcq.message, await MakeMessage(reference_message));
				if (message == null) return null;
				message.GuestQueryId = ubgcq.query_id.ToString();
				return new Update { GuestMessage = message, TLUpdate = update };
			case UpdateBotInlineSend ubis:
				if (NotAllowed(UpdateType.ChosenInlineResult)) return null;
				return new Update
				{
					ChosenInlineResult = new ChosenInlineResult
					{
						ResultId = ubis.id,
						From = await UserOrResolve(ubis.user_id),
						Location = ubis.geo.Location(),
						InlineMessageId = ubis.msg_id.InlineMessageId(),
						Query = ubis.query,
					},
					TLUpdate = update
				};
			case UpdateBotCallbackQuery ubcq:
				if (NotAllowed(UpdateType.CallbackQuery)) return null;
				return new Update
				{
					CallbackQuery = new CallbackQuery
					{
						Id = ubcq.query_id.ToString(),
						From = await UserOrResolve(ubcq.user_id),
						Message = await GetMIMessage(await ChatFromPeer(ubcq.peer, true), ubcq.msg_id, replyToo: true),
						ChatInstance = ubcq.chat_instance.ToString(),
						Data = ubcq.data.NullOrUtf8(),
						GameShortName = ubcq.game_short_name
					},
					TLUpdate = update
				};
			case UpdateInlineBotCallbackQuery ubicq:
				if (NotAllowed(UpdateType.CallbackQuery)) return null;
				return new Update
				{
					CallbackQuery = new CallbackQuery
					{
						Id = ubicq.query_id.ToString(),
						From = await UserOrResolve(ubicq.user_id),
						InlineMessageId = ubicq.msg_id.InlineMessageId(),
						ChatInstance = ubicq.chat_instance.ToString(),
						Data = ubicq.data.NullOrUtf8(),
						GameShortName = ubicq.game_short_name
					},
					TLUpdate = update
				};
			case UpdateEphemeralBotCallbackQuery uebcq:
				if (NotAllowed(UpdateType.CallbackQuery)) return null;
				return new Update
				{
					CallbackQuery = new CallbackQuery
					{
						Id = uebcq.query_id.ToString(),
						From = await UserOrResolve(uebcq.user_id),
						Message = await MakeEphemeralAndReply(uebcq.message),
						ChatInstance = "0",
						Data = uebcq.data.NullOrUtf8(),
					},
					TLUpdate = update
				};
			case UpdateNewEphemeralMessage unem:
				if (unem.message.flags.HasFlag(EphemeralMessage.Flags.out_)) return null;
				if (NotAllowed(UpdateType.Message)) return null;
				message = await MakeEphemeralAndReply(unem.message);
				if (message == null) return null;
				return new Update { Message = message, TLUpdate = update };
			case UpdateEditEphemeralMessage ueem:
				if (ueem.message.flags.HasFlag(EphemeralMessage.Flags.out_)) return null;
				if (NotAllowed(UpdateType.Message)) return null;
				return new Update { EditedMessage = await MakeEphemeralAndReply(ueem.message), TLUpdate = update };
			case UpdateChannelParticipant uchp:
				if (NotAllowed((uchp.new_participant ?? uchp.prev_participant)?.UserId == BotId ? UpdateType.MyChatMember : UpdateType.ChatMember)) return null;
				return MakeUpdate(new ChatMemberUpdated
				{
					Chat = await ChannelOrResolve(uchp.channel_id),
					From = await UserOrResolve(uchp.actor_id),
					Date = uchp.date,
					OldChatMember = uchp.prev_participant.ChatMember(await UserOrResolve((uchp.prev_participant ?? uchp.new_participant)!.UserId)),
					NewChatMember = uchp.new_participant.ChatMember(await UserOrResolve((uchp.new_participant ?? uchp.prev_participant)!.UserId)),
					InviteLink = await MakeChatInviteLink(uchp.invite),
					ViaJoinRequest = uchp.invite is ChatInvitePublicJoinRequests,
					ViaChatFolderInviteLink = uchp.flags.HasFlag(UpdateChannelParticipant.Flags.via_chatlist)
				}, update);
			case UpdateChatParticipant ucp:
				if (NotAllowed((ucp.new_participant ?? ucp.prev_participant)?.UserId == BotId ? UpdateType.MyChatMember : UpdateType.ChatMember)) return null;
				return MakeUpdate(new ChatMemberUpdated
				{
					Chat = await ChatOrResolve(ucp.chat_id),
					From = await UserOrResolve(ucp.actor_id),
					Date = ucp.date,
					OldChatMember = ucp.prev_participant.ChatMember(await UserOrResolve((ucp.prev_participant ?? ucp.new_participant)!.UserId)),
					NewChatMember = ucp.new_participant.ChatMember(await UserOrResolve((ucp.new_participant ?? ucp.prev_participant)!.UserId)),
					InviteLink = await MakeChatInviteLink(ucp.invite)
				}, update);
			case UpdateBotStopped ubs:
				if (NotAllowed(ubs.user_id == BotId ? UpdateType.MyChatMember : UpdateType.ChatMember)) return null;
				var user = await UserOrResolve(ubs.user_id);
				var cmMember = new ChatMemberMember { User = user };
				var cmBanned = new ChatMemberBanned { User = user };
				return MakeUpdate(new ChatMemberUpdated
				{
					Chat = user.Chat(),
					From = user,
					Date = ubs.date,
					OldChatMember = ubs.stopped ? cmMember : cmBanned,
					NewChatMember = ubs.stopped ? cmBanned : cmMember
				}, update);
			case UpdateMessagePoll ump:
				if (NotAllowed(UpdateType.Poll)) return null;
				return new Update { Poll = await MakePoll(ump.poll, ump.results), TLUpdate = update };
			case UpdateMessagePollVote umpv:
				if (NotAllowed(UpdateType.PollAnswer)) return null;
				return new Update
				{
					PollAnswer = new Telegram.Bot.Types.PollAnswer
					{
						PollId = umpv.poll_id.ToString(),
						VoterChat = umpv.peer is PeerChannel pc ? await ChannelOrResolve(pc.channel_id) : null,
						User = umpv.peer is PeerUser pu ? await UserOrResolve(pu.user_id) : null,
						OptionIds = umpv.positions,
						OptionPersistentIds = umpv.options,
					},
					TLUpdate = update
				};
			case TL.UpdateBotChatInviteRequester ubcir:
				if (NotAllowed(UpdateType.ChatJoinRequest)) return null;
				return new Update
				{
					ChatJoinRequest = new Telegram.Bot.Types.ChatJoinRequest
					{
						Chat = (await ChatFromPeer(ubcir.peer))!,
						From = await UserOrResolve(ubcir.user_id),
						Date = ubcir.date,
						Bio = ubcir.about,
						UserChatId = ubcir.user_id,
						InviteLink = await MakeChatInviteLink(ubcir.invite),
						QueryId = ubcir.flags.HasFlag(UpdateBotChatInviteRequester.Flags.has_query_id) ? ubcir.query_id.ToString() : null
					},
					TLUpdate = update
				};
			case TL.UpdateBotShippingQuery ubsq:
				if (NotAllowed(UpdateType.ShippingQuery)) return null;
				return new Update
				{
					ShippingQuery = new Telegram.Bot.Types.Payments.ShippingQuery
					{
						Id = ubsq.query_id.ToString(),
						From = await UserOrResolve(ubsq.user_id),
						InvoicePayload = Encoding.UTF8.GetString(ubsq.payload),
						ShippingAddress = ubsq.shipping_address.ShippingAddress()
					},
					TLUpdate = update
				};
			case TL.UpdateBotPrecheckoutQuery ubpq:
				if (NotAllowed(UpdateType.PreCheckoutQuery)) return null;
				return new Update
				{
					PreCheckoutQuery = new Telegram.Bot.Types.Payments.PreCheckoutQuery
					{
						Id = ubpq.query_id.ToString(),
						From = await UserOrResolve(ubpq.user_id),
						Currency = ubpq.currency,
						TotalAmount = (int)ubpq.total_amount,
						InvoicePayload = Encoding.UTF8.GetString(ubpq.payload),
						ShippingOptionId = ubpq.shipping_option_id,
						OrderInfo = ubpq.info.OrderInfo()
					},
					TLUpdate = update
				};
			case TL.UpdateBotBusinessConnect ubbc:
				if (NotAllowed(UpdateType.BusinessConnection)) return null;
				return new Update { BusinessConnection = await MakeBusinessConnection(ubbc.connection), TLUpdate = update };
			case TL.UpdateBotNewBusinessMessage ubnbm:
				if (NotAllowed(UpdateType.BusinessMessage)) return null;
				var replyToMessage = await MakeMessage(ubnbm.reply_to_message);
				replyToMessage?.BusinessConnectionId = ubnbm.connection_id;
				message = await MakeMessageAndReply(ubnbm.message, replyToMessage, ubnbm.connection_id);
				return message == null ? null : new Update { BusinessMessage = message, TLUpdate = update };
			case TL.UpdateBotEditBusinessMessage ubebm:
				if (NotAllowed(UpdateType.EditedBusinessMessage)) return null;
				replyToMessage = await MakeMessage(ubebm.reply_to_message);
				replyToMessage?.BusinessConnectionId = ubebm.connection_id;
				message = await MakeMessageAndReply(ubebm.message, replyToMessage, ubebm.connection_id);
				return message == null ? null : new Update { EditedBusinessMessage = message, TLUpdate = update };
			case TL.UpdateBotDeleteBusinessMessage ubdbm:
				if (NotAllowed(UpdateType.DeletedBusinessMessages)) return null;
				return new Update
				{
					DeletedBusinessMessages = new BusinessMessagesDeleted
					{
						BusinessConnectionId = ubdbm.connection_id,
						Chat = await ChatFromPeer(ubdbm.peer, true),
						MessageIds = ubdbm.messages
					},
					TLUpdate = update
				};
			case TL.UpdateBotMessageReaction ubmr:
				if (NotAllowed(UpdateType.MessageReaction)) return null;
				return new Update
				{
					MessageReaction = new MessageReactionUpdated
					{
						Chat = await ChatFromPeer(ubmr.peer, true),
						MessageId = ubmr.msg_id,
						User = await UserFromPeer(ubmr.actor),
						ActorChat = await ChatFromPeer(ubmr.actor),
						Date = ubmr.date,
						OldReaction = [.. ubmr.old_reactions.Select(Converters.ReactionType)],
						NewReaction = [.. ubmr.new_reactions.Select(Converters.ReactionType)],
					},
					TLUpdate = update
				};
			case TL.UpdateBotMessageReactions ubmrs:
				if (NotAllowed(UpdateType.MessageReactionCount)) return null;
				return new Update
				{
					MessageReactionCount = new MessageReactionCountUpdated
					{
						Chat = await ChatFromPeer(ubmrs.peer, true),
						MessageId = ubmrs.msg_id,
						Date = ubmrs.date,
						Reactions = [.. ubmrs.reactions.Select(rc => new Telegram.Bot.Types.ReactionCount { Type = rc.reaction.ReactionType(), TotalCount = rc.count })],
					},
					TLUpdate = update
				};
			case TL.UpdateBotChatBoost ubcb:
				bool expired = ubcb.boost.expires < ubcb.boost.date;
				if (NotAllowed(expired ? UpdateType.RemovedChatBoost : UpdateType.ChatBoost)) return null;
				var cb = new ChatBoostUpdated
				{
					Chat = await ChatFromPeer(ubcb.peer, true),
					Boost = await MakeBoost(ubcb.boost)
				};
				return new Update
				{
					ChatBoost = expired ? null : cb,
					RemovedChatBoost = !expired ? null : new ChatBoostRemoved
					{
						Chat = cb.Chat,
						BoostId = cb.Boost.BoostId,
						RemoveDate = cb.Boost.AddDate,
						Source = cb.Boost.Source,
					},
					TLUpdate = update
				};
			case TL.UpdateBotPurchasedPaidMedia ubppm:
				if (NotAllowed(UpdateType.PurchasedPaidMedia)) return null;
				return new Update
				{
					PurchasedPaidMedia = new PaidMediaPurchased
					{
						From = await UserOrResolve(ubppm.user_id),
						PaidMediaPayload = ubppm.payload,
					},
					TLUpdate = update
				};
			case UpdateManagedBot umb:
				if (NotAllowed(UpdateType.ManagedBot)) return null;
				return new Update
				{
					ManagedBot = new ManagedBotUpdated
					{
						User = await UserOrResolve(umb.user_id),
						Bot = await UserOrResolve(umb.bot_id),
					},
					TLUpdate = update
				};
			case UpdateBotStarsSubscription ubss:
				if (NotAllowed(UpdateType.Subscription)) return null;
				return new Update
				{
					Subscription = new BotSubscriptionUpdated
					{
						User = await UserOrResolve(ubss.user_id),
						InvoicePayload = Encoding.UTF8.GetString(ubss.payload),
						State = ubss.flags switch
						{
							UpdateBotStarsSubscription.Flags.canceled => "canceled",
							UpdateBotStarsSubscription.Flags.restored => "active",
							UpdateBotStarsSubscription.Flags.payment_failed => "failed",
							_ => null!
						}
					},
					TLUpdate = update
				};
			//TL.UpdateDraftMessage seems used to update ourself user info
			default:
				return null;
		}
	}

	private Update? MakeUpdate(ChatMemberUpdated chatMember, TL.Update update) => chatMember.NewChatMember?.User.Id == BotId
		? new Update { MyChatMember = chatMember, TLUpdate = update }
		: new Update { ChatMember = chatMember, TLUpdate = update };

	[return: NotNullIfNotNull(nameof(invite))]
	private async Task<ChatInviteLink?> MakeChatInviteLink(ExportedChatInvite? invite)
		=> invite switch
		{
			null => null,
			ChatInvitePublicJoinRequests => null,
			ChatInviteExported cie => new ChatInviteLink
			{
				InviteLink = cie.link,
				Creator = await UserOrResolve(cie.admin_id),
				CreatesJoinRequest = cie.flags.HasFlag(ChatInviteExported.Flags.request_needed),
				IsPrimary = cie.flags.HasFlag(ChatInviteExported.Flags.permanent),
				IsRevoked = cie.flags.HasFlag(ChatInviteExported.Flags.revoked),
				Name = cie.title,
				ExpireDate = cie.expire_date.NullIfDefault(),
				MemberLimit = cie.usage_limit.NullIfZero(),
				PendingJoinRequestCount = cie.flags.HasFlag(ChatInviteExported.Flags.has_requested) ? cie.requested : null,
				SubscriptionPeriod = cie.subscription_pricing?.period,
				SubscriptionPrice = cie.subscription_pricing is { amount: var amount } ? (int)amount : null,
			},
			_ => throw new WTException("Unexpected ExportedChatInvite: " + invite)
		};

	/// <returns>User or a stub on failure</returns>
	public async Task<User> UserOrResolve(long userId)
	{
		lock (_users)
			if (_users.TryGetValue(userId, out var user))
				return user;
		try
		{
			var users = await Client.Users_GetUsers(new InputUser(userId, 0));
			if (users.Length != 0 && users[0] is TL.User user)
				lock (_users)
					return _users[userId] = user.User();
		}
		catch (RpcException) { }
		return new User { Id = userId, FirstName = "" };
	}

	/// <returns>null if peer is not PeerUser ; User or a stub on failure</returns>
	private async Task<User?> UserFromPeer(Peer peer) => peer is not PeerUser pu ? null : await UserOrResolve(pu.user_id);

	private async Task<Chat> ChannelOrResolve(long id)
	{
		if (Chat(id) is { } chat)
			return chat;
		try
		{
			var chats = await Client.Channels_GetChannels(new InputChannel(id, 0));
			if (chats.chats.TryGetValue(id, out var chatBase))
				lock (_chats)
					return _chats[id] = chatBase.Chat();
		}
		catch (RpcException) { }
		return new Chat { Id = ZERO_CHANNEL_ID - id, Type = ChatType.Supergroup };
	}

	private async Task<Chat> ChatOrResolve(long chatId)
	{
		if (Chat(chatId) is { } chat)
			return chat;
		try
		{
			var chats = await Client.Messages_GetChats(chatId);
			if (chats.chats.TryGetValue(chatId, out var chatBase))
				lock (_chats)
					return _chats[chatId] = chatBase.Chat();
		}
		catch (RpcException) { }
		return new Chat { Id = -chatId, Type = ChatType.Group };
	}

	private async Task<Chat?> ChatFromPeer(Peer? peer, [DoesNotReturnIf(true)] bool allowUser = false) => peer switch
	{
		null => null,
		PeerUser pu => allowUser ? (await UserOrResolve(pu.user_id)).Chat() : null,
		PeerChannel pc => await ChannelOrResolve(pc.channel_id),
		_ => await ChatOrResolve(peer.ID),
	};

	private async Task<Chat> ChatFromPeer(InputPeer peer) => peer switch
	{
		InputPeerUser pu => (await UserOrResolve(pu.user_id)).Chat(),
		InputPeerChannel ipc => await ChannelOrResolve(ipc.channel_id),
		_ => await ChatOrResolve(peer.ID)
	};
}
