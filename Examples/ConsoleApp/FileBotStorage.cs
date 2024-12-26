using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WTelegram;

namespace ConsoleApp;
internal class FileBotStorage(string directory) : IBotStorage
{
    public Bot.State? State { get; private set; }
    public string DataSource { get; } = $"File Directory: {directory}";
    public string StorageDirectory { get; } = directory;

    public void Dispose() { }

    public void GetTables(out IBotStorage.ISetCache<WTelegram.Types.User> users, out IBotStorage.ISetCache<WTelegram.Types.Chat> chats)
    {
        Directory.CreateDirectory(StorageDirectory);
        users = new FileSet<WTelegram.Types.User>(this);
        chats = new FileSet<WTelegram.Types.Chat>(this);
    }

    public Dictionary<long, UpdateManager.MBoxState> LoadMBoxStates()
    {
        Directory.CreateDirectory(StorageDirectory);
        var f = Path.Combine(StorageDirectory, "mbox.json");
        if (File.Exists(f))
        {
            using var stream = File.OpenRead(f);
            return JsonSerializer.Deserialize<Dictionary<long, UpdateManager.MBoxState>>(stream)!;
        }
        return [];
    }

    public Stream LoadSessionState()
    {
        Debug.Assert(State is not null);
        Directory.CreateDirectory(StorageDirectory);
        var f = Path.Combine(StorageDirectory, "session");
        using var stream = File.Open(f, FileMode.OpenOrCreate);
        Bot.StateBuffer buffer;
        try
        {
            buffer = JsonSerializer.Deserialize<Bot.StateBuffer>(stream);
        }
        catch (JsonException)
        {
            buffer = Bot.StateBuffer.Empty;
        }

        State.LoadState(buffer);
        return State.CreateSessionStoreStream(SaveSessionState);
    }

    public record struct TLUpdateBuffer(int Id, TL.Update Update)
    {
        public static implicit operator (int id, TL.Update update)(TLUpdateBuffer buffer)
            => (buffer.Id, buffer.Update);

        public (int id, TL.Update update) ToTuple()
            => (Id, Update);
    }

    public IEnumerable<(int id, TL.Update update)> LoadTLUpdates()
    {
        Directory.CreateDirectory(StorageDirectory);
        var f = Path.Combine(StorageDirectory, "updates.json");
        if (File.Exists(f))
        {
            using var stream = File.OpenRead(f);
            return JsonSerializer.Deserialize<IEnumerable<TLUpdateBuffer>>(stream)!.Select(x => x.ToTuple());
        }
        return [];
    }

    public void SaveMBoxStates(IReadOnlyDictionary<long, UpdateManager.MBoxState> state)
    {
        Directory.CreateDirectory(StorageDirectory);
        var f = Path.Combine(StorageDirectory, "mbox.json");
        using var stream = File.Open(f, FileMode.Create);
        JsonSerializer.Serialize(stream, state);
    }

    public void SaveSessionState(byte[]? sessionData = null)
    {
        Directory.CreateDirectory(StorageDirectory);
        var f = Path.Combine(StorageDirectory, "session");
        var sess = sessionData ?? State?.SessionDataMemory;
        if (sess is not ReadOnlyMemory<byte> toWrite)
        {
            if (File.Exists(f))
                File.Delete(f);
            return;
        }

        using var stream = File.Open(f, FileMode.Create);
        var span = toWrite.Span;
        for (int i = 0; i < span.Length; i++)
            stream.WriteByte(span[i]);
    }

    public void SaveTLUpdates(IEnumerable<WTelegram.Types.Update> updates)
    {
        Directory.CreateDirectory(StorageDirectory);
        var f = Path.Combine(StorageDirectory, "updates.json");
        using var stream = File.Open(f, FileMode.Create);
        JsonSerializer.Serialize(stream, updates.Select(x => new TLUpdateBuffer(x.Id, x.TLUpdate!)));
    }

    public void AssignBotState(Bot.State state)
    {
        if (State is not null)
            throw new InvalidOperationException($"Cannot assign the Bot.State to this storage more than once");
        State = state;
    }

    public class FileSet<T> : IBotStorage.ISetCache<T>
        where T : class
    {
        private readonly Dictionary<long, T> cache = [];
        private readonly FileBotStorage storage;
        private readonly string dir;
        internal FileSet(FileBotStorage storage) 
        { 
            this.storage = storage;
            dir = Path.Combine(storage.StorageDirectory, typeof(T).FullName!);
        }

        public string GetFilePathFor(long key)
        {
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"{key}.json");
        }

        public T this[long key]
        {
            set
            {
                Directory.CreateDirectory(dir);
                var f = GetFilePathFor(key);
                using var stream = File.Open(f, FileMode.Create);
                cache[key] = value;
                JsonSerializer.Serialize<T>(stream, value);
            }
        }

        public bool TryGetValue(long key, [MaybeNullWhen(false)] out T value)
        {
            if (cache.TryGetValue(key, out value))
                return true;

            var f = GetFilePathFor(key);
            if (File.Exists(f))
            {
                using var stream = File.Open(f, FileMode.Create);
                value = JsonSerializer.Deserialize<T>(stream)!;
                return true;
            }

            value = default;
            return false;
        }

        public void ClearCache()
        {
            cache.Clear();
        }

        public T? SearchCache(Predicate<T> predicate)
        {
            foreach (var value in cache.Values)
                if (predicate(value)) return value;

            return null;
        }
    }
}
