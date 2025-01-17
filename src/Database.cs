using System.Data.Common;
using System.Diagnostics;
using Telegram.Bot;
using TL;
using TL.Methods;
using Chat = WTelegram.Types.Chat;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

internal partial class Database : IDisposable, IBotStorage
{
    private readonly DbConnection _connection;
    private readonly DbCommand[] _cmd = new DbCommand[DefaultSqlCommands[0].Length];
    private Bot.State? _state;

    public Bot.State? State => _state;

    public string DataSource => _connection.DataSource;

    public void AssignBotState(Bot.State state)
    {
        lock (_cmd)
        {
            if (_state is not null)
                throw new InvalidOperationException("Cannot change the Bot.State after it has been initially set");
            _state = state;
        }
    }

    public Database(DbConnection connection, string[] sqlCommands)
    {
        if (sqlCommands.Length != DefaultSqlCommands[0].Length)
            throw new ArgumentException($"Expected {DefaultSqlCommands[0].Length} SQL commands", nameof(sqlCommands));
        _connection = connection;
        for (int i = 0; i < sqlCommands.Length; i++)
        {
            var command = _cmd[i] = _connection.CreateCommand();
            command.CommandText = sqlCommands[i];
            string defCmd = DefaultSqlCommands[0][i];
            for (int at = 0; (at = defCmd.IndexOf('@', at + 1)) > 0;)
            {
                var end = defCmd.IndexOfAny([',', ' ', ')', ';'], at + 1);
                var param = command.CreateParameter();
                param.ParameterName = defCmd[at..end];
                if (!command.Parameters.Contains(param.ParameterName))
                    command.Parameters.Add(param);
            }
        }
        connection.Open();
        _cmd[DbSetup].ExecuteNonQuery();
    }

    public void Dispose()
    {
        foreach (var cmd in _cmd) cmd.Dispose();
        _connection.Dispose();
    }

    public Stream LoadSessionState()
    {
        Debug.Assert(_state is not null);
        using (var reader = _cmd[LoadSession].ExecuteReader())
            if (reader.Read())
            {
                _state.SessionData = reader.GetValue(0) as byte[];
                _state.LastUpdateId = reader.GetInt32(1);
                _state.AllowedUpdates = reader.GetInt32(2);
            }
        return new IBotStorage.SessionStore(_state.SessionData, SaveSessionState);
    }

    public void SaveSessionState(byte[]? sessionData = null)
    {
        Debug.Assert(_state is not null);
        if (sessionData != null) _state.SessionData = sessionData;
        var cmd = _cmd[SaveSession];
        cmd.Parameters[0].Value = _state.SessionData;
        cmd.Parameters[1].Value = _state.LastUpdateId;
        cmd.Parameters[2].Value = _state.AllowedUpdates;
        cmd.ExecuteSave();
    }

    public IEnumerable<(int id, TL.Update update)> LoadTLUpdates()
    {
        using var reader = _cmd[LoadUpdates].ExecuteReader();
        while (reader.Read())
            using (var breader = new BinaryReader(reader.GetStream(1)))
                yield return (reader.GetInt32(0), (TL.Update)breader.ReadTLObject(0));
    }

    public void SaveTLUpdates(IEnumerable<Update> updates)
    {
        _cmd[DelUpdates].ExecuteNonQuery();
        var cmd = _cmd[SaveUpdates];
        using var memStream = new MemoryStream(1024);
        foreach (var botUpdate in updates)
        {
            if (botUpdate.TLUpdate == null) continue;
            memStream.SetLength(0);
            using (var writer = new BinaryWriter(memStream, System.Text.Encoding.UTF8, leaveOpen: true))
                botUpdate.TLUpdate.WriteTL(writer);
            cmd.Parameters[0].Value = botUpdate.Id;
            cmd.Parameters[1].Value = memStream.ToArray();
            cmd.ExecuteNonQuery();
        }
    }

    public Dictionary<long, UpdateManager.MBoxState> LoadMBoxStates()
    {
        using var reader = _cmd[LoadMBox].ExecuteReader();
        var result = new Dictionary<long, UpdateManager.MBoxState>();
        while (reader.Read())
            result[reader.GetInt64(0)] = new() { pts = reader.GetInt32(1), access_hash = reader.GetInt64(2) };
        return result;
    }

