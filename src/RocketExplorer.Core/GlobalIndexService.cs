using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class GlobalIndexService(Storage storage, ILogger<GlobalIndexService> logger)
{
	private readonly ILogger<GlobalIndexService> logger = logger;
	private readonly Storage storage = storage;

	private ConcurrentDictionary<string, Dictionary<byte[], IndexEntry>?> Entries { get; } = new();

	public Task AddOrUpdateEntryAsync(
		byte[] address, string key, Action<IndexEntry> action,
		CancellationToken cancellationToken = default) =>
		Parallel.ForEachAsync(
			key.NGrams(), cancellationToken, async (nGram, innerCancellationToken) =>
			{
				if (!Entries.TryGetValue(nGram, out Dictionary<byte[], IndexEntry>? bucket))
				{
					this.logger.LogDebug("Loading {snapshot}", Keys.NGram(nGram));
					BlobObject<GlobalIndexShardSnapshot>? globalIndexShard
						= await this.storage.ReadAsync<GlobalIndexShardSnapshot>(
							Keys.NGram(nGram), innerCancellationToken);

					if (globalIndexShard != null)
					{
						bucket = globalIndexShard.Data.Index.ToDictionary(
							x => x.Address, x => new IndexEntry
							{
								Type = x.Type,
								Address = x.Address,
								ValidatorPubKey = x.ValidatorPubKey,
								ValidatorIndex = x.ValidatorIndex,
								MegapoolIndex = x.MegapoolIndex,
							}, new FastByteArrayComparer());
					}
					else
					{
						bucket = new Dictionary<byte[], IndexEntry>(new FastByteArrayComparer());
					}

					Entries[nGram] = bucket;
				}

				// Could be if bucket was marked for deletion
				if (bucket == null)
				{
					bucket = new Dictionary<byte[], IndexEntry>(new FastByteArrayComparer());
					Entries[nGram] = bucket;
				}

				if (!bucket.TryGetValue(address, out IndexEntry? entry))
				{
					entry = new IndexEntry
					{
						Address = address,
					};

					bucket[address] = entry;
				}

				action(entry);
			});

	public Task UpdateOrRemoveEntryAsync(
		byte[] address, string key, Action<IndexEntry> action, CancellationToken cancellationToken = default) =>
		Parallel.ForEachAsync(
			key.NGrams(), cancellationToken, async (nGram, innerCancellationToken) =>
			{
				if (!Entries.TryGetValue(nGram, out Dictionary<byte[], IndexEntry>? bucket))
				{
					this.logger.LogDebug("Loading {snapshot}", Keys.NGram(nGram));
					BlobObject<GlobalIndexShardSnapshot>? globalIndexShard
						= await this.storage.ReadAsync<GlobalIndexShardSnapshot>(
							Keys.NGram(nGram), innerCancellationToken);

					if (globalIndexShard != null)
					{
						bucket = globalIndexShard.Data.Index.ToDictionary(
							x => x.Address, x => new IndexEntry
							{
								Type = x.Type,
								Address = x.Address,
								ValidatorPubKey = x.ValidatorPubKey,
								ValidatorIndex = x.ValidatorIndex,
								MegapoolIndex = x.MegapoolIndex,
							}, new FastByteArrayComparer());

						Entries[nGram] = bucket;
					}
				}

				if (bucket == null)
				{
					throw new InvalidOperationException();
				}

				if (!bucket.TryGetValue(address, out IndexEntry? entry))
				{
					throw new InvalidOperationException();
				}

				action(entry);

				if (entry.Type == 0)
				{
					bucket.Remove(address);

					if (bucket.Count == 0)
					{
						// Mark for deletion
						Entries[nGram] = null;
					}
				}
			});

	public Task WriteAsync(long processedBlockNumber, CancellationToken cancellationToken = default) =>
		Parallel.ForEachAsync(
			Entries, cancellationToken, async (entry, innerCancellationToken) =>
			{
				if (entry.Value == null)
				{
					this.logger.LogDebug("Deleting {snapshot}", Keys.NGram(entry.Key));
					await this.storage.DeleteAsync(Keys.NGram(entry.Key), innerCancellationToken);
				}
				else
				{
					this.logger.LogDebug("Writing {snapshot}", Keys.NGram(entry.Key));
					await this.storage.WriteAsync(
						Keys.NGram(entry.Key), new BlobObject<GlobalIndexShardSnapshot>
						{
							ProcessedBlockNumber = processedBlockNumber,
							Data = new GlobalIndexShardSnapshot
							{
								Index = entry.Value.Values.Select(x => new Shared.IndexEntry
								{
									Type = x.Type,
									Address = x.Address,
									ValidatorPubKey = x.ValidatorPubKey,
									ValidatorIndex = x.ValidatorIndex,
									MegapoolIndex = x.MegapoolIndex,
								}).ToArray(),
							},
						}, cancellationToken: innerCancellationToken);
				}
			});
}