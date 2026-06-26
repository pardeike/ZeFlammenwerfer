using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	public class WorkGiver_RefuelFlammenwerfer : WorkGiver_Refuel
	{
		static readonly ThingRequest weaponRequest = ThingRequest.ForDef(ThingDef.Named("ZeFlammenwerfer"));

		public override ThingRequest PotentialWorkThingRequest => weaponRequest;
		public override JobDef JobStandard => Defs.ZeFlammenwerfer_RefuelGround;
		public override JobDef JobAtomic => Defs.ZeFlammenwerfer_RefuelGroundAtomic;

		public override bool CanRefuelThing(Thing t)
		{
			return t is ZeFlammenwerfer;
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			return CanRefuelThing(t) && FlamethrowerRefuelUtility.CanRefuel(pawn, t, forced);
		}
	}

	public static class FlamethrowerRefuelUtility
	{
		static readonly MethodInfo findBestFuelMethod = AccessTools.DeclaredMethod(typeof(RefuelWorkGiverUtility), "FindBestFuel");
		static readonly MethodInfo findAllFuelMethod = AccessTools.DeclaredMethod(typeof(RefuelWorkGiverUtility), "FindAllFuel");

		public static bool CanRefuel(Pawn pawn, Thing t, bool forced = false)
		{
			var compRefuelable = t.TryGetComp<CompRefuelable>();
			if (compRefuelable == null || compRefuelable.parent.Fogged() || compRefuelable.IsFull || (!forced && !compRefuelable.allowAutoRefuel))
				return false;
			if (compRefuelable.FuelPercentOfMax > 0f && !compRefuelable.Props.allowRefuelIfNotEmpty)
				return false;
			if (!forced && !compRefuelable.ShouldAutoRefuelNow)
				return false;
			if (!pawn.CanReserve(t, 1, -1, null, forced))
				return false;
			if (!IsValidFlamethrowerRefuelTarget(pawn, t, forced))
				return false;
			if (t.TryGetComp(out CompInteractable compInteractable) && compInteractable.Props.cooldownPreventsRefuel && compInteractable.OnCooldown)
			{
				JobFailReason.Is(compInteractable.Props.onCooldownString.CapitalizeFirst());
				return false;
			}
			if (FindBestFuel(pawn, t) == null)
			{
				JobFailReason.Is("NoFuelToRefuel".Translate(compRefuelable.Props.fuelFilter.Summary));
				return false;
			}
			if (compRefuelable.Props.atomicFueling && FindAllFuel(pawn, t) == null)
			{
				JobFailReason.Is("NoFuelToRefuel".Translate(compRefuelable.Props.fuelFilter.Summary));
				return false;
			}
			return true;
		}

		static bool IsValidFlamethrowerRefuelTarget(Pawn pawn, Thing t, bool forced)
		{
			if (t.Faction == pawn.Faction)
				return true;
			if (t.Faction != null || pawn.Faction != Faction.OfPlayer)
				return false;
			if (forced)
				return true;
			return t.MapHeld?.areaManager?.Home != null && t.PositionHeld.IsValid && t.MapHeld.areaManager.Home[t.PositionHeld];
		}

		static Thing FindBestFuel(Pawn pawn, Thing refuelable)
		{
			return (Thing)findBestFuelMethod.Invoke(null, new object[] { pawn, refuelable });
		}

		static List<Thing> FindAllFuel(Pawn pawn, Thing refuelable)
		{
			return (List<Thing>)findAllFuelMethod.Invoke(null, new object[] { pawn, refuelable });
		}

		public static ZeFlammenwerfer EquippedFlamethrower(Pawn bearer)
		{
			return bearer?.equipment?.Primary as ZeFlammenwerfer;
		}

		public static FloatMenuOption MakeGroundRefuelOption(Pawn actor, ZeFlammenwerfer flamethrower)
		{
			const string label = "Prioritize refueling Ze Flammenwerfer";
			if (flamethrower == null)
				return new FloatMenuOption($"{label}: Invalid target", null);
			if (!CanRefuel(actor, flamethrower, true))
			{
				var failReason = JobFailReason.HaveReason ? JobFailReason.Reason : "Cannot refuel now";
				return new FloatMenuOption($"{label}: {failReason}", null);
			}

			return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
			{
				var job = MakeGroundRefuelJob(actor, flamethrower, true);
				if (job != null)
					actor.jobs.TryTakeOrderedJob(job);
			}, MenuOptionPriority.Default), actor, flamethrower);
		}

		public static Job MakeGroundRefuelJob(Pawn actor, ZeFlammenwerfer flamethrower, bool forced)
		{
			if (actor == null || flamethrower == null)
				return null;
			var job = RefuelWorkGiverUtility.RefuelJob(actor, flamethrower, forced, Defs.ZeFlammenwerfer_RefuelGround, Defs.ZeFlammenwerfer_RefuelGroundAtomic);
			if (job != null)
				job.playerForced = forced;
			return job;
		}

		public static FloatMenuOption MakeEquippedRefuelOption(Pawn actor, Pawn bearer)
		{
			string label = actor == bearer ? "Refuel Ze Flammenwerfer" : $"Prioritize refueling {bearer.LabelShort}'s Ze Flammenwerfer";
			if (!TryCanRefuelEquipped(actor, bearer, true, out string failReason))
				return new FloatMenuOption($"{label}: {failReason}", null);
			return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
			{
				var job = MakeRefuelEquippedJob(actor, bearer, true);
				if (job != null)
					actor.jobs.TryTakeOrderedJob(job);
			}, MenuOptionPriority.Default), actor, bearer);
		}

		public static FloatMenuOption MakeSelfRefuelFromFuelOption(Pawn actor, Thing fuel)
		{
			const string label = "Refuel Ze Flammenwerfer";
			if (!TryCanRefuelEquipped(actor, actor, true, out string failReason))
				return new FloatMenuOption($"{label}: {failReason}", null);
			if (!IsValidFuelForEquipped(actor, fuel, out failReason))
				return new FloatMenuOption($"{label}: {failReason}", null);
			return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
			{
				var job = MakeRefuelEquippedJob(actor, actor, true, fuel);
				if (job != null)
					actor.jobs.TryTakeOrderedJob(job);
			}, MenuOptionPriority.Default), actor, fuel);
		}

		public static Job MakeRefuelEquippedJob(Pawn actor, Pawn bearer, bool forced)
		{
			var flamethrower = EquippedFlamethrower(bearer);
			var fuel = flamethrower == null ? null : FindBestFuel(actor, flamethrower);
			return MakeRefuelEquippedJob(actor, bearer, forced, fuel);
		}

		static Job MakeRefuelEquippedJob(Pawn actor, Pawn bearer, bool forced, Thing fuel)
		{
			var flamethrower = EquippedFlamethrower(bearer);
			if (flamethrower == null || fuel == null)
				return null;
			var job = JobMaker.MakeJob(Defs.ZeFlammenwerfer_RefuelEquipped, bearer, fuel);
			job.playerForced = forced;
			return job;
		}

		static bool IsValidFuelForEquipped(Pawn actor, Thing fuel, out string failReason)
		{
			failReason = null;
			var flamethrower = EquippedFlamethrower(actor);
			var compRefuelable = flamethrower?.refuelable ?? flamethrower?.TryGetComp<CompRefuelable>();
			if (compRefuelable == null)
			{
				failReason = "No flamethrower equipped";
				return false;
			}
			if (fuel == null || compRefuelable.Props.fuelFilter.Allows(fuel) == false)
			{
				failReason = $"Not valid fuel: {compRefuelable.Props.fuelFilter.Summary}";
				return false;
			}
			if (!actor.CanReserve(fuel, 1, -1, null, true))
			{
				failReason = "Cannot reserve fuel";
				return false;
			}
			if (!actor.CanReach(fuel, PathEndMode.ClosestTouch, Danger.Some))
			{
				failReason = "No path to fuel";
				return false;
			}
			return true;
		}

		public static bool TryCanRefuelEquipped(Pawn actor, Pawn bearer, bool forced, out string failReason)
		{
			failReason = null;
			if (actor == null || bearer == null)
			{
				failReason = "Invalid target";
				return false;
			}
			if (bearer.Faction != actor.Faction)
			{
				failReason = "Wrong faction";
				return false;
			}
			var flamethrower = EquippedFlamethrower(bearer);
			var compRefuelable = flamethrower?.refuelable ?? flamethrower?.TryGetComp<CompRefuelable>();
			if (compRefuelable == null)
			{
				failReason = "No flamethrower equipped";
				return false;
			}
			if (compRefuelable.IsFull)
			{
				failReason = "Already fully fueled";
				return false;
			}
			if (!forced && !compRefuelable.allowAutoRefuel)
			{
				failReason = "Auto-refuel disabled";
				return false;
			}
			if (!forced && !compRefuelable.ShouldAutoRefuelNow)
			{
				failReason = "Not ready for refuel";
				return false;
			}
			if (actor != bearer && !actor.CanReserve(bearer, 1, -1, null, forced))
			{
				failReason = "Cannot reserve target";
				return false;
			}
			if (actor != bearer && !actor.CanReach(bearer, PathEndMode.Touch, Danger.Some))
			{
				failReason = "No path to target";
				return false;
			}
			if (FindBestFuel(actor, flamethrower) == null)
			{
				failReason = $"No fuel to refuel: {compRefuelable.Props.fuelFilter.Summary}";
				return false;
			}
			if (compRefuelable.Props.atomicFueling && FindAllFuel(actor, flamethrower) == null)
			{
				failReason = $"No fuel to refuel: {compRefuelable.Props.fuelFilter.Summary}";
				return false;
			}
			return true;
		}

		public static void FinalizeEquippedRefueling(Job job)
		{
			var bearer = job.GetTarget(TargetIndex.A).Thing as Pawn;
			var flamethrower = EquippedFlamethrower(bearer);
			var compRefuelable = flamethrower?.refuelable ?? flamethrower?.TryGetComp<CompRefuelable>();
			if (compRefuelable == null)
				return;
			if (job.placedThings.NullOrEmpty())
				compRefuelable.Refuel(new List<Thing> { job.GetTarget(TargetIndex.B).Thing });
			else
				compRefuelable.Refuel(job.placedThings.Select(thingCount => thingCount.thing).ToList());
		}
	}
}
