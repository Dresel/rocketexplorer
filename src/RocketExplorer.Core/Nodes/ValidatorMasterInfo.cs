using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class ValidatorMasterInfo
{
	public required byte[] NodeAddress { get; init; }

	public byte[]? MinipoolAddress { get; init; }

	public byte[]? MegapoolAddress { get; init; }

	public byte[]? PubKey { get; set; }

	public long? ValidatorIndex { get; set; }

	public bool? ExpressTicketUsed { get; init; }

	public int? MegapoolIndex { get; init; }

	public ValidatorType Type { get; set; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	public float Bond { get; set; }

	public ValidatorStatus Status { get; set; }

	public List<ValidatorHistory> History { get; set; } = [];
}
