namespace RocketExplorer.Shared.Minipools;

public enum MinipoolStatus : byte
{
	InQueue, // Initialised

	PreStaked, // PreLaunch

	Dissolved,

	Staking,

	Exited,

	Dequeued,
}