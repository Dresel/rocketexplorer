using Nethereum.RPC.Eth.DTOs;
using RocketExplorer.Shared.Validators;

namespace RocketExplorer.Core.Nodes.EventHandlers;

public class MegapoolUpdatedEvent
{
	public required FilterLog Log { get; set; }

	public required string MegapoolAddress { get; set; }

	public required ValidatorStatus Status { get; set; }

	public required long Time { get; set; }

	// Megapool Id (0, 1, 2, ...)
	public required int ValidatorId { get; set; }

	// Beacon Chain Index
	public long? ValidatorIndex { get; set; } = null;
}