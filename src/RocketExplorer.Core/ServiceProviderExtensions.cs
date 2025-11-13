using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Polly.Retry;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Ens;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketNodeManager;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public static class ServiceProviderExtensions
{
	public static async Task<GlobalContext> CreateGlobalContextAsync(
		this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
	{
		SyncOptions options = serviceProvider.GetRequiredService<IOptions<SyncOptions>>().Value;

		Web3 web3 = serviceProvider.GetRequiredService<Web3>();

		serviceProvider.GetRequiredService<ILogger<GlobalContext>>().LogInformation(
			"Using Rocket Pool environment {Environment} with rpc endpoint {RPCUrl}", options.Environment,
			options.RPCUrl);

		Storage storage = serviceProvider.GetRequiredService<Storage>();
		BlobObject<SnapshotMetadata>? snapshotMetadata = await storage.ReadAsync<SnapshotMetadata>(Keys.SnapshotMetadata, cancellationToken);

		BlockWithTransactions latestBlock =
			await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
		latestBlock = await web3.Eth.Blocks
			.GetBlockWithTransactionsByNumber
			.SendRequestAsync(new BlockParameter((ulong)(latestBlock.Number.Value - 12)));

		AsyncRetryPolicy policy =
			NethereumPolicies.Retry(serviceProvider.GetRequiredService<ILogger<GlobalContext>>());

		RocketStorageService rocketStorageService = new(web3, options.RocketStorageContractAddress);

		ContractsContext contractsContext = await ContractsContext.ReadAsync(storage, cancellationToken);
		Task<NodesContext> nodesContextFactory = NodesContext.ReadAsync(
			storage, contractsContext, serviceProvider.GetRequiredService<ILogger<NodesContext>>(), cancellationToken);

		Func<string, Task<long?>> getDeploymentBlock = async address =>
		{
			return await Helper.FindFirstBlockAsync(
				async blockParameter =>
				{
					string code = await policy.ExecuteAsync(() => web3.Eth.GetCode.SendRequestAsync(address, blockParameter));
					return !string.Equals(code, "0x", StringComparison.OrdinalIgnoreCase);
				},
				0,
				(long)latestBlock.Number.Value,
				TimeSpan.FromDays(60).BlockCount());
		};

		Task<TokensContext> tokensContextFactory = TokensContext.ReadAsync(getDeploymentBlock,
			storage, contractsContext, options, serviceProvider.GetRequiredService<ILogger<TokensContext>>(),
			cancellationToken);

		AddressEnsProcessHistory addressEnsProcessHistory = new();

		return new GlobalContext
		{
			LatestBlock = latestBlock,
			Policy = policy,
			LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),

			Services = new ContextServices
			{
				Web3 = web3,
				Storage = storage,
				RocketStorage = rocketStorageService,
				BeaconChainService = serviceProvider.GetRequiredService<BeaconChainService>(),
				GlobalIndexService = serviceProvider.GetRequiredService<GlobalIndexService>(),
				GlobalEnsIndexService = serviceProvider.GetRequiredService<GlobalEnsIndexService>(),
				AddressEnsProcessHistory = addressEnsProcessHistory,
				RocketNodeManager = new RocketNodeManagerService(
					web3,
					await policy.ExecuteAsync(() => rocketStorageService.GetAddressQueryAsync("rocketNodeManager"))),
			},

			DashboardContext = await DashboardInfo.ReadAsync(
				storage,
				serviceProvider.GetRequiredService<ILogger<DashboardInfo>>(),
				cancellationToken),

			ContractsContext = contractsContext,
			NodesContextFactory = nodesContextFactory,
			TokensContextFactory =
				tokensContextFactory,
			EnsContextFactory = EnsContext.ReadAsync(
				storage,
				nodesContextFactory,
				tokensContextFactory,
				addressEnsProcessHistory,
				serviceProvider.GetRequiredService<ILogger<EnsContext>>(),
				cancellationToken),
		};
	}
}