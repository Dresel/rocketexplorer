using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Nethereum.RPC.Eth.DTOs;
using Polly.Retry;
using RocketExplorer.Core.Contracts;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Core.Tokens;
using RocketExplorer.Shared.Contracts;

namespace RocketExplorer.Core;

public record class GlobalContext
{
	public ReadOnlyDictionary<string, RocketPoolContract> Contracts => ContractsContext.ContextContracts.AsReadOnly();

	public required ContractsContext ContractsContext { get; init; }

	public required DashboardInfo DashboardContext { get; init; }

	public required BlockWithTransactions LatestBlock { get; init; }

	public long LatestBlockHeight => (long)LatestBlock.Number.Value;

	public required ILoggerFactory LoggerFactory { get; set; }

	public required Task<NodesContext> NodesContextFactory { get; init; }

	public required AsyncRetryPolicy Policy { get; init; }

	public required ContextServices Services { get; init; }

	public required Task<TokensContext> TokensContextFactory { get; init; }

	public ILogger GetLogger<T>() => LoggerFactory.CreateLogger<T>();
}