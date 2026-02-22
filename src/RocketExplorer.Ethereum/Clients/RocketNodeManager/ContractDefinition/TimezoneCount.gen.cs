using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace RocketExplorer.Ethereum.RocketNodeManager.ContractDefinition
{
    public partial class TimezoneCount : TimezoneCountBase { }

    public class TimezoneCountBase 
    {
        [Parameter("string", "timezone", 1)]
        public virtual string Timezone { get; set; }
        [Parameter("uint256", "count", 2)]
        public virtual BigInteger Count { get; set; }
    }
}
