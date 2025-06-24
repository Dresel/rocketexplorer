using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class ValidatorInfo
{
	public required ValidatorInfoFull Data { get; init; }

	public ValidatorInfoPartial Partial { get; init; } = new();

	public ValidatorInfoCache Cache { get; init; } = new();

	public required string[] RocketMinipoolManagerAddresses { get; init; }

	public class ValidatorInfoPartial
	{
		public Dictionary<string, Validator> UpdatedMinipoolValidators { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public Dictionary<(string Address, int Index), Validator> UpdatedMegapoolValidators { get; init; } = new(new MegapoolIndexEqualityComparer());
	}

	public class ValidatorInfoFull
	{
		public required Dictionary<string, MinipoolValidatorIndexEntry> MinipoolValidatorIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public required Dictionary<(string Address, int Index), MegapoolValidatorIndexEntry> MegapoolValidatorIndex { get; init; }
	}

	public class ValidatorInfoCache
	{
		public Dictionary<string, string> MinipoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public Dictionary<string, string> MegapoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	}
}

public class MegapoolIndexEqualityComparer : IEqualityComparer<(string Address, int Index)>
{
	public bool Equals((string Address, int Index) x, (string Address, int Index) y)
	{
		return string.Equals(x.Address, y.Address, StringComparison.OrdinalIgnoreCase) && x.Index == y.Index;
	}

	public int GetHashCode((string Address, int Index) obj)
	{
		return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Address), obj.Index);
	}
}