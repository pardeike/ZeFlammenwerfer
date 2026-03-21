using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace ZeFlammenwerfer
{
	static class BlockerLifecycle
	{
		public struct State
		{
			public Map map;
			public IntVec3[] cells;
		}

		public static bool AffectsFlameBlocking(Thing thing)
		{
			return thing is not Pawn && (thing.def?.fillPercent ?? 0f) >= 0.25f;
		}

		public static void Capture(Thing thing, out State state)
		{
			if (AffectsFlameBlocking(thing) == false || thing.Map == null)
			{
				state = default;
				return;
			}

			state = new State
			{
				map = thing.Map,
				cells = thing.OccupiedRect().Cells.ToArray()
			};
		}

		public static void Update(State state, Thing thing)
		{
			var map = state.map ?? thing.Map;
			if (map == null)
				return;

			var cells = state.cells ?? [];
			if (thing.Map == map && AffectsFlameBlocking(thing))
				cells = cells.Concat(thing.OccupiedRect().Cells).Distinct().ToArray();

			if (cells.Length > 0)
				PawnShooterTracker.Update(map, cells);
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
	static class Thing_SpawnSetup_BlockerLifecycle_Patch
	{
		static void Postfix(Thing __instance)
		{
			if (BlockerLifecycle.AffectsFlameBlocking(__instance) == false || __instance.Map == null)
				return;

			PawnShooterTracker.Update(__instance.Map, __instance.OccupiedRect().Cells);
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
	static class Thing_DeSpawn_BlockerLifecycle_Patch
	{
		static void Prefix(Thing __instance, out BlockerLifecycle.State __state)
		{
			BlockerLifecycle.Capture(__instance, out __state);
		}

		static void Postfix(Thing __instance, BlockerLifecycle.State __state)
		{
			BlockerLifecycle.Update(__state, __instance);
		}
	}

	[HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Setter)]
	static class Thing_Position_BlockerLifecycle_Patch
	{
		static void Prefix(Thing __instance, IntVec3 value, out BlockerLifecycle.State __state)
		{
			if (value == __instance.positionInt)
			{
				__state = default;
				return;
			}

			BlockerLifecycle.Capture(__instance, out __state);
		}

		static void Postfix(Thing __instance, BlockerLifecycle.State __state)
		{
			BlockerLifecycle.Update(__state, __instance);
		}
	}
}
