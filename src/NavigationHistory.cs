using System;
using System.Collections.Generic;

namespace Luma;

public sealed class NavigationHistory
{
	private readonly List<NavigationLocation> locations = new List<NavigationLocation>();

	private int position;

	public bool CanBack => position > 0;

	public bool CanForward => position < locations.Count - 1;

	public NavigationLocation Current => locations[position];

	public NavigationHistory(string initial)
	{
		locations.Add(new NavigationLocation(initial));
	}

	public void Remember(string query, double offset)
	{
		Current.Query = query ?? "";
		Current.Offset = ((double.IsNaN(offset) || double.IsInfinity(offset)) ? 0.0 : Math.Max(0.0, offset));
	}

	public void Visit(string scope)
	{
		if (!(scope == Current.Scope))
		{
			if (CanForward)
			{
				locations.RemoveRange(position + 1, locations.Count - position - 1);
			}
			locations.Add(new NavigationLocation(scope));
			position++;
		}
	}

	public NavigationLocation Back()
	{
		if (CanBack)
		{
			position--;
		}
		return Current;
	}

	public NavigationLocation Forward()
	{
		if (CanForward)
		{
			position++;
		}
		return Current;
	}
}
