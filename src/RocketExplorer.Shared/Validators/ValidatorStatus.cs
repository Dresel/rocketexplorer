namespace RocketExplorer.Shared.Validators;

public enum ValidatorStatus : byte
{
	Created, // Initialised

	InQueue,

	PreStaked,

	PreLaunch,

	Dissolved,

	Staking,

	Exited, // EtherWithdrawalProcessed, Finalised = true

	Dequeued,
}