using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	// remove fire deflect sound (too many when hit with a flamethrower)
	//
	[HarmonyPatch(typeof(Effecter))]
	[HarmonyPatch(nameof(Effecter.Trigger))]
	public static class Effecter_Trigger_Patch
	{
		public static bool Prefix(Effecter __instance, TargetInfo B)
		{
			return __instance.def != EffecterDefOf.Deflect_General || (B.Thing is Fire) == false;
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

	// increase fire damage when a thing has a FireDamage comp
	//
	[HarmonyPatch(typeof(Fire))]
	[HarmonyPatch("DoFireDamage")]
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
					return newDamage;
				}
			}
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
	[HarmonyPatch(typeof(Projectile))]
	[HarmonyPatch(nameof(Projectile.Launch))]
	[HarmonyPatch(new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(Thing), typeof(ThingDef) })]
	public static class Projectile_Launch_Patch
	{
		public static void Postfix(Thing launcher, Projectile __instance, Thing equipment)
		{
			if (!(__instance is FlamethrowerFlame flame)) return;
			if (launcher is Pawn pawn)
			{
				var flamethrower = equipment.TryGetComp<FlamethrowerComp>();
				if (flamethrower == null) return;
				flame.Configure(pawn, flamethrower);
			}
		}
	}

	// handle when things disappear
	//
	[HarmonyPatch(typeof(Thing))]
	[HarmonyPatch(nameof(Thing.Destroy))]
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
}