    public void SaveMBoxStates(IReadOnlyDictionary<long, UpdateManager.MBoxState> state)
    {
        var cmd = _cmd[SaveMBox];
        foreach (var mboxState in state)
        {
            cmd.Parameters[0].Value = mboxState.Key;
            cmd.Parameters[1].Value = mboxState.Value.pts;
            cmd.Parameters[2].Value = mboxState.Value.access_hash;
            cmd.ExecuteNonQuery();
        }
    }

    public void GetTables(out IBotStorage.ISetCache<User> users, out IBotStorage.ISetCache<Chat> chats)
    {
        users = new CachedTable<User>(DoLoadUser, DoSaveUser);
        chats = new CachedTable<Chat>(DoLoadChat, DoSaveChat);
    }

    private User? DoLoadUser(long id)
    {
        _cmd[LoadUser].Parameters[0].Value = id;
        using var reader = _cmd[LoadUser].ExecuteReader();
        if (!reader.Read()) return null;
        var flags = reader.GetInt32(1);
        return new User
        {
            Id = id,
            AccessHash = reader.GetInt64(0),
            FirstName = reader.GetString(2),
            LastName = reader.GetString(3).NullIfEmpty(),
            Username = reader.GetString(4).NullIfEmpty(),
            LanguageCode = reader.GetString(5).NullIfEmpty(),
            IsBot = (flags & 1) != 0,
            IsPremium = (flags & 2) != 0,
            AddedToAttachmentMenu = (flags & 4) != 0,
            CanJoinGroups = (flags & 8) != 0,
            CanReadAllGroupMessages = (flags & 16) != 0,
            SupportsInlineQueries = (flags & 32) != 0,
        };
    }

    private void DoSaveUser(User user)
    {
        var param = _cmd[SaveUser].Parameters;
        param[0].Value = user.Id;
        param[1].Value = user.AccessHash;
        param[2].Value = (user.IsBot ? 1 : 0) | (user.IsPremium == true ? 2 : 0) | (user.AddedToAttachmentMenu == true ? 4 : 0)
            | (user.CanJoinGroups == true ? 8 : 0) | (user.CanReadAllGroupMessages == true ? 16 : 0) | (user.SupportsInlineQueries == true ? 32 : 0);
        param[3].Value = user.FirstName;
        param[4].Value = user.LastName ?? "";
        param[5].Value = user.Username ?? "";
        param[6].Value = user.LanguageCode ?? "";
        _cmd[SaveUser].ExecuteSave();
    }

    private Chat? DoLoadChat(long id)
    {
        _cmd[LoadChat].Parameters[0].Value = id;
        using var reader = _cmd[LoadChat].ExecuteReader();
        if (!reader.Read()) return null;
        var flags = reader.GetInt32(1);
        var type = (ChatType)reader.GetInt32(5);
        var firstName = reader.GetString(2);
        return new Chat
        {
            Id = type == ChatType.Group ? -id : Bot.ZERO_CHANNEL_ID - id,
            AccessHash = reader.GetInt64(0),
            Type = type,
            Title = type == ChatType.Private ? null : firstName,
            FirstName = type == ChatType.Private ? firstName : null,
            LastName = type == ChatType.Private ? reader.GetString(3).NullIfEmpty() : null,
            Username = reader.GetString(4).NullIfEmpty(),
            IsForum = (flags & 1) != 0,
        };
    }

    private void DoSaveChat(Chat chat)
    {
        var param = _cmd[SaveChat].Parameters;
        param[0].Value = chat.Id;
        param[1].Value = chat.AccessHash;
        param[2].Value = chat.IsForum == true ? 1 : 0;
        param[3].Value = (chat.Type == ChatType.Private ? chat.FirstName : chat.Title) ?? "";
        param[4].Value = chat.LastName ?? "";
        param[5].Value = chat.Username ?? "";
        param[6].Value = chat.Type;
        _cmd[SaveChat].ExecuteSave();
    }

    internal class CachedTable<T>(Func<long, T?> load, Action<T> save) : IBotStorage.ISetCache<T> where T : class
    {
        private readonly Dictionary<long, T> _cache = [];

        public T this[long key] { set => save(_cache[key] = value); }
        public bool TryGetValue(long key, [MaybeNullWhen(false)] out T value)
        {
            if (_cache.TryGetValue(key, out value)) return true;
            value = load(key);
            if (value == null) return false;
            _cache[key] = value;
            return true;
        }

        public void ClearCache() => _cache.Clear();
        public T? SearchCache(Predicate<T> predicate)
        {
            foreach (var chat in _cache.Values)
                if (predicate(chat))
                    return chat;
            return null;
        }
    }
}

//TODO use bulk insert or selective update instead of full reupload of the tables
