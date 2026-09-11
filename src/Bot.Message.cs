using System.Text;
using TL;
using Chat = WTelegram.Types.Chat;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

public partial class Bot
{
	/// <summary>Converts Client API TL.MessageBase to Bot Telegram.Bot.Types.Message</summary>
	[return: NotNullIfNotNull(nameof(msgBase))]
	protected async Task<Message?> MakeMessage(MessageBase? msgBase)
	{
		switch (msgBase)
		{
			case TL.Message message:
				var msg = new WTelegram.Types.Message
				{
					TLMessage = message,
					Id = message.flags.HasFlag(TL.Message.Flags.from_scheduled) ? 0 : message.id,
					From = await UserFromPeer(message.from_id),
					SenderChat = await ChatFromPeer(message.from_id),
					Date = message.date,
					Chat = await ChatFromPeer(message.peer_id, allowUser: true),
					AuthorSignature = message.post_author,
					ReplyMarkup = message.reply_markup.InlineKeyboardMarkup(),
					SenderBoostCount = message.from_boosts_applied > 0 ? message.from_boosts_applied : null,
					SenderBusinessBot = User(message.via_business_bot_id),
					SenderTag = message.from_rank,
					IsFromOffline = message.flags2.HasFlag(TL.Message.Flags2.offline),
					EffectId = message.flags2.HasFlag(TL.Message.Flags2.has_effect) ? message.effect.ToString() : null,
					PaidStarCount = message.paid_message_stars.NullIfNegative(),
					DirectMessagesTopic = message.saved_peer_id is PeerUser pu ? new DirectMessagesTopic { TopicId = pu.user_id, User = User(pu.user_id) } : null,
					IsPaidPost = (message.flags2 & (TL.Message.Flags2.paid_suggested_post_stars | TL.Message.Flags2.paid_suggested_post_ton)) != 0,
					SuggestedPostInfo = message.suggested_post.SuggestedPostInfo(),
					GuestBotCallerUser = await UserFromPeer(message.guestchat_via_from),
					GuestBotCallerChat = await ChatFromPeer(message.guestchat_via_from),
				};
				if (message.fwd_from is { } fwd)
				{
					msg.ForwardOrigin = await MakeOrigin(fwd);
					msg.IsAutomaticForward = msg.Chat.Type == ChatType.Supergroup && await ChatFromPeer(fwd.saved_from_peer) is Chat { Type: ChatType.Channel } && fwd.saved_from_msg_id != 0;
				}
				if (msg.Chat.Type is ChatType.Supergroup or ChatType.Private && message.reply_to is MessageReplyHeader reply_to)
				{
					msg.IsTopicMessage = reply_to.flags.HasFlag(MessageReplyHeader.Flags.forum_topic);
					if (reply_to.reply_to_top_id > 0)
						msg.MessageThreadId = reply_to.reply_to_top_id;
					else if (reply_to.reply_to_msg_id > 0 // reply to same-chat?
						&& (reply_to.reply_to_peer_id == null || reply_to.reply_to_peer_id.ID == message.Peer.ID))
						msg.MessageThreadId = reply_to.reply_to_msg_id;
				}

				await FixMsgFrom(msg, message.from_id, message.peer_id);
				if (message.via_bot_id != 0) msg.ViaBot = await UserOrResolve(message.via_bot_id);
				if (message.edit_date != default) msg.EditDate = message.edit_date;
				if (message.flags.HasFlag(TL.Message.Flags.noforwards)) msg.HasProtectedContent = true;
				if (message.grouped_id != 0) msg.MediaGroupId = message.grouped_id.ToString();
				if (message.rich_message != null) { msg.RichMessage = RichMessage(message.rich_message); return CacheMessage(msg, msgBase); }
				return CacheMessage(await FillTextAndMedia(msg, message.message, message.entities, message.media, message.flags.HasFlag(TL.Message.Flags.invert_media)), msgBase);
			case TL.MessageService msgSvc:
				msg = new WTelegram.Types.Message
				{
					TLMessage = msgSvc,
					Id = msgSvc.id,
					From = await UserFromPeer(msgSvc.from_id),
					SenderChat = await ChatFromPeer(msgSvc.from_id),
					Date = msgSvc.date,
					Chat = await ChatFromPeer(msgSvc.peer_id, allowUser: true),
				};
				if (msgSvc.action is MessageActionTopicCreate)
				{
					msg.IsTopicMessage = true;
					msg.MessageThreadId = msgSvc.id;
				}
				await FixMsgFrom(msg, msgSvc.from_id, msgSvc.peer_id);
				if (await MakeServiceMessage(msgSvc, msg) == null) return CacheMessage(null, msgBase);
				return CacheMessage(msg, msgBase);
			case null:
				return null;
			default:
				return CacheMessage(new WTelegram.Types.Message
				{
					TLMessage = msgBase,
					Id = msgBase.ID,
					Chat = await ChatFromPeer(msgBase.Peer, allowUser: true)!,
				}, msgBase);
		}

		async Task FixMsgFrom(Message msg, Peer from_id, Peer peer_id)
		{
			if (msg.From == null)
				switch (msg.Chat.Type)
				{
					case ChatType.Channel: break;
					case ChatType.Private:
						msg.From = await UserFromPeer(peer_id);
						break;
					default:
						if (from_id == null)
						{
							msg.From = GroupAnonymousBot;
							msg.SenderChat = msg.Chat;
						}
						else if (msg.IsAutomaticForward)
							msg.From = ServiceNotification;
						break;
				}
		}
	}

