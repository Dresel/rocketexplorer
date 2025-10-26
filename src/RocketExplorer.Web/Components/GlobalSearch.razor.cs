using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Nethereum.Util;
using RocketExplorer.Shared;
using RocketExplorer.Web.Pages;

namespace RocketExplorer.Web.Components;

public partial class GlobalSearch(IBrowserViewportService browserViewportService)
	: ComponentBase, IBrowserViewportObserver, IAsyncDisposable
{
	private readonly IBrowserViewportService browserViewportService = browserViewportService;

	private readonly DialogOptions dialogOptions = new()
	{
		Position = DialogPosition.TopCenter,
		NoHeader = true,
	};

	private bool compact;
	private bool isSearchDialogOpen;

	private int prefixLength = 18;
	private int suffixLength = 10;

	public Guid Id { get; } = Guid.NewGuid();

	public async ValueTask DisposeAsync() => await this.browserViewportService.UnsubscribeAsync(this);

	public Task NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
	{
		switch (browserViewportEventArgs.Breakpoint)
		{
			case Breakpoint.Xs:
				this.prefixLength = 12;
				this.suffixLength = 10;
				break;

			case Breakpoint.Sm:
			case Breakpoint.Md:
			case Breakpoint.Lg:
			case Breakpoint.Xl:
			case Breakpoint.Xxl:
				break;
		}

		switch (browserViewportEventArgs.Breakpoint)
		{
			case Breakpoint.Xs:
			case Breakpoint.Sm:
			case Breakpoint.Md:
				this.compact = true;
				break;

			case Breakpoint.Lg:
			case Breakpoint.Xl:
			case Breakpoint.Xxl:
				this.compact = false;
				break;
		}

		return InvokeAsync(StateHasChanged);
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await this.browserViewportService.SubscribeAsync(this);
		}

		await base.OnAfterRenderAsync(firstRender);
	}

	private static string GetDisplayText(IndexEntryType type) =>
		type switch
		{
			IndexEntryType.NodeOperator => "Node Operator",
			IndexEntryType.Megapool => "Megapool",
			IndexEntryType.MinipoolValidator => "Minipool Validator",
			IndexEntryType.MegapoolValidator => "Megapool Validator",
			IndexEntryType.RETHHolder => "rETH Holder",
			IndexEntryType.RPLHolder => "RPL Holder",
			IndexEntryType.RPLOldHolder => "RPLv1 Holder",
			IndexEntryType.RockRETHHolder => "rock.rETH Holder",
			_ => throw new ArgumentOutOfRangeException(nameof(type)),
		};

	private GroupedListItem<IndexEntryViewModel> CreateGroupListItem(
		IndexEntry entry, string groupName, string displayText, string searchText) =>
		new()
		{
			GroupName = groupName,
			Data = new IndexEntryViewModel
			{
				Address = entry.Address,
				MegapoolAddress = entry.MegapoolAddress,
				MegapoolIndex = entry.MegapoolIndex,
				DisplayText = displayText.Ellipsize(this.prefixLength, this.suffixLength),
				HighlightedTexts = displayText.ExtractHighlightTexts(searchText, this.prefixLength, this.suffixLength),
				Type = entry.Type,
				Chips = Enum.GetValues<IndexEntryType>().Where(flag => entry.Type.HasFlag(flag) && flag != 0)
					.Select(GetDisplayText).ToArray(),
			},
		};

	private GroupedListItem<IndexEntryViewModel> CreateGroupListItem(
		EnsIndexEntry entry, string groupName, string displayText, string searchText) =>
		new()
		{
			GroupName = groupName,
			Data = new IndexEntryViewModel
			{
				Address = entry.Address,
				MegapoolAddress = null,
				MegapoolIndex = null,
				DisplayText = displayText,
				HighlightedTexts = displayText.ExtractHighlightTexts(searchText, this.prefixLength, this.suffixLength),
				Type = entry.Type,
				Chips = Enum.GetValues<IndexEntryType>()
					.Where(flag => entry.Type.HasFlag(flag) && flag != 0)
					.Select(GetDisplayText).ToArray(),
			},
		};

	private void OnSearchResultClicked(GroupedListItem<IndexEntryViewModel> result)
	{
		if (result.Data is null)
		{
			return;
		}

		string address = AddressUtil.Current.ConvertToChecksumAddress(result.Data.Address);

		if (result.Data!.Type.HasFlag(IndexEntryType.NodeOperator) ||
			result.Data!.Type.HasFlag(IndexEntryType.Megapool))
		{
			NavigationManager.NavigateTo($"/node/{address}");
			return;
		}

		if (result.Data!.Type.HasFlag(IndexEntryType.MinipoolValidator))
		{
			NavigationManager.NavigateTo($"/validator/{address}");
			return;
		}

		if (result.Data!.Type.HasFlag(IndexEntryType.MegapoolValidator))
		{
			string megapoolAddress = AddressUtil.Current.ConvertToChecksumAddress(result.Data.MegapoolAddress);
			NavigationManager.NavigateTo($"/validator/{megapoolAddress}/{result.Data.MegapoolIndex}");
			return;
		}

		_ = JSRuntime.InvokeVoidAsync(
			"open", $"https://{Configuration.EtherscanPrefix}etherscan.io/address/{address}", "_blank");
	}

	private async Task<IEnumerable<GroupedListItem<IndexEntryViewModel>>>? SearchFunc(
		string? search, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return [];
		}

		// TODO: Handle 0x input, check valid address / number and do ENS only
		string[] ngrams = search.NGrams(4).Take(1).ToArray();

		ConcurrentBag<object> indexes = [];

		// TODO: Parallelize
		await Parallel.ForEachAsync(
			ngrams, new ParallelOptions
			{
				CancellationToken = cancellationToken,
				MaxDegreeOfParallelism = 16,
			}, async (nGram, innerCancellationToken) =>
			{
				SnapshotResponse<GlobalIndexShardSnapshot<IndexEntry>> response =
					await HttpClient.GetSnapshotResponse<GlobalIndexShardSnapshot<IndexEntry>>(
						$"{Configuration.ObjectStoreBaseUrl}/{Keys.GlobalIndexTemplate(nGram)}",
						innerCancellationToken);

				if (!response.IsSuccess)
				{
					return;
				}

				Snapshot<GlobalIndexShardSnapshot<IndexEntry>> snapshotAsync =
					await response.ToSnapshotAsync(innerCancellationToken);

				foreach (IndexEntry indexEntry in snapshotAsync.Data.Index)
				{
					indexes.Add(indexEntry);
				}
			});

		string nGram = search.NGrams(3).First();

		SnapshotResponse<GlobalIndexShardSnapshot<EnsIndexEntry>> response =
			await HttpClient.GetSnapshotResponse<GlobalIndexShardSnapshot<EnsIndexEntry>>(
				$"{Configuration.ObjectStoreBaseUrl}/{Keys.GlobalIndexTemplate(nGram)}", cancellationToken);

		if (response.IsSuccess)
		{
			Snapshot<GlobalIndexShardSnapshot<EnsIndexEntry>> snapshotAsync =
				await response.ToSnapshotAsync(cancellationToken);

			foreach (EnsIndexEntry indexEntry in snapshotAsync.Data.Index)
			{
				indexes.Add(indexEntry);
			}
		}

		string byENS = "By ENS";
		string byAddress = "By Address";
		string byPublicKey = "By Public Key";
		string byValidatorIndex = "By Validator Index";

		List<IGrouping<string?, GroupedListItem<IndexEntryViewModel>>> grouped = indexes.Distinct()
			.SelectMany(entry =>
			{
				List<GroupedListItem<IndexEntryViewModel>> result = [];

				if (entry is EnsIndexEntry ensEntry)
				{
					if (ensEntry.AddressEnsName.AsSpan()[..^4].Contains(search, StringComparison.OrdinalIgnoreCase))
					{
						result.Add(CreateGroupListItem(ensEntry, byENS, ensEntry.AddressEnsName, search));
					}
				}

				if (entry is IndexEntry indexEntry)
				{
					string address = AddressUtil.Current.ConvertToChecksumAddress(indexEntry.Address);

					if (address.Contains(search, StringComparison.OrdinalIgnoreCase))
					{
						result.Add(CreateGroupListItem(indexEntry, byAddress, address, search));
					}

					if (indexEntry.MegapoolAddress is not null)
					{
						string megapoolAddress =
							AddressUtil.Current.ConvertToChecksumAddress(indexEntry.MegapoolAddress);

						if (megapoolAddress.Contains(search, StringComparison.OrdinalIgnoreCase))
						{
							result.Add(CreateGroupListItem(indexEntry, byAddress, megapoolAddress, search));
						}
					}

					if (indexEntry.ValidatorPubKey is not null)
					{
						string pubKey = Convert.ToHexString(indexEntry.ValidatorPubKey);
						if (pubKey.Contains(search, StringComparison.OrdinalIgnoreCase))
						{
							result.Add(CreateGroupListItem(indexEntry, byPublicKey, pubKey, search));
						}
					}

					if (indexEntry.ValidatorIndex is not null)
					{
						string validatorIndex = indexEntry.ValidatorIndex.Value.ToString();
						if (validatorIndex.Contains(search, StringComparison.OrdinalIgnoreCase))
						{
							result.Add(CreateGroupListItem(indexEntry, byValidatorIndex, validatorIndex, search));
						}
					}
				}

				return result;
			}).GroupBy(x => x.GroupName)
			.OrderBy(g => Array.IndexOf([byENS, byValidatorIndex, byAddress, byPublicKey,], g.Key)).ToList();

		List<GroupedListItem<IndexEntryViewModel>> groupedListItems = grouped.SelectMany(x =>
		{
			List<GroupedListItem<IndexEntryViewModel>> result =
			[
				new()
				{
					GroupName = x.Key,
				},
				..x.Select(item => new GroupedListItem<IndexEntryViewModel>
				{
					Data = item.Data,
				}).OrderBy(item => item.Data!.DisplayText.IndexOf(search, StringComparison.OrdinalIgnoreCase)).ThenBy(
					item => item.Data!.DisplayText, StringComparer.OrdinalIgnoreCase).Take(5),
			];

			return result;
		}).ToList();

		return groupedListItems;
	}

	public class GroupedListItem<T>
	{
		public T? Data { get; init; }

		public string? GroupName { get; init; }
	}

	public class IndexEntryViewModel
	{
		public required byte[] Address { get; init; }

		public required string[] Chips { get; init; }

		public required string DisplayText { get; init; }

		public required IEnumerable<string> HighlightedTexts { get; init; }

		public required byte[]? MegapoolAddress { get; set; }

		public required int? MegapoolIndex { get; init; }

		public required IndexEntryType Type { get; init; }
	}
}