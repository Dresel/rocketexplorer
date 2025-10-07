using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using RocketExplorer.Core.ENS;
using RocketExplorer.Ethereum;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;

namespace RocketExplorer.Core.Nodes;

public class ENSSync(IOptions<SyncOptions> options)
	: SyncBase<ENSSyncContext>(options)
{
	protected override async Task HandleBlocksAsync(
		ENSSyncContext context, long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		ENSService ensService = context.Web3.Eth.GetEnsService();

		string address = "0xa4186193281f7727C070766ba60B63Df74eA4Da1";
		string eventReverseAddress = address.RemoveHexPrefix().ToLower() + ENSService.REVERSE_NAME_SUFFIX;
		byte[]? eventReverseAddressNameHash = context.EnsUtil.GetNameHash(eventReverseAddress).HexToByteArray();

		long? findFirstBlock = await Helper.FindFirstBlockAsync(
			async blockNumber =>
			{
				string reverseResolverAddress = await ensService.ENSRegistryService
					.ResolverQueryAsync(eventReverseAddressNameHash, blockNumber);

				if (reverseResolverAddress.IsNullOrZeroAddress())
				{
					return false;
				}

				PublicResolverService reverseResolver = new(context.Web3.Eth, reverseResolverAddress);

				// Primary address of ENS name
				string? primaryEnsName = await reverseResolver.NameQueryAsync(eventReverseAddressNameHash, blockNumber);

				if (string.IsNullOrWhiteSpace(primaryEnsName))
				{
					return false;
				}

				byte[]? primaryEnsNameHash = context.EnsUtil.GetNameHash(primaryEnsName).HexToByteArray();

				string? forwardResolverAddress =
					await ensService.ENSRegistryService.ResolverQueryAsync(primaryEnsNameHash, blockNumber);
				if (forwardResolverAddress.IsNullOrZeroAddress())
				{
					return false;
				}

				PublicResolverService forwardResolver = new(context.Web3.Eth, forwardResolverAddress);

				string? primaryAddress = await forwardResolver.AddrQueryAsync(primaryEnsNameHash, blockNumber);

				if (primaryAddress.IsNullOrZeroAddress())
				{
					return false;
				}

				if (address.RemoveHexPrefix().ToLower() != primaryAddress.RemoveHexPrefix().ToLower())
				{
					return false;
				}

				return true;
			}, fromBlock,
			context.LatestBlockHeight, 100);

		IEnumerable<IEventLog> nodeAddedEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock,
			[
				typeof(AddrChangedEventDTO), typeof(AddressChangedEventDTO), typeof(NameChangedEventDTO),
				typeof(NewResolverEventDTO), typeof(ResolverChangedEventDTO),
			], [],
			context.Policy);

		foreach (IEventLog eventLog in nodeAddedEvents)
		{
			await eventLog.WhenIsAsync<AddrChangedEventDTO, ENSSyncContext>(
				ENSEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<NewResolverEventDTO, ENSSyncContext>(
				ENSEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<ResolverChangedEventDTO, ENSSyncContext>(
				ENSEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<AddressChangedEventDTO, ENSSyncContext>(
				ENSEventHandlers.HandleAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<NameChangedEventDTO, ENSSyncContext>(
				ENSEventHandlers.HandleAsync, context, cancellationToken);
		}
	}

	protected override async Task<ENSSyncContext> LoadContextAsync(
		ContextBase contextBase,
		CancellationToken cancellationToken = default)
	{
		long activationHeight = contextBase.Contracts["rocketStorage"].Versions.Single().ActivationHeight;

		contextBase.Logger.LogInformation("Loading {snapshot}", Keys.NodesSnapshot);

		Task<BlobObject<NodesSnapshot>?> readNodesTask =
			contextBase.Storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot, cancellationToken);
		Task<BlobObject<TokensRPLOldSnapshot>?> readRPLOldTask =
			contextBase.Storage.ReadAsync<TokensRPLOldSnapshot>(Keys.TokensRPLOldSnapshot, cancellationToken);
		Task<BlobObject<TokensRPLSnapshot>?> readRPLTask =
			contextBase.Storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot, cancellationToken);
		Task<BlobObject<TokensRETHSnapshot>?> readRETHTask =
			contextBase.Storage.ReadAsync<TokensRETHSnapshot>(Keys.TokensRETHSnapshot, cancellationToken);

		await Task.WhenAll(readNodesTask, readRPLOldTask, readRPLTask, readRETHTask);

		ENSSyncContext ensSyncContext = new()
		{
			Storage = contextBase.Storage,
			Policy = contextBase.Policy,
			Logger = contextBase.Logger,
			Web3 = contextBase.Web3,
			BeaconChainService = contextBase.BeaconChainService,
			GlobalIndexService = contextBase.GlobalIndexService,
			DashboardInfo = contextBase.DashboardInfo,
			RocketStorage = contextBase.RocketStorage,
			Contracts = contextBase.Contracts,
			LatestBlockHeight = contextBase.LatestBlockHeight,

			CurrentBlockHeight = 14280000,
		};

		ensSyncContext.AddToReverseAddressNameHashMap((await readNodesTask)?.Data.Index.Select(x => x.ContractAddress) ?? []);
		ensSyncContext.AddToEnsMaps((await readNodesTask)?.Data.Index.Where(x => x.ContractAddressEnsName is not null).Select(x => (x.ContractAddress, x.ContractAddressEnsName!)) ?? []);

		ensSyncContext.AddToReverseAddressNameHashMap((await readRPLOldTask)?.Data.RPLOld.Holders.Select(x => x.Address[2..].HexToByteArray()) ?? []);
		ensSyncContext.AddToEnsMaps((await readRPLOldTask)?.Data.RPLOld.Holders.Where(x => x.AddressEnsName is not null).Select(x => (x.Address[2..].HexToByteArray(), x.AddressEnsName!)) ?? []);

		ensSyncContext.AddToReverseAddressNameHashMap((await readRPLTask)?.Data.RPL.Holders.Select(x => x.Address[2..].HexToByteArray()) ?? []);
		ensSyncContext.AddToEnsMaps((await readRPLTask)?.Data.RPL.Holders.Where(x => x.AddressEnsName is not null).Select(x => (x.Address[2..].HexToByteArray(), x.AddressEnsName!)) ?? []);

		ensSyncContext.AddToReverseAddressNameHashMap((await readRETHTask)?.Data.RETH.Holders.Select(x => x.Address[2..].HexToByteArray()) ?? []);
		ensSyncContext.AddToEnsMaps((await readRETHTask)?.Data.RETH.Holders.Where(x => x.AddressEnsName is not null).Select(x => (x.Address[2..].HexToByteArray(), x.AddressEnsName!)) ?? []);

		return ensSyncContext;
	}

	protected override async Task SaveContextAsync(
		ENSSyncContext context, CancellationToken cancellationToken = default) =>
		context.Logger.LogInformation("Writing {snapshot}", Keys.NodesSnapshot);
}

[Event("ResolverChanged")]
public class ResolverChangedEventDTO : IEventDTO
{
	[Parameter("bytes32", "node", 1, true)]
	public byte[] Node { get; set; }

	[Parameter("address", "resolver", 2, false)]
	public string Resolver { get; set; }
}