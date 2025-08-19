using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Web3;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
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

		IEnumerable<IEventLog> preSaturn1StakingEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [
				typeof(RPLLegacyStakedEventDto),
				typeof(RPLOrRPLLegacyWithdrawnEventDTO),
			],
			context.PreSaturn1RocketNodeStakingAddresses, Policy);

		foreach (IEventLog eventLog in preSaturn1StakingEvents)
		{
			eventLog.WhenIs<RPLLegacyStakedEventDto, TokensSyncContext>(
				StakingEventHandlers.HandleRPLLegacyStaked, context);

			eventLog.WhenIs<RPLOrRPLLegacyWithdrawnEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLLegacyUnstaked, context);
		}

		IEnumerable<IEventLog> postSaturn1StakingEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [
				typeof(RPLLegacyWithdrawnEventDTO),
				typeof(RPLStakedEventDTO),
				typeof(RPLUnstakedEventDTO),
			],
			context.PostSaturn1RocketNodeStakingAddresses, Policy);

		foreach (IEventLog eventLog in postSaturn1StakingEvents)
		{
			eventLog.WhenIs<RPLLegacyWithdrawnEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLLegacyUnstaked, context);

			eventLog.WhenIs<RPLStakedEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLMegapoolStaked, context);

			eventLog.WhenIs<RPLUnstakedEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLMegapoolUnstaked, context);
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
						MintsDaily = [],
						BurnsDaily = [],
					},
					RPL = new RPLToken
					{
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
						LegacyStakedDaily = [],
						LegacyUnstakedDaily = [],
						LegacyStakedTotal = [],
						MegapoolStakedDaily = [],
						MegapoolUnstakedDaily = [],
						MegapoolStakedTotal = [],
					},
					RETH = new Token
					{
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
					},
					RPLSwappedTotal = [],
					RPLSwappedDaily = [],
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
			PreSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions.Where(x => x.Version <= 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS]).ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions.Where(x => x.Version > 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS]).ToArray(),
			RPLOldTokenInfo = new TokenInfo
			{
				Holders = snapshot.Data.RPLOld.Holders.ToDictionary(
					x => x.Address.ToHex(true), x => x.Balance, StringComparer.OrdinalIgnoreCase),
				SupplyTotal = snapshot.Data.RPLOld.SupplyTotal,
				MintsDaily = snapshot.Data.RPLOld.MintsDaily,
				BurnsDaily = snapshot.Data.RPLOld.BurnsDaily,
			},
			RPLTokenInfo = new RPLTokenInfo
			{
				Holders = snapshot.Data.RPL.Holders.ToDictionary(
					x => x.Address.ToHex(true), x => x.Balance, StringComparer.OrdinalIgnoreCase),
				SupplyTotal = snapshot.Data.RPL.SupplyTotal,
				MintsDaily = snapshot.Data.RPL.MintsDaily,
				BurnsDaily = snapshot.Data.RPL.BurnsDaily,
				SwappedTotal = snapshot.Data.RPLSwappedTotal,
				SwappedDaily = snapshot.Data.RPLSwappedDaily,
				LegacyStakedDaily = snapshot.Data.RPL.LegacyStakedDaily,
				LegacyUnstakedDaily = snapshot.Data.RPL.LegacyUnstakedDaily,
				LegacyStakedTotal = snapshot.Data.RPL.LegacyStakedTotal,
				MegapoolStakedDaily = snapshot.Data.RPL.MegapoolStakedDaily,
				MegapoolUnstakedDaily = snapshot.Data.RPL.MegapoolUnstakedDaily,
				MegapoolStakedTotal = snapshot.Data.RPL.MegapoolStakedTotal,
			},
			RETHTokenInfo = new TokenInfo
			{
				Holders = snapshot.Data.RETH.Holders.ToDictionary(
					x => x.Address.ToHex(true), x => x.Balance, StringComparer.OrdinalIgnoreCase),
				SupplyTotal = snapshot.Data.RETH.SupplyTotal,
				MintsDaily = snapshot.Data.RETH.MintsDaily,
				BurnsDaily = snapshot.Data.RETH.BurnsDaily,
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
						MintsDaily = context.RPLOldTokenInfo.MintsDaily,
						BurnsDaily = context.RPLOldTokenInfo.BurnsDaily,
					},
					RPL = new RPLToken
					{
						Holders = context.RPLTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key.HexToByteArray(),
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RPLTokenInfo.SupplyTotal,
						MintsDaily = context.RPLTokenInfo.MintsDaily,
						BurnsDaily = context.RPLTokenInfo.BurnsDaily,
						LegacyStakedDaily = context.RPLTokenInfo.LegacyStakedDaily,
						LegacyUnstakedDaily = context.RPLTokenInfo.LegacyUnstakedDaily,
						LegacyStakedTotal = context.RPLTokenInfo.LegacyStakedTotal,
						MegapoolStakedDaily = context.RPLTokenInfo.MegapoolStakedDaily,
						MegapoolUnstakedDaily = context.RPLTokenInfo.MegapoolUnstakedDaily,
						MegapoolStakedTotal = context.RPLTokenInfo.MegapoolStakedTotal,
					},
					RETH = new Token
					{
						Holders = context.RETHTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key.HexToByteArray(),
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RETHTokenInfo.SupplyTotal,
						MintsDaily = context.RETHTokenInfo.MintsDaily,
						BurnsDaily = context.RETHTokenInfo.BurnsDaily,
					},
					RPLSwappedTotal = context.RPLTokenInfo.SwappedTotal,
					RPLSwappedDaily = context.RPLTokenInfo.SwappedDaily,
				},
			}, cancellationToken: cancellationToken);
	}
}