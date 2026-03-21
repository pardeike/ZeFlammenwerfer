using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Collections;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	// debug draw
	[HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate))]
	static class MapInterface_MapInterfaceOnGUI_AfterMainTabs_Patch
	{
		static void Postfix()
		{
			FlameDangerTracker.DrawDebugCells(Find.CurrentMap);
		}
	}

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

	// tick flamethrowers (weapons don't tick per default)
	//
	[HarmonyPatch(typeof(Pawn_EquipmentTracker))]
	[HarmonyPatch(nameof(Pawn_EquipmentTracker.EquipmentTrackerTick))]
	static class Pawn_EquipmentTracker_EquipmentTrackerTick_Patch
	{
		static void Postfix(Pawn ___pawn)
		{
			if (___pawn.equipment?.Primary is ZeFlammenwerfer flamethrower)
				flamethrower.Tick();
		}
	}

	// draw tank and pipe
	//
	[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
	public static class PawnRenderer_RenderPawnInternal_Patch
	{
		public const float magicOffset = 0.008687258f;

		public static void Postfix(Pawn pawn, Vector3 drawPos, Rot4 facing)
		{
			if (pawn.Downed || pawn.Dead)
				return;
			if (pawn.HasFlameThrower() == false)
				return;

			var orientation = facing;
			var location = drawPos;
			location.y += magicOffset + (orientation == Rot4.North ? 0.0014478763f : -0.0014478763f);
			Graphics.DrawMesh(MeshPool.plane10, location + ZeFlameComp.tankOffset[orientation.AsInt], Quaternion.identity, Assets.tank, 0);

			var flameComp = pawn.equipment?.Primary?.TryGetComp<ZeFlameComp>();
			if (flameComp == null)
				return;
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
			ZeFlameComp.allParticleSystems?.Do(Object.DestroyImmediate);
			ZeFlameComp.allParticleSystems = new HashSet<ParticleSystem>();
			FlameDangerTracker.ClearAll();
		}
	}

	// show fuel level when flamethrower is equipped
	//
	[HarmonyPatch]
	static class Pawn_EquipmentTracker_YieldGizmos_Patch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.FirstMethod(typeof(Pawn_EquipmentTracker), mi =>
			{
				if (mi.GetParameters().Length < 1)
					return false;
				if (mi.GetParameters()[0].ParameterType != typeof(ThingWithComps))
					return false;
				return mi.Name.Contains("__YieldGizmos");
			});
		}

		public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, ThingWithComps eq)
		{
			if (eq is ZeFlammenwerfer flamethrower && flamethrower.pawn != null)
			{
				foreach (var gizmo in flamethrower.refuelable?.CompGetGizmosExtra() ?? Enumerable.Empty<Gizmo>())
					yield return gizmo;
			}
			foreach (var gizmo in gizmos)
				yield return gizmo;
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
			if (__instance == null || Current.Game == null)
				return;
			var paused = __instance.Paused;

			ZeFlameSound.allFlameSounds.Where(sound => sound != null).Do(sound => sound.SetPause(paused));
			ZeFlameComp.allParticleSystems.Where(ps => ps != null && ps.isPaused != paused).Do(particleSystem =>
			{
				if (paused)
					particleSystem.Pause(true);
				else
					particleSystem.Play(true);
			});
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
				flameComp?.SetPipeActive(false);
			if (eq is ZeFlammenwerfer flamethrower)
			{
				flamethrower.ClearManualTarget();
				FlameDangerTracker.Clear(flamethrower.pawn);
			}
			}
		}

		[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
		public static class Pawn_EquipmentTracker_TryDropEquipment_Patch
		{
			public static void Prefix(ThingWithComps eq, ref bool forbid)
			{
				if (eq is ZeFlammenwerfer)
					forbid = false;
			}
		}

		[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
		public static class FloatMenuMakerMap_GetOptions_Patch
		{
			public static void Postfix(FloatMenuContext context, ref List<FloatMenuOption> __result)
			{
				if (context == null || context.IsMultiselect || context.FirstSelectedPawn == null || __result == null)
					return;
				var actor = context.FirstSelectedPawn;
				var droppedFlamethrower = context.ClickedThings.OfType<ZeFlammenwerfer>().FirstOrDefault(thing => thing.pawn == null);
				if (droppedFlamethrower != null)
					__result.Add(FlamethrowerRefuelUtility.MakeGroundRefuelOption(actor, droppedFlamethrower));
				var bearer = context.ClickedPawns.FirstOrDefault(pawn => pawn != actor && FlamethrowerRefuelUtility.EquippedFlamethrower(pawn) != null);
				if (bearer == null)
					return;
				__result.Add(FlamethrowerRefuelUtility.MakeEquippedRefuelOption(actor, bearer));
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
			if ((stanceTracker.curStance is Stance_Warmup) == false)
				return flag;
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
	[HarmonyPatch(typeof(Fire), nameof(Fire.DoFireDamage))]
	public static class Fire_DoFireDamage_Patch
	{
		const float factorPawn = 0.001f;
		const float factorThing = 0.1f;

		public static void Multiply(ref int damage, Thing thing)
		{
			if (thing is ThingWithComps thingWithComps)
			{
				var fireDamageComp = thingWithComps.GetComp<FireDamage>();
				if (fireDamageComp != null)
				{
					var factor = (thing as Pawn) != null ? factorPawn : factorThing;
					damage = (int)(damage * factor * fireDamageComp.multiplier);
				}
			}
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return new CodeMatcher(instructions)
				.MatchStartForward(
					new CodeMatch(OpCodes.Ldarg_1),
					new CodeMatch(OpCodes.Isinst, typeof(Pawn))
				)
				.Insert(
					new CodeInstruction(OpCodes.Ldloca_S, 0),
					new CodeInstruction(OpCodes.Ldarg_1),
					CodeInstruction.Call((int dummy) => Multiply(ref dummy, default))
				)
				.InstructionEnumeration();
		}
	}

	// attach flamethrower logic to custom projectile
	//
		[HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch))]
		[HarmonyPatch(new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) })]
		public static class Projectile_Launch_Patch
		{
			static bool Prefix(Projectile __instance, Thing equipment)
			{
				if (__instance is not ZeFlame)
					return true;
				if (equipment is not ZeFlammenwerfer flamethrower || flamethrower.refuelable == null)
					return true;
				if (flamethrower.CanFireNow == false)
				{
					flamethrower.flameComp?.SetActive(false);
					return false;
				}
				if (flamethrower.FuelPerShot > 0f)
					flamethrower.refuelable.ConsumeFuel(flamethrower.FuelPerShot);
				return true;
			}

			public static void Postfix(Thing launcher, Projectile __instance, Thing equipment)
			{
				if (__instance is not ZeFlame flame)
					return;
			if (launcher is Pawn pawn)
			{
				var flameComp = equipment?.TryGetComp<ZeFlameComp>();
				DebugTrace.Log($"Projectile.Launch projectile={__instance.def.defName} launcher={pawn.LabelShortCap} equipment={equipment?.def?.defName ?? "null"} flameComp={(flameComp != null ? "present" : "missing")}");
				if (flameComp == null)
					return;
				flame.Configure(pawn, flameComp);
			}
		}
	}

	[HarmonyPatch(typeof(PathFinderMapData), nameof(PathFinderMapData.ParameterizeGridJob))]
	public static class PathFinderMapData_ParameterizeGridJob_Patch
	{
		public static void Postfix(PathRequest request, ref PathGridJob job)
		{
			if (request?.map == null)
				return;
			if (request.customizer != null)
				return;
			if (FlameDangerTracker.ShouldIgnorePathDanger(request.pawn))
				return;
			if (FlameDangerTracker.TryGetRouteCostGrid(request.map, out NativeArray<ushort>.ReadOnly routeCosts) == false)
				return;

			job.custom = routeCosts;
		}
	}

	[HarmonyPatch]
	public static class Pawn_PathFollower_SetupMoveIntoNextCell_Patch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(Pawn_PathFollower), "SetupMoveIntoNextCell");
		}

		public static void Postfix(Pawn_PathFollower __instance)
		{
			FlameDangerTracker.ResetPathIfUpcomingDanger(__instance);
		}
	}

	[HarmonyPatch(typeof(Verb), nameof(Verb.Available))]
	public static class Verb_Available_Patch
	{
		public static void Postfix(Verb __instance, ref bool __result)
		{
			if (__result == false)
				return;
			if (__instance.EquipmentSource is ZeFlammenwerfer flamethrower && flamethrower.CanFireNow == false)
				__result = false;
		}
	}

	[HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn))]
	[HarmonyPatch(new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
	public static class Verb_TryStartCastOn_Patch
	{
		public static bool Prefix(Verb __instance)
		{
			return __instance.EquipmentSource is not ZeFlammenwerfer flamethrower || flamethrower.CanFireNow;
		}
	}

	[HarmonyPatch(typeof(Verb), nameof(Verb.OrderForceTarget))]
	public static class Verb_OrderForceTarget_Patch
	{
		public static bool Prefix(Verb __instance, LocalTargetInfo target)
		{
			if (__instance.EquipmentSource is not ZeFlammenwerfer flamethrower)
				return true;
			if (__instance.CasterPawn == null || flamethrower.pawn != __instance.CasterPawn)
				return true;

			flamethrower.OrderManualTarget(target);
			return false;
		}
	}

	[HarmonyPatch(typeof(Targeter), nameof(Targeter.OrderPawnForceTarget))]
	public static class Targeter_OrderPawnForceTarget_Patch
	{
		static readonly MethodInfo currentTargetUnderMouse = AccessTools.Method(typeof(Targeter), "CurrentTargetUnderMouse", new[] { typeof(bool) });

		public static bool Prefix(Targeter __instance, ITargetingSource targetingSource)
		{
			if (targetingSource is not Verb verb)
				return true;
			if (verb.EquipmentSource is not ZeFlammenwerfer flamethrower)
				return true;
			if (__instance == null || currentTargetUnderMouse == null || flamethrower.pawn != verb.CasterPawn)
				return true;

			var target = (LocalTargetInfo)currentTargetUnderMouse.Invoke(__instance, new object[] { true });
			if (target.IsValid)
				flamethrower.OrderManualTarget(target);
			__instance.StopTargeting();
			return false;
		}
	}

	[HarmonyPatch]
	public static class VerbTracker_CreateVerbTargetCommand_Patch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(VerbTracker), "CreateVerbTargetCommand", new[] { typeof(Thing), typeof(Verb) });
		}

		public static void Postfix(Thing ownerThing, Verb verb, Command_VerbTarget __result)
		{
			if (ownerThing is not ZeFlammenwerfer flamethrower || __result == null)
				return;

			__result.requiresAvailableVerb = true;
			if (flamethrower.CanFireNow == false)
				__result.Disable(flamethrower.OutOfFuelReason);
		}
	}

	// handle when things disappear
	//
	[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
	public static class Thing_Destroy_Patch
	{
		public struct Info
		{
			public Map map;
			public IntVec3[] cells;
		}

		public static void Prefix(Thing __instance, out Info __state)
		{
			__state = new Info()
			{
				map = __instance.Map,
				cells = __instance.OccupiedRect().Cells.ToArray()
			};
		}

		public static void Postfix(Thing __instance, Info __state)
		{
			if (__instance is Pawn)
				return;
			var map = __state.map;
			if (map == null)
				return;
			PawnShooterTracker.Update(map, __state.cells);
		}
	}

	// make mechs flammable
	//
	[HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
	public static class StatExtension_GetStatValue_Patch
	{
		const float flammableValue = 0.5f;

		public static bool Prefix(Thing thing, StatDef stat, ref float __result)
		{
			if (stat != StatDefOf.Flammability)
				return true;
			if (thing is not Pawn pawn || pawn.RaceProps.IsMechanoid == false)
				return true;
			if (pawn.TryGetComp<FireDamage>() == null)
				return true;
			__result = flammableValue;
			return false;
		}
	}

	// make flames hurt them
	//
	[HarmonyPatch(typeof(DamageWorker_AddInjury), nameof(DamageWorker_AddInjury.ApplyDamageToPart))]
	public static class DamageWorker_AddInjury_ApplyDamageToPart_Patch
	{
		public static void Prefix(Pawn pawn, ref DamageInfo dinfo)
		{
			if (pawn.RaceProps.IsMechanoid == false)
				return;
			if (dinfo.ignoreInstantKillProtectionInt)
				return;
			if (pawn.TryGetComp<FireDamage>() == null)
				return;
			var newDinfo = new DamageInfo(dinfo);
			newDinfo.SetAllowDamagePropagation(false);
			newDinfo.SetIgnoreArmor(true);
			dinfo = newDinfo;
		}
	}
}
