namespace RocketExplorer.Shared.Validators;

public enum ValidatorStatus : byte
{
	InQueue, // Initialised

	PreStaked, // PreLaunch

	Dissolved,

	Staking,

	Exited,

	Dequeued,
}