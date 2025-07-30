using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using RocketExplorer.Shared.Contracts;
using RocketExplorer.Shared.Nodes;
using RocketExplorer.Shared.Tokens;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSync(IOptions<SyncOptions> options, Storage storage, ILogger<TokensSync> logger)
	: SyncBase<TokensSyncContext>(options, storage, logger)
{
	protected override async Task HandleBlocksAsync(
		TokensSyncContext context, long fromBlock, long toBlock, long latestBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> rplOldEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RPLOldTokenAddress,], Policy);

		foreach (IEventLog eventLog in rplOldEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, TokensSyncContext>(
				TokenEventHandlers.HandleRPLOldAsync, context, cancellationToken);
		}

		IEnumerable<IEventLog> rplEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO), typeof(RPLFixedSupplyBurnEventDTO),],
			[context.RPLTokenAddress,], Policy);

		foreach (IEventLog eventLog in rplEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, TokensSyncContext>(
				TokenEventHandlers.HandleRPLAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<RPLFixedSupplyBurnEventDTO, TokensSyncContext>(
				TokenEventHandlers.Handle, context, cancellationToken);
		}

		IEnumerable<IEventLog> rethEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RETHTokenAddress,], Policy);

		foreach (IEventLog eventLog in rethEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, TokensSyncContext>(
				TokenEventHandlers.HandleRETHAsync, context, cancellationToken);
		}
	}

	protected override async Task<TokensSyncContext> LoadContextAsync(
		Web3 web3, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		DashboardInfo dashboardInfo,
		CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Loading {snapshot}", Keys.TokensSnapshot);

		BlobObject<TokensSnapshot> snapshot =
			await Storage.ReadAsync<TokensSnapshot>(Keys.TokensSnapshot, cancellationToken) ??
			new BlobObject<TokensSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new TokensSnapshot
				{
					RPLOld = new Token
					{
						Holders = [],
						SupplyTotal = [],
						MintsPerDay = [],
						BurnsPerDay = [],
					},
					RPL = new Token
					{
						Holders = [],
						SupplyTotal = [],
						MintsPerDay = [],
						BurnsPerDay = [],
					},
					RETH = new Token
					{
						Holders = [],
						SupplyTotal = [],
						MintsPerDay = [],
						BurnsPerDay = [],
					},
					RPLSwappedTotal = [],
					RPLSwappedPerDay = [],
				},
			};

		return new TokensSyncContext
		{
			Storage = Storage,
			Policy = Policy,
			Logger = Logger,
			Web3 = web3,
			DashboardInfo = dashboardInfo,
			CurrentBlockHeight = snapshot.ProcessedBlockNumber,
			RocketStorage = rocketStorage,
			Contracts = contracts,
			RPLOldTokenInfo = new TokenInfo
			{
				Holders = snapshot.Data.RPLOld.Holders.ToDictionary(
					x => x.Address.ToHex(true), x => x.Balance, StringComparer.OrdinalIgnoreCase),
				SupplyTotal = snapshot.Data.RPLOld.SupplyTotal,
				MintsPerDay = snapshot.Data.RPLOld.MintsPerDay,
				BurnsPerDay = snapshot.Data.RPLOld.BurnsPerDay,
			},
			RPLTokenInfo = new RPLTokenInfo
			{
				Holders = snapshot.Data.RPL.Holders.ToDictionary(
					x => x.Address.ToHex(true), x => x.Balance, StringComparer.OrdinalIgnoreCase),
				SupplyTotal = snapshot.Data.RPL.SupplyTotal,
				MintsPerDay = snapshot.Data.RPL.MintsPerDay,
				BurnsPerDay = snapshot.Data.RPL.BurnsPerDay,
				SwappedTotal = snapshot.Data.RPLSwappedTotal,
				SwappedDaily = snapshot.Data.RPLSwappedPerDay,
			},
			RETHTokenInfo = new TokenInfo
			{
				Holders = snapshot.Data.RETH.Holders.ToDictionary(
					x => x.Address.ToHex(true), x => x.Balance, StringComparer.OrdinalIgnoreCase),
				SupplyTotal = snapshot.Data.RETH.SupplyTotal,
				MintsPerDay = snapshot.Data.RETH.MintsPerDay,
				BurnsPerDay = snapshot.Data.RETH.BurnsPerDay,
			},
		};
	}

	protected override async Task SaveContextAsync(
		TokensSyncContext context, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Writing {snapshot}", Keys.TokensSnapshot);

		await Storage.WriteAsync(
			Keys.TokensSnapshot,
			new BlobObject<TokensSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new TokensSnapshot
				{
					RPLOld = new Token
					{
						Holders = context.RPLOldTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key.HexToByteArray(),
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RPLOldTokenInfo.SupplyTotal,
						MintsPerDay = context.RPLOldTokenInfo.MintsPerDay,
						BurnsPerDay = context.RPLOldTokenInfo.BurnsPerDay,
					},
					RPL = new Token
					{
						Holders = context.RPLTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key.HexToByteArray(),
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RPLTokenInfo.SupplyTotal,
						MintsPerDay = context.RPLTokenInfo.MintsPerDay,
						BurnsPerDay = context.RPLTokenInfo.BurnsPerDay,
					},
					RETH = new Token
					{
						Holders = context.RETHTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key.HexToByteArray(),
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RETHTokenInfo.SupplyTotal,
						MintsPerDay = context.RETHTokenInfo.MintsPerDay,
						BurnsPerDay = context.RETHTokenInfo.BurnsPerDay,
					},
					RPLSwappedTotal = context.RPLTokenInfo.SwappedTotal,
					RPLSwappedPerDay = context.RPLTokenInfo.SwappedDaily,
				},
			}, cancellationToken: cancellationToken);
	}
}