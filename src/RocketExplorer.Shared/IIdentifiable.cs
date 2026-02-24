namespace RocketExplorer.Shared;

public interface IIdentifiable<out TIdentifier>
{
	public TIdentifier Identifier { get; }
}