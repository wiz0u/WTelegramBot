using System.Text;
using TL;
using Chat = WTelegram.Types.Chat;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

public partial class Bot
{
	private Message? FillRichMessage(Message msg, TL.RichMessage rich_message)
	{
		TextUrl? hasLink = null;
		HashSet<string>? refs = null;
		var blocks = rich_message.blocks.Select(RichBlock).ToArray();
		if (refs != null && hasLink != null) // 2nd pass to fix RichTextReferenceLink
			blocks = [.. rich_message.blocks.Select(RichBlock)];
		msg.RichMessage = new()
		{
			IsRtl = rich_message.flags.HasFlag(TL.RichMessage.Flags.rtl),
			Blocks = blocks
		};
		return msg;

		RichBlock RichBlock(PageBlock block) => block switch
		{
			PageBlockParagraph pb => new RichBlockParagraph { Text = RichText(pb.text) },
			PageBlockPreformatted pb => new RichBlockPreformatted { Text = RichText(pb.text), Language = pb.language.NullIfEmpty() },
			PageBlockFooter pb => new RichBlockFooter { Text = RichText(pb.text) },
			PageBlockDivider => new RichBlockDivider(),
			PageBlockAnchor pb => new RichBlockAnchor { Name = pb.name },
			PageBlockList pb => new RichBlockList { Items = [.. pb.items.Select(RichBlockListItem)] },
			PageBlockOrderedList pb when (pb.flags.HasFlag(PageBlockOrderedList.Flags.has_start) ? pb.start : 1) is var start
				=> new RichBlockList { Items = [.. pb.items.Select((oi, idx) => RichBlockListItemO(oi, pb, start + idx))] },
			PageBlockBlockquote pb => new RichBlockBlockQuotation { Blocks = RichBlockPara(pb.text), Credit = RichText(pb.caption) },
			PageBlockBlockquoteBlocks pb => new RichBlockBlockQuotation { Blocks = [.. pb.blocks.Select(RichBlock)], Credit = RichText(pb.caption) },
			PageBlockPullquote pb => new RichBlockPullQuotation { Text = RichText(pb.text), Credit = RichText(pb.caption) },
			PageBlockCollage pb => new RichBlockCollage { Blocks = [.. pb.items.Select(RichBlock)], Caption = RichBlockCaption(pb.caption) },
			PageBlockSlideshow pb => new RichBlockSlideshow { Blocks = [.. pb.items.Select(RichBlock)], Caption = RichBlockCaption(pb.caption) },
			PageBlockTable pb => new RichBlockTable
			{
				IsBordered = pb.flags.HasFlag(PageBlockTable.Flags.bordered),
				IsStriped = pb.flags.HasFlag(PageBlockTable.Flags.striped),
				Caption = RichText(pb.title)!,
				Cells = [.. pb.rows.Select(r => r.cells.Select(RichBlockTableCell).ToArray())]
			},
			PageBlockDetails pb => new RichBlockDetails { Summary = RichText(pb.title), Blocks = [.. pb.blocks.Select(RichBlock)], IsOpen = pb.flags.HasFlag(PageBlockDetails.Flags.open) },
			PageBlockMap pb => new RichBlockMap { Location = pb.geo.Location(), Zoom = pb.zoom, Width = pb.w, Height = pb.h, Caption = RichBlockCaption(pb.caption) },
			PageBlockHeading1 pb => new RichBlockSectionHeading { Text = RichText(pb.text), Size = 1 },
			PageBlockHeading2 pb => new RichBlockSectionHeading { Text = RichText(pb.text), Size = 2 },
			PageBlockHeading3 pb => new RichBlockSectionHeading { Text = RichText(pb.text), Size = 3 },
			PageBlockHeading4 pb => new RichBlockSectionHeading { Text = RichText(pb.text), Size = 4 },
			PageBlockHeading5 pb => new RichBlockSectionHeading { Text = RichText(pb.text), Size = 5 },
			PageBlockHeading6 pb => new RichBlockSectionHeading { Text = RichText(pb.text), Size = 6 },
			PageBlockMath pb => new RichBlockMathematicalExpression { Expression = pb.source },
			PageBlockThinking pb => new RichBlockThinking { Text = RichText(pb.text) },
			PageBlockPhoto pb when rich_message.photos.FirstOrDefault(d => d.ID == pb.photo_id) is TL.Photo photo
				=> new RichBlockPhoto { Photo = photo.PhotoSizes()!, Caption = RichBlockCaption(pb.caption), HasSpoiler = pb.flags.HasFlag(PageBlockPhoto.Flags.spoiler) },
			PageBlockVideo pb when rich_message.documents.FirstOrDefault(d => d.ID == pb.video_id) is TL.Document doc
				=> doc.GetAttribute<DocumentAttributeAnimated>() == null
					? new RichBlockVideo { Video = doc.Video(null), Caption = RichBlockCaption(pb.caption), HasSpoiler = pb.flags.HasFlag(PageBlockVideo.Flags.spoiler) }
					: new RichBlockAnimation
					{
						Animation = MakeAnimation(doc.Document(doc.LargestThumbSize)!, doc.GetAttribute<DocumentAttributeVideo>()),
						Caption = RichBlockCaption(pb.caption),
						HasSpoiler = pb.flags.HasFlag(PageBlockVideo.Flags.spoiler)
					},
			PageBlockAudio pb when rich_message.documents.FirstOrDefault(d => d.ID == pb.audio_id) is TL.Document doc && doc.GetAttribute<DocumentAttributeAudio>() is { } audio
				=> audio.flags.HasFlag(DocumentAttributeAudio.Flags.voice)
					? new RichBlockVoiceNote { VoiceNote = doc?.Voice(audio)!, Caption = RichBlockCaption(pb.caption) }
					: new RichBlockAudio { Audio = doc?.Audio(audio)!, Caption = RichBlockCaption(pb.caption) },
			_ => null!
		};

		[return: NotNullIfNotNull(nameof(text))]
		Telegram.Bot.Types.RichText? RichText(TL.RichText? text) => text switch
		{
			TextPlain t => new RichTextText { Text = t.text },
			TextConcat t => new RichTextArray { Array = [.. t.texts.Select(t => RichText(t))] },
			TextBold t => new RichTextBold { Text = RichText(t.text) },
			TextItalic t => new RichTextItalic { Text = RichText(t.text) },
			TextUnderline t => new RichTextUnderline { Text = RichText(t.text) },
			TextStrike t => new RichTextStrikethrough { Text = RichText(t.text) },
			TextSpoiler t => new RichTextSpoiler { Text = RichText(t.text) },
			TextSubscript t => new RichTextSubscript { Text = RichText(t.text) },
			TextSuperscript t => new RichTextSuperscript { Text = RichText(t.text) },
			TextMarked t => new RichTextMarked { Text = RichText(t.text) },
			TextFixed t => new RichTextCode { Text = RichText(t.text) },
			TextCustomEmoji t => new RichTextCustomEmoji { CustomEmojiId = t.document_id.ToString(), AlternativeText = t.alt },
			TextMath t => new RichTextMathematicalExpression { Expression = t.source },
			TextEmail t => new RichTextEmailAddress { Text = RichText(t.text), EmailAddress = t.email },
			TextPhone t => new RichTextPhoneNumber { Text = RichText(t.text), PhoneNumber = t.phone },
			TextMentionName t => new RichTextTextMention { Text = RichText(t.text), User = User(t.user_id)! },
			TextMention t => new RichTextMention { Text = RichText(t.text), Username = PlainText(t.text).TrimStart('@') },
			TextHashtag t => new RichTextHashtag { Text = RichText(t.text), Hashtag = PlainText(t.text).TrimStart('#') },
			TextCashtag t => new RichTextCashtag { Text = RichText(t.text), Cashtag = PlainText(t.text).TrimStart('$') },
			TextBotCommand t => new RichTextBotCommand { Text = RichText(t.text), BotCommand = PlainText(t.text).TrimStart('/') },
			TextAutoEmail t => new RichTextEmailAddress { Text = RichText(t.text), EmailAddress = PlainText(t.text) },
			TextAutoPhone t => new RichTextPhoneNumber { Text = RichText(t.text), PhoneNumber = PlainText(t.text) },
			TextAutoUrl t => new RichTextUrl { Text = RichText(t.text), Url = PlainText(t.text) },
			TextBankCard t => new RichTextBankCardNumber { Text = RichText(t.text), BankCardNumber = PlainText(t.text) },
			TextDate t => new RichTextDateTime { Text = RichText(t.text), UnixTime = t.date, DateTimeFormat = ((MessageEntityFormattedDate.Flags)t.flags).ToDateFormat() },
			TextAnchor t => t.text == null ? new RichTextAnchor { Name = t.name }
				: new RichTextReference { Name = (refs ??= []).Add(t.name) ? t.name : t.name, Text = RichText(t.text) },
			TextUrl t => t.url.StartsWith("#") ? MakeLink(hasLink = t) : new RichTextUrl { Text = RichText(t.text), Url = t.url },
			_ => null,
		};

		RichBlockCaption? RichBlockCaption(PageCaption caption) => caption is { credit: null, text: null } ? null
			: new RichBlockCaption { Text = RichText(caption.text)!, Credit = RichText(caption.credit) };

		RichBlockListItem RichBlockListItem(PageListItem li)
		{
			if (li is PageListItemText lit)
				return new RichBlockListItem
				{
					Label = "•",
					Blocks = RichBlockPara(lit.text),
					HasCheckbox = lit.flags.HasFlag(PageListItemText.Flags.checkbox),
					IsChecked = lit.flags.HasFlag(PageListItemText.Flags.checked_)
				};
			else if (li is PageListItemBlocks lib)
				return new RichBlockListItem
				{
					Label = "•",
					Blocks = [.. lib.blocks.Select(RichBlock)],
					HasCheckbox = lib.flags.HasFlag(PageListItemBlocks.Flags.checkbox),
					IsChecked = lib.flags.HasFlag(PageListItemBlocks.Flags.checked_)
				};
			throw new WTException("Unexpected PageListItem: " + li);
		}

		RichBlockListItem RichBlockListItemO(PageListOrderedItem li, PageBlockOrderedList pb, int next_value)
		{
			if (li is PageListOrderedItemText lit)
				return FixLabel(new RichBlockListItem
				{
					Blocks = RichBlockPara(lit.text),
					Label = lit.num,
					Type = lit.type ?? pb.type ?? "1",
					Value = lit.flags.HasFlag(PageListOrderedItemText.Flags.has_value) ? lit.value : next_value,
					HasCheckbox = lit.flags.HasFlag(PageListOrderedItemText.Flags.checkbox),
					IsChecked = lit.flags.HasFlag(PageListOrderedItemText.Flags.checked_)
				});
			else if (li is PageListOrderedItemBlocks lib)
				return FixLabel(new RichBlockListItem
				{
					Blocks = [.. lib.blocks.Select(RichBlock)],
					Label = lib.num,
					Type = lib.type ?? pb.type ?? "1",
					Value = lib.flags.HasFlag(PageListOrderedItemBlocks.Flags.has_value) ? lib.value : next_value,
					HasCheckbox = lib.flags.HasFlag(PageListOrderedItemBlocks.Flags.checkbox),
					IsChecked = lib.flags.HasFlag(PageListOrderedItemBlocks.Flags.checked_)
				});
			throw new WTException("Unexpected PageListOrderedItem: " + li);

			static RichBlockListItem FixLabel(RichBlockListItem item)
			{
				if (!string.IsNullOrEmpty(item.Label)) return item;
				int value = item.Value!.Value;
				var sb = new StringBuilder();
				if (item.Type is "A" or "a")
					for (; value-- > 0; value /= 26)
						sb.Insert(0, (char)(item.Type[0] + (value % 26)));
				else if (item.Type is "I" or "i")
				{
					for (; value >= 1000; value -= 1000) sb.Append('M');
					if (value >= 900) { sb.Append("CM"); value -= 900; }
					else if (value >= 500) { sb.Append('D'); value -= 500; }
					else if (value >= 400) { sb.Append("CD"); value -= 400; }
					for (; value >= 100; value -= 100) sb.Append('C');
					if (value >= 90) { sb.Append("XC"); value -= 90; }
					else if (value >= 50) { sb.Append('L'); value -= 50; }
					else if (value >= 40) { sb.Append("XL"); value -= 40; }
					for (; value >= 10; value -= 10) sb.Append('X');
					if (value >= 9) { sb.Append("IX"); value -= 9; }
					else if (value >= 5) { sb.Append('V'); value -= 5; }
					else if (value >= 4) { sb.Append("IV"); value -= 4; }
					for (; value >= 1; value--) sb.Append('I');
					if (item.Type is "i") for (int i = 0; i < sb.Length; i++) sb[i] = (char)(sb[i] | 0x20);
				}
				else
					sb.Append(value);
				item.Label = sb.Append('.').ToString();
				return item;
			}
		}

		Telegram.Bot.Types.RichText MakeLink(TextUrl t)
		{
			var name = t.url[1..];
			return refs?.Contains(name) == true ? new RichTextReferenceLink { Text = RichText(t.text), ReferenceName = name }
				: new RichTextAnchorLink { Text = RichText(t.text), AnchorName = name };
		}

		RichBlock[] RichBlockPara(TL.RichText text) => [new RichBlockParagraph { Text = RichText(text) }];

		RichBlockTableCell RichBlockTableCell(PageTableCell cell) => new()
		{
			Text = RichText(cell.text),
			IsHeader = cell.flags.HasFlag(PageTableCell.Flags.header),
			Rowspan = cell.flags.HasFlag(PageTableCell.Flags.has_rowspan) ? cell.rowspan : null,
			Colspan = cell.flags.HasFlag(PageTableCell.Flags.has_colspan) ? cell.colspan : null,
			Align = cell.flags.HasFlag(PageTableCell.Flags.align_center) ? RichBlockTableCellAlign.Center
				 : cell.flags.HasFlag(PageTableCell.Flags.align_right) ? RichBlockTableCellAlign.Right : RichBlockTableCellAlign.Left,
			Valign = cell.flags.HasFlag(PageTableCell.Flags.valign_middle) ? RichBlockTableCellValign.Middle
				: cell.flags.HasFlag(PageTableCell.Flags.valign_bottom) ? RichBlockTableCellValign.Bottom : RichBlockTableCellValign.Top
		};

		static string PlainText(TL.RichText rt) => rt switch
		{
			TextPlain t => t.text, TextCustomEmoji t => t.alt, TextMath t => t.source,
			TextConcat t => string.Concat(t.texts.Select(PlainText)),
			TextBold t => PlainText(t.text), TextItalic t => PlainText(t.text), TextUnderline t => PlainText(t.text), TextStrike t => PlainText(t.text),
			TextSpoiler t => PlainText(t.text), TextSubscript t => PlainText(t.text), TextSuperscript t => PlainText(t.text), TextMarked t => PlainText(t.text),
			TextFixed t => PlainText(t.text), TextEmail t => PlainText(t.text), TextPhone t => PlainText(t.text), TextMentionName t => PlainText(t.text),
			TextMention t => PlainText(t.text), TextHashtag t => PlainText(t.text), TextCashtag t => PlainText(t.text), TextBotCommand t => PlainText(t.text),
			TextAutoEmail t => PlainText(t.text), TextAutoPhone t => PlainText(t.text), TextAutoUrl t => PlainText(t.text), TextBankCard t => PlainText(t.text),
			TextDate t => PlainText(t.text), TextAnchor t => PlainText(t.text), TextUrl t => PlainText(t.text),
			_ => "",
		};
	}
}
