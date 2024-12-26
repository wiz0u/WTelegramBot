using TL;
using Chat = WTelegram.Types.Chat;
using Message = WTelegram.Types.Message;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

public interface IBotStorage : IDisposable
{
    public Bot.State? State { get; }

    public string DataSource { get; }

    public interface ISetCache<T> where T : class
    {
        public T this[long key] { set; }
        public bool TryGetValue(long key, [MaybeNullWhen(false)] out T value);
        public void ClearCache();
        public T? SearchCache(Predicate<T> predicate);
    }

    public class SessionStore(byte[]? _data, Action<byte[]> save) : Stream
    {
        private int _dataLen = _data?.Length ?? 0;
        private DateTime _lastWrite;
        private Task? _delayedWrite;

        protected override void Dispose(bool disposing) => _delayedWrite?.Wait();

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Copy(_data!, 0, buffer, offset, count);
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _data = buffer; _dataLen = count;
            if (_delayedWrite != null) return;
            var left = 1000 - (int)(DateTime.UtcNow - _lastWrite).TotalMilliseconds;
            if (left < 0)
            {
                save(buffer[offset..(offset + count)]);
                _lastWrite = DateTime.UtcNow;
            }
            else
                _delayedWrite = Task.Delay(left).ContinueWith(t => { lock (this) { _delayedWrite = null; Write(_data, 0, _dataLen); } });
        }

        public override long Length => _dataLen;
        public override long Position { get => 0; set { } }
        public override bool CanSeek => false;
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Flush() { }
    }

    public void Dispose();
    public void GetTables(out ISetCache<User> users, out ISetCache<Chat> chats);
    public Dictionary<long, UpdateManager.MBoxState> LoadMBoxStates();
    public Stream LoadSessionState();
    public IEnumerable<(int id, TL.Update update)> LoadTLUpdates();
    public void SaveMBoxStates(IReadOnlyDictionary<long, UpdateManager.MBoxState> state);
    public void SaveSessionState(byte[]? sessionData = null);
    public void SaveTLUpdates(IEnumerable<Update> updates);

    /// <summary>
    /// Sets the state of the storage
    /// </summary>
    /// <remarks>
    /// This method will only be called once internally; and the underlying object should throw an exception if an attempt is made to set it more than once
    /// </remarks>
    /// <param name="state"></param>
    public void AssignBotState(Bot.State state);
}