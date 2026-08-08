using TL;

namespace WTelegram;

public partial class Bot
{
	//https://github.com/tdlib/td/blob/66c4751742d2ca810033b289fc57ab4f83cfc833/td/telegram/StarManager.cpp?plain=1#L272
	//https://github.com/tdlib/telegram-bot-api/blob/53e15345b04fcea73b415897f10d7543005044ce/telegram-bot-api/Client.cpp?plain=1#L4241
	internal StarTransaction MakeStarTransaction(TL.StarsTransaction transaction)
	{
		var is_purchase = transaction.amount.IsPositive() == transaction.flags.HasFlag(StarsTransaction.Flags.refund);
		TransactionPartner? partner = transaction.peer switch
		{
			StarsTransactionPeerFragment => transaction.flags.HasFlag(StarsTransaction.Flags.gift)
				? null                                                                      //td_api::starTransactionTypeUserDeposit
				: new TransactionPartnerFragment { WithdrawalState = WithdrawalState() },   //td_api::starTransactionTypeFragmentWithdrawal or starTransactionTypeFragmentDeposit
			StarsTransactionPeer { peer: PeerChannel { channel_id: var channel_id } } => transaction switch
			{
				{ stargift: StarGift starGift } when is_purchase
					=> new TransactionPartnerChat { Chat = Chat(channel_id)!,               //td_api::starTransactionTypeGiftPurchase (dialog_id)
						Gift = MakeGift(starGift) },
				_ => null
			},
			StarsTransactionPeer { peer: PeerUser { user_id: var user_id } } => transaction switch
			{
				{ starref_commission_permille: not 0 } =>
					new TransactionPartnerAffiliateProgram { SponsorUser = User(user_id)!,	//td_api::starTransactionTypeAffiliateProgramCommission
						CommissionPerMille = transaction.starref_commission_permille },
				{ flags: var flags } when flags.HasFlag(StarsTransaction.Flags.business_transfer) => is_purchase ? null :
					new TransactionPartnerUser { User = User(user_id)!,                     //td_api::starTransactionTypeBusinessBotTransferReceive
						TransactionType = TransactionPartnerUserTransactionType.BusinessAccountTransfer },
				{ stargift: StarGift starGift } => is_purchase
					? new TransactionPartnerUser { User = User(user_id)!,                   //td_api::starTransactionTypeGiftPurchase (user_id)
						TransactionType = TransactionPartnerUserTransactionType.GiftPurchase,
						Gift = MakeGift(starGift) }
					: null,                                                                 //td_api::starTransactionTypeGiftSale
				{ subscription_period: > 0 } =>
					new TransactionPartnerUser { User = User(user_id)!,						//td_api::starTransactionTypeBotSubscriptionSale
						TransactionType = TransactionPartnerUserTransactionType.InvoicePayment,
						InvoicePayload = transaction.bot_payload.NullOrUtf8(),
						Affiliate = Affiliate(transaction),
						SubscriptionPeriod = transaction.subscription_period },
				{ premium_gift_months: > 0 } when is_purchase =>
					new TransactionPartnerUser { User = User(user_id)!,						//td_api::starTransactionTypePremiumPurchase,
						TransactionType = TransactionPartnerUserTransactionType.PremiumPurchase,
						PremiumSubscriptionDuration = transaction.premium_gift_months },
				{ title: null, description: null, photo: null } or { extended_media.Length: > 0 } =>
					new TransactionPartnerUser { User = User(user_id)!,						//td_api::starTransactionTypeBotPaidMediaSale
						TransactionType = TransactionPartnerUserTransactionType.PaidMediaPayment,
						Affiliate = Affiliate(transaction),
						PaidMedia = transaction.extended_media?.Select(Converters.PaidMedia).ToArray(),
						PaidMediaPayload = transaction.bot_payload.NullOrUtf8() },
				_ => new TransactionPartnerUser { User = User(user_id)!,					//td_api::starTransactionTypeBotInvoiceSale
						TransactionType = TransactionPartnerUserTransactionType.InvoicePayment,
						Affiliate = Affiliate(transaction),
						InvoicePayload = transaction.bot_payload.NullOrUtf8() }
			},
			StarsTransactionPeerAds => new TransactionPartnerTelegramAds(),                 //td_api::starTransactionTypeTelegramAdsWithdrawal
			StarsTransactionPeerAPI => new TransactionPartnerTelegramApi {					//td_api::starTransactionTypeTelegramApiUsage
						RequestCount = transaction.floodskip_number },
			_ => null,
		};
		return new StarTransaction
		{
			Id = transaction.id,
			Amount = Math.Abs(transaction.amount.Amount),
			NanostarAmount = Math.Abs((transaction.amount as StarsAmount)?.nanos ?? 0).NullIfZero(),
			Date = transaction.date,
			Source = transaction.amount.IsPositive() ? partner ?? new TransactionPartnerOther() : null,
			Receiver = transaction.amount.IsPositive() ? null : partner ?? new TransactionPartnerOther(),
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

	internal Gift MakeGift(TL.StarGift gift) => new()
	{
		Id = gift.id.ToString(),
		Sticker = gift.sticker is TL.Document doc ? MakeSticker(doc, doc.GetAttribute<DocumentAttributeSticker>(), sync: true).Result : null!,
		StarCount = gift.stars,
		UpgradeStarCount = gift.upgrade_stars.NullIfNegative(),
		IsPremium = gift.flags.HasFlag(StarGift.Flags.require_premium),
		HasColors = gift.flags.HasFlag(StarGift.Flags.peer_color_available),
		TotalCount = gift.availability_total > 0 ? gift.availability_total : null,
		RemainingCount = gift.availability_total > 0 ? gift.availability_remains : null,
		PersonalTotalCount = gift.per_user_total > 0 ? gift.per_user_total : null,
		PersonalRemainingCount = gift.per_user_total > 0 ? gift.per_user_remains : null,
		Background = gift.background?.GiftBackground(),
		UniqueGiftVariantCount = gift.upgrade_variants.NullIfZero(),
		PublisherChat = gift.released_by is PeerChannel pch ? Chat(pch.channel_id) : null,
	};

	internal async Task<UniqueGift> MakeUniqueGift(TL.StarGiftUnique sgu) => new()
	{
		GiftId = sgu.id.ToString(),
		BaseName = sgu.title,
		Name = sgu.slug,
		Number = sgu.num,
		Model = await UniqueGiftModel(sgu.attributes.OfType<StarGiftAttributeModel>().First()),
		Symbol = await UniqueGiftSymbol(sgu.attributes.OfType<StarGiftAttributePattern>().First()),
		Backdrop = UniqueGiftBackdrop(sgu.attributes.OfType<StarGiftAttributeBackdrop>().First()),
		IsPremium = sgu.flags.HasFlag(StarGiftUnique.Flags.require_premium),
		IsFromBlockchain = sgu.flags.HasFlag(StarGiftUnique.Flags.has_host_id),
		IsBurned = sgu.flags.HasFlag(StarGiftUnique.Flags.burned),
		Colors = (sgu.peer_color as PeerColorCollectible)?.UniqueGiftColors(),
		PublisherChat = sgu.released_by is PeerChannel pch ? Chat(pch.channel_id) : null,
	};

	internal AffiliateInfo? Affiliate(TL.StarsTransaction transaction)
		=> transaction.starref_commission_permille is > 0 and < 1000 && transaction.starref_peer != null ?
			new AffiliateInfo
			{
				AffiliateChat = transaction.starref_peer is PeerChannel pc ? Chat(pc.channel_id) : null,
				AffiliateUser = transaction.starref_peer is PeerUser pu ? User(pu.user_id) : null,
				CommissionPerMille = transaction.starref_commission_permille,
				Amount = (int)transaction.starref_amount.Amount,
				NanostarAmount = (transaction.starref_amount as StarsAmount)?.nanos,
			} : null;

	private async Task<InputSavedStarGift> InputSavedStarGift(string giftId)
	{
		await InitComplete();
		if (giftId.IndexOf('_') is >= 0 and int underscore_pos)
			return new InputSavedStarGiftChat
			{
				peer = await InputPeerChat(long.Parse(giftId[..underscore_pos])),
				saved_id = long.Parse(giftId[(underscore_pos + 1)..])
			};
		else
			return new InputSavedStarGiftUser { msg_id = int.Parse(giftId) };
	}

	private async Task<OwnedGift> OwnedGift(SavedStarGift gift) => gift.gift switch
	{
		StarGiftUnique sgu => new OwnedGiftUnique
		{
			OwnedGiftId = gift.msg_id.ToString(),
			Gift = await MakeUniqueGift(sgu),
			SenderUser = User(gift.from_id.ID),
			SendDate = gift.date,
			IsSaved = !gift.flags.HasFlag(SavedStarGift.Flags.unsaved),
			CanBeTransferred = gift.flags.HasFlag(SavedStarGift.Flags.has_transfer_stars),
			TransferStarCount = gift.transfer_stars.NullIfNegative(),
			NextTransferDate = gift.can_transfer_at.NullIfDefault(),
		},
		StarGift sg => new OwnedGiftRegular
		{
			OwnedGiftId = gift.msg_id.ToString(),
			Gift = MakeGift(sg),
			SenderUser = User(gift.from_id.ID),
			SendDate = gift.date,
			IsSaved = !gift.flags.HasFlag(SavedStarGift.Flags.unsaved),
			Text = gift.message?.text,
			Entities = MakeEntities(gift.message?.entities),
			IsPrivate = gift.flags.HasFlag(SavedStarGift.Flags.name_hidden),
			CanBeUpgraded = gift.flags.HasFlag(SavedStarGift.Flags.can_upgrade),
			WasRefunded = gift.flags.HasFlag(SavedStarGift.Flags.refunded),
			ConvertStarCount = gift.convert_stars.NullIfNegative(),
			PrepaidUpgradeStarCount = gift.upgrade_stars.NullIfNegative(),
			IsUpgradeSeparate = gift.flags.HasFlag(SavedStarGift.Flags.upgrade_separate),
			UniqueGiftNumber = gift.gift_num.NullIfZero(),
		},
		_ => null!,
	};

	private async Task<UniqueGiftModel> UniqueGiftModel(StarGiftAttributeModel model) => new()
	{
		Name = model.name,
		Sticker = await MakeSticker((TL.Document)model.document),
		RarityPerMille = (model.rarity as StarGiftAttributeRarity)?.permille ?? 0,
		Rarity = model.rarity.Rarity()
	};

	private async Task<UniqueGiftSymbol> UniqueGiftSymbol(StarGiftAttributePattern pattern) => new()
	{
		Name = pattern.name,
		Sticker = await MakeSticker((TL.Document)pattern.document),
		RarityPerMille = (pattern.rarity as StarGiftAttributeRarity)?.permille ?? 0,
	};

	private static UniqueGiftBackdrop UniqueGiftBackdrop(StarGiftAttributeBackdrop backdrop) => new()
	{
		Name = backdrop.name,
		Colors = new UniqueGiftBackdropColors()
		{
			CenterColor = backdrop.center_color,
			EdgeColor = backdrop.edge_color,
			SymbolColor = backdrop.pattern_color,
			TextColor = backdrop.text_color
		},
		RarityPerMille = (backdrop.rarity as StarGiftAttributeRarity)?.permille ?? 0,
	};
}