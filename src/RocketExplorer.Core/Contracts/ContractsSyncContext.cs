using System.Collections.ObjectModel;
using RocketExplorer.Ethereum;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core.Contracts;

public class ContractsSyncContext : ContextBase
{
	public required Dictionary<string, RocketPoolContract> ContextContracts { get; init; }

	public required Dictionary<string, RocketPoolUpgradeContract> ContextUpgradeContracts { get; init; }

	public ReadOnlyDictionary<byte[], string> ContractsMap { get; } = new Dictionary<byte[], string>(
			Ethereum.Contracts.Names.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
			new FastByteArrayComparer())
		.AsReadOnly();

	public required string? ProtocolVersion { get; set; }

	public required List<string> TrustedUpgradeContractAddress { get; init; }

	public ReadOnlyDictionary<byte[], string> UpgradeContractsMap { get; } = new Dictionary<byte[], string>(
			Ethereum.Contracts.UpgradeContractNames.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
			new FastByteArrayComparer())
		.AsReadOnly();
}