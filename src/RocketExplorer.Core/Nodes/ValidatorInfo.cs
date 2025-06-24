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

		public Dictionary<string, Dictionary<int, Validator>> UpdatedMegapoolValidators { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	}

	public class ValidatorInfoFull
	{
		public required Dictionary<string, MinipoolValidatorIndexEntry> MinipoolValidatorIndex { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		// TODO: Ignore Case
		public required Dictionary<(string Address, int Index), MinipoolValidatorIndexEntry> MegapoolValidatorIndex { get; init; }
	}

	public class ValidatorInfoCache
	{
		public Dictionary<string, string> MinipoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public Dictionary<string, string> MegapoolNodeOperatorMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	}
}