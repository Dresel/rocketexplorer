using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RocketExplorer.Core;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;
using RocketExplorer.Shared.Validators;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;
using static System.Net.WebRequestMethods;

IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(configuration => configuration.AddJsonFile("appsettings.local.json", true, true))
	.ConfigureLogging(logging =>
	{
		logging.ClearProviders();

		Logger logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo
			.Console(theme: AnsiConsoleTheme.Code)
			.CreateLogger();
		logging.AddSerilog(logger);
	})
	.ConfigureServices((context, services) =>
	{
		string environment = context.Configuration.GetValue<string>("Environment") ??
			throw new InvalidOperationException("Environment is null");

		services.Configure<SyncOptions>(context.Configuration.GetSection(environment));

		services.AddTransient<BeaconChainService>(provider => new BeaconChainService(
			new HttpClient
			{
				BaseAddress = new Uri(provider.GetRequiredService<IOptions<SyncOptions>>().Value.BeaconChainUrl),
			}));

		services.AddTransient<ContractsSync>();
		services.AddTransient<TokensSync>();
		services.AddTransient<NodesSync>();

		services.AddTransient<Storage>();

		services.AddTransient(_ =>
		{
			AmazonS3Client s3Client = new(
				context.Configuration["BlobStorage:User"],
				context.Configuration["BlobStorage:Password"],
				new AmazonS3Config
				{
					ServiceURL = context.Configuration["BlobStorage:Url"],
					ForcePathStyle = true,
					AuthenticationRegion = context.Configuration["BlobStorage:Region"],
					RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
					ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
				});

			return s3Client;
		});
	})
	.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
SyncOptions options = host.Services.GetRequiredService<IOptions<SyncOptions>>().Value;

logger.LogInformation(
	"Using Rocket Pool environment {Environment} with rpc endpoint {RPCUrl}", options.Environment, options.RPCUrl);

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
latestBlock = await web3.Eth.Blocks
	.GetBlockWithTransactionsByNumber
	.SendRequestAsync(new BlockParameter((ulong)(latestBlock.Number.Value - 12)));

logger.LogInformation("Latest block: {Block}", latestBlock.Number);

//var registry = web3.Eth.GetEvent<NewResolverEventDTO>(CommonAddresses.ENS_REGISTRY_ADDRESS);

//IEnumerable<IEventLog> nodeAddedEvents = await web3.FilterAsync(
//	23_200_000, 23_300_000, [typeof(NewResolverEventDTO),],
//	[CommonAddresses.ENS_REGISTRY_ADDRESS], NethereumPolicies.Retry(logger));

RocketStorageService rocketStorage = new(web3, options.RocketStorageContractAddress);
Storage storage = host.Services.GetRequiredService<Storage>();

List<MinipoolValidatorIndexEntry2> entries = new();

BlobObject<ValidatorSnapshot> validatorSnapshot =
	await storage.ReadAsync<ValidatorSnapshot>(Keys.ValidatorSnapshot) ?? throw new InvalidOperationException();

HttpClient httpClient = new HttpClient();

var count = 0;

foreach (MinipoolValidatorIndexEntry[] minipoolValidatorIndexEntry in validatorSnapshot.Data.MinipoolValidatorIndex.Where(x => x.PubKey is not null).Chunk(50))
{
	string query = $"https://ethereum-beacon-api.publicnode.com/eth/v1/beacon/states/head/validators?{string.Join("&", minipoolValidatorIndexEntry.Select(x => $"id={x.PubKey!.ToHex(true)}"))}";
	BeaconStateResult? beaconStateResult = await httpClient.GetFromJsonAsync<BeaconStateResult>(query) ?? throw new InvalidOperationException();

	entries.AddRange(minipoolValidatorIndexEntry.Select((entry, index) => new MinipoolValidatorIndexEntry2()
	{
		NodeAddress = entry.NodeAddress,
		MinipoolAddress = entry.MinipoolAddress,
		PubKey = entry.PubKey,
		ValidatorIndex = beaconStateResult.Data[index].Index,
	}));

	count += 50;

	if (count % 1000 == 0)
	{
		logger.LogInformation("Count: {Count}", count);
	}
}

