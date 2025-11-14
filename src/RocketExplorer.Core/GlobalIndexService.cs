using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class GlobalIndexService(Storage storage, ILogger<GlobalIndexService> logger)
	: IndexService<byte[], IndexEntry, Shared.IndexEntry>(
		4, entry => entry.Address, entry => entry.Address, entry => new Shared.IndexEntry
		{
			Type = entry.Type,
			Address = entry.Address,
			AddressEnsName = entry.AddressEnsName,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			ValidatorIndex = entry.ValidatorIndex,
			ValidatorPubKey = entry.ValidatorPubKey,
			NodeAddresses = entry.NodeAddresses,
		}, entry => new IndexEntry
		{
			Type = entry.Type,
			Address = entry.Address,
			AddressEnsName = entry.AddressEnsName,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			ValidatorIndex = entry.ValidatorIndex,
			ValidatorPubKey = entry.ValidatorPubKey,
			NodeAddresses = entry.NodeAddresses,
		}, new FastByteArrayComparer(), storage, Keys.GlobalIndexTemplate, logger);