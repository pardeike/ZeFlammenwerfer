using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	static class RefuelProgressBar
	{
		const int fallbackMinimumDurationTicks = 60;
		const int fallbackMaximumDurationTicks = 240;

		public static Toil DynamicWait(CompRefuelable refuelable, Job job, TargetIndex fuelInd, TargetIndex progressTarget)
		{
			var duration = fallbackMaximumDurationTicks;
			var toil = Toils_General.Wait(duration);
			toil.initAction = delegate
			{
				toil.actor.pather.StopDead();
				duration = DurationFor(refuelable, job, fuelInd);
				toil.defaultDuration = duration;
				toil.actor.jobs.curDriver.ticksLeftThisToil = duration;
			};
			return toil.WithProgressBar(progressTarget,
				() => 1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / Mathf.Max(1, duration),
				interpolateBetweenActorAndTarget: false,
				offsetZ: -0.5f,
				alwaysShow: true);
		}

		static int DurationFor(CompRefuelable refuelable, Job job, TargetIndex fuelInd)
		{
			if (FuelScaling.TryGetFlamethrower(refuelable, out var flamethrower) == false)
				return fallbackMaximumDurationTicks;

			var props = flamethrower.FlameProps;
			var min = Mathf.Max(1, props?.minimumRefuelDurationTicks ?? fallbackMinimumDurationTicks);
			var max = Mathf.Max(min, props?.maximumRefuelDurationTicks ?? fallbackMaximumDurationTicks);
			var targetFuel = Mathf.Max(1f, FuelScaling.TargetFuelLevelFor(refuelable, flamethrower));
			var missingFuel = Mathf.Max(0f, targetFuel - refuelable.Fuel);
			if (missingFuel <= 0f)
				return min;

			var fuelCount = FuelCount(job, fuelInd);
			var multiplier = Mathf.Max(0f, refuelable.Props?.FuelMultiplierCurrentDifficulty ?? 1f);
			var fuelToAdd = fuelCount > 0 && multiplier > 0f ? Mathf.Min(missingFuel, fuelCount * multiplier) : missingFuel;
			var ratio = Mathf.Clamp01(fuelToAdd / targetFuel);
			return Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(min, max, ratio)), min, max);
		}

		static int FuelCount(Job job, TargetIndex fuelInd)
		{
			if (job == null)
				return 0;

			if (job.placedThings.NullOrEmpty() == false)
			{
				var count = 0;
				foreach (var placedThing in job.placedThings)
					count += Mathf.Max(0, placedThing.Count);
				return count;
			}

			return Mathf.Max(0, job.GetTarget(fuelInd).Thing?.stackCount ?? job.count);
		}
	}

	public class JobDriver_RefuelGroundFlammenwerfer : JobDriver
	{
		const TargetIndex RefuelableInd = TargetIndex.A;
		const TargetIndex FuelInd = TargetIndex.B;

		Thing Refuelable => job.GetTarget(RefuelableInd).Thing;
		CompRefuelable RefuelableComp => Refuelable?.TryGetComp<CompRefuelable>();
		Thing Fuel => job.GetTarget(FuelInd).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Refuelable, job, 1, -1, null, errorOnFailed) && pawn.Reserve(Fuel, job, 1, -1, null, errorOnFailed);
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(RefuelableInd);
			AddEndCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null ? JobCondition.Incompletable : (comp.IsFull ? JobCondition.Succeeded : JobCondition.Ongoing);
			});
			AddFailCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null || (!job.playerForced && !comp.ShouldAutoRefuelNowIgnoringFuelPct);
			});
			AddFailCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null || (!comp.allowAutoRefuel && !job.playerForced);
			});

			yield return Toils_General.DoAtomic(delegate
			{
				job.count = RefuelableComp?.GetFuelCountToFullyRefuel() ?? 0;
			});
			Toil reserveFuel = Toils_Reserve.Reserve(FuelInd);
			yield return reserveFuel;
			yield return Toils_Goto.GotoThing(FuelInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(FuelInd).FailOnSomeonePhysicallyInteracting(FuelInd);
			yield return Toils_Haul.StartCarryThing(FuelInd, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(FuelInd);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, FuelInd, TargetIndex.None, takeFromValidStorage: true);
			yield return Toils_Goto.GotoThing(RefuelableInd, PathEndMode.Touch);
			Toil refuelDelay = RefuelProgressBar.DynamicWait(RefuelableComp, job, FuelInd, RefuelableInd).FailOnDestroyedNullOrForbidden(FuelInd).FailOnDestroyedNullOrForbidden(RefuelableInd)
				.FailOnCannotTouch(RefuelableInd, PathEndMode.Touch);
			yield return refuelDelay;
			yield return Toils_Refuel.FinalizeRefueling(RefuelableInd, FuelInd);
		}
	}

	public class JobDriver_RefuelGroundAtomicFlammenwerfer : JobDriver
	{
		const TargetIndex RefuelableInd = TargetIndex.A;
		const TargetIndex FuelInd = TargetIndex.B;
		const TargetIndex FuelPlaceCellInd = TargetIndex.C;

		Thing Refuelable => job.GetTarget(RefuelableInd).Thing;
		CompRefuelable RefuelableComp => Refuelable?.TryGetComp<CompRefuelable>();

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			pawn.ReserveAsManyAsPossible(job.GetTargetQueue(FuelInd), job);
			return pawn.Reserve(Refuelable, job, 1, -1, null, errorOnFailed);
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(RefuelableInd);
			AddEndCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null ? JobCondition.Incompletable : (comp.IsFull ? JobCondition.Succeeded : JobCondition.Ongoing);
			});
			AddFailCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null || (!job.playerForced && !comp.ShouldAutoRefuelNowIgnoringFuelPct);
			});
			AddFailCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null || (!comp.allowAutoRefuel && !job.playerForced);
			});

			yield return Toils_General.DoAtomic(delegate
			{
				job.count = RefuelableComp?.GetFuelCountToFullyRefuel() ?? 0;
			});
			Toil getNextIngredient = Toils_General.Label();
			yield return getNextIngredient;
			yield return Toils_JobTransforms.ExtractNextTargetFromQueue(FuelInd);
			yield return Toils_Goto.GotoThing(FuelInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(FuelInd).FailOnSomeonePhysicallyInteracting(FuelInd);
			yield return Toils_Haul.StartCarryThing(FuelInd, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(FuelInd);
			yield return Toils_Goto.GotoThing(RefuelableInd, PathEndMode.Touch);
			Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(RefuelableInd, FuelInd, FuelPlaceCellInd);
			yield return findPlaceTarget;
			yield return Toils_Haul.PlaceHauledThingInCell(FuelPlaceCellInd, findPlaceTarget, storageMode: false);
			yield return Toils_Jump.JumpIf(getNextIngredient, () => !job.GetTargetQueue(FuelInd).NullOrEmpty());
			Toil refuelDelay = RefuelProgressBar.DynamicWait(RefuelableComp, job, FuelInd, RefuelableInd)
				.FailOnDestroyedNullOrForbidden(RefuelableInd)
				.FailOnCannotTouch(RefuelableInd, PathEndMode.Touch);
			yield return refuelDelay;
			yield return Toils_Refuel.FinalizeRefueling(RefuelableInd, TargetIndex.None);
		}
	}

	public class JobDriver_RefuelEquippedFlammenwerfer : JobDriver
	{
		const TargetIndex BearerInd = TargetIndex.A;
		const TargetIndex FuelInd = TargetIndex.B;

		Pawn Bearer => job.GetTarget(BearerInd).Thing as Pawn;
		ZeFlammenwerfer Flamethrower => FlamethrowerRefuelUtility.EquippedFlamethrower(Bearer);
		CompRefuelable RefuelableComp => Flamethrower?.refuelable ?? Flamethrower?.TryGetComp<CompRefuelable>();

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (pawn != Bearer && !pawn.Reserve(Bearer, job, 1, -1, null, errorOnFailed))
				return false;
			return pawn.Reserve(job.GetTarget(FuelInd), job, 1, -1, null, errorOnFailed);
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			var initialFlamethrower = Flamethrower;

			// The fuel target is carried after pickup, so despawning alone is not a failure.
			this.FailOnDestroyedNullOrForbidden(FuelInd);
			AddEndCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null ? JobCondition.Incompletable : (comp.IsFull ? JobCondition.Succeeded : JobCondition.Ongoing);
			});
			AddFailCondition(() => Bearer == null || !Bearer.Spawned || Bearer.Dead);
			AddFailCondition(() => initialFlamethrower == null || Flamethrower != initialFlamethrower);
			AddFailCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null || (!job.playerForced && !comp.ShouldAutoRefuelNowIgnoringFuelPct);
			});
			AddFailCondition(() =>
			{
				var comp = RefuelableComp;
				return comp == null || (!comp.allowAutoRefuel && !job.playerForced);
			});

			yield return Toils_General.DoAtomic(delegate
			{
				job.count = RefuelableComp?.GetFuelCountToFullyRefuel() ?? 0;
			});
			Toil reserveFuel = Toils_Reserve.Reserve(FuelInd);
			yield return reserveFuel;
			yield return Toils_Goto.GotoThing(FuelInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(FuelInd).FailOnSomeonePhysicallyInteracting(FuelInd);
			yield return Toils_Haul.StartCarryThing(FuelInd, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(FuelInd);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, FuelInd, TargetIndex.None, takeFromValidStorage: true);
			if (pawn != Bearer)
				yield return Toils_Goto.GotoThing(BearerInd, PathEndMode.Touch);
			Toil refuelDelay = RefuelProgressBar.DynamicWait(RefuelableComp, job, FuelInd, BearerInd).FailOnDestroyedNullOrForbidden(FuelInd)
				.FailOnCannotTouch(BearerInd, PathEndMode.Touch)
				.FailOn(() => Flamethrower == null || Flamethrower != initialFlamethrower || RefuelableComp == null);
			yield return refuelDelay;
			yield return Toils_General.Do(delegate
			{
				FlamethrowerRefuelUtility.FinalizeEquippedRefueling(job);
			});
		}
	}
}
