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

	public Guid Id { get; } = Guid.NewGuid();

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
			IndexEntryType.RETHHolder => "RETH Holder",
			IndexEntryType.RPLHolder => "RPL Holder",
			IndexEntryType.RPLOldHolder => "RPLv1 Holder",
			IndexEntryType.RockRETHHolder => "ROCK.RETH Holder",
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
				MegapoolIndex = entry.MegapoolIndex,
				DisplayText = displayText.Ellipsize(this.prefixLength, this.suffixLength),
				HighlightedTexts = displayText.ExtractHighlightTexts(searchText, this.prefixLength, this.suffixLength),
				Type = entry.Type,
				Chips = Enum.GetValues<IndexEntryType>().Where(flag => entry.Type.HasFlag(flag) && flag != 0)
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
			NavigationManager.NavigateTo($"/validator/{address}/{result.Data.MegapoolIndex}");
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

		await Task.Delay(100, cancellationToken);

		// TODO: Handle 0x input, check valid address / number and do ENS only
		string[] ngrams = search.NGrams().Take(1).ToArray();

		ConcurrentBag<IndexEntry> indexes = [];

		// TODO: Parallelize
		await Parallel.ForEachAsync(
			ngrams, new ParallelOptions
			{
				CancellationToken = cancellationToken,
				MaxDegreeOfParallelism = 16,
			}, async (nGram, innerCancellationToken) =>
			{
				SnapshotResponse<GlobalIndexShardSnapshot> response =
					await HttpClient.GetSnapshotResponse<GlobalIndexShardSnapshot>(
						$"{Configuration.ObjectStoreBaseUrl}/{Keys.NGram(nGram)}", innerCancellationToken);

				if (!response.IsSuccess)
				{
					return;
				}

				Snapshot<GlobalIndexShardSnapshot> snapshotAsync =
					await response.ToSnapshotAsync(innerCancellationToken);

				foreach (IndexEntry indexEntry in snapshotAsync.Data.Index)
				{
					indexes.Add(indexEntry);
				}
			});

		string byAddress = "By Address";
		string byPublicKey = "By Public Key";
		string byValidatorIndex = "By Validator Index";

		IEnumerable<IGrouping<string?, GroupedListItem<IndexEntryViewModel>>> grouped = indexes.Distinct()
			.SelectMany(entry =>
			{
				List<GroupedListItem<IndexEntryViewModel>> result = [];

				string address = AddressUtil.Current.ConvertToChecksumAddress(entry.Address);

				if (address.Contains(search, StringComparison.OrdinalIgnoreCase))
				{
					result.Add(CreateGroupListItem(entry, byAddress, address, search));
				}

				if (entry.ValidatorPubKey is not null)
				{
					string pubKey = Convert.ToHexString(entry.ValidatorPubKey);
					if (pubKey.Contains(search, StringComparison.OrdinalIgnoreCase))
					{
						result.Add(CreateGroupListItem(entry, byPublicKey, pubKey, search));
					}
				}

				if (entry.ValidatorIndex is not null)
				{
					string validatorIndex = entry.ValidatorIndex.Value.ToString();
					if (validatorIndex.Contains(search, StringComparison.OrdinalIgnoreCase))
					{
						result.Add(CreateGroupListItem(entry, byValidatorIndex, validatorIndex, search));
					}
				}

				return result;
			}).GroupBy(x => x.GroupName)
			.OrderBy(g => Array.IndexOf([byValidatorIndex, byAddress, byPublicKey,], g.Key));

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

		public required int? MegapoolIndex { get; init; }

		public required IndexEntryType Type { get; init; }
	}
}