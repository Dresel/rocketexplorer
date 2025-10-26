namespace RocketExplorer.Core.Ens;

public class EnsEventHandlerResult
{
	public EnsNameLookupResult? ForwardResult { get; set; }

	public ReverseAddressLookupResult? ReverseResult { get; set; }
}