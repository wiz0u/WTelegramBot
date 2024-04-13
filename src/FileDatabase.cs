using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Telegram.Bot
{
	//TODO: use DB instead (generic?)
	internal class FileDatabase<T> : IDictionary<long, T>, IReadOnlyDictionary<long, T> where T : class
	{
		private readonly string _folder;
		private readonly Dictionary<long, T> _cache = [];
		//private readonly JsonSerializerOptions options = new()
		//	{ IncludeFields = true, IgnoreReadOnlyProperties = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

		public FileDatabase(string folder) => Directory.CreateDirectory(_folder = folder);

		public void Add(KeyValuePair<long, T> item) => throw new NotImplementedException();
		public bool Contains(KeyValuePair<long, T> item) => throw new NotImplementedException();
		public void CopyTo(KeyValuePair<long, T>[] array, int arrayIndex) => throw new NotImplementedException();
		public bool Remove(KeyValuePair<long, T> item) => throw new NotImplementedException();
		public IEnumerator<KeyValuePair<long, T>> GetEnumerator() => throw new NotImplementedException();
		IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
		IEnumerable<long> IReadOnlyDictionary<long, T>.Keys => throw new NotImplementedException();
		IEnumerable<T> IReadOnlyDictionary<long, T>.Values => throw new NotImplementedException();

		public void Clear() => throw new NotImplementedException();
		public void Add(long key, T value) => throw new NotImplementedException();
		public bool Remove(long key) => throw new NotImplementedException();

		public ICollection<long> Keys => throw new NotImplementedException();
		public ICollection<T> Values => throw new NotImplementedException();
		public int Count => throw new NotImplementedException();
		public bool IsReadOnly => false;

		public T this[long key]
		{
			get => throw new NotImplementedException();
			set
			{
				_cache[key] = value;
				var filePath = Path.Combine(_folder, key + ".json");
				using var fs = System.IO.File.Create(filePath);
				JsonSerializer.Serialize(fs, value);
			}
		}

		public bool ContainsKey(long key)
		{
			if (_cache.ContainsKey(key)) return true;
			return System.IO.File.Exists(Path.Combine(_folder, key + ".json"));
		}

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
		public bool TryGetValue(long key, [MaybeNullWhen(false)] out T value)
		{
			if (_cache.TryGetValue(key, out value)) return true;
			try
			{
				var filePath = Path.Combine(_folder, key + ".json");
				using var fs = System.IO.File.OpenRead(filePath);
				_cache[key] = value = (T)JsonSerializer.Deserialize(fs, typeof(T))!;
				return true;
			}
			catch (IOException)
			{
				return false;
			}
		}

		internal void ClearCache() => _cache.Clear();
		internal T? SearchCache(Predicate<T> predicate)
		{
			foreach (var chat in _cache.Values)
				if (predicate(chat))
					return chat;
			return null;
		}
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
	}
}
