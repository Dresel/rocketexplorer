using System.Collections.ObjectModel;
using Nethereum.Util;
using RocketExplorer.Ethereum;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core.Contracts;

public class ContractsSyncContext : ContextBase
{
	public required Dictionary<string, RocketPoolContract> ContextContracts { get; init; }

	public required Dictionary<string, RocketPoolUpgradeContract> ContextUpgradeContracts { get; init; }

	public ReadOnlyDictionary<byte[], string> ContractsMap { get; } = new Dictionary<byte[], string>(
			Ethereum.Contracts.Names.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
			new ByteArrayComparer())
		.AsReadOnly();

	// Assert address of rocketDAONodeTrustedUpgrade does not change (would require an updated filter expression)
	public required string TrustedUpgradeContractAddress { get; init; }

	public ReadOnlyDictionary<byte[], string> UpgradeContractsMap { get; } = new Dictionary<byte[], string>(
			Ethereum.Contracts.UpgradeContractNames.Select(x => new KeyValuePair<byte[], string>(x.Sha3(), x)),
			new ByteArrayComparer())
		.AsReadOnly();
}