await storage.WriteAsync(
	Keys.ValidatorSnapshot2, new BlobObject<ValidatorSnapshot2>()
	{
		ProcessedBlockNumber = validatorSnapshot.ProcessedBlockNumber,
		Data = new ValidatorSnapshot2()
		{
			MinipoolValidatorIndex = entries.ToArray(),
			MegapoolValidatorIndex = [],
		},
	});

Dictionary<ushort, List<IndexEntry>> index = Enumerable.Range(0, 65536).ToDictionary(
	x => (ushort)x, x => new List<IndexEntry>());

BlobObject<NodesSnapshot> nodesSnapshot =
	await storage.ReadAsync<NodesSnapshot>(Keys.NodesSnapshot) ?? throw new InvalidOperationException();

foreach (NodeIndexEntry entry in nodesSnapshot.Data.Index)
{
	foreach (ushort nGram in entry.ContractAddress.NGrams())
	{
		IndexEntry? indexEntry =
			index[nGram].SingleOrDefault(x => x.Address.AsSpan().SequenceEqual(entry.ContractAddress));

		if (indexEntry != null)
		{
			indexEntry.Type |= IndexEntryType.NodeOperator;
		}
		else
		{
			index[nGram].Add(
				new IndexEntry
				{
					Type = IndexEntryType.NodeOperator,
					Address = entry.ContractAddress,
				});
		}
	}
}

BlobObject<TokensRETHSnapshot2> rethSnapshot =
	await storage.ReadAsync<TokensRETHSnapshot2>(Keys.TokensRETHSnapshot2) ?? throw new InvalidOperationException();

List<ENSIndexEntry> ens = new();

foreach (HolderEntry2 entry in rethSnapshot.Data.RETH.Holders)
{
	byte[] address = Convert.FromHexString(entry.Address[2..]);

	foreach (ushort nGram in address.NGrams())
	{
		IndexEntry? indexEntry = index[nGram].SingleOrDefault(x => x.Address.AsSpan().SequenceEqual(address));

		if (indexEntry != null)
		{
			indexEntry.Type |= IndexEntryType.RETHHolder;
		}
		else
		{
			index[nGram].Add(
				new IndexEntry
				{
					Type = IndexEntryType.RETHHolder,
					Address = address,
				});
		}
	}

	if (!string.IsNullOrWhiteSpace(entry.ENSName))
	{
		ens.Add(new ENSIndexEntry()
		{
			Address = Convert.FromHexString(entry.Address[2..]),
			ENSName = entry.ENSName,
			Type = IndexEntryType.RETHHolder,
		});
	}
}

BlobObject<TokensRPLSnapshot> rplSnapshot =
	await storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot) ?? throw new InvalidOperationException();

foreach (HolderEntry entry in rplSnapshot.Data.RPL.Holders)
{
	byte[] address = Convert.FromHexString(entry.Address[2..]);

	foreach (ushort nGram in address.NGrams())
	{
		IndexEntry? indexEntry = index[nGram].SingleOrDefault(x => x.Address.AsSpan().SequenceEqual(address));

		if (indexEntry != null)
		{
			indexEntry.Type |= IndexEntryType.RPLHolder;
		}
		else
		{
			index[nGram].Add(
				new IndexEntry
				{
					Type = IndexEntryType.RPLHolder,
					Address = address,
				});
		}
	}
}

////BlobObject<ValidatorSnapshot> validatorSnapshot =
////	await storage.ReadAsync<ValidatorSnapshot>(Keys.ValidatorSnapshot) ?? throw new InvalidOperationException();

////foreach (MinipoolValidatorIndexEntry entry in validatorSnapshot.Data.MinipoolValidatorIndex)
////{
////	foreach (ushort nGram in entry.MinipoolAddress.NGrams())
////	{
////		IndexEntry? indexEntry =
////			index[nGram].SingleOrDefault(x => x.Address.AsSpan().SequenceEqual(entry.MinipoolAddress));

