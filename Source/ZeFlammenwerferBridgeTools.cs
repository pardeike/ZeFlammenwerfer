using RimBridgeServer.Annotations;
using RimWorld;
using System;
using System.Linq;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	public sealed class ZeFlammenwerferBridgeTools
	{
		static Map CurrentMap => Find.CurrentMap;

		static Pawn FindPawn(string pawnId)
		{
			var map = CurrentMap;
			if (map == null)
				return null;

			if (string.IsNullOrWhiteSpace(pawnId))
				return Find.Selector.SingleSelectedThing as Pawn;

			return map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn =>
				string.Equals(pawn.ThingID, pawnId, StringComparison.Ordinal)
				|| string.Equals($"Thing_{pawn.ThingID}", pawnId, StringComparison.Ordinal)
				|| string.Equals(pawn.GetUniqueLoadID(), pawnId, StringComparison.Ordinal)
				|| string.Equals(pawn.Name?.ToStringShort, pawnId, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(pawn.LabelShort, pawnId, StringComparison.OrdinalIgnoreCase));
		}

		static ZeFlammenwerfer FindFlamethrower(Pawn pawn)
		{
			return pawn?.equipment?.Primary as ZeFlammenwerfer;
		}

		static object DescribeCell(IntVec3 cell)
		{
			if (cell.IsValid == false)
				return null;

			return new
			{
				x = cell.x,
				z = cell.z
			};
		}

		static object DescribeTarget(LocalTargetInfo target)
		{
			if (target.IsValid == false)
				return null;

			return new
			{
				hasThing = target.HasThing,
				cell = DescribeCell(target.Cell),
				thingId = target.Thing?.ThingID == null ? null : $"Thing_{target.Thing.ThingID}",
				label = target.Thing?.LabelCap
			};
		}

		static object DescribeState(Pawn pawn, ZeFlammenwerfer flamethrower, bool immediateRepathApplied = false)
		{
			return new
			{
				success = true,
				pawnId = $"Thing_{pawn.ThingID}",
				pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort,
				drafted = pawn.Drafted,
				spawned = pawn.Spawned,
				currentJob = pawn.CurJobDef?.defName,
				currentJobReport = pawn.CurJob?.GetReport(pawn),
				position = DescribeCell(pawn.Position),
				moving = pawn.pather?.Moving ?? false,
				moveDestination = pawn?.pather?.Destination.IsValid == true ? DescribeCell(pawn.pather.Destination.Cell) : null,
				fuel = flamethrower.refuelable?.Fuel ?? 0f,
				fuelCapacity = flamethrower.refuelable?.Props?.fuelCapacity ?? 0f,
				canFireNow = flamethrower.CanFireNow,
				flameActive = flamethrower.flameComp?.isActive ?? false,
				hasManualTarget = flamethrower.HasManualTarget,
				manualTarget = DescribeTarget(flamethrower.ManualTarget),
				currentAimTarget = DescribeTarget(flamethrower.CurrentAimTarget),
				manualTargetRepathPending = flamethrower.ManualTargetRepathPending,
				pathDangerIgnored = FlameDangerTracker.ShouldIgnorePathDanger(pawn),
				immediateRepathApplied
			};
		}

		static bool TryGetPawnAndWeapon(string pawnId, out Pawn pawn, out ZeFlammenwerfer flamethrower, out object error)
		{
			pawn = FindPawn(pawnId);
			flamethrower = FindFlamethrower(pawn);
			error = null;

			if (CurrentMap == null)
			{
				error = new
				{
					success = false,
					error = "No current map is loaded."
				};
				return false;
			}

			if (pawn == null)
			{
				error = new
				{
					success = false,
					error = string.IsNullOrWhiteSpace(pawnId)
						? "No single pawn is selected."
						: $"Pawn '{pawnId}' was not found on the current map."
				};
				return false;
			}

			if (flamethrower == null)
			{
				error = new
				{
					success = false,
					error = $"{pawn.LabelShortCap} is not currently carrying a Ze Flammenwerfer."
				};
				return false;
			}

			if (pawn.Spawned == false || pawn.Map != CurrentMap)
			{
				error = new
				{
					success = false,
					error = $"{pawn.LabelShortCap} is not spawned on the current map."
				};
				return false;
			}

			return true;
		}

		static bool TryGetCell(int x, int z, out IntVec3 cell, out object error)
		{
			cell = new IntVec3(x, 0, z);
			error = null;

			if (CurrentMap == null)
			{
				error = new
				{
					success = false,
					error = "No current map is loaded."
				};
				return false;
			}

			if (cell.InBounds(CurrentMap) == false)
			{
				error = new
				{
					success = false,
					error = $"Cell ({x}, {z}) is outside the current map."
				};
				return false;
			}

			return true;
		}

		[Tool("zeflammenwerfer/get_control_state", Description = "Read live Ze Flammenwerfer control state for one pawn, or the selected pawn when no id is provided.")]
		public static object GetControlState([ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			return DescribeState(pawn, flamethrower);
		}

		[Tool("zeflammenwerfer/order_fire_target", Description = "Set or replace the manual Ze Flammenwerfer fire target for one pawn, optionally immediately repathing the current move order.")]
		public static object OrderFireTarget(
			[ToolParameter(Description = "Fire target x coordinate.")] int x,
			[ToolParameter(Description = "Fire target z coordinate.")] int z,
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "When true, immediately refresh the pawn's current move path after setting the fire target.", Required = false, DefaultValue = true)] bool repathCurrentMove = true)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;
			if (TryGetCell(x, z, out var cell, out error) == false)
				return error;
			if (pawn.Drafted == false)
			{
				return new
				{
					success = false,
					error = $"{pawn.LabelShortCap} must be drafted before a manual flamethrower target can be set."
				};
			}

			flamethrower.OrderManualTarget(cell);
			var immediateRepathApplied = repathCurrentMove && flamethrower.ForceImmediateManualRepath();
			return DescribeState(pawn, flamethrower, immediateRepathApplied);
		}

		[Tool("zeflammenwerfer/clear_fire_target", Description = "Clear the current manual Ze Flammenwerfer fire target for one pawn and optionally refresh the remaining move path.")]
		public static object ClearFireTarget(
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "When true, immediately refresh the pawn's current move path after clearing the fire target.", Required = false, DefaultValue = true)] bool repathCurrentMove = true)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			flamethrower.ClearManualTarget();
			flamethrower.flameComp?.SetActive(false);
			FlameDangerTracker.Clear(pawn);

			var immediateRepathApplied = repathCurrentMove && flamethrower.TryForceCurrentMoveRepath();
			return DescribeState(pawn, flamethrower, immediateRepathApplied);
		}

		[Tool("zeflammenwerfer/order_move_and_fire", Description = "Issue a fresh move order and a manual Ze Flammenwerfer fire target for one drafted pawn in one bridge call.")]
		public static object OrderMoveAndFire(
			[ToolParameter(Description = "Move destination x coordinate.")] int moveX,
			[ToolParameter(Description = "Move destination z coordinate.")] int moveZ,
			[ToolParameter(Description = "Fire target x coordinate.")] int fireX,
			[ToolParameter(Description = "Fire target z coordinate.")] int fireZ,
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;
			if (TryGetCell(moveX, moveZ, out var moveCell, out error) == false)
				return error;
			if (TryGetCell(fireX, fireZ, out var fireCell, out error) == false)
				return error;
			if (pawn.Drafted == false)
			{
				return new
				{
					success = false,
					error = $"{pawn.LabelShortCap} must be drafted before order_move_and_fire can be used."
				};
			}

			var moveJob = JobMaker.MakeJob(JobDefOf.Goto, moveCell);
			moveJob.locomotionUrgency = LocomotionUrgency.Jog;

			if (pawn.jobs.TryTakeOrderedJob(moveJob) == false)
			{
				return new
				{
					success = false,
					error = $"Failed to issue a move order for {pawn.LabelShortCap}.",
					moveCell = DescribeCell(moveCell)
				};
			}

			flamethrower.OrderManualTarget(fireCell);
			var immediateRepathApplied = flamethrower.ForceImmediateManualRepath();
			return DescribeState(pawn, flamethrower, immediateRepathApplied);
		}
	}
}
