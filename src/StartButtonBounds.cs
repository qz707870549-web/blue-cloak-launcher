namespace Luma;

public class StartButtonBounds
{
	public int Left;

	public int Top;

	public int Right;

	public int Bottom;

	public long Handle;

	public bool Contains(int horizontal, int vertical)
	{
		if (horizontal >= Left && horizontal < Right && vertical >= Top)
		{
			return vertical < Bottom;
		}
		return false;
	}
}