////		if (indexEntry != null)
////		{
////			indexEntry.Type |= IndexEntryType.MinipoolValidator;
////		}
////		else
////		{
////			index[nGram].Add(
////				new IndexEntry
////				{
////					Type = IndexEntryType.MinipoolValidator,
////					Address = entry.MinipoolAddress,
////				});
////		}
////	}

////	foreach (ushort nGram in entry.PubKey?.NGrams() ?? [])
////	{
////		IndexEntry? indexEntry =
////			index[nGram].SingleOrDefault(x => x.Address.AsSpan().SequenceEqual(entry.MinipoolAddress));

////		if (indexEntry != null)
////		{
////			indexEntry.Type |= IndexEntryType.MinipoolValidator;
////			indexEntry.ValidatorPubKey = entry.PubKey;
////		}
////		else
////		{
////			index[nGram].Add(
////				new IndexEntry
////				{
////					Type = IndexEntryType.MinipoolValidator,
////					ValidatorPubKey = entry.PubKey,
////				});
////		}
////	}
////}

//List<HolderEntry2> entries = [];

//int index = 0;

//foreach (var entry in nodesSnapshot.Data.RETH.Holders)
//{
//	ENSService ensService = web3.Eth.GetEnsService();
//	string? ensName = null;

//	if (++index % 100 == 0)
//	{
//		logger.LogInformation("Resolved {index}/{total}", index, nodesSnapshot.Data.RETH.Holders.Length);
//	}

//	try
//	{
//		ensName = await ensService.ReverseResolveAsync(entry.Address[2..]);
//	}
//	catch
//	{
//	}

//	entries.Add(new HolderEntry2()
//	{
//		Address = entry.Address,
//		ENSName = ensName,
//		Balance = entry.Balance,
//	});
//}

//logger.LogInformation("Found {Count}", entries.Count(x => x.ENSName is not null));

//await storage.WriteAsync(Keys.TokensRETHSnapshot2, new BlobObject<TokensRETHSnapshot2>()
//{
//	ProcessedBlockNumber = nodesSnapshot.ProcessedBlockNumber,
//	Data = new TokensRETHSnapshot2()
//	{
//		RETH = new()
//		{
//			Address = nodesSnapshot.Data.RETH.Address,
//			SupplyTotal = nodesSnapshot.Data.RETH.SupplyTotal,
//			MintsDaily = nodesSnapshot.Data.RETH.MintsDaily,
//			BurnsDaily = nodesSnapshot.Data.RETH.BurnsDaily,
//			Holders = entries.OrderBy(x => x.Address, StringComparer.OrdinalIgnoreCase).ToArray(),
//		},
//	},
//});

await storage.WriteAsync(
	$"ens.mgspack", new BlobObject<GlobalIndexENSSnapshot>
	{
		ProcessedBlockNumber = nodesSnapshot.ProcessedBlockNumber,
		Data = new()
		{
			Index = ens.ToArray(),
		},
	});

await Parallel.ForEachAsync(
	index.Where(x => x.Key == 57005),
	async (tuple, cancellationToken) =>
	{
		await storage.WriteAsync(
			$"index/{Convert.ToString(tuple.Key.ToString("x4"))}.mgspack",
			new BlobObject<GlobalIndexShardSnapshot>
			{
				ProcessedBlockNumber = nodesSnapshot.ProcessedBlockNumber,
				Data = new GlobalIndexShardSnapshot
				{
					Index = tuple.Value.ToArray(),
				},
			});
	});

return;

//// 0xa58E81fe9b61B5c3fE2AFD33CF304c454AbFc7Cb

////await storage.WriteCorsConfigurationAsync();

logger.LogInformation("Loading {snapshot}", Keys.DashboardSnapshot);

