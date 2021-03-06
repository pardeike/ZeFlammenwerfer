using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	// remove fire deflect sound (too many when hit with a flamethrower)
	//
	[HarmonyPatch(typeof(Effecter), nameof(Effecter.Trigger))]
	public static class Effecter_Trigger_Patch
	{
		public static bool Prefix(Effecter __instance, TargetInfo B)
		{
			return __instance.def != EffecterDefOf.Deflect_General || (B.Thing is Fire) == false;
		}
	}

	// draw tank and pipe
	//
	[HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
	[HarmonyPatch(new Type[] { typeof(Vector3), typeof(float), typeof(bool), typeof(Rot4), typeof(Rot4), typeof(RotDrawMode), typeof(bool), typeof(bool), typeof(bool) })]
	public static class PawnRenderer_RenderPawnInternal_Patch
	{
		public const float magicOffset = 0.009183673f;

		public static void Postfix(PawnRenderer __instance, Vector3 rootLoc)
		{
			var pawn = __instance.graphics.pawn;
			if (pawn.Downed || pawn.Dead) return;
			if (pawn.HasFlameThrower() == false) return;

			var orientation = pawn.Rotation;
			var location = rootLoc;
			location.y += magicOffset + (orientation == Rot4.North ? Altitudes.AltInc : -Altitudes.AltInc / 12f);

			Graphics.DrawMesh(MeshPool.plane10, location + ZeFlameComp.tankOffset[orientation.AsInt], Quaternion.identity, Assets.tank, 0);

			var flameComp = pawn.equipment?.Primary?.TryGetComp<ZeFlameComp>();
			if (flameComp == null) return;
			flameComp.UpdateDrawPos(pawn);
		}
	}

	// init our static gameobject holder when a game is initialized
	//
	[HarmonyPatch]
	public static class Game_InitNewGame_LoadGame_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return SymbolExtensions.GetMethodInfo(() => new Game().InitNewGame());
			yield return SymbolExtensions.GetMethodInfo(() => new Game().LoadGame());
		}

		public static void Prefix()
		{
			ZeFlameComp.allParticleSystems?.Do(particleSystem => UnityEngine.Object.DestroyImmediate(particleSystem));
			ZeFlameComp.allParticleSystems = new HashSet<ParticleSystem>();
		}
	}

	// start/stop flamethrowers when game is paused/resumed
	//
	[HarmonyPatch]
	public static class Current_ProgramState_Patch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(TickManager), nameof(TickManager.TogglePaused));
			yield return AccessTools.PropertySetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed));
		}

		public static void Postfix(TickManager __instance)
		{
			if (__instance == null || Current.Game == null) return;
			var paused = __instance.Paused;

			ZeFlameComp.allParticleSystems.Where(ps => ps.isPaused != paused).Do(particleSystem =>
			{
				if (paused) particleSystem.Pause(true);
				else particleSystem.Play(true);
			});
		}
	}

	// stop flamethrower when in non-aiming stance
	//
	[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.Tick))]
	public static class ThingWithComps_Tick_Patch
	{
		public static void Postfix(ThingWithComps __instance)
		{
			var pawn = __instance as Pawn;
			var flameComp = pawn?.equipment?.Primary?.TryGetComp<ZeFlameComp>();
			if (flameComp == null) return;
			if (WeaponTool.IsAiming(pawn) == false && flameComp.isActive)
				flameComp.SetActive(false);
		}
	}

	// stop flamethrower when removed
	//
	[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved))]
	public static class Pawn_EquipmentTracker_Notify_EquipmentRemoved_Patch
	{
		public static void Prefix(ThingWithComps eq)
		{
			var flameComp = eq.TryGetComp<ZeFlameComp>();
			flameComp?.SetActive(false);
		}
	}

	// make fire below flame projectiles
	//
	[HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Setter)]
	public static class Thing_Position_Patch
	{
		public static void Postfix(Thing __instance, IntVec3 value)
		{
			if (__instance is ZeFlame flame)
			{
				var map = flame.Map;
				if (map != null && value.DistanceToSquared(flame.Launcher.Position) > 4)
				{
					if (map.thingGrid.ThingAt<Fire>(value) == null)
					{
						var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire, null);
						fire.fireSize = 0.5f;
						_ = GenSpawn.Spawn(fire, flame.Position, map, Rot4.North, WipeMode.Vanish, false);
					}
				}
			}
		}
	}

	// limit new fire bullet casts
	//
	[HarmonyPatch(typeof(Pawn_StanceTracker), nameof(Pawn_StanceTracker.StanceTrackerTick))]
	public static class Pawn_StanceTracker_StanceTrackerTick_Patch
	{
		const int maximumNumberOfBullets = 5;

		public static bool SkipStanceTickIfNecessary(bool flag, Pawn_StanceTracker stanceTracker)
		{
			if ((stanceTracker.curStance is Stance_Warmup) == false) return flag;
			var flameComp = stanceTracker.pawn.equipment?.Primary?.TryGetComp<ZeFlameComp>();
			return flag || (flameComp?.flames.Count ?? 0) >= maximumNumberOfBullets;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				if (instruction.Branches(out var _))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return CodeInstruction.Call(() => SkipStanceTickIfNecessary(default, default));
				}
				yield return instruction;
			}
		}
	}

	// increase fire damage when a thing has a FireDamage comp
	//
	[HarmonyPatch(typeof(Fire), "DoFireDamage")]
	public static class Fire_DoFireDamage_Patch
	{
		const float factorPawn = 0.001f;
		const float factorThing = 0.1f;

		public static float Multiply(int damage, Thing thing)
		{
			if (thing is ThingWithComps thingWithComps)
			{
				var fireDamageComp = thingWithComps.GetComp<FireDamage>();
				if (fireDamageComp != null)
				{
					var factor = (thing as Pawn) != null ? factorPawn : factorThing;
					var newDamage = damage * factor * fireDamageComp.multiplier;

					Tools.Log($"DAMAGE {thing.ThingID} {damage} x {factor} x {fireDamageComp.multiplier} = {newDamage}");
					return newDamage;
				}
				else
					Tools.Log($"DAMAGE {damage} ON {thing} - NO FireDamage");
			}
			Tools.Log($"DAMAGE {thing.ThingID} {damage}");
			return damage;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			var m_RoundRandom = SymbolExtensions.GetMethodInfo(() => GenMath.RoundRandom(default));
			var m_Multiply = SymbolExtensions.GetMethodInfo(() => Multiply(default, default));

			var floatVar = generator.DeclareLocal(typeof(float));

			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldloc_0)
				{
					instruction.opcode = OpCodes.Ldloc;
					instruction.operand = floatVar;
				}

				if (instruction.opcode == OpCodes.Stloc_0)
				{
					instruction.opcode = OpCodes.Stloc;
					instruction.operand = floatVar;
				}

				yield return instruction;

				if (instruction.opcode == OpCodes.Ldc_I4_1)
					yield return new CodeInstruction(OpCodes.Conv_R4);

				if (instruction.Calls(m_RoundRandom))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_1);
					yield return new CodeInstruction(OpCodes.Call, m_Multiply);
				}
			}
		}
	}

	// attach flamethrower logic to custom projectile
	//
	[HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch))]
	[HarmonyPatch(new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(Thing), typeof(ThingDef) })]
	public static class Projectile_Launch_Patch
	{
		public static void Postfix(Thing launcher, Projectile __instance, Thing equipment)
		{
			if (!(__instance is ZeFlame flame)) return;
			if (launcher is Pawn pawn)
			{
				var flameComp = equipment?.TryGetComp<ZeFlameComp>();
				if (flameComp == null) return;
				flame.Configure(pawn, flameComp);
			}
		}
	}

	// handle when things disappear
	//
	[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
	public static class Thing_Destroy_Patch
	{
		public class Info
		{
			public Map map;
			public IEnumerable<IntVec3> cells;
		}

		public static void Prefix(Thing __instance, out Info __state)
		{
			__state = new Info()
			{
				map = __instance.Map,
				cells = __instance.OccupiedRect().Cells
			};
		}

		public static void Postfix(Thing __instance, Info __state)
		{
			if (__instance is Pawn) return;
			var map = __state.map;
			if (map == null) return;
			var vec2s = __state.cells.Select(cell => cell.ToIntVec2);
			if (vec2s.Any(vec2 => map.BlocksFlamethrower(vec2)))
				PawnShooterTracker.RemoveThing(map, vec2s);
		}
	}

	// make mechs flammable
	//
	[HarmonyPatch(typeof(StatExtension), "GetStatValue")]
	public static class StatExtension_GetStatValue_Patch
	{
		const float flammableValue = 0.5f;

		public static bool Prefix(Thing thing, StatDef stat, ref float __result)
		{
			if (stat != StatDefOf.Flammability) return true;
			if (!(thing is Pawn pawn) || pawn.RaceProps.IsMechanoid == false) return true;
			if (pawn.TryGetComp<FireDamage>() == null) return true;
			__result = flammableValue;
			return false;
		}
	}

	// make flames hurt them
	//
	[HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
	public static class DamageWorker_AddInjury_ApplyDamageToPart_Patch
	{
		public static void Prefix(Pawn pawn, ref DamageInfo dinfo)
		{
			if (pawn.RaceProps.IsMechanoid == false) return;
			if (pawn.TryGetComp<FireDamage>() == null) return;
			var newDinfo = new DamageInfo(dinfo);
			newDinfo.SetAllowDamagePropagation(false);
			newDinfo.SetIgnoreArmor(true);
			dinfo = newDinfo;
		}
	}
}
