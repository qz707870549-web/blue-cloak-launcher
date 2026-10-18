using System.Collections.Generic;

namespace Luma;

public class WindowSnapshot
{
	public List<RunningWindow> Windows = new List<RunningWindow>();

	public List<RunningWindow> Hidden = new List<RunningWindow>();

	public List<RunningProcess> Processes = new List<RunningProcess>();
}
