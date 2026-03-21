using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ZeFlammenwerfer
{
	static class MapRenderState
	{
		static bool initialized;
		static Map renderedMap;

		public static bool ShouldRenderMap(Map map)
		{
			return map != null && DetermineRenderedMap() == map;
		}

		public static void Refresh(bool force = false)
		{
			var nextRenderedMap = DetermineRenderedMap();
			var changed = initialized == false || renderedMap != nextRenderedMap;
			if (force == false && changed == false)
				return;

			initialized = true;
			renderedMap = nextRenderedMap;
			Apply(force || changed);
		}

		public static void SuspendAll()
		{
			if (initialized && renderedMap == null)
				return;

			initialized = true;
			renderedMap = null;
			Apply(force: true);
		}

		public static void Invalidate()
		{
			initialized = false;
			renderedMap = null;
		}

		static Map DetermineRenderedMap()
		{
			if (Current.ProgramState != ProgramState.Playing)
				return null;
			if (Screen_Credits.creditsShowing)
				return null;
			if (WorldRendererUtility.DrawingMap == false)
				return null;
			return Find.CurrentMap;
		}

		static void Apply(bool force)
		{
			PawnShooterTracker.RefreshRenderedMap(renderedMap);
			ColliderHolder.RefreshRenderedMap(renderedMap);
			ZeFlameComp.RefreshRenderedMap(renderedMap, force);
		}
	}
}
