namespace RocketExplorer.Web.Components;

public sealed class FuncEqualityComparer<T>(Func<T, T, bool> equalsFunc, Func<T, int> getHashCodeFunc)
	: IEqualityComparer<T>
{
	private readonly Func<T, T, bool> equals = equalsFunc ?? throw new ArgumentNullException(nameof(equalsFunc));

	private readonly Func<T, int> getHashCode =
		getHashCodeFunc ?? throw new ArgumentNullException(nameof(getHashCodeFunc));

	public bool Equals(T? x, T? y) => this.equals(x!, y!);

	public int GetHashCode(T obj) => this.getHashCode(obj);
}