using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RocketExplorer.Core;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Ens;
using RocketExplorer.Core.Nodes;
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
		Task nodesSyncTask = this.serviceProvider.GetRequiredService<NodesSync>().HandleBlocksAsync();
		List<Task> tokenSyncTasks = this.serviceProvider.HandleTokenBlocksAsync();

		await Task.WhenAll([contractsSyncTask, nodesSyncTask, ..tokenSyncTasks]);

		Storage storage = this.serviceProvider.GetRequiredService<Storage>();

		Task writeContractsTask = globalContext.ContractsContext.SaveAsync(storage, this.serviceProvider.GetRequiredService<ILogger<ContractsContext>>());

		NodesMasterContext nodesMasterContext = await globalContext.NodesMasterContextFactory;
		Task writeNodesTask = nodesMasterContext.SaveAsync(storage, this.serviceProvider.GetRequiredService<ILogger<NodesMasterContext>>());

		List<Task> writeTokenTasks = await this.serviceProvider.SaveTokenTasksAsync();

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

		await Task.WhenAll([writeContractsTask, writeNodesTask, ..writeTokenTasks, writeDashboardTask, writeMetadataTask]);

		await this.serviceProvider.GetRequiredService<EnsSync>().HandleBlocksAsync();

		EnsContext ensContext = await globalContext.EnsContextFactory;
		Task writeEnsTask = ensContext.SaveAsync(globalContext.Services.Storage, this.serviceProvider.GetRequiredService<ILogger<EnsContext>>());
		Task writeIndexTask = globalContext.Services.GlobalIndexService.WriteAsync(globalContext.LatestBlockHeight);
		Task writeEnsIndexTask = globalContext.Services.GlobalEnsIndexService.WriteAsync(globalContext.LatestBlockHeight);

		await Task.WhenAll(writeEnsTask, writeIndexTask, writeEnsIndexTask);

		logger.LogInformation("Sync completed");
	}
}