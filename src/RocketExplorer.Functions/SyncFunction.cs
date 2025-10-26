using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Ens;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared;

namespace RocketExplorer.Functions;

public class SyncFunction(IServiceProvider serviceProvider)
{
	private readonly IServiceProvider serviceProvider = serviceProvider;

	[Function("SyncFunction")]
	public async Task Run([TimerTrigger("%SyncFunctionSchedule%")] TimerInfo myTimer)
	{
		GlobalContext globalContext = await this.serviceProvider.CreateGlobalContextAsync();
		this.serviceProvider.GetRequiredService<GlobalContextAccessor>().GlobalContext = globalContext;

		ILogger<SyncFunction> logger = this.serviceProvider.GetRequiredService<ILogger<SyncFunction>>();

		Task contractsSyncTask = this.serviceProvider.GetRequiredService<ContractsSync>().HandleBlocksAsync();
		Task tokensSyncTask = this.serviceProvider.GetRequiredService<TokensSync>().HandleBlocksAsync();
		Task nodesSyncTask = this.serviceProvider.GetRequiredService<NodesSync>().HandleBlocksAsync();
		Task ensSyncTask = this.serviceProvider.GetRequiredService<EnsSync>().HandleBlocksAsync();

		await Task.WhenAll(contractsSyncTask, tokensSyncTask, nodesSyncTask, ensSyncTask);

		Storage storage = this.serviceProvider.GetRequiredService<Storage>();

		Task writeContractsTask = globalContext.ContractsContext.SaveAsync(
			storage, this.serviceProvider.GetRequiredService<ILogger<ContractsContext>>());
		NodesContext nodesContext = await globalContext.NodesContextFactory;
		Task writeNodesTask = nodesContext.SaveAsync(
			storage, this.serviceProvider.GetRequiredService<ILogger<NodesContext>>());
		TokensContext tokensContext = await globalContext.TokensContextFactory;
		Task writeTokensTask = tokensContext.SaveAsync(
			storage, this.serviceProvider.GetRequiredService<ILogger<TokensContext>>());
		EnsContext ensContext = await globalContext.EnsContextFactory;
		Task writeEnsTask = ensContext.SaveAsync(
			globalContext.Services.Storage, this.serviceProvider.GetRequiredService<ILogger<EnsContext>>());

		Task writeDashboardTask = globalContext.DashboardContext.SaveAsync(
			storage, globalContext.LatestBlockHeight, this.serviceProvider.GetRequiredService<ILogger<DashboardInfo>>());

		logger.LogInformation("Writing {snapshot}", Keys.SnapshotMetadata);
		Task writeMetadataTask = storage.WriteAsync(
			Keys.SnapshotMetadata, new BlobObject<SnapshotMetadata>
			{
				ProcessedBlockNumber = globalContext.LatestBlockHeight,
				Data = new SnapshotMetadata
				{
					BlockNumber = globalContext.LatestBlockHeight,
					Timestamp = (long)globalContext.LatestBlock.Timestamp.Value,
				},
			}, 10);

		Task writeIndexTask = globalContext.Services.GlobalIndexService.WriteAsync(globalContext.LatestBlockHeight);
		Task writeEnsIndexTask = globalContext.Services.GlobalEnsIndexService.WriteAsync(globalContext.LatestBlockHeight);

		await Task.WhenAll(writeContractsTask, writeNodesTask, writeTokensTask, writeEnsTask, writeDashboardTask, writeMetadataTask, writeIndexTask, writeEnsIndexTask);

		logger.LogInformation("Sync completed");
	}
}