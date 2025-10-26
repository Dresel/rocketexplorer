using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RocketExplorer.Ethereum;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core.Contracts;

public record class ContractsContext
{
	public required Dictionary<string, RocketPoolContract> ContextContracts { get; init; }

	public required Dictionary<string, RocketPoolUpgradeContract> ContextUpgradeContracts { get; init; }

	public ReadOnlyDictionary<byte[], string> ContractsMap { get; } = new Dictionary<byte[], string>(
			Ethereum.Contracts.Names.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
			new FastByteArrayComparer())
		.AsReadOnly();

	public required long CurrentBlockHeight { get; set; }

	public Task IsFinished => ProcessingCompletionSource.Task;

	public TaskCompletionSource ProcessingCompletionSource { get; } = new();

	public required string? ProtocolVersion { get; set; }

	public required List<string> TrustedUpgradeContractAddress { get; init; }

	public ReadOnlyDictionary<byte[], string> UpgradeContractsMap { get; } = new Dictionary<byte[], string>(
			Ethereum.Contracts.UpgradeContractNames.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
			new FastByteArrayComparer())
		.AsReadOnly();

	public static async Task<ContractsContext> ReadAsync(Storage storage, CancellationToken cancellationToken = default)
	{
		BlobObject<ContractsSnapshot> snapshot =
			await storage.ReadAsync<ContractsSnapshot>(Keys.ContractsSnapshotKey, cancellationToken) ??
			new BlobObject<ContractsSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new ContractsSnapshot
				{
					ProtocolVersion = null,
					Contracts = [],
					UpgradeContracts = [],
				},
			};

		return new ContractsContext
		{
			CurrentBlockHeight = snapshot.ProcessedBlockNumber,

			ProtocolVersion = snapshot.Data.ProtocolVersion,
			TrustedUpgradeContractAddress = snapshot.Data.Contracts
				.SingleOrDefault(x => x.Name == "rocketDAONodeTrustedUpgrade")?.Versions.Select(x => x.Address)
				.ToList() ?? [],
			ContextContracts = snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x),
			ContextUpgradeContracts = snapshot.Data.UpgradeContracts.ToDictionary(x => x.Name, x => x),
		};
	}

	public async Task SaveAsync(
		Storage storage, ILogger<ContractsContext> logger, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Writing {snapshot}", Keys.ContractsSnapshotKey);

		await storage.WriteAsync(
			Keys.ContractsSnapshotKey,
			new BlobObject<ContractsSnapshot>
			{
				ProcessedBlockNumber = CurrentBlockHeight,
				Data = new ContractsSnapshot
				{
					Contracts = ContextContracts.Values.OrderBy(x =>
						Array.IndexOf(Ethereum.Contracts.Names, x.Name) == -1
							? int.MaxValue
							: Array.IndexOf(Ethereum.Contracts.Names, x.Name)).ToArray(),
					UpgradeContracts = ContextUpgradeContracts.Values.ToArray(),
					ProtocolVersion = ProtocolVersion,
				},
			}, cancellationToken: cancellationToken);
	}
}