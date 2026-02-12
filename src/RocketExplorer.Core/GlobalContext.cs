using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.RPC.Eth.DTOs;
using Polly.Retry;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Ens;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public record class GlobalContext
{
	public ReadOnlyDictionary<string, RocketPoolContract> Contracts => ContractsContext.ContextContracts.AsReadOnly();

	public required ContractsContext ContractsContext { get; init; }

	public required DashboardInfo DashboardContext { get; init; }

	public required Task<EnsContext> EnsContextFactory { get; init; }

	public required BlockWithTransactions LatestBlock { get; init; }

	public long LatestBlockHeight => (long)LatestBlock.Number.Value;

	public required ILoggerFactory LoggerFactory { get; set; }

	public required Task<NodesMasterContext> NodesMasterContextFactory { get; init; }

	public required AsyncRetryPolicy Policy { get; init; }

	public string RocketStorageAddress => Contracts["rocketStorage"].Versions.Single().Address;

	public required ContextServices Services { get; init; }

	public required Task<TokensContextRETH> TokensContextRETHFactory { get; init; }

	public required Task<TokensContextRockRETH> TokensContextRockRETHFactory { get; init; }

	public required Task<TokensContextRPL> TokensContextRPLFactory { get; init; }

	public required Task<TokensContextRPLOld> TokensContextRPLOldFactory { get; init; }

	public required Task<TokensContextStakedRPL> TokensContextStakedRPLFactory { get; init; }

	public ILogger GetLogger<T>() => LoggerFactory.CreateLogger<T>();
}