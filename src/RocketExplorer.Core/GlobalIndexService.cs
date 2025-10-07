using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class GlobalIndexService(Storage storage, ILogger<GlobalIndexService> logger)
{
	private readonly bool initialSync = false;
	private readonly ILogger<GlobalIndexService> logger = logger;
	private readonly Storage storage = storage;

	private ConcurrentDictionary<string, Dictionary<byte[], IndexEntry>?> EnsEntries { get; } = new();

	private ConcurrentDictionary<string, Dictionary<byte[], IndexEntry>?> Entries { get; } = new();

	public async Task AddOrUpdateEntryAsync(
		byte[] address, string key, Action<IndexEntry> action,
		CancellationToken cancellationToken = default)
	{
		IndexEntry? anyEntry = null;

		await Parallel.ForEachAsync(
			key.NGrams(), cancellationToken, async (nGram, innerCancellationToken) =>
			{
				if (!Entries.TryGetValue(nGram, out Dictionary<byte[], IndexEntry>? bucket))
				{
					this.logger.LogTrace("Loading {snapshot}", Keys.NGram(nGram));
					BlobObject<GlobalIndexShardSnapshot>? globalIndexShard
						= !this.initialSync
							? await this.storage.ReadAsync<GlobalIndexShardSnapshot>(
								Keys.NGram(nGram), innerCancellationToken)
							: null;

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

				if (entry.AddressEnsName is not null)
				{
					anyEntry ??= entry;
				}
			});

		if (anyEntry?.AddressEnsName is not null)
		{
			await Parallel.ForEachAsync(
				anyEntry.AddressEnsName.NGrams(3), cancellationToken, async (nGram, innerCancellationToken) =>
				{
					if (!EnsEntries.TryGetValue(nGram, out Dictionary<byte[], IndexEntry>? bucket))
					{
						this.logger.LogTrace("Loading {snapshot}", Keys.EnsNGram(nGram));
						BlobObject<GlobalIndexShardSnapshot>? globalIndexShard
							= !this.initialSync
								? await this.storage.ReadAsync<GlobalIndexShardSnapshot>(
									Keys.EnsNGram(nGram), innerCancellationToken)
								: null;

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

						EnsEntries[nGram] = bucket;
					}

					// Could be if bucket was marked for deletion
					if (bucket == null)
					{
						bucket = new Dictionary<byte[], IndexEntry>(new FastByteArrayComparer());
						EnsEntries[nGram] = bucket;
					}

					// TODO: Entry kopieren
					bucket[address] = new IndexEntry()
					{
						Address = anyEntry.Address,
	public string? AddressEnsName { get; set; }

	public int? MegapoolIndex { get; set; }

	public IndexEntryType Type { get; set; }

	public long? ValidatorIndex { get; set; }

	public byte[]? ValidatorPubKey { get; set; }
}
				});
		}
	}

	public Task UpdateOrRemoveEntryAsync(
		byte[] address, string key, Action<IndexEntry> action, CancellationToken cancellationToken = default) =>
		Parallel.ForEachAsync(
			key.NGrams(), cancellationToken, async (nGram, innerCancellationToken) =>
			{
				if (!Entries.TryGetValue(nGram, out Dictionary<byte[], IndexEntry>? bucket))
				{
					this.logger.LogTrace("Loading {snapshot}", Keys.NGram(nGram));
					BlobObject<GlobalIndexShardSnapshot>? globalIndexShard
						= !this.initialSync
							? await this.storage.ReadAsync<GlobalIndexShardSnapshot>(
								Keys.NGram(nGram), innerCancellationToken)
							: null;

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

				// Update Ens index

				if (entry.Type == 0)
				{
					// Remove Ens index
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
					this.logger.LogTrace("Deleting {snapshot}", Keys.NGram(entry.Key));
					await this.storage.DeleteAsync(Keys.NGram(entry.Key), innerCancellationToken);
				}
				else
				{
					this.logger.LogTrace("Writing {snapshot}", Keys.NGram(entry.Key));
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