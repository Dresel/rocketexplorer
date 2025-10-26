using Microsoft.Extensions.Logging;
using RocketExplorer.Shared;

namespace RocketExplorer.Core;

public class GlobalEnsIndexService(Storage storage, ILogger<GlobalEnsIndexService> logger)
	: IndexService<string, EnsIndexEntry, Shared.EnsIndexEntry>(
		3, entry => entry.AddressEnsName, entry => entry.AddressEnsName, entry => new Shared.EnsIndexEntry
		{
			Type = entry.Type,
			Address = entry.Address,
			AddressEnsName = entry.AddressEnsName,
		}, entry => new EnsIndexEntry
		{
			Type = entry.Type,
			Address = entry.Address,
			AddressEnsName = entry.AddressEnsName,
		}, StringComparer.OrdinalIgnoreCase, storage, Keys.GlobalIndexTemplate, logger);