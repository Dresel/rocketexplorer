using System.Numerics;
using MessagePack;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Shared.Nodes;

[MessagePackObject]
public record class NodeMaster
{
	[Key(0)]
	public required byte[] ContractAddress { get; init; }

	[Key(1)]
	public long RegistrationTimestamp { get; init; }

	[Key(2)]
	public byte[]? MegapoolAddress { get; init; }

	[Key(3)]
	public required ValidatorMaster[] MinipoolValidators { get; init; }

	[Key(4)]
	public required ValidatorMaster[] MegapoolValidators { get; init; }

	[Key(5)]
	public required string Timezone { get; init; }

	[Key(6)]
	public required BigInteger RPLLegacyStaked { get; init; }

	[Key(7)]
	public required BigInteger RPLMegapoolStaked { get; init; }

	[Key(8)]
	public required byte[]? WithdrawalAddress { get; init; }

	[Key(9)]
	public required byte[]? RPLWithdrawalAddress { get; init; }

	[Key(10)]
	public required HashSet<byte[]> StakeOnBehalfAddresses { get; init; }

	[Key(11)]
	public required bool InSmoothingPool { get; init; }
}
