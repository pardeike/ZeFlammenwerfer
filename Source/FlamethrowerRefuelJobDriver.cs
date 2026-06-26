using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
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
			yield return Toils_General.Wait(240).FailOnDestroyedNullOrForbidden(FuelInd)
				.FailOnCannotTouch(BearerInd, PathEndMode.Touch)
				.FailOn(() => Flamethrower == null || Flamethrower != initialFlamethrower || RefuelableComp == null)
				.WithProgressBarToilDelay(BearerInd);
			yield return Toils_General.Do(delegate
			{
				FlamethrowerRefuelUtility.FinalizeEquippedRefueling(job);
			});
		}
	}
}
