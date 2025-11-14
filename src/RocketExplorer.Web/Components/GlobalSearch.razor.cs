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
	private const string ByAddress = "By Address";

	private const string ByEns = "By ENS";
	private const string ByPublicKey = "By Public Key";
	private const string ByValidatorIndex = "By Validator Index";

	private static readonly char[] Digits = Enumerable.Range('0', 10).Select(x => (char)x).ToArray();
	private static readonly char[] Letters = Enumerable.Range('a', 26).Select(x => (char)x).ToArray();
	private static readonly char[] ValidNGramCharacters = [.. Digits, .. Letters,];
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

	public Guid Id { get; } = Guid.NewGuid();

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
			IndexEntryType.WithdrawalAddress => "Withdrawal Address",
			IndexEntryType.RPLWithdrawalAddress => "RPL Withdrawal Address",
			IndexEntryType.StakeOnBehalfAddress => "Stake On Behalf Address",
			_ => throw new ArgumentOutOfRangeException(nameof(type)),
		};

	private static string RemoveAddressPrefix(string value)
	{
		if (value.StartsWith("0x"))
		{
			return value[2..];
		}

		if (value.StartsWith("x"))
		{
			return value[1..];
		}

		return value;
	}

	private static bool StartsWithAddressPrefix(string value) => value.StartsWith("0x") || value.StartsWith("x");

	private static bool StartsWithPubKeyChar(string value) =>
		value.Length > 0 && value[0] is '8' or '9' or 'a' or 'b';

	private GroupedListItem<IndexEntryViewModel> CreateGroupListItem(
		IndexEntry entry, string groupName, string displayText, string searchText, bool highlightStartOnly = false) =>
		new()
		{
			GroupName = groupName,
			Data = new IndexEntryViewModel
			{
				Address = entry.Address,
				NodeAddresses = entry.NodeAddresses,
				MegapoolAddress = entry.MegapoolAddress,
				MegapoolIndex = entry.MegapoolIndex,
				DisplayTextRaw = displayText,
				DisplayText = displayText.Ellipsize(this.prefixLength, this.suffixLength),
				IndexOfHighlightedText = displayText.ExtractIndexOf(searchText, this.prefixLength, this.suffixLength),
				HighlightedTexts = displayText.ExtractHighlightTexts(searchText, this.prefixLength, this.suffixLength),
				HighlightStartOnly = highlightStartOnly,
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
				NodeAddresses = entry.NodeAddresses,
				MegapoolAddress = null,
				MegapoolIndex = null,
				DisplayTextRaw = displayText,
				DisplayText = displayText,
				IndexOfHighlightedText = displayText.ExtractIndexOf(searchText, this.prefixLength, this.suffixLength),
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

		if (result.Data!.Type.HasFlag(IndexEntryType.WithdrawalAddress) ||
			result.Data!.Type.HasFlag(IndexEntryType.RPLWithdrawalAddress) ||
			result.Data!.Type.HasFlag(IndexEntryType.StakeOnBehalfAddress))
		{
			NavigationManager.NavigateTo(
				$"/node/{AddressUtil.Current.ConvertToChecksumAddress(result.Data.NodeAddresses.First())}?highlight={result.Data.DisplayTextRaw}");
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

	private async Task<IEnumerable<GroupedListItem<IndexEntryViewModel>>> SearchEnsIndexAsync(
		string search, CancellationToken cancellationToken = default)
	{
		IEnumerable<string> ngrams;

		if (search.Length < 2)
		{
			return [];
		}

		if (search.Length == 2)
		{
			// Add all possible ngrams with 3 characters
			ngrams = ValidNGramCharacters.Select(character => $"{character}{search.Map()}")
				.Concat(ValidNGramCharacters.Select(character => $"{search.Map()}{character}"));
		}
		else
		{
			ngrams = [search[..3].Map(),];
		}

		ConcurrentBag<EnsIndexEntry> indexes = [];

		await Parallel.ForEachAsync(
			ngrams, new ParallelOptions
			{
				CancellationToken = cancellationToken,
				MaxDegreeOfParallelism = 16,
			}, async (nGram, innerCancellationToken) =>
			{
				SnapshotResponse<GlobalIndexShardSnapshot<EnsIndexEntry>> response =
					await HttpClient.GetSnapshotResponse<GlobalIndexShardSnapshot<EnsIndexEntry>>(
						$"{Configuration.ObjectStoreBaseUrl}/{Keys.GlobalIndexTemplate(nGram)}",
						innerCancellationToken);

				if (!response.IsSuccess)
				{
					return;
				}

				Snapshot<GlobalIndexShardSnapshot<EnsIndexEntry>> snapshot =
					await response.ToSnapshotAsync(innerCancellationToken);

				foreach (EnsIndexEntry indexEntry in snapshot.Data.Index)
				{
					indexes.Add(indexEntry);
				}
			});

		return indexes.Distinct().Where(x => x.AddressEnsName.Contains(search, StringComparison.OrdinalIgnoreCase))
			.Select(x => CreateGroupListItem(x, ByEns, x.AddressEnsName, search));
	}

	private async Task<IEnumerable<GroupedListItem<IndexEntryViewModel>>>? SearchFunc(
		string? search, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return [];
		}

		search = search.ToLowerInvariant();

		IEnumerable<GroupedListItem<IndexEntryViewModel>> ensIndexResults =
			await SearchEnsIndexAsync(search, cancellationToken);

		IEnumerable<GroupedListItem<IndexEntryViewModel>> indexResults = await SearchIndexAsync(
			search, cancellationToken);

		List<IGrouping<string?, GroupedListItem<IndexEntryViewModel>>> grouped = indexResults.Concat(ensIndexResults)
			.GroupBy(x => x.GroupName)
			.OrderBy(g => Array.IndexOf([ByEns, ByValidatorIndex, ByAddress, ByPublicKey,], g.Key)).ToList();

		return grouped.SelectMany(x =>
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
					}).OrderBy(item => item.Data!.IndexOfHighlightedText).ThenBy(
						item => item.Data!.DisplayText, StringComparer.OrdinalIgnoreCase)
					.Take(5),
			];

			return result;
		});
	}

	private async Task<IEnumerable<GroupedListItem<IndexEntryViewModel>>> SearchIndexAsync(
		string search, CancellationToken cancellationToken)
	{
		IEnumerable<string> ngrams = [];

		bool startsWithAddressPrefix = false;
		string searchWithoutAddressPrefix = string.Empty;

		// Special handling for PubKey / Addresses
		if (StartsWithAddressPrefix(search))
		{
			startsWithAddressPrefix = true;
			searchWithoutAddressPrefix = RemoveAddressPrefix(search);
		}

		if ((!startsWithAddressPrefix && !search.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) ||
			(startsWithAddressPrefix && !searchWithoutAddressPrefix.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')))
		{
			return [];
		}

		if (startsWithAddressPrefix)
		{
			if (string.IsNullOrEmpty(searchWithoutAddressPrefix))
			{
				// Enough to get 5 results
				ngrams = ["8000", "8001",];
			}
			else if (searchWithoutAddressPrefix.Length < 4 && StartsWithPubKeyChar(searchWithoutAddressPrefix))
			{
				// Enough to get 5 results
				ngrams =
				[
					.. ValidNGramCharacters.Take(4).Select(c => $"{searchWithoutAddressPrefix}{c}".PadRight(4, '0')),
				];
			}
			else
			{
				// Just use the first 4 characters in combination with StartsWith
				ngrams = [searchWithoutAddressPrefix.PadRight(4, '0')[..4],];
			}
		}
		else
		{
			// We have enough items per ngram, so just fill up with 0s
			ngrams = [search.PadRight(4, '0')[..4],];
		}

		ConcurrentBag<IndexEntry> indexes = [];

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

				Snapshot<GlobalIndexShardSnapshot<IndexEntry>> snapshot =
					await response.ToSnapshotAsync(innerCancellationToken);

				foreach (IndexEntry indexEntry in snapshot.Data.Index)
				{
					indexes.Add(indexEntry);
				}
			});

		return indexes.Distinct().SelectMany(entry =>
		{
			List<GroupedListItem<IndexEntryViewModel>> result = [];

			string address = AddressUtil.Current.ConvertToChecksumAddress(entry.Address);

			if (address.Contains(search, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(CreateGroupListItem(entry, ByAddress, address, search));
			}

			if (entry.MegapoolAddress is not null)
			{
				string megapoolAddress =
					AddressUtil.Current.ConvertToChecksumAddress(entry.MegapoolAddress);

				if (megapoolAddress.Contains(search, StringComparison.OrdinalIgnoreCase))
				{
					result.Add(CreateGroupListItem(entry, ByAddress, megapoolAddress, search));
				}
			}

			if (entry.ValidatorPubKey is not null)
			{
				string pubKey = Convert.ToHexString(entry.ValidatorPubKey);

				if (startsWithAddressPrefix)
				{
					if (pubKey.StartsWith(searchWithoutAddressPrefix, StringComparison.OrdinalIgnoreCase))
					{
						// HighlightStartOnly necessary cause 0x prefix is omitted for pubkey display
						result.Add(CreateGroupListItem(entry, ByPublicKey, pubKey, searchWithoutAddressPrefix, true));
					}
				}
				else
				{
					if (pubKey.Contains(search, StringComparison.OrdinalIgnoreCase))
					{
						result.Add(CreateGroupListItem(entry, ByPublicKey, pubKey, search));
					}
				}
			}

			if (entry.ValidatorIndex is not null && !startsWithAddressPrefix)
			{
				string validatorIndex = entry.ValidatorIndex.Value.ToString();
				if (validatorIndex.Contains(search, StringComparison.OrdinalIgnoreCase))
				{
					result.Add(CreateGroupListItem(entry, ByValidatorIndex, validatorIndex, search));
				}
			}

			return result;
		});
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

		public required string DisplayTextRaw { get; set; }

		public required IEnumerable<string> HighlightedTexts { get; init; }

		public bool HighlightStartOnly { get; set; }

		public required int IndexOfHighlightedText { get; set; }

		public required byte[]? MegapoolAddress { get; set; }

		public required int? MegapoolIndex { get; init; }

		public required List<byte[]> NodeAddresses { get; set; }

		public required IndexEntryType Type { get; init; }
	}
}