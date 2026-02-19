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

		AsyncRetryPolicy policy =
			NethereumPolicies.Retry(serviceProvider.GetRequiredService<ILogger<GlobalContext>>());

		BlockWithTransactions latestBlock = await policy.ExecuteAsync(
			() => web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest()));
		latestBlock = await policy.ExecuteAsync(
			() => web3.Eth.Blocks.GetBlockWithTransactionsByNumber
				.SendRequestAsync(new BlockParameter((ulong)(latestBlock.Number.Value - 12))));

		RocketStorageService rocketStorageService = new(web3, options.RocketStorageContractAddress);

		Storage storage = serviceProvider.GetRequiredService<Storage>();

		ContractsContext contractsContext = await ContractsContext.ReadAsync(storage, cancellationToken);
		Task<NodesContext> nodesContextFactory = NodesContext.ReadAsync(
			storage, contractsContext, serviceProvider.GetRequiredService<ILogger<NodesContext>>(), cancellationToken);

		Func<string, Task<long?>> getDeploymentBlock = async address =>
		{
			return await Helper.FindFirstBlockAsync(
				async blockParameter =>
				{
					string code =
						await policy.ExecuteAsync(() => web3.Eth.GetCode.SendRequestAsync(address, blockParameter));
					return !string.Equals(code, "0x", StringComparison.OrdinalIgnoreCase);
				},
				0,
				(long)latestBlock.Number.Value,
				TimeSpan.FromDays(60).BlockCount());
		};

		Task<TokensContextRPL> tokensContextRPLFactory = TokensContextRPL.ReadAsync(
			getDeploymentBlock,
			storage, contractsContext, options, serviceProvider.GetRequiredService<ILogger<TokensContextRPL>>(),
			cancellationToken);

		Task<TokensContextRPLOld> tokensContextOldFactory = TokensContextRPLOld.ReadAsync(
			getDeploymentBlock,
			storage, contractsContext, options, serviceProvider.GetRequiredService<ILogger<TokensContextRPLOld>>(),
			cancellationToken);

		Task<TokensContextStakedRPL> tokensContextStakedRPLFactory = TokensContextStakedRPL.ReadAsync(
			getDeploymentBlock,
			storage, contractsContext, options, serviceProvider.GetRequiredService<ILogger<TokensContextStakedRPL>>(),
			cancellationToken);

		Task<TokensContextRETH> tokensContextRETHFactory = TokensContextRETH.ReadAsync(
			getDeploymentBlock,
			storage, contractsContext, options, serviceProvider.GetRequiredService<ILogger<TokensContextRETH>>(),
			cancellationToken);

		Task<TokensContextRockRETH> tokensContextRockRETHFactory = TokensContextRockRETH.ReadAsync(
			getDeploymentBlock,
			storage, contractsContext, options, serviceProvider.GetRequiredService<ILogger<TokensContextRockRETH>>(),
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

			TokensContextRPLFactory = tokensContextRPLFactory,
			TokensContextRPLOldFactory = tokensContextOldFactory,
			TokensContextStakedRPLFactory = tokensContextStakedRPLFactory,
			TokensContextRETHFactory = tokensContextRETHFactory,
			TokensContextRockRETHFactory = tokensContextRockRETHFactory,

			EnsContextFactory = EnsContext.ReadAsync(
				storage,
				nodesContextFactory,
				tokensContextRPLFactory,
				tokensContextOldFactory,
				tokensContextRETHFactory,
				tokensContextRockRETHFactory,
				addressEnsProcessHistory,
				serviceProvider.GetRequiredService<ILogger<EnsContext>>(),
				cancellationToken),
		};
	}

	public static List<Task> HandleTokenBlocksAsync(this IServiceProvider serviceProvider)
	{
		Task tokensSyncRPLTask = serviceProvider.GetRequiredService<TokensSyncRPL>().HandleBlocksAsync();
		Task tokensSyncRPLOldTask = serviceProvider.GetRequiredService<TokensSyncRPLOld>().HandleBlocksAsync();
		Task tokensSyncStakedRPLTask = serviceProvider.GetRequiredService<TokensSyncStakedRPL>().HandleBlocksAsync();
		Task tokensSyncRETHTask = serviceProvider.GetRequiredService<TokensSyncRETH>().HandleBlocksAsync();
		Task tokensSyncRockRETHTask = serviceProvider.GetRequiredService<TokensSyncRockRETH>().HandleBlocksAsync();

		return
		[
			tokensSyncRPLTask,
			tokensSyncRPLOldTask,
			tokensSyncStakedRPLTask,
			tokensSyncRETHTask,
			tokensSyncRockRETHTask,
		];
	}

	public static async Task<List<Task>> SaveTokenTasksAsync(this IServiceProvider serviceProvider)
	{
		GlobalContext globalContext = serviceProvider.GetRequiredService<GlobalContextAccessor>().GlobalContext ??
			throw new InvalidOperationException("GlobalContext not initialized");

		TokensContextRPL tokensContextRPL = await globalContext.TokensContextRPLFactory;
		TokensContextRPLOld tokensContextRPLOld = await globalContext.TokensContextRPLOldFactory;
		TokensContextStakedRPL tokensContextStakedRPL = await globalContext.TokensContextStakedRPLFactory;
		TokensContextRETH tokensContextRETH = await globalContext.TokensContextRETHFactory;
		TokensContextRockRETH tokensContextRockRETH = await globalContext.TokensContextRockRETHFactory;

		Task writeTokensRPLTask = tokensContextRPL.SaveAsync(
			globalContext.Services.Storage, serviceProvider.GetRequiredService<ILogger<TokensContextRPL>>());
		Task writeTokensRPLOldTask = tokensContextRPLOld.SaveAsync(
			globalContext.Services.Storage, serviceProvider.GetRequiredService<ILogger<TokensContextRPLOld>>());
		Task writeTokensStakedRPLTask = tokensContextStakedRPL.SaveAsync(
			globalContext.Services.Storage, serviceProvider.GetRequiredService<ILogger<TokensContextStakedRPL>>());
		Task writeTokensRETHTask = tokensContextRETH.SaveAsync(
			globalContext.Services.Storage, serviceProvider.GetRequiredService<ILogger<TokensContextRETH>>());
		Task writeTokensRockRETHTask = tokensContextRockRETH.SaveAsync(
			globalContext.Services.Storage, serviceProvider.GetRequiredService<ILogger<TokensContextRockRETH>>());

		return
		[
			writeTokensRPLTask,
			writeTokensRPLOldTask,
			writeTokensStakedRPLTask,
			writeTokensRETHTask,
			writeTokensRockRETHTask,
		];
	}
}