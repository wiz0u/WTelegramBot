using TL;

namespace WTelegram;

public partial class Bot
{
	private async Task<InputRichFile> InputRichFile(InputPeer? peer, string id, IInputRichMedia media)
	{
		if (media is InputMedia medium)
		{
			if (peer == null && medium.Media.FileType != FileType.Id) throw new WTException("Only FileId media allowed in Inline messages");
			var ism = await InputSingleMedia(peer!, medium);
			if (medium.Media.FileType != FileType.Id) // External or Uploaded
				ism.media = (await Client.Messages_UploadMedia(peer, ism.media)).ToInputMedia();
			if (ism.media is TL.InputMediaPhoto imp)
				return new InputRichFilePhoto { id = id, photo = imp.id };
			else if (ism.media is TL.InputMediaDocument imd)
				return new InputRichFileDocument { id = id, document = imd.id };
		}
		else if (media is InputMediaVoiceNote imvn)
		{
			if (peer == null && imvn.Media.FileType != FileType.Id) throw new WTException("Only FileId media allowed in Inline messages");
			var imedia = await InputMediaDocument(imvn.Media);
			if (imedia is TL.InputMediaUploadedDocument doc)
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeAudio {
					duration = imvn.Duration, flags = DocumentAttributeAudio.Flags.voice }];
			if (imvn.Media.FileType != FileType.Id) // External or Uploaded
				imedia = (await Client.Messages_UploadMedia(peer, imedia)).ToInputMedia();
			if (imedia is TL.InputMediaDocument imd)
				return new InputRichFileDocument { id = id, document = imd.id };
		}
		throw new WTException("Unexpected InputMedia type for InputRichMessageMedia");
	}

	// peer may be null for inline message, in which case uploaded/external media are not allowed in the rich message
	internal async Task<InputRichMessageBase?> InputRichMessage(Telegram.Bot.Types.InputRichMessage? richMessage, InputPeer? peer = null)
	{
		if (richMessage == null) return null;
		InputRichFile[]? files = null;
		if (richMessage.Media != null)
			files = await Task.WhenAll(richMessage.Media.Select(m => InputRichFile(peer, m.Id, m.Media)));
		if (richMessage.Html != null)
		{
			var html = richMessage.Html;
			files ??= Converters.ParseRichHtmlFileIds(ref html);
			return new InputRichMessageHTML
			{
				html = html,
				files = files,
				flags = (richMessage.IsRtl ? InputRichMessageHTML.Flags.rtl : 0)
					| (files != null ? InputRichMessageHTML.Flags.has_files : 0)
					| (richMessage.SkipEntityDetection ? InputRichMessageHTML.Flags.noautolink : 0)
			};
		}
		else if (richMessage.Markdown != null)
			return new InputRichMessageMarkdown
			{
				markdown = richMessage.Markdown,
				files = files,
				flags = (richMessage.IsRtl ? InputRichMessageMarkdown.Flags.rtl : 0)
					| (files != null ? InputRichMessageMarkdown.Flags.has_files : 0)
					| (richMessage.SkipEntityDetection ? InputRichMessageMarkdown.Flags.noautolink : 0)
			};
		else if (richMessage.Blocks != null)
		{
			List<TL.InputDocument> documents = [];
			List<TL.InputPhoto> photos = [];
			List<TL.InputUserBase> users = [];
			return new TL.InputRichMessage
			{
				blocks = await Task.WhenAll(richMessage.Blocks.Select(PageBlock)),
				documents = [.. documents],
				photos = [.. photos],
				users = [.. users],
				flags = (richMessage.IsRtl ? TL.InputRichMessage.Flags.rtl : 0)
					| (richMessage.SkipEntityDetection ? TL.InputRichMessage.Flags.noautolink : 0)
					| (photos.Count > 0 ? TL.InputRichMessage.Flags.has_photos : 0)
					| (documents.Count > 0 ? TL.InputRichMessage.Flags.has_documents : 0)
					| (users.Count > 0 ? TL.InputRichMessage.Flags.has_users : 0)
			};

			async Task<long> AddMedia(IInputRichMedia media) => await InputRichFile(peer, "", media) switch
			{
				InputRichFilePhoto p => photos.AddAndReturn(p.photo).id,
				InputRichFileDocument d => documents.AddAndReturn(d.document).id,
				_ => throw new WTException("Unexpected InputRichFile type for InputRichBlock*")
			};

			async Task<PageBlock> PageBlock(InputRichBlock block) => block switch
			{
				InputRichBlockParagraph irb => new PageBlockParagraph { text = TLRichText(irb.Text) },
				InputRichBlockPreformatted irb => new PageBlockPreformatted { text = TLRichText(irb.Text), language = irb.Language ?? "" },
				InputRichBlockFooter irb => new PageBlockFooter { text = TLRichText(irb.Text) },
				InputRichBlockDivider => new PageBlockDivider(),
				InputRichBlockAnchor irb => new PageBlockAnchor { name = irb.Name },
				InputRichBlockList irb => await PageBlockList(irb),
				InputRichBlockPullQuotation irb => new PageBlockPullquote { text = TLRichText(irb.Text), caption = TLRichText(irb.Credit) },
				InputRichBlockBlockQuotation irb => new PageBlockBlockquoteBlocks { blocks = await Task.WhenAll(irb.Blocks.Select(PageBlock)), caption = TLRichText(irb.Credit) },
				InputRichBlockCollage irb => new PageBlockCollage { items = await Task.WhenAll(irb.Blocks.Select(PageBlock)), caption = PageCaption(irb.Caption) },
				InputRichBlockSlideshow irb => new PageBlockSlideshow { items = await Task.WhenAll(irb.Blocks.Select(PageBlock)), caption = PageCaption(irb.Caption) },
				InputRichBlockTable irb => new PageBlockTable
				{
					flags = (irb.IsBordered ? PageBlockTable.Flags.bordered : 0) | (irb.IsStriped ? PageBlockTable.Flags.striped : 0),
					title = TLRichText(irb.Caption),
					rows = [.. irb.Cells.Select(r => new PageTableRow { cells = [.. r.Select(PageTableCell)] })]
				},
				InputRichBlockDetails irb => new PageBlockDetails { title = TLRichText(irb.Summary), blocks = await Task.WhenAll(irb.Blocks.Select(PageBlock)), flags = irb.IsOpen ? PageBlockDetails.Flags.open : 0 },
				InputRichBlockMap irb => new PageBlockMap { geo = irb.Location.GeoPoint(), zoom = irb.Zoom, w = irb.Width, h = irb.Height, caption = PageCaption(irb.Caption) },
				InputRichBlockSectionHeading irb => irb.Size switch {
					1 => new PageBlockHeading1 { text = TLRichText(irb.Text) },
					2 => new PageBlockHeading2 { text = TLRichText(irb.Text) },
					3 => new PageBlockHeading3 { text = TLRichText(irb.Text) },
					4 => new PageBlockHeading4 { text = TLRichText(irb.Text) },
					5 => new PageBlockHeading5 { text = TLRichText(irb.Text) },
					_ => new PageBlockHeading6 { text = TLRichText(irb.Text) }},
				InputRichBlockMathematicalExpression irb => new PageBlockMath { source = irb.Expression },
				InputRichBlockThinking irb => new PageBlockThinking { text = TLRichText(irb.Text) },
				InputRichBlockPhoto irb => new PageBlockPhoto { photo_id = await AddMedia(irb.Photo), caption = PageCaption(irb.Caption), flags = irb.Photo.HasSpoiler ? PageBlockPhoto.Flags.spoiler : 0 },
				InputRichBlockVideo irb => new PageBlockVideo { video_id = await AddMedia(irb.Video), caption = PageCaption(irb.Caption), flags = irb.Video.HasSpoiler ? PageBlockVideo.Flags.spoiler : 0 },
				InputRichBlockAnimation irb => new PageBlockVideo { video_id = await AddMedia(irb.Animation), caption = PageCaption(irb.Caption), flags = irb.Animation.HasSpoiler ? PageBlockVideo.Flags.spoiler : 0 },
				InputRichBlockVoiceNote irb => new PageBlockAudio { audio_id = await AddMedia(irb.VoiceNote), caption = PageCaption(irb.Caption) },
				InputRichBlockAudio irb => new PageBlockAudio { audio_id = await AddMedia(irb.Audio), caption = PageCaption(irb.Caption) },
				_ => null!
			};

			PageCaption? PageCaption(RichBlockCaption? caption) => caption is null ? new PageCaption()
				: new PageCaption { text = TLRichText(caption.Text), credit = TLRichText(caption.Credit) };

			TL.RichText? TLRichText(Telegram.Bot.Types.RichText? text) => text switch
			{
				RichTextText t => new TextPlain { text = t.Text },
				RichTextArray t => new TextConcat { texts = [.. t.Array.Select(t => TLRichText(t))] },
				RichTextBold t => new TextBold { text = TLRichText(t.Text) },
				RichTextItalic t => new TextItalic { text = TLRichText(t.Text) },
				RichTextUnderline t => new TextUnderline { text = TLRichText(t.Text) },
				RichTextStrikethrough t => new TextStrike { text = TLRichText(t.Text) },
				RichTextSpoiler t => new TextSpoiler { text = TLRichText(t.Text) },
				RichTextSubscript t => new TextSubscript { text = TLRichText(t.Text) },
				RichTextSuperscript t => new TextSuperscript { text = TLRichText(t.Text) },
				RichTextMarked t => new TextMarked { text = TLRichText(t.Text) },
				RichTextCode t => new TextFixed { text = TLRichText(t.Text) },
				RichTextCustomEmoji t => new TextCustomEmoji { document_id = long.Parse(t.CustomEmojiId), alt = t.AlternativeText },
				RichTextMathematicalExpression t => new TextMath { source = t.Expression },
				RichTextEmailAddress t => new TextEmail { text = TLRichText(t.Text), email = t.EmailAddress },
				RichTextPhoneNumber t => new TextPhone { text = TLRichText(t.Text), phone = t.PhoneNumber },
				RichTextUrl t => new TextUrl { text = TLRichText(t.Text), url = t.Url },
				RichTextReferenceLink t => new TextUrl { text = TLRichText(t.Text), url = '#' + t.ReferenceName },
				RichTextAnchorLink t => new TextUrl { text = TLRichText(t.Text), url = '#' + t.AnchorName },
				RichTextTextMention t => new TextMentionName { text = TLRichText(t.Text), user_id = users.AddAndReturn(InputUser(t.User.Id)).UserId ?? 0 },
				RichTextMention t => new TextMention { text = TLRichText(t.Text) ?? new TextPlain { text = '@' + t.Username } },
				RichTextHashtag t => new TextHashtag { text = TLRichText(t.Text) ?? new TextPlain { text = '#' + t.Hashtag } },
				RichTextCashtag t => new TextCashtag { text = TLRichText(t.Text) ?? new TextPlain { text = '$' + t.Cashtag } },
				RichTextBotCommand t => new TextBotCommand { text = TLRichText(t.Text) ?? new TextPlain { text = '/' + t.BotCommand } },
				RichTextBankCardNumber t => new TextBankCard { text = TLRichText(t.Text) ?? new TextPlain { text = t.BankCardNumber } },
				RichTextDateTime t => new TextDate { text = TLRichText(t.Text), date = t.UnixTime, flags = (TextDate.Flags)TL.HtmlText.ToDateFlags(t.DateTimeFormat) },
				RichTextAnchor t => new TextAnchor {  name = t.Name },
				RichTextReference t => new TextAnchor { text = TLRichText(t.Text), name = t.Name },
				_ => null,
			};

			async Task<PageBlock> PageBlockList(InputRichBlockList irb)
			{
				PageBlockOrderedList? ol = null;
				PageBlockList? ul = null;
				List<PageListItem>? uli = null;
				List<PageListOrderedItem>? oli = null;
				foreach (var item in irb.Items)
				{
					if (ul == null && ol == null)
						if (item.Value == null) (uli, ul) = ([], new());
						else (oli, ol) = ([], new PageBlockOrderedList
						{
							start = item.Value.Value,
							type = item.Type,
							flags = PageBlockOrderedList.Flags.has_start | (item.Type != null ? PageBlockOrderedList.Flags.has_type : 0)
						});
					if (uli != null)
						uli.Add(new PageListItemBlocks
						{
							blocks = await Task.WhenAll(item.Blocks.Select(PageBlock)),
							flags = (item.HasCheckbox ? PageListItemBlocks.Flags.checkbox : 0) | (item.IsChecked ? PageListItemBlocks.Flags.checked_ : 0),
						});
					else //if (oli != null)
						oli!.Add(new PageListOrderedItemBlocks
						{
							blocks = await Task.WhenAll(item.Blocks.Select(PageBlock)),
							value = item.Value ?? 0,
							type = item.Type,
							flags = (item.HasCheckbox ? PageListOrderedItemBlocks.Flags.checkbox : 0) | (item.IsChecked ? PageListOrderedItemBlocks.Flags.checked_ : 0)
								| (item.Value.HasValue ? PageListOrderedItemBlocks.Flags.has_value : 0) | (item.Type != null ? PageListOrderedItemBlocks.Flags.has_type : 0),
						});
				}
				if (ul != null) { ul.items = [.. uli!]; return ul; }
				else if (ol != null) { ol.items = [.. oli!]; return ol; }
				else return new PageBlockList();
			}

			PageTableCell PageTableCell(RichBlockTableCell cell) => new()
			{
				text = TLRichText(cell.Text),
				rowspan = cell.Rowspan ?? 0,
				colspan = cell.Colspan ?? 0,
				flags = (cell.IsHeader ? TL.PageTableCell.Flags.header : 0) | (cell.Text != null ? TL.PageTableCell.Flags.has_text : 0)
				| (cell.Rowspan > 0 ? TL.PageTableCell.Flags.has_rowspan : 0) | (cell.Colspan > 0 ? TL.PageTableCell.Flags.has_colspan : 0)
				| (cell.Align == RichBlockTableCellAlign.Center ? TL.PageTableCell.Flags.align_center : cell.Align == RichBlockTableCellAlign.Right ? TL.PageTableCell.Flags.align_right : 0)
				| (cell.Valign == RichBlockTableCellValign.Middle ? TL.PageTableCell.Flags.valign_middle : cell.Valign == RichBlockTableCellValign.Bottom ? TL.PageTableCell.Flags.valign_bottom : 0)
			};
		}
		throw new RpcException(500, $"Invalid InputRichMessage");
	}
}