	private async Task<Message> FillTextAndMedia(Message msg, string? text, TL.MessageEntity[] entities, MessageMedia media, bool invert_media = false)
	{
		switch (media)
		{
			case null:
				if (entities?.Any(e => e is MessageEntityUrl or MessageEntityTextUrl) == true)
					msg.LinkPreviewOptions = new LinkPreviewOptions { IsDisabled = true };
				msg.Text = text;
				msg.Entities = MakeEntities(entities);
				return msg;
			case MessageMediaWebPage mmwp:
				msg.LinkPreviewOptions = mmwp.LinkPreviewOptions(invert_media);
				msg.Text = text;
				msg.Entities = MakeEntities(entities);
				return msg;
			case MessageMediaDocument { document: TL.Document document } mmd:
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.spoiler)) msg.HasMediaSpoiler = true;
				msg.ShowCaptionAboveMedia = invert_media;
				var thumb = document.LargestThumbSize;
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.voice))
				{
					msg.Voice = document.Voice();
				}
				else if (mmd.flags.HasFlag(MessageMediaDocument.Flags.round))
				{
					var video = document.GetAttribute<DocumentAttributeVideo>();
					msg.VideoNote = new Telegram.Bot.Types.VideoNote
					{
						FileSize = document.size,
						Length = video?.w ?? 0,
						Duration = (int)(Math.Ceiling(video?.duration ?? 0.0)),
						 Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id)
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (mmd.flags.HasFlag(MessageMediaDocument.Flags.video))
					msg.Video = document.Video(mmd);
				else if (document.GetAttribute<DocumentAttributeAudio>() is { } audio)
					msg.Audio = document.Audio(audio);
				else if (document.GetAttribute<DocumentAttributeSticker>() is { } sticker)
					msg.Sticker = await MakeSticker(document, sticker);
				else
				{
					msg.Document = document.Document(thumb);
					if (document.GetAttribute<DocumentAttributeAnimated>() is { })
						msg.Animation = MakeAnimation(msg.Document!, document.GetAttribute<DocumentAttributeVideo>());
				}
				break;
			case MessageMediaPhoto { photo: TL.Photo photo } mmp:
				if (mmp.flags.HasFlag(MessageMediaPhoto.Flags.spoiler)) msg.HasMediaSpoiler = true;
				msg.ShowCaptionAboveMedia = invert_media;
				msg.Photo = photo.PhotoSizes();
				if (mmp.video is TL.Document lpVideo)
					msg.LivePhoto = lpVideo.LivePhoto(msg.Photo);
				break;
			case MessageMediaVenue mmv:
				msg.Venue = mmv.Venue();
				break;
			case MessageMediaContact mmc:
				msg.Contact = new Telegram.Bot.Types.Contact
				{
					PhoneNumber = mmc.phone_number,
					FirstName = mmc.first_name,
					LastName = mmc.last_name,
					UserId = mmc.user_id,
					Vcard = mmc.vcard,
				};
				break;
			case MessageMediaGeo mmg:
				msg.Location = mmg.Location();
				break;
			case MessageMediaPoll { poll: TL.Poll poll, results: TL.PollResults pollResults }:
				msg.Poll = await MakePoll(poll, pollResults);
				msg.Poll.Description = text;
				msg.Poll.DescriptionEntities = MakeEntities(entities);
				msg.Poll.Media = await ToPollMedia(media);
				return msg;
			case MessageMediaDice mmd:
				msg.Dice = new Dice { Emoji = mmd.emoticon, Value = mmd.value };
				return msg;
			case MessageMediaInvoice mmi:
				msg.Invoice = new Telegram.Bot.Types.Payments.Invoice
				{
					Title = mmi.title,
					Description = mmi.description,
					StartParameter = mmi.start_param,
					Currency = mmi.currency,
					TotalAmount = (int)mmi.total_amount
				};
				return msg;
			case MessageMediaGame mmg:
				msg.Game = new Telegram.Bot.Types.Game
				{
					Title = mmg.game.title,
					Description = mmg.game.description,
					Photo = mmg.game.photo.PhotoSizes()!,
					Text = text.NullIfEmpty(),
					TextEntities = MakeEntities(entities)
				};
				if (mmg.game.document is TL.Document doc && doc.GetAttribute<DocumentAttributeAnimated>() != null)
				{
					thumb = doc.LargestThumbSize;
					msg.Game.Animation = MakeAnimation(doc.Document(thumb)!, doc.GetAttribute<DocumentAttributeVideo>());
				}
				return msg;
			case MessageMediaStory mms:
				msg.Story = new Story
				{
					Chat = await ChatFromPeer(mms.peer, true),
					Id = mms.id
				};
				break;
			case MessageMediaGiveaway mmg:
				msg.Giveaway = new Giveaway
				{
					Chats = await mmg.channels.Select(ChannelOrResolve).WhenAllSequential(),
					WinnersSelectionDate = mmg.until_date,
					WinnerCount = mmg.quantity,
					OnlyNewMembers = mmg.flags.HasFlag(MessageMediaGiveaway.Flags.only_new_subscribers),
					HasPublicWinners = mmg.flags.HasFlag(MessageMediaGiveaway.Flags.winners_are_visible),
					PrizeDescription = mmg.prize_description,
					CountryCodes = mmg.countries_iso2,
					PremiumSubscriptionMonthCount = mmg.months.NullIfZero(),
					PrizeStarCount = ((int)mmg.stars).NullIfZero(),
				};
				break;
			case MessageMediaGiveawayResults mmgr:
				msg.GiveawayWinners = new GiveawayWinners
				{
					Chat = await ChannelOrResolve(mmgr.channel_id),
					GiveawayMessageId = mmgr.launch_msg_id,
					WinnersSelectionDate = mmgr.until_date,
					WinnerCount = mmgr.winners_count,
					Winners = await mmgr.winners.Select(UserOrResolve).WhenAllSequential(),
					AdditionalChatCount = mmgr.additional_peers_count,
					PremiumSubscriptionMonthCount = mmgr.months,
					UnclaimedPrizeCount = mmgr.unclaimed_count,
					OnlyNewMembers = mmgr.flags.HasFlag(MessageMediaGiveawayResults.Flags.only_new_subscribers),
					WasRefunded = mmgr.flags.HasFlag(MessageMediaGiveawayResults.Flags.refunded),
					PrizeDescription = mmgr.prize_description,
					PrizeStarCount = ((int)mmgr.stars).NullIfZero(),
				};
				break;
			case MessageMediaPaidMedia mmpm:
				msg.PaidMedia = new PaidMediaInfo
				{
					StarCount = (int)mmpm.stars_amount,
					PaidMedia = [.. mmpm.extended_media.Select(Converters.PaidMedia)]
				};
				break;
			case MessageMediaToDo mmtd:
				msg.Checklist = Checklist(mmtd.todo, mmtd.completions);
				break;
			default:
				break;
		}
		if (text != "") msg.Caption = text;
		msg.CaptionEntities = MakeEntities(entities);
		return msg;
	}

	private async Task<object?> MakeServiceMessage(MessageService msgSvc, Message msg)
	{
		return msgSvc.action switch
		{
			MessageActionChatAddUser macau => msg.NewChatMembers = await macau.users.Select(UserOrResolve).WhenAllSequential(),
			MessageActionChatDeleteUser macdu => msg.LeftChatMember = await UserOrResolve(macdu.user_id),
			MessageActionChatEditTitle macet => msg.NewChatTitle = macet.title,
			MessageActionChatEditPhoto macep => msg.NewChatPhoto = macep.photo.PhotoSizes(),
			MessageActionChatDeletePhoto macdp => msg.DeleteChatPhoto = true,
			MessageActionChatCreate => msg.GroupChatCreated = true,
			MessageActionChannelCreate => (await ChatFromPeer(msgSvc.peer_id))?.Type == ChatType.Channel
				? msg.SupergroupChatCreated = true : msg.ChannelChatCreated = true,
			MessageActionSetMessagesTTL macsmt => msg.MessageAutoDeleteTimerChanged =
				new MessageAutoDeleteTimerChanged { MessageAutoDeleteTime = macsmt.period },
			MessageActionChatMigrateTo macmt => msg.MigrateToChatId = ZERO_CHANNEL_ID - macmt.channel_id,
			MessageActionChannelMigrateFrom macmf => msg.MigrateFromChatId = -macmf.chat_id,
			MessageActionPinMessage macpm => msg.PinnedMessage = await GetMIMessage(
				await ChatFromPeer(msgSvc.peer_id, allowUser: true), msgSvc.reply_to is MessageReplyHeader mrh ? mrh.reply_to_msg_id : 0),
			MessageActionChatJoinedByLink or MessageActionChatJoinedByRequest => msg.NewChatMembers = [msg.From!],
			MessageActionPaymentSentMe mapsm => msg.SuccessfulPayment = new Telegram.Bot.Types.Payments.SuccessfulPayment
			{
				Currency = mapsm.currency,
				TotalAmount = (int)mapsm.total_amount,
				InvoicePayload = Encoding.UTF8.GetString(mapsm.payload),
				ShippingOptionId = mapsm.shipping_option_id,
				OrderInfo = mapsm.info.OrderInfo(),
				TelegramPaymentChargeId = mapsm.charge.id,
				ProviderPaymentChargeId = mapsm.charge.provider_charge_id,
				SubscriptionExpirationDate = mapsm.subscription_until_date.NullIfDefault(),
				IsRecurring = mapsm.flags.HasFlag(MessageActionPaymentSentMe.Flags.recurring_used),
				IsFirstRecurring = mapsm.flags.HasFlag(MessageActionPaymentSentMe.Flags.recurring_init),
			},
			MessageActionRequestedPeer { peers.Length: > 0 } marp => marp.peers[0] is PeerUser
				? msg.UsersShared = new UsersShared { RequestId = marp.button_id, Users = [.. marp.peers.Select(p => new SharedUser { UserId = p.ID })] }
				: msg.ChatShared = new ChatShared { RequestId = marp.button_id, ChatId = marp.peers[0].ToChatId() },
			MessageActionRequestedPeerSentMe { peers.Length: > 0 } marpsm => marpsm.peers[0] is RequestedPeerUser
				? msg.UsersShared = new UsersShared { RequestId = marpsm.button_id, Users = [.. marpsm.peers.Select(p => p.ToSharedUser())] }
				: msg.ChatShared = marpsm.peers[0].ToSharedChat(marpsm.button_id),
			MessageActionBotAllowed maba => maba switch
			{
				{ domain: not null } => msg.ConnectedWebsite = maba.domain,
				{ app: not null } => msg.WriteAccessAllowed = new WriteAccessAllowed
				{
					WebAppName = maba.app.short_name,
					FromRequest = maba.flags.HasFlag(MessageActionBotAllowed.Flags.from_request),
					FromAttachmentMenu = maba.flags.HasFlag(MessageActionBotAllowed.Flags.attach_menu)
				},
				_ => null
			},
			MessageActionSecureValuesSentMe masvsm => msg.PassportData = masvsm.PassportData(),
			MessageActionGeoProximityReached magpr => msg.ProximityAlertTriggered = new ProximityAlertTriggered
			{
				Traveler = (await UserFromPeer(magpr.from_id))!,
				Watcher = (await UserFromPeer(magpr.to_id))!,
				Distance = magpr.distance
			},
			MessageActionGroupCallScheduled magcs => msg.VideoChatScheduled = new VideoChatScheduled { StartDate = magcs.schedule_date },
			MessageActionGroupCall magc => magc.flags.HasFlag(MessageActionGroupCall.Flags.has_duration)
				? msg.VideoChatEnded = new VideoChatEnded { Duration = magc.duration }
				: msg.VideoChatStarted = new VideoChatStarted(),
			MessageActionInviteToGroupCall maitgc => msg.VideoChatParticipantsInvited = new VideoChatParticipantsInvited
			{
				Users = await maitgc.users.Select(UserOrResolve).WhenAllSequential()
			},
			MessageActionWebViewDataSentMe mawvdsm => msg.WebAppData = new WebAppData { ButtonText = mawvdsm.text, Data = mawvdsm.data },
			MessageActionTopicCreate matc => msg.ForumTopicCreated = new ForumTopicCreated
			{
				Name = matc.title,
				IconColor = matc.icon_color,
				IconCustomEmojiId = matc.flags.HasFlag(MessageActionTopicCreate.Flags.has_icon_emoji_id) ? matc.icon_emoji_id.ToString() : null,
				IsNameImplicit = matc.flags.HasFlag(MessageActionTopicCreate.Flags.title_missing)
			},
			MessageActionTopicEdit mate => mate.flags.HasFlag(MessageActionTopicEdit.Flags.has_closed) ?
					mate.closed ? msg.ForumTopicClosed = new() : msg.ForumTopicReopened = new()
				: mate.flags.HasFlag(MessageActionTopicEdit.Flags.has_hidden)
					? mate.hidden ? msg.GeneralForumTopicHidden = new() : msg.GeneralForumTopicUnhidden = new()
					: msg.ForumTopicEdited = new ForumTopicEdited
					{
						Name = mate.title,
						IconCustomEmojiId = mate.icon_emoji_id != 0
						? mate.icon_emoji_id.ToString() : mate.flags.HasFlag(MessageActionTopicEdit.Flags.has_icon_emoji_id) ? "" : null
					},
			MessageActionBoostApply maba => msg.BoostAdded = new ChatBoostAdded { BoostCount = maba.boosts },
			MessageActionGiveawayLaunch magl => msg.GiveawayCreated = new GiveawayCreated
			{
				PrizeStarCount = ((int)magl.stars).NullIfZero()
			},
			MessageActionGiveawayResults magr => msg.GiveawayCompleted = new GiveawayCompleted
			{
				WinnerCount = magr.winners_count,
				UnclaimedPrizeCount = magr.unclaimed_count,
				GiveawayMessage = await GetRepliedMessage(msgSvc),
				IsStarGiveaway = magr.flags.HasFlag(MessageActionGiveawayResults.Flags.stars)
			},
			MessageActionSetChatWallPaper mascwp => msg.ChatBackgroundSet = new ChatBackground { Type = mascwp.wallpaper.BackgroundType() },
			MessageActionPaymentRefunded mapr => msg.RefundedPayment = new RefundedPayment
			{
				Currency = mapr.currency,
				TotalAmount = (int)mapr.total_amount,
				InvoicePayload = mapr.payload.NullOrUtf8() ?? "",
				TelegramPaymentChargeId = mapr.charge.id,
				ProviderPaymentChargeId = mapr.charge.provider_charge_id
			},
			MessageActionStarGift masg => masg.gift is StarGift gift && new GiftInfo
			{
				Gift = MakeGift(gift),
				OwnedGiftId = masg.peer != null ? $"{masg.peer.ID}_{masg.saved_id}" : msgSvc.id.ToString(),
				ConvertStarCount = masg.convert_stars.NullIfNegative(),
				PrepaidUpgradeStarCount = masg.upgrade_stars.NullIfNegative(),
				IsUpgradeSeparate = masg.flags.HasFlag(MessageActionStarGift.Flags.upgrade_separate),
				CanBeUpgraded = masg.flags.HasFlag(MessageActionStarGift.Flags.can_upgrade),
				Text = masg.message?.text,
				Entities = MakeEntities(masg.message?.entities),
				IsPrivate = masg.flags.HasFlag(MessageActionStarGift.Flags.name_hidden),
				UniqueGiftNumber = masg.gift_num.NullIfZero()
			} is { } giftInfo ? masg.flags.HasFlag(MessageActionStarGift.Flags.prepaid_upgrade)
				? msg.GiftUpgradeSent = giftInfo
				: msg.Gift = giftInfo
			: null,
			MessageActionStarGiftUnique masgu => masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.refunded)
			? masgu.gift is not StarGift gift ? null : msg.Gift = new GiftInfo { Gift = MakeGift(gift) }
			: masgu.gift is not StarGiftUnique giftUnique ? null : msg.UniqueGift = new UniqueGiftInfo
			{
				Gift = await MakeUniqueGift(giftUnique),
				Origin = masgu.Origin(),
				OwnedGiftId = masgu.peer != null && masgu.saved_id != 0 ? $"{masgu.peer.ID}_{masgu.saved_id}" :
					msgSvc.peer_id is PeerUser pu ? msgSvc.id.ToString() : null,
				TransferStarCount = masgu.transfer_stars.NullIfNegative(),
				NextTransferDate = masgu.can_transfer_at.NullIfDefault(),
				LastResaleAmount = masgu.resale_amount?.Amount.NullIfNegative(),
				LastResaleCurrency = masgu.resale_amount?.Currency(),
				IsPrivate = masgu.flags.HasFlag(MessageActionStarGiftUnique.Flags.name_hidden),
				Text = masgu.message?.text,
				Entities = MakeEntities(masgu.message?.entities),
			},
			MessageActionPaidMessagesPrice mapmp => msg.Chat.Type == ChatType.Channel
			? msg.DirectMessagePriceChanged = new DirectMessagePriceChanged { DirectMessageStarCount = mapmp.stars, AreDirectMessagesEnabled = mapmp.flags.HasFlag(MessageActionPaidMessagesPrice.Flags.broadcast_messages_allowed) }
			: msg.PaidMessagePriceChanged = new PaidMessagePriceChanged { PaidMessageStarCount = mapmp.stars },
			MessageActionTodoCompletions matc => msg.ChecklistTasksDone = new ChecklistTasksDone
			{
				ChecklistMessage = await GetRepliedMessage(msgSvc),
				MarkedAsDoneTaskIds = matc.completed,
				MarkedAsNotDoneTaskIds = matc.incompleted,
			},
			MessageActionTodoAppendTasks matat => msg.ChecklistTasksAdded = new ChecklistTasksAdded
			{
				ChecklistMessage = await GetRepliedMessage(msgSvc),
				Tasks = ChecklistTasks(matat.list)
			},
			MessageActionSuggestedPostApproval maspa => await GetRepliedMessage(msgSvc) is var spm ?
				maspa.flags.HasFlag(MessageActionSuggestedPostApproval.Flags.balance_too_low)
				? msg.SuggestedPostApprovalFailed = new SuggestedPostApprovalFailed { SuggestedPostMessage = spm, Price = maspa.price.SuggestedPostPrice(), }
				: maspa.flags.HasFlag(MessageActionSuggestedPostApproval.Flags.rejected)
				? msg.SuggestedPostDeclined = new SuggestedPostDeclined { SuggestedPostMessage = spm, Comment = maspa.reject_comment }
				: msg.SuggestedPostApproved = new SuggestedPostApproved { SuggestedPostMessage = spm, Price = maspa.price.SuggestedPostPrice(), SendDate = maspa.schedule_date }
				: null,
			MessageActionSuggestedPostSuccess masps => await GetRepliedMessage(msgSvc) is var spm ?
				msg.SuggestedPostPaid = masps.price switch
				{
					TL.StarsAmount sa => new SuggestedPostPaid { Currency = "XTR", StarAmount = sa.StarAmount(), SuggestedPostMessage = spm },
					TL.StarsTonAmount sta => new SuggestedPostPaid { Currency = "TON", Amount = sta.amount, SuggestedPostMessage = spm },
					_ => new() { SuggestedPostMessage = spm }
				} : null,
			MessageActionSuggestedPostRefund maspr => msg.SuggestedPostRefunded = new SuggestedPostRefunded
			{
				SuggestedPostMessage = await GetRepliedMessage(msgSvc),
				Reason = maspr.flags.HasFlag(MessageActionSuggestedPostRefund.Flags.payer_initiated) ? SuggestedPostRefundedReason.PaymentRefunded : SuggestedPostRefundedReason.PostDeleted
			},
			MessageActionNewCreatorPending mancp => msg.ChatOwnerLeft = new ChatOwnerLeft { NewOwner = User(mancp.new_creator_id) },
			MessageActionChangeCreator macc => msg.ChatOwnerChanged = new ChatOwnerChanged { NewOwner = User(macc.new_creator_id)! },
			MessageActionManagedBotCreated mambc => msg.ManagedBotCreated = new ManagedBotCreated { Bot = User(mambc.bot_id)! },
			MessageActionPollAppendAnswer { answer: TL.PollAnswer answer } mapaa => msg.PollOptionAdded = new PollOptionAdded
			{
				PollMessage = await GetRepliedMessage(msgSvc),
				OptionPersistentId = answer.option,
				OptionText = answer.text.text,
				OptionTextEntities = MakeEntities(answer.text.entities)
			},
			MessageActionPollDeleteAnswer { answer: TL.PollAnswer answer } mapda => msg.PollOptionDeleted = new PollOptionDeleted
			{
				PollMessage = await GetRepliedMessage(msgSvc),
				OptionPersistentId = answer.option,
				OptionText = answer.text.text,
				OptionTextEntities = MakeEntities(answer.text.entities)
			},
			MessageActionChangeCommunity macc2 => macc2.community_id == 0 ? msg.CommunityChatRemoved = new()
				: msg.CommunityChatAdded = new() { Community = new() { Id = macc2.community_id, Name = Chat(macc2.community_id)?.Title! } },
			MessageActionChatJoinedViaCommunity macjvc =>
				msg.CommunityChatJoined = new() { Community = new() { Id = macjvc.community_id, Name = Chat(macjvc.community_id)?.Title! } },
			_ => null,
		};
	}


	private async Task<Message> MakeEphemeralAndReply(EphemeralMessage message, Message? replyToMessage = null)
	{
		var msg = new Message
		{
			//TLMessage = message,
			Id = 0,
			EphemeralMessageId = message.id,
			ReceiverUser = User(message.receiver_id),
			From = await UserFromPeer(message.from_id),
			//SenderChat = await ChatFromPeer(message.from_id),
			Date = message.date,
			Chat = await ChatFromPeer(message.peer_id, allowUser: true),
			ReplyMarkup = message.reply_markup.InlineKeyboardMarkup(),
		};
		await FillTextAndMedia(msg, message.message, message.entities, message.media, false);
		await FillReply(msg, message.reply_to, async reply_to => {
			if (replyToMessage != null && reply_to.reply_to_msg_id == replyToMessage.Id)
				return replyToMessage;
			var chat = await ChatFromPeer(message.peer_id);
			return await GetMessage(chat!, reply_to.flags.HasFlag(MessageReplyHeader.Flags.reply_to_ephemeral) ? -reply_to.reply_to_msg_id : reply_to.reply_to_msg_id);
		});
		lock (CachedMessages)
			CachedMessages[(message.peer_id.ID, -message.id)] = msg;
		return msg;
	}

	/// <summary>Converts Client API TL.MessageBase to Bot Telegram.Bot.Types.Message and assign the ReplyToMessage/ExternalReply</summary>
	public async Task<Message?> MakeMessageAndReply(MessageBase? msgBase, Message? replyToMessage = null, string? bConnId = null)
	{
		var msg = await MakeMessage(msgBase);
		if (msg == null) return null;
		msg.BusinessConnectionId = bConnId;
		return await FillReply(msg, msgBase!.ReplyTo, reply_to => replyToMessage != null && reply_to.reply_to_msg_id == replyToMessage.Id
																	? Task.FromResult((Message?)replyToMessage) : GetRepliedMessage(msgBase, true));
	}

	private async Task<Message?> FillReply(Message msg, MessageReplyHeaderBase replyTo, Func<MessageReplyHeader, Task<Message?>> fetchReplied)
	{
		switch (replyTo)
		{
			case MessageReplyHeader reply_to:
				if (reply_to.reply_from == null)
					msg.ReplyToMessage = await fetchReplied(reply_to);
				if (reply_to.todo_item_id != 0) msg.ReplyToChecklistTaskId = reply_to.todo_item_id;
				if (reply_to.poll_option != null) msg.ReplyToPollOptionId = reply_to.poll_option;
				if (reply_to.reply_from?.date > default(DateTime))
				{
					var ext = await FillTextAndMedia(new Message(), null, null!, reply_to.reply_media);
					msg.ExternalReply = new ExternalReplyInfo
					{
						MessageId = reply_to.reply_to_msg_id,
						Chat = await ChatFromPeer(reply_to.reply_to_peer_id),
						HasMediaSpoiler = ext.HasMediaSpoiler,
						LinkPreviewOptions = ext.LinkPreviewOptions,
						Origin = (await MakeOrigin(reply_to.reply_from))!,
						Animation = ext.Animation, Audio = ext.Audio, Contact = ext.Contact, Dice = ext.Dice, Document = ext.Document,
						Game = ext.Game, Giveaway = ext.Giveaway, GiveawayWinners = ext.GiveawayWinners, Invoice = ext.Invoice,
						Location = ext.Location, Photo = ext.Photo, Poll = ext.Poll, Sticker = ext.Sticker, Story = ext.Story,
						Venue = ext.Venue, Video = ext.Video, VideoNote = ext.VideoNote, Voice = ext.Voice, PaidMedia = ext.PaidMedia,
						Checklist = ext.Checklist, LivePhoto = ext.LivePhoto
					};
				}
				if (reply_to.quote_text != null)
					msg.Quote = new TextQuote
					{
						Text = reply_to.quote_text,
						Entities = MakeEntities(reply_to.quote_entities),
						Position = reply_to.quote_offset,
						IsManual = reply_to.flags.HasFlag(MessageReplyHeader.Flags.quote)
					};
				break;
			case MessageReplyStoryHeader mrsh:
				msg.ReplyToStory = new Story
				{
					Chat = await ChatFromPeer(mrsh.peer, true),
					Id = mrsh.story_id
				};
				break;
		}
		return msg;
	}

	private async Task<MessageOrigin?> MakeOrigin(MessageFwdHeader fwd)
	{
		MessageOrigin? origin = fwd.from_id switch
		{
			PeerUser pu => new MessageOriginUser { SenderUser = await UserOrResolve(pu.user_id) },
			PeerChat pc => new MessageOriginChat { SenderChat = await ChatOrResolve(pc.chat_id), AuthorSignature = fwd.post_author },
			PeerChannel pch => new MessageOriginChannel
			{
				Chat = await ChannelOrResolve(pch.channel_id),
				AuthorSignature = fwd.post_author,
				MessageId = fwd.channel_post
			},
			_ => fwd.from_name != null ? new MessageOriginHiddenUser { SenderUserName = fwd.from_name } : null
		};
		origin?.Date = fwd.date;
		return origin;
	}

	internal async Task<PollMedia?> ToPollMedia(MessageMedia? media)
	{
		if (media == null) return null;
		var pm = new PollMedia();
		switch (media)
		{
			case MessageMediaDocument { document: TL.Document document } mmd:
				var thumb = document.LargestThumbSize;
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.video))
					pm.Video = document.Video(mmd);
				else if (document.GetAttribute<DocumentAttributeAudio>() is { } audio)
					pm.Audio = document.Audio(audio);
				else if (document.GetAttribute<DocumentAttributeSticker>() is { } sticker)
					pm.Sticker = await MakeSticker(document, sticker);
				else if (document.GetAttribute<DocumentAttributeAnimated>() is { })
					pm.Animation = MakeAnimation(pm.Document!, document.GetAttribute<DocumentAttributeVideo>());
				else
					pm.Document = document.Document(thumb);
				break;
			case MessageMediaPhoto { photo: TL.Photo photo } mmp:
				if (mmp.video is TL.Document lpVideo)
					pm.LivePhoto = lpVideo.LivePhoto(photo.PhotoSizes());
				else
					pm.Photo = photo.PhotoSizes();
				break;
			case MessageMediaVenue mmv:
				pm.Venue = mmv.Venue();
				break;
			case MessageMediaGeo mmg:
				pm.Location = mmg.Location();
				break;
			case MessageMediaWebPage mmwp:
				pm.Link = mmwp.webpage.Url;
				break;
			default:
				return null;
		}
		return pm;
	}

	private static Animation MakeAnimation(Telegram.Bot.Types.Document msgDoc, DocumentAttributeVideo video) => new()
	{
		FileSize = msgDoc.FileSize,
		Width = video?.w ?? 0,
		Height = video?.h ?? 0,
		Duration = (int)(Math.Ceiling(video?.duration ?? 0.0)),
		Thumbnail = msgDoc.Thumbnail,
		FileName = msgDoc.FileName,
		MimeType = msgDoc.MimeType,
		FileId = msgDoc.FileId,
		FileUniqueId = msgDoc.FileUniqueId
	};

	private async Task<Telegram.Bot.Types.Poll> MakePoll(TL.Poll poll, PollResults pollResults)
	{
		int[]? correctOptionIds = pollResults.results?.Select((pav, i) => pav.flags.HasFlag(PollAnswerVoters.Flags.correct) ? i : -1).Where(i => i >= 0).ToArray();
		return new Telegram.Bot.Types.Poll
		{
			Id = poll.id.ToString(),
			Question = poll.question.text,
			QuestionEntities = MakeEntities(poll.question.entities),
			Options = await Task.WhenAll(poll.answers.Cast<TL.PollAnswer>().Select(async (pa, i) => new PollOption
			{
				Text = pa.text.text,
				VoterCount = pollResults.results?[i].voters ?? 0,
				PersistentId = pa.option,
				AdditionDate = pa.date.NullIfDefault(),
				AddedByUser = await UserFromPeer(pa.added_by),
				AddedByChat = await ChatFromPeer(pa.added_by),
				Media = await ToPollMedia(pa.media)
			})),
			TotalVoterCount = pollResults.total_voters,
			IsClosed = poll.flags.HasFlag(TL.Poll.Flags.closed),
			IsAnonymous = !poll.flags.HasFlag(TL.Poll.Flags.public_voters),
			Type = poll.flags.HasFlag(TL.Poll.Flags.quiz) ? PollType.Quiz : PollType.Regular,
			AllowsMultipleAnswers = poll.flags.HasFlag(TL.Poll.Flags.multiple_choice),
			CorrectOptionIds = correctOptionIds,
			Explanation = pollResults.solution,
			ExplanationEntities = MakeEntities(pollResults.solution_entities),
			OpenPeriod = poll.close_period.NullIfZero(),
			CloseDate = poll.close_date.NullIfDefault(),
			AllowsRevoting = !poll.flags.HasFlag(TL.Poll.Flags.revoting_disabled),
			ExplanationMedia = await ToPollMedia(pollResults.solution_media),
			MembersOnly = poll.flags.HasFlag(TL.Poll.Flags.subscribers_only),
			CountryCodes = poll.countries_iso2
		};
	}

	private async Task<TL.InputPollAnswer> MakePollAnswer(InputPollOption ipo, InputPeer peer)
	{
		var text = ipo.Text;
		var entities = ApplyParse(ipo.TextParseMode, ref text, ipo.TextEntities);
		return new()
		{
			text = new() { text = text, entities = entities },
			media = ipo.Media != null ? await InputPollMedia(peer, ipo.Media.Type, ipo.Media) : null,
			flags = ipo.Media != null ? TL.InputPollAnswer.Flags.has_media : 0
		};
	}
}
