using Newtonsoft.Json.Linq;

namespace RocketExplorer.Core.ENS;

public class Bictionary<T1, T2>
	where T1 : notnull
	where T2 : notnull
{
	private readonly Dictionary<T1, T2> forward;
	private readonly Dictionary<T2, T1> reverse;

	public Bictionary()
	{
		this.forward = new Dictionary<T1, T2>();
		this.reverse = new Dictionary<T2, T1>();
	}

	public Bictionary(IEnumerable<KeyValuePair<T1, T2>> collection) : this()
	{
		foreach ((T1 key, T2 value) in collection)
		{
			this.forward[key] = value;
			this.reverse[value] = key;
		}
	}

	public Bictionary(IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2)
	{
		this.forward = new Dictionary<T1, T2>(comparer1);
		this.reverse = new Dictionary<T2, T1>(comparer2);
	}

	public Bictionary(
		IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2,
		IEnumerable<KeyValuePair<T1, T2>> collection) : this(comparer1, comparer2)
	{
		foreach ((T1 key, T2 value) in collection)
		{
			this.forward[key] = value;
			this.reverse[value] = key;
		}
	}

	public IReadOnlyDictionary<T1, T2> Forward => this.forward;

	public virtual T1 this[T2 key]
	{
		get => Reverse[key];
		set
		{
			if (Reverse.ContainsKey(key))
			{
				this.forward.Remove(Reverse[key]);
			}

			this.reverse[key] = value;
			this.forward[value] = key;
		}
	}

	public virtual T2 this[T1 key]
	{
		get => Forward[key];
		set
		{
			if (Forward.ContainsKey(key))
			{
				this.reverse.Remove(Forward[key]);
			}

			this.forward[key] = value;
			this.reverse[value] = key;
		}
	}

	public virtual IEnumerable<T1> Keys => Forward.Keys;

	public IReadOnlyDictionary<T2, T1> Reverse => this.reverse;

	public virtual IEnumerable<T2> Values => Forward.Values;

	public virtual void Add(T1 key, T2 value)
	{
		if (Forward.ContainsKey(key))
		{
			throw new ArgumentException("An item with the same key has already been added.", nameof(key));
		}

		this[key] = value;
	}

	public virtual bool Contains(T1 key) => Forward.ContainsKey(key);

	public virtual bool Contains(T2 key) => Reverse.ContainsKey(key);

	public virtual bool TryGetValue(T1 key, out T2? value) => Forward.TryGetValue(key, out value);

	public virtual bool TryGetValue(T2 key, out T1? value) => Reverse.TryGetValue(key, out value);

	public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator() => Forward.GetEnumerator();

	public virtual bool Remove(T1 key)
	{
		bool removed = false;

		if (Forward.ContainsKey(key))
		{
			this.reverse.Remove(Forward[key]);
			removed = true;
		}

		this.forward.Remove(key);
		return removed;
	}

	public virtual bool Remove(T2 key)
	{
		bool removed = false;

		if (Reverse.ContainsKey(key))
		{
			this.forward.Remove(Reverse[key]);
			removed = true;
		}

		this.reverse.Remove(key);
		return removed;
	}
}