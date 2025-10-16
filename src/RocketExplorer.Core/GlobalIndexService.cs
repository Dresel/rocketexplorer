using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class GlobalIndexService(Storage storage, ILogger<GlobalIndexService> logger)
	: IndexService<byte[], IndexEntry, Shared.IndexEntry>(
		4, entry => entry.Address, entry => entry.Address, entry => new Shared.IndexEntry
		{
			Type = entry.Type,
			Address = entry.Address,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			ValidatorIndex = entry.ValidatorIndex,
			ValidatorPubKey = entry.ValidatorPubKey,
		}, entry => new IndexEntry
		{
			Type = entry.Type,
			Address = entry.Address,
			MegapoolAddress = entry.MegapoolAddress,
			MegapoolIndex = entry.MegapoolIndex,
			ValidatorIndex = entry.ValidatorIndex,
			ValidatorPubKey = entry.ValidatorPubKey,
		}, new FastByteArrayComparer(), storage, Keys.GlobalIndexTemplate, logger);