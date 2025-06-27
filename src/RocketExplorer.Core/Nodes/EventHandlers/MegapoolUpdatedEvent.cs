using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MegapoolUpdatedEvent
{
	public required FilterLog Log { get; set; }

	public required string MegapoolAddress { get; set; }

	public required ValidatorStatus Status { get; set; }

	public required long Time { get; set; }

	public required int ValidatorId { get; set; }
}