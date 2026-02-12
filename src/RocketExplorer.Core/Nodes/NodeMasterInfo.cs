using System.Numerics;
using RocketExplorer.Shared;

namespace RocketExplorer.Core.Nodes;

public class NodeMasterInfo
{
	public required byte[] ContractAddress { get; set; }

	public long RegistrationTimestamp { get; set; }

	public byte[]? MegapoolAddress { get; set; }

	public Dictionary<string, ValidatorMasterInfo> MinipoolValidators { get; set; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<(string Address, int Index), ValidatorMasterInfo> MegapoolValidators { get; set; } = new(new MegapoolIndexEqualityComparer());

	public required string Timezone { get; set; }

	public BigInteger RPLLegacyStaked { get; set; }

	public BigInteger RPLMegapoolStaked { get; set; }

	public byte[]? WithdrawalAddress { get; set; }

	public byte[]? RPLWithdrawalAddress { get; set; }

	public HashSet<byte[]> StakeOnBehalfAddresses { get; set; } = new(new FastByteArrayComparer());

	public bool InSmoothingPool { get; set; }
}
