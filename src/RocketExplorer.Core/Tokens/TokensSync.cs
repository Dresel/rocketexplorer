using System.Globalization;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Util;
using RocketExplorer.Ethereum;
using RocketExplorer.Ethereum.RocketNodeStaking.ContractDefinition;
using RocketExplorer.Ethereum.RocketTokenRPL.ContractDefinition;
using RocketExplorer.Shared;
using RocketExplorer.Shared.Tokens;
using TransferEventDTO = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace RocketExplorer.Core.Tokens;

public class TokensSync : SyncBase<TokensSyncContext>
{
	private readonly string? rockRETHTokenAddress;

	public TokensSync(IOptions<SyncOptions> options) : base(options)
	{
		this.rockRETHTokenAddress = options.Value.Environment.ToLower(CultureInfo.InvariantCulture) switch
		{
			"testnet" => "0x066b118c1A8012E6E5DFf4274c91A4B85F5f3CC2",
			"local-testnet" => "0x066b118c1A8012E6E5DFf4274c91A4B85F5f3CC2",
			"mainnet" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			"local-mainnet" => "0x936faCdf10c8c36294e7b9d28345255539d81bc7",
			_ => null,
		};
	}

	protected override async Task HandleBlocksAsync(
		TokensSyncContext context, long fromBlock, long toBlock,
		CancellationToken cancellationToken = default)
	{
		IEnumerable<IEventLog> rplOldEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RPLOldTokenAddress,], context.Policy);

		foreach (IEventLog eventLog in rplOldEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, TokensSyncContext>(
				TokenEventHandlers.HandleRPLOldAsync, context, cancellationToken);
		}

		IEnumerable<IEventLog> rplEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO), typeof(RPLFixedSupplyBurnEventDTO),],
			[context.RPLTokenAddress,], context.Policy);

		foreach (IEventLog eventLog in rplEvents)
		{
			await eventLog.WhenIsAsync<TransferEventDTO, TokensSyncContext>(
				TokenEventHandlers.HandleRPLAsync, context, cancellationToken);

			await eventLog.WhenIsAsync<RPLFixedSupplyBurnEventDTO, TokensSyncContext>(
				TokenEventHandlers.Handle, context, cancellationToken);
		}

		IEnumerable<IEventLog> rethEvents = await context.Web3.FilterAsync(
			fromBlock, toBlock, [typeof(TransferEventDTO),],
			[context.RETHTokenAddress,], context.Policy);

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
			context.PreSaturn1RocketNodeStakingAddresses, context.Policy);

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
			context.PostSaturn1RocketNodeStakingAddresses, context.Policy);

		foreach (IEventLog eventLog in postSaturn1StakingEvents)
		{
			eventLog.WhenIs<RPLLegacyWithdrawnEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLLegacyUnstaked, context);

			eventLog.WhenIs<RPLStakedEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLMegapoolStaked, context);

			eventLog.WhenIs<RPLUnstakedEventDTO, TokensSyncContext>(
				StakingEventHandlers.HandleRPLMegapoolUnstaked, context);
		}

		if (this.rockRETHTokenAddress is not null)
		{
			IEnumerable<IEventLog> rockRETHEvents = await context.Web3.FilterAsync(
				fromBlock, toBlock, [typeof(TransferEventDTO),],
				[this.rockRETHTokenAddress,], context.Policy);

			foreach (IEventLog eventLog in rockRETHEvents)
			{
				await eventLog.WhenIsAsync<TransferEventDTO, TokensSyncContext>(
					TokenEventHandlers.HandleRockRETHAsync, context, cancellationToken);
			}
		}
	}

	protected override async Task<TokensSyncContext> LoadContextAsync(
		ContextBase contextBase,
		CancellationToken cancellationToken = default)
	{
		string rplContractAddress = contextBase.Contracts["rocketTokenRPL"].Versions.Select(x => x.Address).Single();
		string rplOldContractAddress =
			contextBase.Contracts["rocketTokenRPLFixedSupply"].Versions.Select(x => x.Address).Single();
		string rethContractAddress = contextBase.Contracts["rocketTokenRETH"].Versions.Select(x => x.Address).Single();

		contextBase.Logger.LogInformation("Loading token snapshots");

		Task<BlobObject<TokensRPLOldSnapshot>?> readRPLOldTask =
			contextBase.Storage.ReadAsync<TokensRPLOldSnapshot>(Keys.TokensRPLOldSnapshot, cancellationToken);
		Task<BlobObject<TokensRPLSnapshot>?> readRPLTask =
			contextBase.Storage.ReadAsync<TokensRPLSnapshot>(Keys.TokensRPLSnapshot, cancellationToken);
		Task<BlobObject<TokensRETHSnapshot>?> readRETHTask =
			contextBase.Storage.ReadAsync<TokensRETHSnapshot>(Keys.TokensRETHSnapshot, cancellationToken);
		Task<BlobObject<StakedRPLSnapshot>?> readStakedRPLTask =
			contextBase.Storage.ReadAsync<StakedRPLSnapshot>(Keys.TokensStakedRPLSnapshot, cancellationToken);
		Task<BlobObject<TokensRockRETHSnapshot>?> readRockRETHTask =
			contextBase.Storage.ReadAsync<TokensRockRETHSnapshot>(Keys.TokensRockRETHSnapshot, cancellationToken);

		await Task.WhenAll(readRPLOldTask, readRPLTask, readRETHTask, readStakedRPLTask, readRockRETHTask);

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

		BlobObject<TokensRockRETHSnapshot> rockRETHSnapshot =
			await readRockRETHTask ??
			new BlobObject<TokensRockRETHSnapshot>
			{
				ProcessedBlockNumber = 0,
				Data = new TokensRockRETHSnapshot
				{
					RockRETH = this.rockRETHTokenAddress is null
						? null
						: new Token
						{
							Address = this.rockRETHTokenAddress,
							Holders = [],
							SupplyTotal = [],
							MintsDaily = [],
							BurnsDaily = [],
						},
				},
			};

		return new TokensSyncContext
		{
			Storage = contextBase.Storage,
			Policy = contextBase.Policy,
			Logger = contextBase.Logger,
			Web3 = contextBase.Web3,
			BeaconChainService = contextBase.BeaconChainService,
			GlobalIndexService = contextBase.GlobalIndexService,
			DashboardInfo = contextBase.DashboardInfo,
			RocketStorage = contextBase.RocketStorage,
			Contracts = contextBase.Contracts,
			LatestBlockHeight = contextBase.LatestBlockHeight,

			CurrentBlockHeight = rplSnapshot.ProcessedBlockNumber,

			PreSaturn1RocketNodeStakingAddresses = contextBase.Contracts["rocketNodeStaking"].Versions
				.Where(x => x.Version <= 6)
				.Select(x => x.Address).Concat([AddressUtil.ZERO_ADDRESS,]).ToArray(),
			PostSaturn1RocketNodeStakingAddresses = contextBase.Contracts["rocketNodeStaking"].Versions
				.Where(x => x.Version > 6)
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
			RockRETHTokenInfo = new TokenInfo
			{
				Holders = new SortedDictionary<string, BigInteger>(
					rockRETHSnapshot.Data.RockRETH?.Holders.Select(entry =>
						new KeyValuePair<string, BigInteger>(entry.Address, entry.Balance)).ToDictionary() ?? [],
					StringComparer.OrdinalIgnoreCase),
				SupplyTotal = rockRETHSnapshot.Data.RockRETH?.SupplyTotal ?? [],
				MintsDaily = rockRETHSnapshot.Data.RockRETH?.MintsDaily ?? [],
				BurnsDaily = rockRETHSnapshot.Data.RockRETH?.BurnsDaily ?? [],
			},
		};
	}

	protected override async Task SaveContextAsync(
		TokensSyncContext context, CancellationToken cancellationToken = default)
	{
		context.Logger.LogInformation("Writing {snapshot}", Keys.TokensRPLOldSnapshot);

		Task writeRplOldTask = context.Storage.WriteAsync(
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

		context.Logger.LogInformation("Writing {snapshot}", Keys.TokensRPLSnapshot);

		Task writeRPLTask = context.Storage.WriteAsync(
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

		context.Logger.LogInformation("Writing {snapshot}", Keys.TokensRETHSnapshot);

		Task writeRETHTask = context.Storage.WriteAsync(
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

		context.Logger.LogInformation("Writing {snapshot}", Keys.TokensStakedRPLSnapshot);

		Task writeStakedRPLTask = context.Storage.WriteAsync(
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

		context.Logger.LogInformation("Writing {snapshot}", Keys.TokensRockRETHSnapshot);

		Task writeRockRETHTask = context.Storage.WriteAsync(
			Keys.TokensRockRETHSnapshot, new BlobObject<TokensRockRETHSnapshot>
			{
				ProcessedBlockNumber = context.CurrentBlockHeight,
				Data = new TokensRockRETHSnapshot
				{
					RockRETH = this.rockRETHTokenAddress is null
						? null
						: new Token
						{
							Address = this.rockRETHTokenAddress,
							Holders = context.RockRETHTokenInfo.Holders.Select(x => new HolderEntry
							{
								Address = x.Key,
								Balance = x.Value,
							}).ToArray(),
							SupplyTotal = context.RockRETHTokenInfo.SupplyTotal,
							MintsDaily = context.RockRETHTokenInfo.MintsDaily,
							BurnsDaily = context.RockRETHTokenInfo.BurnsDaily,
						},
				},
			}, cancellationToken: cancellationToken);

		await Task.WhenAll(writeRplOldTask, writeRPLTask, writeRETHTask, writeStakedRPLTask, writeRockRETHTask);
	}
}