using System.Collections.Concurrent;
using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class IndexService<TIdentifier, TEntry, TStoredEntry>(
	int nGramLength,
	Func<TEntry, TIdentifier> identifierFunc,
	Func<TStoredEntry, TIdentifier> storedIdentifierFunc,
	Func<TEntry, TStoredEntry> mapToStoredEntry,
	Func<TStoredEntry, TEntry> mapToEntry,
	IEqualityComparer<TIdentifier> equalityComparer,
	Storage storage,
	Func<string, string> storagePathTemplate,
	ILogger logger)
	where TEntry : new()
	where TIdentifier : notnull
{
	private readonly AsyncKeyedLocker<string> asyncKeyedLocker = new();
	private readonly IEqualityComparer<TIdentifier> equalityComparer = equalityComparer;
	private readonly Func<TEntry, TIdentifier> identifierFunc = identifierFunc;
	private readonly ILogger logger = logger;
	private readonly Func<TStoredEntry, TEntry> mapToEntry = mapToEntry;
	private readonly Func<TEntry, TStoredEntry> mapToStoredEntry = mapToStoredEntry;
	private readonly int nGramLength = nGramLength;
	private readonly ConcurrentDictionary<TIdentifier, ConcurrentQueue<EventIndex>> queues = new(equalityComparer);
	private readonly Storage storage = storage;
	private readonly Func<string, string> storagePathTemplate = storagePathTemplate;
	private readonly Func<TStoredEntry, TIdentifier> storedIdentifierFunc = storedIdentifierFunc;
	private int activeOperationsCount;

	public bool SkipLoading { get; set; } = false;

	private ConcurrentDictionary<string, Dictionary<TIdentifier, TEntry>?> Entries { get; } = new();

	public async Task AddOrUpdateEntryAsync(
		string key, TIdentifier identifier, EventIndex index, Action<TEntry> updater,
		Func<TEntry, bool>? shouldDelete = null,
		CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref this.activeOperationsCount);

		// Get or create queue
		ConcurrentQueue<EventIndex> queue = this.queues.GetOrAdd(identifier, x => []);
		queue.Enqueue(index);

		while (queue.TryPeek(out EventIndex currentEventIndex) && currentEventIndex != index)
		{
			await Task.Delay(10, cancellationToken);
		}

		try
		{
			await Parallel.ForEachAsync(
				key.NGrams(this.nGramLength), cancellationToken, async (nGram, innerCancellationToken) =>
				{
					using (await this.asyncKeyedLocker.LockAsync(nGram, innerCancellationToken))
					{
						Dictionary<TIdentifier, TEntry> bucket =
							await GetBucket(nGram, true, false, innerCancellationToken) ??
							throw new InvalidOperationException("Bucket must not be null");

						if (!bucket.TryGetValue(identifier, out TEntry? entry))
						{
							entry = new TEntry();

							bucket[identifier] = entry;
						}

						updater(entry);

						if (shouldDelete is not null && shouldDelete(entry))
						{
							bucket.Remove(identifier);

							if (bucket.Count == 0)
							{
								// Mark for deletion
								Entries[nGram] = null;
							}
						}
					}
				});
		}
		finally
		{
			Interlocked.Decrement(ref this.activeOperationsCount);
			queue.TryDequeue(out _);
		}
	}

	public async Task AddOrUpdateEntryAsync(
		string key, TIdentifier identifier, EventIndex index, TEntry newEntry,
		CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref this.activeOperationsCount);

		// Get or create queue
		ConcurrentQueue<EventIndex> queue = this.queues.GetOrAdd(identifier, x => []);
		queue.Enqueue(index);

		while (queue.TryPeek(out EventIndex currentEventIndex) && currentEventIndex != index)
		{
			await Task.Delay(10, cancellationToken);
		}

		try
		{
			await Parallel.ForEachAsync(
				key.NGrams(this.nGramLength), cancellationToken, async (nGram, innerCancellationToken) =>
				{
					using (await this.asyncKeyedLocker.LockAsync(nGram, innerCancellationToken))
					{
						Dictionary<TIdentifier, TEntry> bucket =
							await GetBucket(nGram, true, false, innerCancellationToken) ??
							throw new InvalidOperationException("Bucket must not be null");

						bucket[identifier] = newEntry;
					}
				});
		}
		finally
		{
			Interlocked.Decrement(ref this.activeOperationsCount);
			queue.TryDequeue(out _);
		}
	}

	public async Task UpdateEntryAsync(
		string key, TIdentifier identifier, EventIndex index, Action<TEntry> updater,
		Func<TEntry, bool>? shouldDelete = null,
		CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref this.activeOperationsCount);

		// Get or create queue
		ConcurrentQueue<EventIndex> queue = this.queues.GetOrAdd(identifier, x => []);
		queue.Enqueue(index);

		while (queue.TryPeek(out EventIndex currentEventIndex) && currentEventIndex != index)
		{
			await Task.Delay(10, cancellationToken);
		}

		try
		{
			await Parallel.ForEachAsync(
				key.NGrams(this.nGramLength), cancellationToken, async (nGram, innerCancellationToken) =>
				{
					using (await this.asyncKeyedLocker.LockAsync(nGram, innerCancellationToken))
					{
						Dictionary<TIdentifier, TEntry>? bucket = await GetBucket(
							nGram, false, true, innerCancellationToken);

						if (bucket is null)
						{
							this.logger.LogError("Bucket does not exist for nGram {nGram} ({Key})", nGram, key);
							return;
						}

						if (!bucket.TryGetValue(identifier, out TEntry? entry))
						{
							this.logger.LogError("Entry does not exist for identifier {identifier} in nGram {nGram} ({Key})", identifier, nGram, key);
							return;
						}

						updater(entry);

						if (shouldDelete is not null && shouldDelete(entry))
						{
							bucket.Remove(identifier);

							if (bucket.Count == 0)
							{
								// Mark for deletion
								Entries[nGram] = null;
							}
						}
					}
				});
		}
		finally
		{
			Interlocked.Decrement(ref this.activeOperationsCount);
			queue.TryDequeue(out _);
		}
	}

	public async Task TryRemoveEntryAsync(
		string key, TIdentifier identifier, EventIndex index,
		CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref this.activeOperationsCount);

		// Get or create queue
		ConcurrentQueue<EventIndex> queue = this.queues.GetOrAdd(identifier, x => []);
		queue.Enqueue(index);

		while (queue.TryPeek(out EventIndex currentEventIndex) && currentEventIndex != index)
		{
			await Task.Delay(10, cancellationToken);
		}

		try
		{
			await Parallel.ForEachAsync(
				key.NGrams(this.nGramLength), cancellationToken, async (nGram, innerCancellationToken) =>
				{
					using (await this.asyncKeyedLocker.LockAsync(nGram, innerCancellationToken))
					{
						Dictionary<TIdentifier, TEntry>? bucket = await GetBucket(
							nGram, false, true, innerCancellationToken);

						if (bucket is null)
						{
							this.logger.LogError("Bucket does not exist for nGram {nGram} ({Key})", nGram, key);
							return;
						}

						if (!bucket.Remove(identifier, out TEntry? _))
						{
							this.logger.LogError("Entry does not exist for identifier {identifier} in nGram {nGram} ({Key})", identifier, nGram, key);
							return;
						}

						if (bucket.Count == 0)
						{
							// Mark for deletion
							Entries[nGram] = null;
						}
					}
				});
		}
		finally
		{
			Interlocked.Decrement(ref this.activeOperationsCount);
			queue.TryDequeue(out _);
		}
	}

	public async Task WaitForCompletion(CancellationToken cancellationToken = default)
	{
		while (Volatile.Read(ref this.activeOperationsCount) > 0)
		{
			await Task.Delay(10, cancellationToken);
		}
	}

	public async Task WriteAsync(long processedBlockNumber, CancellationToken cancellationToken = default)
	{
		await WaitForCompletion(cancellationToken);

		int count = 0;

		await Parallel.ForEachAsync(
			Entries.ToArray(), cancellationToken, async (entry, innerCancellationToken) =>
			{
				if (entry.Value == null)
				{
					this.logger.LogTrace("Deleting {snapshot}", this.storagePathTemplate(entry.Key));
					await this.storage.DeleteAsync(this.storagePathTemplate(entry.Key), innerCancellationToken);
				}
				else
				{
					int current = Interlocked.Increment(ref count);
					if (current % 1000 == 0)
					{
						this.logger.LogInformation("{Count} shards written", current);
					}

					this.logger.LogTrace("Writing {snapshot}", this.storagePathTemplate(entry.Key));
					await this.storage.WriteAsync(
						this.storagePathTemplate(entry.Key), new BlobObject<GlobalIndexShardSnapshot<TStoredEntry>>
						{
							ProcessedBlockNumber = processedBlockNumber,
							Data = new GlobalIndexShardSnapshot<TStoredEntry>
							{
								Index = entry.Value.Values.Select(x => this.mapToStoredEntry(x)).ToArray(),
							},
						}, cancellationToken: innerCancellationToken);
				}
			});
	}

	private async Task<Dictionary<TIdentifier, TEntry>?> GetBucket(
		string nGram, bool createIfMissing, bool mustExist, CancellationToken innerCancellationToken)
	{
		if (!Entries.TryGetValue(nGram, out Dictionary<TIdentifier, TEntry>? bucket))
		{
			this.logger.LogTrace("Loading {snapshot}", this.storagePathTemplate(nGram));
			BlobObject<GlobalIndexShardSnapshot<TStoredEntry>>? globalIndexShard
				= !SkipLoading
					? await this.storage.ReadAsync<GlobalIndexShardSnapshot<TStoredEntry>>(
						this.storagePathTemplate(nGram), innerCancellationToken)
					: null;

			if (globalIndexShard != null)
			{
				bucket = globalIndexShard.Data.Index.ToDictionary(
					x => this.storedIdentifierFunc(x), x => this.mapToEntry(x), this.equalityComparer);
			}
			else
			{
				if (createIfMissing)
				{
					bucket = new Dictionary<TIdentifier, TEntry>(this.equalityComparer);
				}
			}

			Entries[nGram] = bucket;
		}

		if (bucket == null && createIfMissing)
		{
			if (mustExist)
			{
				throw new InvalidOperationException("Bucket does not exist");
			}

			// Could be if bucket was marked for deletion
			bucket = new Dictionary<TIdentifier, TEntry>(this.equalityComparer);
			Entries[nGram] = bucket;
		}

		return bucket;
	}
}