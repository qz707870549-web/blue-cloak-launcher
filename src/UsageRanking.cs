using System;
using System.Collections.Generic;
using System.Linq;

namespace Luma;

public static class UsageRanking
{




	public static void Record(Entry entry, DateTime utcNow)
	{
		if (entry != null)
		{
			entry.LaunchCount = Math.Min(2147483646, Math.Max(0, entry.LaunchCount)) + 1;
			entry.LastLaunchTicks = utcNow.ToUniversalTime().Ticks;
		}
	}

}
