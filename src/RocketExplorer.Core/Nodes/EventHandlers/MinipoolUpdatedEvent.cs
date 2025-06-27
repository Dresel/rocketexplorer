using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MinipoolUpdatedEvent
{
	public required FilterLog Log { get; set; }

	public required string MinipoolAddress { get; set; }

	public required ValidatorStatus Status { get; set; }

	public required BigInteger Time { get; set; }
}