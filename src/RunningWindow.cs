namespace Luma;

public class RunningWindow
{
	public long Handle;

	public int ProcessId;

	public string Title;

	public string Executable;

	public string ProcessName;

	public string AppId;

	public bool Visible;

	public string Key => WindowController.Family(Executable ?? ProcessName);
}
