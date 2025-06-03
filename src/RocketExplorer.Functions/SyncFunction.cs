using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Core;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Nodes;

namespace RocketExplorer.Functions;

public class SyncFunction
{
	private readonly ContractsSync contracts;
	private readonly ILogger logger;
	private readonly NodesSync nodes;
	private readonly SyncOptions options;
	private readonly Storage storage;

	public SyncFunction(
		IOptions<SyncOptions> options, Storage storage, ContractsSync contracts, NodesSync nodes,
		ILogger<SyncFunction> logger)
	{
		this.options = options.Value;
		this.storage = storage;
		this.contracts = contracts;
		this.nodes = nodes;
		this.logger = logger;
	}

	[Function("SyncFunction")]
	public async Task Run([TimerTrigger("*/15 * * * * *")] TimerInfo myTimer)
	{
		Web3 web3;

		if (!string.IsNullOrWhiteSpace(options.RpcBasicAuthUsername) &&
			!string.IsNullOrWhiteSpace(options.RpcBasicAuthPassword))
		{
			logger.LogInformation("Using BasicAuth...");

			byte[] byteArray = Encoding.ASCII.GetBytes($"{options.RpcBasicAuthUsername}:{options.RpcBasicAuthPassword}");
			AuthenticationHeaderValue authenticationHeaderValue = new("Basic", Convert.ToBase64String(byteArray));
			web3 = new Web3(options.RPCUrl, authenticationHeader: authenticationHeaderValue);
		}
		else
		{
			web3 = new Web3(options.RPCUrl);
		}

		BlockWithTransactions latestBlock =
			await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
		this.logger.LogInformation("Latest block: {Block}", latestBlock.Number);

		RocketStorageService rocketStorage = new(web3, this.options.RocketStorageContractAddress);

		////await storage.WriteCorsConfigurationAsync();

		await contracts.HandleBlocksAsync(
			web3, rocketStorage, new Dictionary<string, RocketPoolContract>().AsReadOnly(),
			(long)latestBlock.Number.Value);

		BlobObject<ContractsSnapshot> snapshot =
			await storage.ReadAsync<ContractsSnapshot>(ContractsSync.ContractsSnapshotKey) ??
			throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

		await nodes.HandleBlocksAsync(
			web3, rocketStorage, snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
			(long)latestBlock.Number.Value);

		this.logger.LogInformation("Writing {snapshot}", Keys.SnapshotMetadata);
		await storage.WriteAsync(
			Keys.SnapshotMetadata, new BlobObject<SnapshotMetadata>
			{
				ProcessedBlockNumber = (long)latestBlock.Number.Value,
				Data = new SnapshotMetadata
				{
					BlockNumber = (long)latestBlock.Number.Value,
					Timestamp = (long)latestBlock.Timestamp.Value,
				},
			}, 10);
	}
}