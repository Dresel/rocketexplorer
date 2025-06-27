using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes;

public static class ValidatorExtensions
{
	public static ValidatorStatus ToValidatorStatus(this byte status) =>
		status switch
		{
			0 => ValidatorStatus.Created,
			1 => ValidatorStatus.PreLaunch,
			2 => ValidatorStatus.Staking,
			3 => ValidatorStatus.Exited,
			4 => ValidatorStatus.Dissolved,
			_ => throw new ArgumentException("Unknown status", nameof(status)),
		};
}