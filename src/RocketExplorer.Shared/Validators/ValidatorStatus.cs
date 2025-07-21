namespace RocketExplorer.Shared.Validators;

public enum ValidatorStatus : byte
{
	Created, // Initialised

	InQueue,

	PreStaked,

	PreLaunch, // Assigned

	Dissolved,

	Staking,

	Exiting,

	Exited, // EtherWithdrawalProcessed, Finalised = true

	Dequeued,
}