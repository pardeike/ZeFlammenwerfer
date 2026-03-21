using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	public partial class ZeFlammenwerfer : ThingWithComps
	{
		static readonly MethodInfo setNewPathRequestMethod = AccessTools.Method(typeof(Pawn_PathFollower), "SetNewPathRequest");
		static readonly FieldInfo curPathJobIsStaleField = AccessTools.Field(typeof(Pawn_PathFollower), "curPathJobIsStale");

		LocalTargetInfo manualTarget = LocalTargetInfo.Invalid;
		int manualWarmupUntilTick = int.MinValue;
		int nextManualShotTick = int.MinValue;
		bool manualTargetRepathPending;

		public Pawn pawn;

		public CompRefuelable refuelable;
		public ZeFlameComp flameComp;

		public float FuelPerShot => refuelable?.Props?.fuelConsumptionRate ?? 0f;
		public bool CanFireNow => refuelable == null || FuelPerShot <= 0f || refuelable.Fuel >= FuelPerShot;
		public string OutOfFuelReason => refuelable?.Props?.outOfFuelMessage ?? "Needs chemfuel";
		public bool HasManualTarget => manualTarget.IsValid;
		public LocalTargetInfo ManualTarget => manualTarget;
		public bool ManualTargetRepathPending => manualTargetRepathPending;

		public void Setup()
		{
			refuelable = GetComp<CompRefuelable>();
			flameComp = GetComp<ZeFlameComp>();
		}

		public override void Tick()
		{
			base.Tick();
			if (CanFireNow == false)
			{
				ClearManualTarget();
				flameComp.SetActive(false);
				FlameDangerTracker.Clear(pawn);
				return;
			}
			if (pawn == null)
			{
				ClearManualTarget();
				DebugTrace.LogThrottled($"missing-pawn-{ThingID}", 300, $"{ThingID} lost its owning pawn while ticking");
				flameComp.SetActive(false);
				return;
			}
			ForcePendingManualRepath();
			if (HasManualTarget)
				TickManualTarget();
			if (flameComp.isActive == false)
			{
				FlameDangerTracker.Clear(pawn);
				return;
			}
			var currentTarget = CurrentAimTarget;
			if (flameComp.isActive && currentTarget.IsValid == false && flameComp.ShouldStayActiveBetweenShots() == false)
			{
				DebugTrace.LogThrottled($"not-aiming-{ThingID}", 60, $"{pawn.LabelShortCap} deactivating flamethrower because aiming data is unavailable. job={pawn.CurJob?.def?.defName ?? "null"} stance={pawn.stances?.curStance?.GetType().Name ?? "null"}");
				flameComp.SetActive(false);
				FlameDangerTracker.Clear(pawn);
				return;
			}
			if (currentTarget.IsValid)
			{
				var from = pawn.DrawPos.WithHeight(0);
				var to = currentTarget.HasThing ? currentTarget.Thing.DrawPos : currentTarget.Cell.ToVector3Shifted();
				var vector = to - from;
				var startOffset = vector.magnitude > 1f ? vector.normalized : Vector3.zero;
				flameComp.Update(from + 1.75f * startOffset, to);
				FlameDangerTracker.Update(pawn, currentTarget);
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_TargetInfo.Look(ref manualTarget, "manualTarget");
			Scribe_Values.Look(ref manualWarmupUntilTick, "manualWarmupUntilTick", int.MinValue);
			Scribe_Values.Look(ref nextManualShotTick, "nextManualShotTick", int.MinValue);
			Scribe_Values.Look(ref manualTargetRepathPending, "manualTargetRepathPending", false);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				Setup();
				manualTargetRepathPending |= manualTarget.IsValid;
			}
		}

		/*public override string DescriptionDetailed
		{
			get
			{
				var builder = new StringBuilder();
				_ = builder.Append("FlamethrowerDesc".Translate());
				if (refuelable.HasFuel)
					_ = builder.Append("FlamethrowerDescFuel".Translate(Mathf.RoundToInt(refuelable.FuelPercentOfMax * 100)));
				else
					_ = builder.Append($"{"FlamethrowerDescNoFuel".Translate()}");
				return builder.ToString();
			}
		}*/

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			Setup();
		}

		public override void Notify_Equipped(Pawn pawn)
		{
			this.pawn = pawn;
			base.Notify_Equipped(pawn);
		}

		public override void Notify_Unequipped(Pawn pawn)
		{
			ClearManualTarget();
			FlameDangerTracker.Clear(pawn);
			this.pawn = null;
			base.Notify_Unequipped(pawn);
		}

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			ClearManualTarget();
			FlameDangerTracker.Clear(pawn);
			pawn = null;
			base.DeSpawn(mode);
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			ClearManualTarget();
			FlameDangerTracker.Clear(pawn);
			pawn = null;
			base.Destroy(mode);
		}

		public LocalTargetInfo CurrentAimTarget
		{
			get
			{
				if (HasManualTarget)
					return manualTarget;
				if (pawn?.stances?.curStance is Stance_Busy stanceBusy && stanceBusy.focusTarg.IsValid)
					return stanceBusy.focusTarg;
				return LocalTargetInfo.Invalid;
			}
		}

		public void OrderManualTarget(LocalTargetInfo target)
		{
			if (SameTarget(manualTarget, target))
			{
				ClearManualTarget();
				flameComp?.SetActive(false);
				FlameDangerTracker.Clear(pawn);
				return;
			}

			manualTarget = target;
			var warmupTicks = ManualWarmupTicks;
			manualWarmupUntilTick = GenTicks.TicksGame + warmupTicks;
			nextManualShotTick = manualWarmupUntilTick;
			manualTargetRepathPending = true;
		}

		public void ClearManualTarget()
		{
			manualTarget = LocalTargetInfo.Invalid;
			manualWarmupUntilTick = int.MinValue;
			nextManualShotTick = int.MinValue;
			manualTargetRepathPending = false;
		}

		public bool TryForceCurrentMoveRepath()
		{
			if (pawn?.pather == null || pawn.pather.Moving == false || pawn.pather.Destination.IsValid == false)
				return false;

			curPathJobIsStaleField?.SetValue(pawn.pather, true);
			setNewPathRequestMethod?.Invoke(pawn.pather, Array.Empty<object>());
			return true;
		}

		public bool ForceImmediateManualRepath()
		{
			manualTargetRepathPending = true;
			ForcePendingManualRepath();
			return manualTargetRepathPending == false;
		}

		void TickManualTarget()
		{
			if (pawn.Spawned == false || pawn.Map == null || pawn.Drafted == false || pawn.Dead || pawn.Downed)
			{
				ClearManualTarget();
				flameComp.SetActive(false);
				FlameDangerTracker.Clear(pawn);
				return;
			}

			if (manualTarget.HasThing && (manualTarget.Thing.DestroyedOrNull() || manualTarget.Thing.Spawned == false || manualTarget.Thing.Map != pawn.Map))
			{
				ClearManualTarget();
				flameComp.SetActive(false);
				FlameDangerTracker.Clear(pawn);
				return;
			}

			if (nextManualShotTick == int.MinValue)
				nextManualShotTick = GenTicks.TicksGame + ManualWarmupTicks;

			var verb = PrimaryVerb;
			if (verb == null)
				return;

			if (GenTicks.TicksGame < nextManualShotTick)
				return;

			if (verb.CanHitTargetFrom(pawn.Position, manualTarget) == false)
			{
				nextManualShotTick = GenTicks.TicksGame + 1;
				return;
			}

			if (TryLaunchManualShot(verb, manualTarget))
				nextManualShotTick = GenTicks.TicksGame + verb.TicksBetweenBurstShots;
			else
				nextManualShotTick = GenTicks.TicksGame + 1;
		}

		bool TryLaunchManualShot(Verb verb, LocalTargetInfo target)
		{
			if (verb.TryFindShootLineFromTo(pawn.Position, target, out var resultingLine, ignoreRange: false) == false)
				return false;

			var projectileDef = verb.verbProps?.defaultProjectile;
			if (projectileDef == null)
				return false;

			var projectile = (Projectile)GenSpawn.Spawn(projectileDef, resultingLine.Source, pawn.Map);
			var drawPos = pawn.DrawPos;
			var hitFlags = ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetPawns;
			if (target.HasThing == false || target.Thing.def.Fillage == FillCategory.Full)
				hitFlags |= ProjectileHitFlags.NonTargetWorld;

			if (target.HasThing)
				projectile.Launch(pawn, drawPos, target, target, hitFlags, preventFriendlyFire: false, equipment: this);
			else
				projectile.Launch(pawn, drawPos, resultingLine.Dest, target, hitFlags, preventFriendlyFire: false, equipment: this);

			return true;
		}

		Verb PrimaryVerb => GetComp<CompEquippable>()?.PrimaryVerb;

		int ManualWarmupTicks
		{
			get
			{
				var verb = PrimaryVerb;
				if (verb == null || pawn == null)
					return 0;
				return (verb.WarmupTime * pawn.GetStatValue(StatDefOf.AimingDelayFactor)).SecondsToTicks();
			}
		}

		static bool SameTarget(LocalTargetInfo left, LocalTargetInfo right)
		{
			if (left.IsValid == false || right.IsValid == false)
				return false;
			if (left.HasThing || right.HasThing)
				return left.HasThing && right.HasThing && left.Thing == right.Thing;
			return left.Cell == right.Cell;
		}

		void ForcePendingManualRepath()
		{
			if (manualTargetRepathPending == false)
				return;
			if (TryForceCurrentMoveRepath() == false)
				return;
			manualTargetRepathPending = false;
		}
	}
}
