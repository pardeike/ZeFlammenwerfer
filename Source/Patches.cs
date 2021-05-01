using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	[HarmonyPatch]
	public static class Current_ProgramState_Patch
	{
		static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(TickManager), nameof(TickManager.TogglePaused));
			yield return AccessTools.PropertySetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed));
			yield return AccessTools.Constructor(typeof(TickManager), new Type[0]);
		}

		public static void Postfix(TickManager __instance, MethodBase __originalMethod)
		{
			if (__instance == null || Current.Game == null) return;
			var paused = __instance.Paused;

			if (__originalMethod.IsConstructor)
			{
				if (FlamethrowerComp.allParticleSystems == null)
					FlamethrowerComp.allParticleSystems = new HashSet<ParticleSystem>();
				FlamethrowerComp.allParticleSystems.Do(particleSystem => UnityEngine.Object.DestroyImmediate(particleSystem));
				FlamethrowerComp.allParticleSystems.Clear();
			}

			var toUpdate = FlamethrowerComp.allParticleSystems.Where(ps => ps.isPaused != paused);
			foreach (var particleSystem in toUpdate)
			{
				if (paused) particleSystem.Pause(true);
				else particleSystem.Play(true);
			}
		}
	}

	[HarmonyPatch(typeof(Projectile))]
	[HarmonyPatch(nameof(Projectile.Launch))]
	[HarmonyPatch(new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(Thing), typeof(ThingDef) })]
	public static class Projectile_Launch_Patch
	{
		public static void Postfix(Projectile __instance, Thing equipment)
		{
			if (!(__instance is Flamethrower_Flame flame)) return;
			var flamethrower = equipment.TryGetComp<FlamethrowerComp>();
			if (flamethrower == null) return;
			flame.Configure(flamethrower);
		}
	}

	[HarmonyPatch(typeof(Thing))]
	[HarmonyPatch(nameof(Thing.Destroy))]
	public static class Thing_Destroy_Patch
	{
		public static void Prefix(Thing __instance)
		{
			FirePawnTracker.UpdateFromTarget(__instance);
			if (__instance is Pawn pawn && pawn.Map != null && pawn.RaceProps.Humanlike)
				FirePawnTracker.Remove(pawn);
			else
			{
				var map = __instance.Map;
				__instance.OccupiedRect().Cells.Do(cell => FirePawnTracker.Remove(map, cell));
			}
		}
	}

	[HarmonyPatch(typeof(Map))]
	[HarmonyPatch(nameof(Map.FinalizeInit))]
	public static class Map_FinalizeInit_Patch
	{
		public static void Postfix(Map __instance)
		{
			__instance.mapPawns.AllPawnsSpawned
				.DoIf(pawn => pawn.Map != null && pawn.HasFlameThrower(), pawn => FirePawnTracker.UpdateShooter(pawn));
		}
	}

	[HarmonyPatch(typeof(Thing))]
	[HarmonyPatch(nameof(Thing.Position), MethodType.Setter)]
	public static class Thing_Position_Setter_Patch
	{
		public static void Postfix(Thing __instance)
		{
			if (__instance is Pawn pawn && pawn.Map != null)
			{
				FirePawnTracker.UpdateFromTarget(pawn);
				if (pawn.HasFlameThrower())
					FirePawnTracker.UpdateShooter(pawn);
			}
		}
	}
}
