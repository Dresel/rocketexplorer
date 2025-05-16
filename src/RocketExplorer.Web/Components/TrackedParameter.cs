using System.Collections.Generic;

namespace RocketExplorer.Web.Components;

public class TrackedParameter(Func<object?> propertyAccessor)
{
	private readonly Func<object?> propertyAccessor = propertyAccessor;

	public object? Current { get; private set; }

	public object? Previous { get; private set; }

	public bool Update()
	{
		object? value = this.propertyAccessor();

		bool changed = !EqualityComparer<object>.Default.Equals(Current, value);

		if (changed)
		{
			Previous = Current;
			Current = value;
		}

		return changed;
	}
}