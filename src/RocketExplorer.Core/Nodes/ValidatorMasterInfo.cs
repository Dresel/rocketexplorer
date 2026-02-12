using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public class ValidatorMasterInfo
{
	public byte[]? MegapoolAddress { get; set; }

	public byte[]? MinipoolAddress { get; set; }

	public byte[]? PubKey { get; set; }

	public long? ValidatorIndex { get; set; }

	public bool? ExpressTicketUsed { get; set; }

	public int? MegapoolIndex { get; set; }

	public ValidatorType Type { get; set; }

	// TODO: Bond? Average NETH for Megapool? getBondRequirement, hardcode?
	public float Bond { get; set; }

	public ValidatorStatus Status { get; set; }

	public List<ValidatorHistory> History { get; set; } = [];
}
