namespace RocketExplorer.Core.Ens;

public class ConcurrentBictionary<T1, T2> : IDisposable
	where T1 : notnull
	where T2 : notnull
{
	private readonly Dictionary<T1, T2> forward;
	private readonly ReaderWriterLockSlim readWriterLock = new();
	private readonly Dictionary<T2, T1> reverse;

	public ConcurrentBictionary()
	{
		this.forward = new Dictionary<T1, T2>();
		this.reverse = new Dictionary<T2, T1>();
	}

	public ConcurrentBictionary(IEnumerable<KeyValuePair<T1, T2>> collection) : this()
	{
		foreach ((T1 key, T2 value) in collection)
		{
			this.forward[key] = value;
			this.reverse[value] = key;
		}
	}

	public ConcurrentBictionary(IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2)
	{
		this.forward = new Dictionary<T1, T2>(comparer1);
		this.reverse = new Dictionary<T2, T1>(comparer2);
	}

	public ConcurrentBictionary(
		IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2,
		IEnumerable<KeyValuePair<T1, T2>> collection) : this(comparer1, comparer2)
	{
		foreach ((T1 key, T2 value) in collection)
		{
			this.forward[key] = value;
			this.reverse[value] = key;
		}
	}

	public virtual IReadOnlyDictionary<T1, T2> Forward
	{
		get
		{
			this.readWriterLock.EnterReadLock();

			try
			{
				return this.forward.ToDictionary(this.forward.Comparer);
			}
			finally
			{
				this.readWriterLock.ExitReadLock();
			}
		}
	}

	public virtual T1 this[T2 key]
	{
		get
		{
			this.readWriterLock.EnterReadLock();

			try
			{
				return this.reverse[key];
			}
			finally
			{
				this.readWriterLock.ExitReadLock();
			}
		}

		set
		{
			this.readWriterLock.EnterWriteLock();

			try
			{
				if (this.forward.TryGetValue(value, out T2? reverseValue) &&
					!this.reverse.Comparer.Equals(reverseValue, key))
				{
					throw new InvalidOperationException("Value is already mapped to a different key.");
				}

				if (this.reverse.TryGetValue(key, out T1? forwardKey))
				{
					this.forward.Remove(forwardKey);
				}

				this.reverse[key] = value;
				this.forward[value] = key;
			}
			finally
			{
				this.readWriterLock.ExitWriteLock();
			}
		}
	}

	public virtual T2 this[T1 key]
	{
		get
		{
			this.readWriterLock.EnterReadLock();

			try
			{
				return this.forward[key];
			}
			finally
			{
				this.readWriterLock.ExitReadLock();
			}
		}

		set
		{
			this.readWriterLock.EnterWriteLock();

			try
			{
				if (this.reverse.TryGetValue(value, out T1? reverseValue) &&
					!this.forward.Comparer.Equals(reverseValue, key))
				{
					throw new InvalidOperationException("Value is already mapped to a different key.");
				}

				if (this.forward.TryGetValue(key, out T2? reverseKey))
				{
					this.reverse.Remove(reverseKey);
				}

				this.forward[key] = value;
				this.reverse[value] = key;
			}
			finally
			{
				this.readWriterLock.ExitWriteLock();
			}
		}
	}

	public virtual IEnumerable<T1> Keys
	{
		get
		{
			this.readWriterLock.EnterReadLock();

			try
			{
				return this.forward.Keys.ToList();
			}
			finally
			{
				this.readWriterLock.ExitReadLock();
			}
		}
	}

	public virtual IReadOnlyDictionary<T2, T1> Reverse
	{
		get
		{
			this.readWriterLock.EnterReadLock();

			try
			{
				return this.reverse.ToDictionary(this.reverse.Comparer);
			}
			finally
			{
				this.readWriterLock.ExitReadLock();
			}
		}
	}

	public virtual IEnumerable<T2> Values
	{
		get
		{
			this.readWriterLock.EnterReadLock();

			try
			{
				return this.forward.Values.ToList();
			}
			finally
			{
				this.readWriterLock.ExitReadLock();
			}
		}
	}

	public void Dispose() => this.readWriterLock.Dispose();

	public virtual void Add(T1 key, T2 value)
	{
		this.readWriterLock.EnterWriteLock();

		try
		{
			if (this.forward.ContainsKey(key))
			{
				throw new ArgumentException("An item with the same key has already been added.", nameof(key));
			}

			if (this.reverse.ContainsKey(value))
			{
				throw new ArgumentException("Value is already mapped to a different key.", nameof(key));
			}

			this.forward[key] = value;
			this.reverse[value] = key;
		}
		finally
		{
			this.readWriterLock.ExitWriteLock();
		}
	}

	public virtual bool Contains(T1 key)
	{
		this.readWriterLock.EnterReadLock();

		try
		{
			return this.forward.ContainsKey(key);
		}
		finally
		{
			this.readWriterLock.ExitReadLock();
		}
	}

	public virtual bool Contains(T2 key)
	{
		this.readWriterLock.EnterReadLock();

		try
		{
			return this.reverse.ContainsKey(key);
		}
		finally
		{
			this.readWriterLock.ExitReadLock();
		}
	}

	public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
	{
		this.readWriterLock.EnterReadLock();

		try
		{
			return this.forward.GetEnumerator();
		}
		finally
		{
			this.readWriterLock.ExitReadLock();
		}
	}

	public virtual bool Remove(T1 key)
	{
		bool removed = false;

		this.readWriterLock.EnterWriteLock();

		try
		{
			if (this.forward.TryGetValue(key, out T2? value))
			{
				this.reverse.Remove(value);
				this.forward.Remove(key);

				removed = true;
			}
		}
		finally
		{
			this.readWriterLock.ExitWriteLock();
		}

		return removed;
	}

	public virtual bool Remove(T2 key)
	{
		bool removed = false;

		this.readWriterLock.EnterWriteLock();

		try
		{
			if (this.reverse.TryGetValue(key, out T1? value))
			{
				this.forward.Remove(value);
				this.reverse.Remove(key);

				removed = true;
			}
		}
		finally
		{
			this.readWriterLock.ExitWriteLock();
		}

		return removed;
	}

	public virtual bool TryGetValue(T1 key, out T2? value)
	{
		this.readWriterLock.EnterReadLock();

		try
		{
			return this.forward.TryGetValue(key, out value);
		}
		finally
		{
			this.readWriterLock.ExitReadLock();
		}
	}

	public virtual bool TryGetValue(T2 key, out T1? value)
	{
		this.readWriterLock.EnterReadLock();

		try
		{
			return this.reverse.TryGetValue(key, out value);
		}
		finally
		{
			this.readWriterLock.ExitReadLock();
		}
	}
}