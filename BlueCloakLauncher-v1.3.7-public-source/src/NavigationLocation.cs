namespace Luma;

public sealed class NavigationLocation
{
	public string Scope;

	public string Query = "";

	public double Offset;

	public NavigationLocation(string scope)
	{
		Scope = scope;
	}
}
