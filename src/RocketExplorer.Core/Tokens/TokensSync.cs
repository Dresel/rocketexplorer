using System.Collections.ObjectModel;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3;
using RocketExplorer.Core.BeaconChain;
using RocketExplorer.Core.Nodes;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Ethereum.RocketStorage;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Contracts;
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
		Web3 web3, BeaconChainService beaconChainService, RocketStorageService rocketStorage, ReadOnlyDictionary<string, RocketPoolContract> contracts,
		DashboardInfo dashboardInfo,
		CancellationToken cancellationToken = default)
	{
		string rplContractAddress = contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();
		string rplOldContractAddress = contracts["rocketTokenRPLFixedSupply"].Versions.Select(x => x.Address).Single();
		string rethContractAddress = contracts["rocketTokenRETH"].Versions.Select(x => x.Address).Single();

		Logger.LogInformation("Loading token snapshots");

		Task<BlobObject<TokensRPLOldSnapshot>?> readRPLOldTask =
			Storage.ReadAsync<TokensRPLOldSnapshot>(Keys.TokensRPLOldSnapshot, cancellationToken);
		Task<BlobObject<TokensRPLSnapshot>?> readRPLTask =
			Storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot, cancellationToken);
		Task<BlobObject<TokensRETHSnapshot>?> readRETHTask =
			Storage.ReadAsync<TokensRETHSnapshot>(Keys.TokensRETHSnapshot, cancellationToken);
		Task<BlobObject<StakedRPLSnapshot>?> readStakedRPLTask =
			Storage.ReadAsync<StakedRPLSnapshot>(Keys.TokensStakedRPLSnapshot, cancellationToken);

		await Task.WhenAll(readRPLOldTask, readRPLTask, readRETHTask, readStakedRPLTask);

		BlobObject<TokensRPLOldSnapshot> rplOldSnapshot =
			await readRPLOldTask ??
			new BlobObject<TokensRPLOldSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new TokensRPLOldSnapshot
				{
					RPLOld = new RPLOldToken
					{
						Address = rplOldContractAddress,
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
						SwappedDaily = [],
						SwappedTotal = [],
					},
				},
			};

		BlobObject<TokensRPLSnapshot> rplSnapshot =
			await readRPLTask ??
			new BlobObject<TokensRPLSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new TokensRPLSnapshot
				{
					RPL = new Token
					{
						Address = rplContractAddress,
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
					},
				},
			};

		BlobObject<TokensRETHSnapshot> rethSnapshot =
			await readRETHTask ??
			new BlobObject<TokensRETHSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new TokensRETHSnapshot
				{
					RETH = new Token
					{
						Address = rethContractAddress,
						Holders = [],
						SupplyTotal = [],
						MintsDaily = [],
						BurnsDaily = [],
					},
				},
			};

		BlobObject<StakedRPLSnapshot> stakedRPLSnapshot =
			await readStakedRPLTask ??
			new BlobObject<StakedRPLSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new StakedRPLSnapshot
				{
					LegacyStakedDaily = [],
					LegacyUnstakedDaily = [],
					LegacyStakedTotal = [],
					MegapoolStakedDaily = [],
					MegapoolUnstakedDaily = [],
					MegapoolStakedTotal = [],
				},
			};

		return new TokensSyncContext
		{
			Storage = Storage,
			Policy = Policy,
			Logger = Logger,
			Web3 = web3,
			BeaconChainService = beaconChainService,
			DashboardInfo = dashboardInfo,
			CurrentBlockHeight = rplSnapshot.ProcessedBlockNumber,
			RocketStorage = rocketStorage,
			Contracts = contracts,
			PreSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions.Where(x => x.Version <= 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contracts["rocketNodeStaking"].Versions.Where(x => x.Version > 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),
			RPLOldTokenInfo = new RPLOldTokenInfo
			{
				Holders = new SortedDictionary<string, BigInteger>(
					rplOldSnapshot.Data.RPLOld.Holders.Select(entry =>
						new KeyValuePair<string, BigInteger>(entry.Address, entry.Balance)).ToDictionary(),
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rplOldSnapshot.Data.RPLOld.SupplyTotal,
				MintsDaily = rplOldSnapshot.Data.RPLOld.MintsDaily,
				BurnsDaily = rplOldSnapshot.Data.RPLOld.BurnsDaily,
				SwappedTotal = rplOldSnapshot.Data.RPLOld.SwappedTotal,
				SwappedDaily = rplOldSnapshot.Data.RPLOld.SwappedDaily,
			},
			RPLTokenInfo = new TokenInfo
			{
				Holders = new SortedDictionary<string, BigInteger>(
					rplSnapshot.Data.RPL.Holders.Select(entry =>
						new KeyValuePair<string, BigInteger>(entry.Address, entry.Balance)).ToDictionary(),
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rplSnapshot.Data.RPL.SupplyTotal,
				MintsDaily = rplSnapshot.Data.RPL.MintsDaily,
				BurnsDaily = rplSnapshot.Data.RPL.BurnsDaily,
			},
			RETHTokenInfo = new TokenInfo
			{
				Holders = new SortedDictionary<string, BigInteger>(
					rethSnapshot.Data.RETH.Holders.Select(entry =>
						new KeyValuePair<string, BigInteger>(entry.Address, entry.Balance)).ToDictionary(),
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rethSnapshot.Data.RETH.SupplyTotal,
				MintsDaily = rethSnapshot.Data.RETH.MintsDaily,
				BurnsDaily = rethSnapshot.Data.RETH.BurnsDaily,
			},
			StakedRPLInfo = new StakedRPLInfo
			{
				LegacyStakedTotal = stakedRPLSnapshot.Data.LegacyStakedTotal,
				LegacyStakedDaily = stakedRPLSnapshot.Data.LegacyStakedDaily,
				LegacyUnstakedDaily = stakedRPLSnapshot.Data.LegacyUnstakedDaily,
				MegapoolStakedTotal = stakedRPLSnapshot.Data.MegapoolStakedTotal,
				MegapoolStakedDaily = stakedRPLSnapshot.Data.MegapoolStakedDaily,
				MegapoolUnstakedDaily = stakedRPLSnapshot.Data.MegapoolUnstakedDaily,
			},
		};
	}

	protected override async Task SaveContextAsync(
		TokensSyncContext context, CancellationToken cancellationToken = default)
	{
		Logger.LogInformation("Writing {snapshot}", Keys.TokensRPLOldSnapshot);

		Task writeRplOldTask = storage.WriteAsync(
			Keys.TokensRPLOldSnapshot, new BlobObject<TokensRPLOldSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new TokensRPLOldSnapshot
				{
					RPLOld = new RPLOldToken
					{
						Address = context.RPLOldTokenAddress,
						Holders = context.RPLOldTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key,
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RPLOldTokenInfo.SupplyTotal,
						MintsDaily = context.RPLOldTokenInfo.MintsDaily,
						BurnsDaily = context.RPLOldTokenInfo.BurnsDaily,
						SwappedTotal = context.RPLOldTokenInfo.SwappedTotal,
						SwappedDaily = context.RPLOldTokenInfo.SwappedDaily,
					},
				},
			}, cancellationToken: cancellationToken);

		Logger.LogInformation("Writing {snapshot}", Keys.TokensRPLSnapshot);

		Task writeRPLTask = storage.WriteAsync(
			Keys.TokensRPLSnapshot, new BlobObject<TokensRPLSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new TokensRPLSnapshot
				{
					RPL = new Token
					{
						Address = context.RPLTokenAddress,
						Holders = context.RPLTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key,
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RPLTokenInfo.SupplyTotal,
						MintsDaily = context.RPLTokenInfo.MintsDaily,
						BurnsDaily = context.RPLTokenInfo.BurnsDaily,
					},
				},
			}, cancellationToken: cancellationToken);

		Logger.LogInformation("Writing {snapshot}", Keys.TokensRETHSnapshot);

		Task writeRETHTask = storage.WriteAsync(
			Keys.TokensRETHSnapshot, new BlobObject<TokensRETHSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new TokensRETHSnapshot
				{
					RETH = new Token
					{
						Address = context.RETHTokenAddress,
						Holders = context.RETHTokenInfo.Holders.Select(x => new HolderEntry
						{
							Address = x.Key,
							Balance = x.Value,
						}).ToArray(),
						SupplyTotal = context.RETHTokenInfo.SupplyTotal,
						MintsDaily = context.RETHTokenInfo.MintsDaily,
						BurnsDaily = context.RETHTokenInfo.BurnsDaily,
					},
				},
			}, cancellationToken: cancellationToken);

		Logger.LogInformation("Writing {snapshot}", Keys.TokensStakedRPLSnapshot);

		Task writeStakedRPLTask = storage.WriteAsync(
			Keys.TokensStakedRPLSnapshot, new BlobObject<StakedRPLSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new StakedRPLSnapshot
				{
					LegacyStakedTotal = context.StakedRPLInfo.LegacyStakedTotal,
					LegacyStakedDaily = context.StakedRPLInfo.LegacyStakedDaily,
					LegacyUnstakedDaily = context.StakedRPLInfo.LegacyUnstakedDaily,
					MegapoolStakedTotal = context.StakedRPLInfo.MegapoolStakedTotal,
					MegapoolStakedDaily = context.StakedRPLInfo.MegapoolStakedDaily,
					MegapoolUnstakedDaily = context.StakedRPLInfo.MegapoolUnstakedDaily,
				},
			}, cancellationToken: cancellationToken);

		await Task.WhenAll(writeRplOldTask, writeRPLTask, writeRETHTask, writeStakedRPLTask);
	}
}