using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core;
using RocketExplorer.Core.Contracts;
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

		await Task.WhenAll(contractsSyncTask, tokensSyncTask, nodesSyncTask);

		Storage storage = this.serviceProvider.GetRequiredService<Storage>();

		Task writeContractsTask = globalContext.ContractsContext.SaveAsync(
			storage, this.serviceProvider.GetRequiredService<ILogger<ContractsContext>>());
		NodesContext nodesContext = await globalContext.NodesContextFactory;
		Task writeNodesTask = nodesContext.SaveAsync(
			storage, this.serviceProvider.GetRequiredService<ILogger<NodesContext>>());
		TokensContext tokensContext = await globalContext.TokensContextFactory;
		Task writeTokensTask = tokensContext.SaveAsync(
			storage, this.serviceProvider.GetRequiredService<ILogger<TokensContext>>());

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

		await Task.WhenAll(writeContractsTask, writeNodesTask, writeTokensTask, writeDashboardTask, writeMetadataTask);
		await globalContext.Services.GlobalIndexService.WriteAsync(globalContext.LatestBlockHeight);

		logger.LogInformation("Finished");
	}
}