using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class ValidatorInfo
{
	public required ValidatorInfoFull Data { get; init; }

	public ValidatorInfoPartial Partial { get; init; } = new();

	public required string[] RocketMinipoolManagerAddresses { get; init; }

	public class ValidatorInfoFull
	{
		public required OrderedDictionary<(string Address, int Index), MegapoolValidatorIndexEntry>
			MegapoolValidatorIndex { get; init; }

		public required OrderedDictionary<string, MinipoolValidatorIndexEntry> MinipoolValidatorIndex { get; init; }
	}

	public class ValidatorInfoPartial
	{
		public Dictionary<(string Address, int Index), Validator> UpdatedMegapoolValidators { get; init; } =
			new(new MegapoolIndexEqualityComparer());

		public Dictionary<string, Validator> UpdatedMinipoolValidators { get; init; } =
			new(StringComparer.OrdinalIgnoreCase);
	}
}