using HarmonyLib;
using RimWorld;
using System;
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
		static bool Prepare()
		{
			return FlameDangerTracker.RenderDebugCellsEnabled;
		}

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

	static class PawnFacingUtility
	{
		public static bool CanRotateNow(Pawn pawn)
		{
			if (pawn == null || pawn.Destroyed || pawn.Spawned == false || pawn.Map == null)
				return false;
			if (pawn.kindDef?.useFixedRotation == true)
				return false;
			if (pawn.stances?.stunner?.Stunned == true && pawn.stances.stunner.DisableRotation)
				return false;
			return true;
		}

		public static void FaceSouthNow(Pawn pawn)
		{
			if (CanRotateNow(pawn) == false)
				return;

			pawn.Rotation = Rot4.South;
			MarkGraphicsDirty(pawn);
		}

		public static void MarkGraphicsDirty(Pawn pawn)
		{
			pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
		}
	}

	[HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
	public static class Pawn_DraftController_Drafted_Patch
	{
		public static void Prefix(Pawn_DraftController __instance, out bool __state)
		{
			__state = __instance?.Drafted == true;
		}

		public static void Postfix(Pawn ___pawn, bool value, bool __state)
		{
			if (value == __state)
				return;

			if (___pawn?.equipment?.Primary is ZeFlammenwerfer flamethrower)
				flamethrower.ClearManualTargetVisuals();

			if (value)
				PawnFacingUtility.FaceSouthNow(___pawn);
		}
	}

	// draw tank and pipe
	//
	[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
	public static class PawnRenderer_RenderPawnInternal_Patch
	{
		public static bool Prefix(Pawn pawn, Vector3 drawPos, PawnRenderFlags flags)
		{
			if (pawn?.equipment?.Primary is not ZeFlammenwerfer flamethrower || flamethrower.HasManualTarget == false)
				return true;
			if (WeaponTool.TryGetAimingData(pawn, drawPos, flags, out var aiming, includeRecoil: false) == false)
				return true;

			PawnRenderUtility.DrawEquipmentAiming(flamethrower, aiming.DrawPos, aiming.AimAngle);
			if (pawn.apparel != null)
				foreach (var apparel in pawn.apparel.WornApparel)
					apparel.DrawWornExtras();
			return false;
		}

		public static void Postfix(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
		{
			if (pawn.Downed || pawn.Dead)
				return;
			if (pawn.equipment?.Primary is not ZeFlammenwerfer flamethrower)
				return;

			var orientation = facing;
			var location = ZeFlameComp.TankDrawPosition(drawPos, orientation);
			Graphics.DrawMesh(MeshPool.plane10, location, Quaternion.identity, Assets.tank, 0);

			var flameComp = flamethrower.flameComp ?? flamethrower.TryGetComp<ZeFlameComp>();
			if (flameComp == null)
				return;
			flameComp.UpdateDrawPos(pawn, drawPos, orientation, flags);
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
			MapRenderState.Invalidate();
			ZeFlameComp.ClearAllVisuals();
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
			var candidates = AccessTools.GetDeclaredMethods(typeof(Pawn_EquipmentTracker))
				.Where(mi =>
				{
					if (mi.GetParameters().Length < 1)
						return false;
					if (mi.GetParameters()[0].ParameterType != typeof(ThingWithComps))
						return false;
					return mi.Name.Contains("__YieldGizmos");
				})
				.ToArray();

			if (candidates.Length != 1)
			{
				var names = string.Join(", ", candidates.Select(method => method.Name));
				throw new InvalidOperationException($"[ZeFlammenwerfer] Expected exactly one Pawn_EquipmentTracker YieldGizmos helper with a ThingWithComps first parameter, found {candidates.Length}: {names}");
			}

			return candidates[0];
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
			MapRenderState.Refresh(force: true);
		}
	}

	[HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootUpdate))]
	public static class UIRoot_Play_UIRootUpdate_Patch
	{
		public static void Postfix()
		{
			MapRenderState.Refresh();
		}
	}

	[HarmonyPatch(typeof(MapInterface), nameof(MapInterface.Notify_SwitchedMap))]
	public static class MapInterface_Notify_SwitchedMap_Patch
	{
		public static void Postfix()
		{
			MapRenderState.Refresh(force: true);
		}
	}

	[HarmonyPatch(typeof(Current), nameof(Current.Notify_LoadedSceneChanged))]
	public static class Current_Notify_LoadedSceneChanged_Patch
	{
		public static void Postfix()
		{
			MapRenderState.Invalidate();
		}
	}

	[HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
	public static class GenScene_GoToMainMenu_Patch
	{
		public static void Prefix()
		{
			MapRenderState.SuspendAll();
		}
	}

	[HarmonyPatch(typeof(Screen_Credits), nameof(Screen_Credits.PreOpen))]
	public static class Screen_Credits_PreOpen_Patch
	{
		public static void Postfix()
		{
			MapRenderState.Refresh(force: true);
		}
	}

	[HarmonyPatch(typeof(Screen_Credits), nameof(Screen_Credits.PostClose))]
	public static class Screen_Credits_PostClose_Patch
	{
		public static void Postfix()
		{
			MapRenderState.Refresh(force: true);
		}
	}

	// stop flamethrower when removed
	//
	[HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved))]
	public static class Pawn_EquipmentTracker_Notify_EquipmentRemoved_Patch
	{
		public static void Prefix(ThingWithComps eq)
		{
			if (eq is ZeFlammenwerfer flamethrower)
			{
				flamethrower.ClearManualTargetVisuals();
				return;
			}

			var flameComp = eq.TryGetComp<ZeFlameComp>();
			flameComp?.SetActive(false);
			flameComp?.SetPipeActive(false);
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
			var actorFlamethrower = FlamethrowerRefuelUtility.EquippedFlamethrower(actor);
			var actorRefuelable = actorFlamethrower?.refuelable ?? actorFlamethrower?.TryGetComp<CompRefuelable>();
			if (actorRefuelable?.IsFull == false)
			{
				var clickedFuel = context.ClickedThings.FirstOrDefault(thing => actorRefuelable.Props?.fuelFilter.Allows(thing) == true);
				if (clickedFuel != null)
					__result.Add(FlamethrowerRefuelUtility.MakeSelfRefuelFromFuelOption(actor, clickedFuel));
			}
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
			if (__instance is not ZeFlame flame)
				return;
			var launcher = flame.Launcher;
			var map = flame.Map;
			if (map == null || launcher?.Spawned != true || value.DistanceToSquared(launcher.Position) <= 4)
				return;
			if (map.thingGrid.ThingAt<Fire>(value) == null)
			{
				var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire, null);
				fire.fireSize = 0.5f;
				_ = GenSpawn.Spawn(fire, flame.Position, map, Rot4.North, WipeMode.Vanish, false);
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
			var result = new List<CodeInstruction>();
			var injectionCount = 0;
			foreach (var instruction in instructions)
			{
				if (instruction.Branches(out var _))
				{
					result.Add(new CodeInstruction(OpCodes.Ldarg_0));
					result.Add(CodeInstruction.Call(() => SkipStanceTickIfNecessary(default, default)));
					injectionCount++;
				}
				result.Add(instruction);
			}
			if (injectionCount == 0)
				throw new InvalidOperationException("[ZeFlammenwerfer] Could not patch Pawn_StanceTracker.StanceTrackerTick: no branch anchor was found.");
			if (injectionCount != 1)
				Log.Warning($"[ZeFlammenwerfer] Patched Pawn_StanceTracker.StanceTrackerTick at {injectionCount} branch anchors; expected 1 for RimWorld 1.6.4850.");
			return result;
		}
	}

	// increase fire damage when a thing is being hit by sustained flames
	//
	[HarmonyPatch(typeof(Fire), nameof(Fire.DoFireDamage))]
	public static class Fire_DoFireDamage_Patch
	{
		const float factorPawn = 0.001f;
		const float factorThing = 0.1f;

		public static float Multiply(float damage, Thing thing)
		{
			var multiplier = FlameDamageTracker.GetMultiplier(thing);
			if (multiplier > 1f)
			{
				var factor = thing is Pawn ? factorPawn : factorThing;
				return damage * factor * multiplier;
			}
			return damage;
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var previousWasDamageLoad = false;
			var result = new List<CodeInstruction>();
			var injectionCount = 0;
			foreach (var instruction in instructions)
			{
				result.Add(instruction);

				if (previousWasDamageLoad && instruction.opcode == OpCodes.Conv_R4)
				{
					result.Add(new CodeInstruction(OpCodes.Ldarg_1));
					result.Add(CodeInstruction.Call(() => Multiply(default, default)));
					injectionCount++;
				}

				previousWasDamageLoad = LoadsDamageLocal(instruction);
			}
			if (injectionCount == 0)
				throw new InvalidOperationException("[ZeFlammenwerfer] Could not patch Fire.DoFireDamage: no damage-local conversion anchor was found.");
			if (injectionCount != 3)
				Log.Warning($"[ZeFlammenwerfer] Patched Fire.DoFireDamage at {injectionCount} damage anchors; expected 3 for RimWorld 1.6.4850.");
			return result;
		}

		static bool LoadsDamageLocal(CodeInstruction instruction)
		{
			if (instruction.opcode == OpCodes.Ldloc_0)
				return true;
			if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S)
				return false;
			return instruction.operand switch
			{
				LocalBuilder local => local.LocalIndex == 0,
				int localIndex => localIndex == 0,
				byte localIndex => localIndex == 0,
				_ => false
			};
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
				flamethrower.ClearManualTargetVisuals();
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

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.FuelPercentOfMax), MethodType.Getter)]
	public static class CompRefuelable_FuelPercentOfMax_Patch
	{
		static void Postfix(CompRefuelable __instance, ref float __result)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower))
			{
				var capacity = flamethrower.FuelCapacity;
				__result = capacity <= 0f ? 0f : __instance.Fuel / capacity;
			}
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.Initialize))]
	public static class CompRefuelable_Initialize_Patch
	{
		static void Postfix(CompRefuelable __instance)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower))
				FuelScaling.InitializeFuel(__instance, flamethrower);
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.FuelPercentOfTarget), MethodType.Getter)]
	public static class CompRefuelable_FuelPercentOfTarget_Patch
	{
		static void Postfix(CompRefuelable __instance, ref float __result)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower))
			{
				var targetFuelLevel = FuelScaling.TargetFuelLevelFor(__instance, flamethrower);
				__result = targetFuelLevel <= 0f ? 0f : __instance.Fuel / targetFuelLevel;
			}
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.TargetFuelLevel), MethodType.Getter)]
	public static class CompRefuelable_TargetFuelLevel_Getter_Patch
	{
		static void Postfix(CompRefuelable __instance, ref float __result)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower))
				__result = FuelScaling.TargetFuelLevelFor(__instance, flamethrower);
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.TargetFuelLevel), MethodType.Setter)]
	public static class CompRefuelable_TargetFuelLevel_Setter_Patch
	{
		static bool Prefix(CompRefuelable __instance, float value)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower) == false)
				return true;

			FuelScaling.SetTargetFuelLevel(__instance, flamethrower, value);
			return false;
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.IsFull), MethodType.Getter)]
	public static class CompRefuelable_IsFull_Patch
	{
		static void Postfix(CompRefuelable __instance, ref bool __result)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower))
				__result = FuelScaling.TargetFuelLevelFor(__instance, flamethrower) - __instance.Fuel < (__instance.Props?.FuelMultiplierCurrentDifficulty ?? 1f);
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.Refuel), new[] { typeof(float) })]
	public static class CompRefuelable_Refuel_Float_Patch
	{
		static bool Prefix(CompRefuelable __instance, float amount)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower) == false)
				return true;

			FuelScaling.Refuel(__instance, flamethrower, amount);
			return false;
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.GetFuelCountToFullyRefuel))]
	public static class CompRefuelable_GetFuelCountToFullyRefuel_Patch
	{
		static void Postfix(CompRefuelable __instance, ref int __result)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower))
				__result = FuelScaling.FuelCountToFullyRefuel(__instance, flamethrower);
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.CompInspectStringExtra))]
	public static class CompRefuelable_CompInspectStringExtra_Patch
	{
		static bool Prefix(CompRefuelable __instance, ref string __result)
		{
			if (FuelScaling.TryGetFlamethrower(__instance, out var flamethrower) == false)
				return true;

			var text = $"{__instance.Props.FuelLabel}: {__instance.Fuel.ToStringDecimalIfSmall()} / {flamethrower.FuelCapacity.ToStringDecimalIfSmall()}";
			if (__instance.HasFuel == false && __instance.Props.outOfFuelMessage.NullOrEmpty() == false)
				text += $"\n{__instance.Props.outOfFuelMessage} ({__instance.GetFuelCountToFullyRefuel()}x {__instance.Props.fuelFilter.AnyAllowedDef.label})";
			if (__instance.Props.targetFuelLevelConfigurable)
				text += "\n" + "ConfiguredTargetFuelLevel".Translate(__instance.TargetFuelLevel.ToStringDecimalIfSmall());

			__result = text;
			return false;
		}
	}

	[HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.CompGetGizmosExtra))]
	public static class CompRefuelable_CompGetGizmosExtra_Patch
	{
		static void Postfix(CompRefuelable __instance, ref IEnumerable<Gizmo> __result)
		{
			if (!FuelScaling.TryGetFlamethrower(__instance, out _))
				return;

			__result = ReplaceDynamicFuelDevGizmos(__instance, __result);
		}

		static IEnumerable<Gizmo> ReplaceDynamicFuelDevGizmos(CompRefuelable refuelable, IEnumerable<Gizmo> gizmos)
		{
			foreach (var gizmo in gizmos)
			{
				if (gizmo is Command_Action command && command.defaultLabel == "DEV: Fuel -20%")
				{
					yield return MakeFuelMinusTwentyPercentAction(refuelable);
					continue;
				}

				if (gizmo is Command_Action command2 && command2.defaultLabel == "DEV: Set fuel to max")
				{
					yield return MakeSetFuelToMaxAction(refuelable);
					continue;
				}

				yield return gizmo;
			}
		}

		static Command_Action MakeFuelMinusTwentyPercentAction(CompRefuelable refuelable)
		{
			return new Command_Action
			{
				defaultLabel = "DEV: Fuel -20%",
				action = delegate
				{
					if (FuelScaling.TryGetFlamethrower(refuelable, out var flamethrower))
						refuelable.ConsumeFuel(flamethrower.FuelCapacity * 0.2f);
				}
			};
		}

		static Command_Action MakeSetFuelToMaxAction(CompRefuelable refuelable)
		{
			return new Command_Action
			{
				defaultLabel = "DEV: Set fuel to max",
				action = delegate
				{
					if (FuelScaling.TryGetFlamethrower(refuelable, out var flamethrower))
					{
						FuelScaling.SetFuelLevel(refuelable, flamethrower, flamethrower.FuelCapacity);
						refuelable.parent.BroadcastCompSignal(CompRefuelable.RefueledSignal);
					}
				}
			};
		}
	}

	[HarmonyPatch(typeof(CompProperties_Refuelable), nameof(CompProperties_Refuelable.SpecialDisplayStats))]
	public static class CompProperties_Refuelable_SpecialDisplayStats_Patch
	{
		static bool Prefix(StatRequest req, ref IEnumerable<StatDrawEntry> __result)
		{
			if (req.Def != Defs.ZeFlammenwerfer)
				return true;

			__result = Enumerable.Empty<StatDrawEntry>();
			return false;
		}
	}

	[HarmonyPatch(typeof(Gizmo_SetFuelLevel), "get_Target")]
	public static class Gizmo_SetFuelLevel_Target_Getter_Patch
	{
		static readonly FieldInfo refuelableField = AccessTools.Field(typeof(Gizmo_SetFuelLevel), "refuelable");

		static bool Prefix(Gizmo_SetFuelLevel __instance, ref float __result)
		{
			if (!FuelScaling.TryGetFlamethrower(RefuelableFor(__instance), out var flamethrower))
				return true;

			var capacity = flamethrower.FuelCapacity;
			__result = capacity <= 0f ? 0f : Mathf.Clamp01(flamethrower.refuelable.TargetFuelLevel / capacity);
			return false;
		}

		internal static CompRefuelable RefuelableFor(Gizmo_SetFuelLevel gizmo)
		{
			return (CompRefuelable)refuelableField.GetValue(gizmo);
		}
	}

	[HarmonyPatch(typeof(Gizmo_SetFuelLevel), "set_Target")]
	public static class Gizmo_SetFuelLevel_Target_Setter_Patch
	{
		static bool Prefix(Gizmo_SetFuelLevel __instance, float value)
		{
			if (!FuelScaling.TryGetFlamethrower(Gizmo_SetFuelLevel_Target_Getter_Patch.RefuelableFor(__instance), out var flamethrower))
				return true;

			flamethrower.refuelable.TargetFuelLevel = Mathf.Clamp01(value) * flamethrower.FuelCapacity;
			return false;
		}
	}

	[HarmonyPatch(typeof(Gizmo_SetFuelLevel), "get_BarLabel")]
	public static class Gizmo_SetFuelLevel_BarLabel_Patch
	{
		static bool Prefix(Gizmo_SetFuelLevel __instance, ref string __result)
		{
			if (!FuelScaling.TryGetFlamethrower(Gizmo_SetFuelLevel_Target_Getter_Patch.RefuelableFor(__instance), out var flamethrower))
				return true;

			__result = flamethrower.refuelable.Fuel.ToStringDecimalIfSmall() + " / " + flamethrower.FuelCapacity.ToStringDecimalIfSmall();
			return false;
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
			if (FlameDamageTracker.IsTracked(pawn) == false)
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
			if (FlameDamageTracker.IsTracked(pawn) == false)
				return;
			var newDinfo = new DamageInfo(dinfo);
			newDinfo.SetAllowDamagePropagation(false);
			newDinfo.SetIgnoreArmor(true);
			dinfo = newDinfo;
		}
	}
}
