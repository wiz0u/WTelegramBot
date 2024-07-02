using TL;
using System.Collections;

namespace WTelegram;

class BotCollectorPeer(Bot client) : Peer, WTelegram.IPeerCollector, IReadOnlyDictionary<long, TL.User>
{
	public override long ID => 0;
	protected override IPeerInfo? UserOrChat(Dictionary<long, TL.User> users, Dictionary<long, TL.ChatBase> chats)
	{
		if (users != null) Collect(users.Values);
		if (chats != null) Collect(chats.Values);
		return null;
	}

	public void Collect(IEnumerable<TL.User> users)
	{
		lock (client._users)
			foreach (var user in users)
				if (user != null)
					if (!user.flags.HasFlag(TL.User.Flags.min) || !client._users.TryGetValue(user.id, out var prevUser))
						client._users[user.id] = user.User();
					else
					{
						prevUser.FirstName ??= user.first_name;
						prevUser.LastName ??= user.last_name;
						if (user.lang_code != null) prevUser.LanguageCode = user.lang_code;
						if (prevUser.IsBot)
						{
							prevUser.CanJoinGroups = !user.flags.HasFlag(TL.User.Flags.bot_nochats);
							prevUser.CanReadAllGroupMessages = user.flags.HasFlag(TL.User.Flags.bot_chat_history);
							prevUser.SupportsInlineQueries = user.flags.HasFlag(TL.User.Flags.has_bot_inline_placeholder);
							prevUser.CanConnectToBusiness = user.flags2.HasFlag(TL.User.Flags2.bot_business);
						}
						client._users[user.id] = prevUser;
					}
	}
	public void Collect(IEnumerable<ChatBase> chats)
	{
		lock (client._chats)
			foreach (var chat in chats)
				if (chat is not Channel channel)
					client._chats[chat.ID] = chat.Chat();
				else if (!channel.flags.HasFlag(Channel.Flags.min) || !client._chats.TryGetValue(channel.id, out var prevChat))
					client._chats[channel.id] = channel.Chat();
				else
				{
					prevChat.Title = channel.title;
					prevChat.Username = channel.MainUsername;
					client._chats[channel.id] = prevChat;
				}
	}

	public bool HasUser(long id) { lock (client._users) return client._users.TryGetValue(id, out _); }
	public bool HasChat(long id) { lock (client._chats) return client._chats.TryGetValue(id, out _); }

	public TL.User this[long key] => throw new NotImplementedException();
	public IEnumerable<long> Keys => throw new NotImplementedException();
	public IEnumerable<TL.User> Values => throw new NotImplementedException();
	public int Count => throw new NotImplementedException();
	public bool ContainsKey(long key) => throw new NotImplementedException();
	public IEnumerator<KeyValuePair<long, TL.User>> GetEnumerator() => throw new NotImplementedException();
	IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
	public bool TryGetValue(long key, out TL.User value) // used only for fetching access_hash in Markdown/HtmlToToEntities
	{
		User? user;
		lock (client._users)
			if (!client._users.TryGetValue(key, out user)) { value = null!; return false; }
		value = new TL.User { id = key, access_hash = user.AccessHash };
		return true;
	}
}