BlobObject<DashboardSnapshot> dashboardSnapshot =
	await storage.ReadAsync<DashboardSnapshot>(Keys.DashboardSnapshot) ??
	new BlobObject<DashboardSnapshot>
	{
		ProcessedBlockNumber = (long)latestBlock.Number.Value,
		Data = new DashboardSnapshot
		{
			RPLOldTotalSupply = 0,
			RPLTotalSupply = 0,
			RETHTotalSupply = 0,
			RPLSwapped = 0,
			RPLLegacyStakedTotal = 0,
			RPLMegapoolStakedTotal = 0,
			NodeOperators = 0,
			MinipoolValidatorsStaking = 0,
			MegapoolValidatorsStaking = 0,
			QueueLength = 0,
		},
	};

DashboardInfo dashboardInfo = new()
{
	RPLOldSupplyTotal = dashboardSnapshot.Data.RPLOldTotalSupply,
	RPLSupplyTotal = dashboardSnapshot.Data.RPLTotalSupply,
	RETHSupplyTotal = dashboardSnapshot.Data.RETHTotalSupply,
	RPLSwappedTotal = dashboardSnapshot.Data.RPLSwapped,
	NodeOperators = dashboardSnapshot.Data.NodeOperators,
	MinipoolValidatorsStaking = dashboardSnapshot.Data.MinipoolValidatorsStaking,
	MegapoolValidatorsStaking = dashboardSnapshot.Data.MegapoolValidatorsStaking,
	QueueLength = dashboardSnapshot.Data.QueueLength,
	RPLLegacyStakedTotal = dashboardSnapshot.Data.RPLLegacyStakedTotal,
	RPLMegapoolStakedTotal = dashboardSnapshot.Data.RPLMegapoolStakedTotal,
};

BeaconChainService beaconChainService = host.Services.GetRequiredService<BeaconChainService>();

ContractsSync contracts = host.Services.GetRequiredService<ContractsSync>();
await contracts.HandleBlocksAsync(
	web3, beaconChainService, rocketStorage, new Dictionary<string, RocketPoolContract>().AsReadOnly(), dashboardInfo,
	(long)latestBlock.Number.Value);

BlobObject<ContractsSnapshot> snapshot =
	await storage.ReadAsync<ContractsSnapshot>(Keys.ContractsSnapshotKey) ??
	throw new InvalidOperationException("Cannot read contracts snapshot from storage.");

TokensSync tokens = host.Services.GetRequiredService<TokensSync>();
await tokens.HandleBlocksAsync(
	web3, beaconChainService, rocketStorage, snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
	dashboardInfo, (long)latestBlock.Number.Value);

NodesSync nodes = host.Services.GetRequiredService<NodesSync>();
await nodes.HandleBlocksAsync(
	web3, beaconChainService, rocketStorage, snapshot.Data.Contracts.ToDictionary(x => x.Name, x => x).AsReadOnly(),
	dashboardInfo, (long)latestBlock.Number.Value);

logger.LogInformation("Writing {snapshot}", Keys.DashboardSnapshot);
await storage.WriteAsync(
	Keys.DashboardSnapshot,
	new BlobObject<DashboardSnapshot>
	{
		ProcessedBlockNumber = (long)latestBlock.Number.Value,
		Data = new DashboardSnapshot
		{
			RPLOldTotalSupply = dashboardInfo.RPLOldSupplyTotal,
			RPLTotalSupply = dashboardInfo.RPLSupplyTotal,
			RETHTotalSupply = dashboardInfo.RETHSupplyTotal,
			RPLSwapped = dashboardInfo.RPLSwappedTotal,
			RPLLegacyStakedTotal = dashboardInfo.RPLLegacyStakedTotal,
			RPLMegapoolStakedTotal = dashboardInfo.RPLMegapoolStakedTotal,
			NodeOperators = dashboardInfo.NodeOperators,
			MinipoolValidatorsStaking = dashboardInfo.MinipoolValidatorsStaking,
			MegapoolValidatorsStaking = dashboardInfo.MegapoolValidatorsStaking,
			QueueLength = dashboardInfo.QueueLength,
		},
	});

logger.LogInformation("Writing {snapshot}", Keys.SnapshotMetadata);
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

public class BeaconStateResult
{
	public BeaconStateEntry[] Data { get; set; }
}

public class BeaconStateEntry
{
	public long? Index { get; set; }
}