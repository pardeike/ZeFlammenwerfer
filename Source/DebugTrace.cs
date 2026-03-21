using System.Collections.Generic;
using Verse;

namespace ZeFlammenwerfer
{
	static class DebugTrace
	{
		static bool Enabled => false;
		static readonly Dictionary<string, int> lastLoggedTicks = new();

		public static void Log(string message)
		{
			if (Enabled == false)
				return;
			Verse.Log.Message($"[ZeFlammenwerfer] {message}");
		}

		public static void LogThrottled(string key, int intervalTicks, string message)
		{
			if (Enabled == false)
				return;

			var tick = Find.TickManager?.TicksGame ?? -1;
			if (lastLoggedTicks.TryGetValue(key, out var lastTick) && tick - lastTick < intervalTicks)
				return;

			lastLoggedTicks[key] = tick;
			Verse.Log.Message($"[ZeFlammenwerfer] {message}");
		}
	}
}
