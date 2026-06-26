using BansheeGz.BGSpline.Curve;
using RimBridgeServer.Sdk;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	public sealed class ZeFlammenwerferBridgeTools
	{
		sealed class AimSweepPose
		{
			public readonly string Label;
			public readonly string Rotation;
			public readonly int XUnit;
			public readonly int ZUnit;

			public AimSweepPose(string label, string rotation, int xUnit, int zUnit)
			{
				Label = label;
				Rotation = rotation;
				XUnit = xUnit;
				ZUnit = zUnit;
			}
		}

		static readonly AimSweepPose[] TankPipeAimSweep =
		{
			new("01-north", "north", 0, 2),
			new("02-north-northeast", "north", 1, 2),
			new("03-northeast", "east", 2, 2),
			new("04-east-northeast", "east", 2, 1),
			new("05-east", "east", 2, 0),
			new("06-east-southeast", "east", 2, -1),
			new("07-southeast", "east", 2, -2),
			new("08-south-southeast", "south", 1, -2),
			new("09-south", "south", 0, -2),
			new("10-south-southwest", "south", -1, -2),
			new("11-southwest", "west", -2, -2),
			new("12-west-southwest", "west", -2, -1),
			new("13-west", "west", -2, 0),
			new("14-west-northwest", "west", -2, 1),
			new("15-northwest", "west", -2, 2),
			new("16-north-northwest", "north", -1, 2)
		};

		static Map CurrentMap => Find.CurrentMap;

		static string StableThingId(Thing thing)
		{
			return thing == null ? null : $"Thing_{thing.ThingID}";
		}

		static T TryRead<T>(Func<T> reader, T fallback = default)
		{
			try
			{
				return reader();
			}
			catch
			{
				return fallback;
			}
		}

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

		static Thing FindThing(string thingId)
		{
			var map = CurrentMap;
			if (map == null || string.IsNullOrWhiteSpace(thingId))
				return null;

			var query = thingId.Trim();
			return map.listerThings.AllThings.FirstOrDefault(thing =>
				string.Equals(StableThingId(thing), query, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(thing.ThingID, query, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(thing.GetUniqueLoadID(), query, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(thing.LabelCap.ToString(), query, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(thing.LabelShort, query, StringComparison.OrdinalIgnoreCase));
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

		static object DescribeVector(Vector3 vector)
		{
			return new
			{
				x = vector.x,
				y = vector.y,
				z = vector.z
			};
		}

		static object DescribeRotation(Rot4 rotation)
		{
			return new
			{
				value = rotation.ToString(),
				asInt = rotation.AsInt,
				facingCell = DescribeCell(rotation.FacingCell)
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

		static object DescribeThingBasic(Thing thing)
		{
			if (thing == null)
				return null;

			return new
			{
				thingId = StableThingId(thing),
				rawThingId = thing.ThingID,
				uniqueLoadId = thing.GetUniqueLoadID(),
				defName = thing.def?.defName,
				label = thing.LabelCap.ToString(),
				type = thing.GetType().Name,
				spawned = thing.Spawned,
				destroyed = thing.Destroyed,
				position = thing.Spawned ? DescribeCell(thing.Position) : null,
				drawPos = thing.Spawned ? DescribeVector(thing.DrawPos) : null,
				fillPercent = thing.def?.fillPercent ?? 0f,
				fillage = thing.def?.Fillage.ToString(),
				passability = thing.def?.passability.ToString(),
				hitPoints = thing.def?.useHitPoints == true ? thing.HitPoints : 0,
				maxHitPoints = thing.def?.useHitPoints == true ? thing.MaxHitPoints : 0
			};
		}

		static object DescribeAttachedFire(Fire fire)
		{
			if (fire == null)
				return null;

			return new
			{
				thingId = StableThingId(fire),
				fireSize = fire.fireSize,
				spawned = fire.Spawned,
				destroyed = fire.Destroyed,
				position = fire.Spawned ? DescribeCell(fire.Position) : null
			};
		}

		static object[] DescribeAttachedFires(ThingWithComps thing)
		{
			return thing?.TryGetComp<CompAttachBase>()?.attachments?
				.OfType<Fire>()
				.Select(DescribeAttachedFire)
				.ToArray() ?? Array.Empty<object>();
		}

		static object DescribeFireDamage(ThingWithComps thing)
		{
			var comp = thing?.GetComp<FireDamage>();
			if (comp == null)
				return null;

			return new
			{
				multiplier = comp.multiplier
			};
		}

		static object DescribeDamageThing(Thing thing)
		{
			if (thing == null)
				return null;

			var thingWithComps = thing as ThingWithComps;
			var pawn = thing as Pawn;
			return new
			{
				thing = DescribeThingBasic(thing),
				pawn = pawn == null ? null : new
				{
					dead = pawn.Dead,
					downed = pawn.Downed,
					healthSummary = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 0f
				},
				fireDamage = DescribeFireDamage(thingWithComps),
				attachedFires = DescribeAttachedFires(thingWithComps)
			};
		}

		static object DescribeParticleSystem(GameObject gameObject)
		{
			if (gameObject == null)
				return null;

			var particleSystem = gameObject.GetComponent<ParticleSystem>();
			var emission = particleSystem == null ? default : particleSystem.emission;
			return new
			{
				name = gameObject.name,
				activeSelf = gameObject.activeSelf,
				activeInHierarchy = gameObject.activeInHierarchy,
				position = DescribeVector(gameObject.transform.position),
				rotationEuler = DescribeVector(gameObject.transform.rotation.eulerAngles),
				localScale = DescribeVector(gameObject.transform.localScale),
				hasParticleSystem = particleSystem != null,
				particleCount = particleSystem?.particleCount ?? 0,
				isPlaying = particleSystem?.isPlaying ?? false,
				isPaused = particleSystem?.isPaused ?? false,
				isStopped = particleSystem?.isStopped ?? false,
				emissionEnabled = particleSystem != null && emission.enabled
			};
		}

		static object DescribeCurvePoint(IBGCurvePointI point)
		{
			if (point == null)
				return null;

			return new
			{
				position = DescribeVector(point.PositionWorld),
				controlFirst = DescribeVector(point.ControlFirstWorld),
				controlSecond = DescribeVector(point.ControlSecondWorld)
			};
		}

		static object DescribeCurve(GameObject gameObject, IBGCurvePointI[] points)
		{
			if (gameObject == null)
				return null;

			return new
			{
				name = gameObject.name,
				activeSelf = gameObject.activeSelf,
				activeInHierarchy = gameObject.activeInHierarchy,
				position = DescribeVector(gameObject.transform.position),
				points = points == null
					? Array.Empty<object>()
					: points.Select(DescribeCurvePoint).ToArray()
			};
		}

		static object DescribeFlameProjectile(ZeFlame flame)
		{
			if (flame == null)
				return null;

			return new
			{
				thingId = StableThingId(flame),
				spawned = flame.Spawned,
				destroyed = flame.Destroyed,
				position = flame.Spawned ? DescribeCell(flame.Position) : null,
				drawPos = flame.Spawned ? DescribeVector(flame.DrawPos) : null,
				launcher = StableThingId(flame.owner?.launcher),
				hasFlameComp = flame.flameComp != null
			};
		}

		static object DescribeFuelState(ZeFlammenwerfer flamethrower)
		{
			var refuelable = flamethrower?.refuelable ?? flamethrower?.TryGetComp<CompRefuelable>();
			if (refuelable == null)
			{
				return new
				{
					hasRefuelable = false
				};
			}

			var capacity = flamethrower?.FuelCapacity ?? refuelable.Props?.fuelCapacity ?? 0f;
			var fuelPerShot = flamethrower?.FuelPerShot ?? 0f;
			var quality = flamethrower?.TryGetComp<CompQuality>()?.Quality;
			var shootingSkill = flamethrower?.pawn?.skills?.GetSkill(SkillDefOf.Shooting)?.Level;
			var flameProps = flamethrower?.FlameProps;
			return new
			{
				hasRefuelable = true,
				fuel = refuelable.Fuel,
				fuelCapacity = capacity,
				fuelPercentOfMax = capacity <= 0f ? 0f : refuelable.FuelPercentOfMax,
				targetFuelLevel = refuelable.TargetFuelLevel,
				fuelPercentOfTarget = refuelable.FuelPercentOfTarget,
				isFull = refuelable.IsFull,
				hasFuel = refuelable.HasFuel,
				allowAutoRefuel = refuelable.allowAutoRefuel,
				shouldAutoRefuelNow = TryRead(() => refuelable.ShouldAutoRefuelNow, false),
				shouldAutoRefuelNowIgnoringFuelPct = TryRead(() => refuelable.ShouldAutoRefuelNowIgnoringFuelPct, false),
				fuelPerShot,
				shotsRemaining = fuelPerShot <= 0f ? int.MaxValue : Mathf.FloorToInt(refuelable.Fuel / fuelPerShot),
				canFireNow = flamethrower?.CanFireNow ?? false,
				outOfFuelReason = flamethrower?.OutOfFuelReason,
				quality = quality?.ToString(),
				shootingSkill,
				scaling = flameProps == null ? null : new
				{
					minimumFuelCapacity = flameProps.minimumFuelCapacity,
					maximumFuelCapacity = flameProps.maximumFuelCapacity,
					minimumFuelConsumption = flameProps.minimumFuelConsumption,
					maximumFuelConsumption = flameProps.maximumFuelConsumption
				},
				props = new
				{
					fuelLabel = refuelable.Props?.fuelLabel,
					fuelConsumptionRate = refuelable.Props?.fuelConsumptionRate ?? 0f,
					fuelMultiplier = refuelable.Props?.FuelMultiplierCurrentDifficulty ?? 1f,
					fuelFilter = refuelable.Props?.fuelFilter?.Summary,
					allowRefuelIfNotEmpty = refuelable.Props?.allowRefuelIfNotEmpty ?? false,
					atomicFueling = refuelable.Props?.atomicFueling ?? false
				}
			};
		}

		static object DescribeRefuelCheck(Pawn actor, Pawn bearer, bool forced)
		{
			if (actor == null)
			{
				return new
				{
					success = false,
					error = "Actor was not found."
				};
			}

			var canRefuel = FlamethrowerRefuelUtility.TryCanRefuelEquipped(actor, bearer, forced, out var failReason);
			var job = canRefuel ? FlamethrowerRefuelUtility.MakeRefuelEquippedJob(actor, bearer, forced) : null;
			return new
			{
				success = true,
				actor = DescribeThingBasic(actor),
				bearer = DescribeThingBasic(bearer),
				forced,
				canRefuel,
				failReason,
				job = job == null ? null : new
				{
					defName = job.def?.defName,
					targetA = DescribeTarget(job.GetTarget(TargetIndex.A)),
					targetB = DescribeTarget(job.GetTarget(TargetIndex.B)),
					playerForced = job.playerForced,
					count = job.count
				}
			};
		}

		static object DescribeAimingState(Pawn pawn, ZeFlammenwerfer flamethrower)
		{
			var (center, angle) = WeaponTool.GetAimingCenter(pawn);
			var aimTarget = flamethrower.CurrentAimTarget;
			return new
			{
				isAiming = angle != int.MinValue,
				aimAngle = angle == int.MinValue ? 0f : angle,
				flipped = angle != int.MinValue && WeaponTool.IsFlipped(angle),
				aimingCenter = angle == int.MinValue ? null : DescribeVector(center),
				currentAimTarget = DescribeTarget(aimTarget)
			};
		}

		static object DescribeRenderState(Pawn pawn, ZeFlammenwerfer flamethrower)
		{
			var comp = flamethrower?.flameComp ?? flamethrower?.TryGetComp<ZeFlameComp>();
			return new
			{
				success = true,
				pawn = new
				{
					pawnId = StableThingId(pawn),
					name = pawn.Name?.ToStringShort ?? pawn.LabelShort,
					position = DescribeCell(pawn.Position),
					drawPos = DescribeVector(pawn.DrawPos),
					rotation = DescribeRotation(pawn.Rotation),
					drafted = pawn.Drafted,
					moving = pawn.pather?.Moving ?? false,
					moveDestination = pawn.pather?.Destination.IsValid == true ? DescribeCell(pawn.pather.Destination.Cell) : null,
					currentJob = pawn.CurJobDef?.defName,
					currentJobReport = pawn.CurJob?.GetReport(pawn)
				},
				weapon = DescribeThingBasic(flamethrower),
				fuel = DescribeFuelState(flamethrower),
				aiming = DescribeAimingState(pawn, flamethrower),
				visuals = comp == null ? null : new
				{
					hasComp = true,
					isActive = comp.isActive,
					isPipeActive = comp.isPipeActive,
					shouldStayActiveBetweenShots = comp.ShouldStayActiveBetweenShots(),
					lastShotTick = comp.lastShotTick,
					ticksSinceLastShot = comp.lastShotTick == int.MinValue ? -1 : GenTicks.TicksGame - comp.lastShotTick,
					allFlameCompCount = ZeFlameComp.allFlameComps.Count,
					allParticleSystemCount = ZeFlameComp.allParticleSystems.Count,
					fire = DescribeParticleSystem(comp.fire),
					smoke = DescribeParticleSystem(comp.smoke),
					curveInner = DescribeCurve(comp.curveInner, comp.pointsInner),
					curveOuter = DescribeCurve(comp.curveOuter, comp.pointsOuter),
					flameProjectileCount = comp.flames.Count,
					flameProjectiles = comp.flames.Take(20).Select(DescribeFlameProjectile).ToArray()
				}
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
				rotation = DescribeRotation(pawn.Rotation),
				moving = pawn.pather?.Moving ?? false,
				moveDestination = pawn?.pather?.Destination.IsValid == true ? DescribeCell(pawn.pather.Destination.Cell) : null,
				fuel = flamethrower.refuelable?.Fuel ?? 0f,
				fuelCapacity = flamethrower.FuelCapacity,
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

		static bool TryGetThing(string thingId, out Thing thing, out object error)
		{
			thing = FindThing(thingId);
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

			if (thing == null)
			{
				error = new
				{
					success = false,
					error = $"Thing '{thingId}' was not found on the current map."
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

		static bool SameTarget(LocalTargetInfo left, LocalTargetInfo right)
		{
			if (left.IsValid == false || right.IsValid == false)
				return false;
			if (left.HasThing || right.HasThing)
				return left.HasThing && right.HasThing && left.Thing == right.Thing;
			return left.Cell == right.Cell;
		}

		static bool TryParseRotation(string value, out Rot4 rotation, out object error)
		{
			error = null;
			rotation = Rot4.South;
			switch ((value ?? "").Trim().ToLowerInvariant())
			{
				case "":
				case "south":
				case "s":
				case "2":
					rotation = Rot4.South;
					return true;
				case "north":
				case "n":
				case "0":
					rotation = Rot4.North;
					return true;
				case "east":
				case "e":
				case "1":
					rotation = Rot4.East;
					return true;
				case "west":
				case "w":
				case "3":
					rotation = Rot4.West;
					return true;
				default:
					error = new
					{
						success = false,
						error = $"Rotation '{value}' is not recognized. Use north, east, south, west, or 0..3."
					};
					return false;
			}
		}

		static IntVec3 DefaultTargetCell(Pawn pawn, Rot4 rotation)
		{
			var map = pawn.Map;
			var cell = pawn.Position + rotation.FacingCell * 6;
			if (cell.InBounds(map))
				return cell;
			return CellRect.WholeMap(map).ClosestCellTo(cell);
		}

		static int AimSweepOffset(int unit, int radius, int halfRadius)
		{
			if (unit == 0)
				return 0;

			var magnitude = Math.Abs(unit) == 2 ? radius : halfRadius;
			return unit > 0 ? magnitude : -magnitude;
		}

		static IntVec3 AimSweepTargetCell(AimSweepPose pose, IntVec3 origin, int radius, int halfRadius)
		{
			return new IntVec3(
				origin.x + AimSweepOffset(pose.XUnit, radius, halfRadius),
				0,
				origin.z + AimSweepOffset(pose.ZUnit, radius, halfRadius));
		}

		static object DescribeAimSweepPose(AimSweepPose pose, int index, IntVec3 origin, int radius, int halfRadius)
		{
			return new
			{
				index,
				label = pose.Label,
				rotation = pose.Rotation,
				target = DescribeCell(AimSweepTargetCell(pose, origin, radius, halfRadius))
			};
		}

		static RimBridgeToolCallResult<object> LocalResult(object result)
		{
			return new RimBridgeToolCallResult<object> { Success = true, Result = result };
		}

		static LocalTargetInfo ApplyRenderPose(Pawn pawn, ZeFlammenwerfer flamethrower, Rot4 rotation, IntVec3 targetCell, bool draft, bool setManualTarget, bool activateVisual, bool stopMovement, bool clearUiState)
		{
			if (draft && pawn.drafter != null)
				pawn.drafter.Drafted = true;
			if (stopMovement)
			{
				pawn.pather?.StopDead();
				pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			}

			pawn.Rotation = rotation;
			var target = new LocalTargetInfo(targetCell);
			if (setManualTarget && SameTarget(flamethrower.ManualTarget, target) == false)
				flamethrower.OrderManualTarget(target);

			RefreshFlameVisual(pawn, flamethrower, target, activateVisual);
			if (clearUiState)
				ClearUiStateForEvidence();

			return target;
		}

		static void RefreshFlameVisual(Pawn pawn, ZeFlammenwerfer flamethrower, LocalTargetInfo target, bool active)
		{
			var comp = flamethrower.flameComp ?? flamethrower.TryGetComp<ZeFlameComp>();
			if (comp == null)
				return;

			comp.UpdateDrawPos(pawn);
			if (active == false)
			{
				comp.SetActive(false, true);
				return;
			}

			comp.NotifyShot();
			comp.SetActive(true, true);
			if (target.IsValid == false)
				return;

			var from = pawn.DrawPos.WithHeight(0);
			var to = target.HasThing ? target.Thing.DrawPos : target.Cell.ToVector3Shifted();
			var vector = to - from;
			var startOffset = vector.magnitude > 1f ? vector.normalized : Vector3.zero;
			comp.Update(from + 1.75f * startOffset, to);
		}

		static void ClearUiStateForEvidence()
		{
			Find.Selector?.ClearSelection();
			Find.DesignatorManager?.Deselect();
		}

		static object DescribeLineCell(Map map, IntVec3 cell)
		{
			var things = map.thingGrid.ThingsListAtFast(cell).ToArray();
			var blockers = things
				.Where(thing => thing is not Pawn && thing.def.fillPercent >= 0.25f)
				.OrderByDescending(thing => thing.def.fillPercent)
				.Select(DescribeThingBasic)
				.ToArray();

			return new
			{
				cell = DescribeCell(cell),
				terrain = cell.GetTerrain(map)?.defName,
				thingCount = things.Length,
				maxFillPercent = map.thingGrid.MaxFillPercentFast(cell),
				hasParticleColliderBlocker = blockers.Length > 0,
				blockers,
				things = things
					.OrderByDescending(thing => thing.def.fillPercent)
					.Take(8)
					.Select(DescribeThingBasic)
					.ToArray()
			};
		}

		static object DescribeColliderCell(Map map, IntVec3 cell, BoxCollider collider)
		{
			return new
			{
				cell = DescribeCell(cell),
				maxFillPercent = map.thingGrid.MaxFillPercentFast(cell),
				collider = collider == null ? null : new
				{
					name = collider.name,
					enabled = collider.enabled,
					size = DescribeVector(collider.size),
					center = DescribeVector(collider.center)
				},
				things = map.thingGrid.ThingsListAtFast(cell)
					.Where(thing => thing is not Pawn)
					.OrderByDescending(thing => thing.def.fillPercent)
					.Take(8)
					.Select(DescribeThingBasic)
					.ToArray()
			};
		}

		[Tool("zeflammenwerfer/get_control_state", Description = "Read live Ze Flammenwerfer control state for one pawn, or the selected pawn when no id is provided.")]
		public static object GetControlState([ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			return DescribeState(pawn, flamethrower);
		}

		[Tool("zeflammenwerfer/list_test_subjects", Description = "List current-map pawns carrying Ze Flammenwerfers with compact control, fuel, and render-test readiness state.")]
		public static object ListTestSubjects([ToolParameter(Description = "Maximum number of subjects to return.", Required = false, DefaultValue = 20)] int limit = 20)
		{
			var map = CurrentMap;
			if (map == null)
			{
				return new
				{
					success = false,
					error = "No current map is loaded."
				};
			}

			limit = Mathf.Clamp(limit, 1, 100);
			var subjects = map.mapPawns.AllPawnsSpawned
				.Select(pawn => new { pawn, flamethrower = FindFlamethrower(pawn) })
				.Where(pair => pair.flamethrower != null)
				.Take(limit)
				.Select(pair => new
				{
					pawn = DescribeThingBasic(pair.pawn),
					control = DescribeState(pair.pawn, pair.flamethrower),
					fuel = DescribeFuelState(pair.flamethrower),
					render = DescribeRenderState(pair.pawn, pair.flamethrower)
				})
				.ToArray();

			return new
			{
				success = true,
				map = map.uniqueID,
				subjectCount = subjects.Length,
				subjects,
				globalVisuals = new
				{
					flameCompCount = ZeFlameComp.allFlameComps.Count,
					particleSystemCount = ZeFlameComp.allParticleSystems.Count,
					shooterTrackerCount = PawnShooterTracker.trackers.Count,
					pawnColliderHolderCount = ColliderHolder.holders.Count
				}
			};
		}

		[Tool("zeflammenwerfer/get_render_state", Description = "Read Ze Flammenwerfer pawn/equipment render state including aim vector, tank/pipe curves, particles, active flames, and fuel.")]
		public static object GetRenderState([ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			return DescribeRenderState(pawn, flamethrower);
		}

		[Tool("zeflammenwerfer/set_render_pose", Description = "Prepare a deterministic static render pose for one Ze Flammenwerfer pawn without taking a shot. Use RimBridge screenshots/ticks for visual verification.")]
		public static object SetRenderPose(
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "Pawn rotation: north, east, south, west, or 0..3.", Required = false, DefaultValue = "south")] string rotation = "south",
			[ToolParameter(Description = "Optional target x coordinate. Use -1 with targetZ -1 for a target six cells in the facing direction.", Required = false, DefaultValue = -1)] int targetX = -1,
			[ToolParameter(Description = "Optional target z coordinate. Use -1 with targetX -1 for a target six cells in the facing direction.", Required = false, DefaultValue = -1)] int targetZ = -1,
			[ToolParameter(Description = "When true, set the pawn drafted so the manual target remains valid.", Required = false, DefaultValue = true)] bool draft = true,
			[ToolParameter(Description = "When true, set the manual fire target to the pose target without toggling it off when already set.", Required = false, DefaultValue = true)] bool setManualTarget = true,
			[ToolParameter(Description = "When true, make the flame particles and pipe active for screenshot inspection without spawning a projectile.", Required = false, DefaultValue = true)] bool activateVisual = true,
			[ToolParameter(Description = "When true, stop the current path/job before posing. Leave false for walking render tests.", Required = false, DefaultValue = false)] bool stopMovement = false,
			[ToolParameter(Description = "When true, clear RimWorld selection and designator UI before returning so screenshots are not contaminated by open tool palettes.", Required = false, DefaultValue = true)] bool clearUiState = true)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;
			if (TryParseRotation(rotation, out var parsedRotation, out error) == false)
				return error;

			IntVec3 targetCell;
			if (targetX >= 0 || targetZ >= 0)
			{
				if (TryGetCell(targetX, targetZ, out targetCell, out error) == false)
					return error;
			}
			else
			{
				targetCell = DefaultTargetCell(pawn, parsedRotation);
			}

			var target = ApplyRenderPose(pawn, flamethrower, parsedRotation, targetCell, draft, setManualTarget, activateVisual, stopMovement, clearUiState);

			return new
			{
				success = true,
				target = DescribeTarget(target),
				render = DescribeRenderState(pawn, flamethrower)
			};
		}

		[Tool("zeflammenwerfer/list_tank_pipe_pose_sweep", Description = "List the standard 16-position tank/pipe close-up aim sweep used by the repeatable RimBridge evidence suite.")]
		public static object ListTankPipePoseSweep(
			[ToolParameter(Description = "Origin x coordinate, normally Rocha's occupied cell in the walkthrough save.", Required = false, DefaultValue = 117)] int cellX = 117,
			[ToolParameter(Description = "Origin z coordinate, normally Rocha's occupied cell in the walkthrough save.", Required = false, DefaultValue = 114)] int cellZ = 114,
			[ToolParameter(Description = "Far aim radius in cells.", Required = false, DefaultValue = 4)] int radius = 4,
			[ToolParameter(Description = "Intermediate aim radius in cells.", Required = false, DefaultValue = 2)] int halfRadius = 2)
		{
			if (TryGetCell(cellX, cellZ, out var origin, out var error) == false)
				return error;

			radius = Mathf.Clamp(radius, 1, 30);
			halfRadius = Mathf.Clamp(halfRadius, 1, radius);
			return new
			{
				success = true,
				origin = DescribeCell(origin),
				radius,
				halfRadius,
				poseCount = TankPipeAimSweep.Length,
				poses = TankPipeAimSweep
					.Select((pose, index) => DescribeAimSweepPose(pose, index + 1, origin, radius, halfRadius))
					.ToArray()
			};
		}

		[Tool("zeflammenwerfer/set_tank_pipe_pose", Description = "Prepare one pose from the standard 16-position tank/pipe close-up aim sweep. Use RimBridge screenshots/ticks after this call for evidence capture.")]
		public static object SetTankPipePose(
			[ToolParameter(Description = "One-based pose index from zeflammenwerfer/list_tank_pipe_pose_sweep, 1 through 16.")] int poseIndex,
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "Origin x coordinate, normally Rocha's occupied cell in the walkthrough save.", Required = false, DefaultValue = 117)] int cellX = 117,
			[ToolParameter(Description = "Origin z coordinate, normally Rocha's occupied cell in the walkthrough save.", Required = false, DefaultValue = 114)] int cellZ = 114,
			[ToolParameter(Description = "Far aim radius in cells.", Required = false, DefaultValue = 4)] int radius = 4,
			[ToolParameter(Description = "Intermediate aim radius in cells.", Required = false, DefaultValue = 2)] int halfRadius = 2,
			[ToolParameter(Description = "When true, set the pawn drafted so the manual target remains valid.", Required = false, DefaultValue = true)] bool draft = true,
			[ToolParameter(Description = "When true, make the flame particles active for screenshot inspection without spawning a projectile.", Required = false, DefaultValue = false)] bool activateVisual = false,
			[ToolParameter(Description = "When true, stop the current path/job before posing.", Required = false, DefaultValue = true)] bool stopMovement = true,
			[ToolParameter(Description = "When true, clear RimWorld selection and designator UI before returning.", Required = false, DefaultValue = true)] bool clearUiState = true)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;
			if (TryGetCell(cellX, cellZ, out var origin, out error) == false)
				return error;
			if (poseIndex < 1 || poseIndex > TankPipeAimSweep.Length)
			{
				return new
				{
					success = false,
					error = $"Pose index {poseIndex} is outside the tank/pipe sweep range 1..{TankPipeAimSweep.Length}."
				};
			}

			radius = Mathf.Clamp(radius, 1, 30);
			halfRadius = Mathf.Clamp(halfRadius, 1, radius);
			var pose = TankPipeAimSweep[poseIndex - 1];
			if (TryParseRotation(pose.Rotation, out var rotation, out error) == false)
				return error;

			var targetCell = AimSweepTargetCell(pose, origin, radius, halfRadius);
			if (TryGetCell(targetCell.x, targetCell.z, out targetCell, out error) == false)
				return error;

			var target = ApplyRenderPose(pawn, flamethrower, rotation, targetCell, draft, true, activateVisual, stopMovement, clearUiState);
			return new
			{
				success = true,
				poseIndex,
				label = pose.Label,
				origin = DescribeCell(origin),
				radius,
				halfRadius,
				rotation = DescribeRotation(rotation),
				target = DescribeTarget(target),
				render = DescribeRenderState(pawn, flamethrower)
			};
		}

		[Tool("zeflammenwerfer/render_tank_pipe_pose_sweep", Description = "Run the full 16-position tank/pipe screenshot evidence sweep from C#, using RimBridgeServer v2 async SDK calls for ticks and screenshots.")]
		public static async Task<object> RenderTankPipePoseSweep(
			IRimBridgeContext ctx,
			CancellationToken cancellationToken,
			[ToolParameter(Description = "When true, load the walkthrough save before capturing.", Required = false, DefaultValue = true)] bool loadGame = true,
			[ToolParameter(Description = "Save to load when loadGame is true.", Required = false, DefaultValue = "zeflammenwerfer walkthrough")] string saveName = "zeflammenwerfer walkthrough",
			[ToolParameter(Description = "Stable pawn id such as Thing_Human776.", Required = false, DefaultValue = "Thing_Human776")] string pawnId = "Thing_Human776",
			[ToolParameter(Description = "Origin x coordinate, normally Rocha's occupied cell in the walkthrough save.", Required = false, DefaultValue = 117)] int cellX = 117,
			[ToolParameter(Description = "Origin z coordinate, normally Rocha's occupied cell in the walkthrough save.", Required = false, DefaultValue = 114)] int cellZ = 114,
			[ToolParameter(Description = "Far aim radius in cells.", Required = false, DefaultValue = 4)] int radius = 4,
			[ToolParameter(Description = "Intermediate aim radius in cells.", Required = false, DefaultValue = 2)] int halfRadius = 2,
			[ToolParameter(Description = "Screenshot padding in cells.", Required = false, DefaultValue = 2)] int paddingCells = 2,
			[ToolParameter(Description = "Camera root size for close-up screenshot framing.", Required = false, DefaultValue = 2.5f)] float rootSize = 2.5f,
			[ToolParameter(Description = "Paused deterministic ticks to advance after each pose.", Required = false, DefaultValue = 1)] int poseTicks = 1,
			[ToolParameter(Description = "Prefix for generated screenshot file names.", Required = false, DefaultValue = "zef-tank-pipe")] string filePrefix = "zef-tank-pipe",
			[ToolParameter(Description = "Optional caller-supplied run id included in the result.", Required = false, DefaultValue = "manual")] string runId = "manual")
		{
			if (ctx == null)
			{
				return new
				{
					success = false,
					error = "RimBridge SDK context was not injected."
				};
			}

			var screenshotTool = ctx.Tools.Get("rimworld/screenshot_cell_rect");
			var capabilityProbe = ctx.Tools.List(new RimBridgeToolQuery { Text = "zeflammenwerfer/" });
			object load = null;
			if (loadGame)
			{
				var loadResult = await ctx.Tools.CallAsync("rimworld/load_game_ready", new
				{
					saveName,
					readiness = "visual",
					pauseIfNeeded = true,
					timeoutMs = 120000
				}, cancellationToken: cancellationToken);
				load = loadResult.Result;
				if (loadResult.Succeeded() == false)
				{
					return new
					{
						success = false,
						error = "Loading the walkthrough save failed.",
						load = loadResult
					};
				}
			}

			var sweep = ListTankPipePoseSweep(cellX, cellZ, radius, halfRadius);
			var sweepResult = LocalResult(sweep);
			if (sweepResult.Succeeded() == false)
			{
				return new
				{
					success = false,
					error = "The tank/pipe pose sweep could not be created.",
					sweep
				};
			}

			await ctx.Tools.CallAsync("rimworld/clear_selection", cancellationToken: cancellationToken);
			var captures = new List<object>();
			for (var poseIndex = 1; poseIndex <= TankPipeAimSweep.Length; poseIndex++)
			{
				var pose = SetTankPipePose(
					poseIndex,
					pawnId,
					cellX,
					cellZ,
					radius,
					halfRadius,
					draft: true,
					activateVisual: false,
					stopMovement: true,
					clearUiState: true);
				var poseResult = LocalResult(pose);
				if (poseResult.Succeeded() == false)
				{
					return new
					{
						success = false,
						error = $"Pose {poseIndex} failed.",
						pose,
						captures = captures.ToArray()
					};
				}

				var tick = await ctx.Game.StepTicksAsync(poseTicks, new RimBridgeTickOptions
				{
					TimeoutMs = 10000,
					PauseFirst = true
				}, cancellationToken: cancellationToken);
				if (tick.Success == false)
				{
					return new
					{
						success = false,
						error = $"Tick advancement after pose {poseIndex} failed.",
						pose,
						tick,
						captures = captures.ToArray()
					};
				}

				var label = poseResult.TryReadResult<string>(out var poseLabel, "label") ? poseLabel : TankPipeAimSweep[poseIndex - 1].Label;
				var shot = await ctx.Tools.CallAsync("rimworld/screenshot_cell_rect", new
				{
					x = cellX,
					z = cellZ,
					width = 1,
					height = 1,
					paddingCells,
					rootSize,
					fileName = $"{filePrefix}-{label}",
					suppressMessage = true
				}, cancellationToken: cancellationToken);
				if (shot.Succeeded() == false)
				{
					return new
					{
						success = false,
						error = $"Screenshot capture after pose {poseIndex} failed.",
						pose,
						tick,
						screenshot = shot,
						captures = captures.ToArray()
					};
				}

				captures.Add(new
				{
					label,
					pose,
					tick,
					screenshot = shot.Result
				});
			}

			var cleanup = ClearFireTarget(pawnId, repathCurrentMove: false);
			var reset = SetRenderPose(
				pawnId,
				rotation: "south",
				draft: true,
				setManualTarget: false,
				activateVisual: false,
				stopMovement: false,
				clearUiState: true);
			await ctx.Tools.CallAsync("rimworld/clear_selection", cancellationToken: cancellationToken);

			return new
			{
				success = true,
				suite = "tank-pipe-16-aims-sdk",
				runId,
				pawnId,
				saveName,
				load,
				sweep,
				requestedRect = new
				{
					x = cellX,
					z = cellZ,
					width = 1,
					height = 1,
					paddingCells,
					rootSize
				},
				toolQuery = new
				{
					screenshotTool = screenshotTool.Id,
					matchingCapabilityCount = capabilityProbe.Count
				},
				cleanup,
				reset,
				captures = captures.ToArray()
			};
		}

		[Tool("zeflammenwerfer/get_fuel_state", Description = "Read Ze Flammenwerfer fuel state and optionally check whether an actor can refuel the equipped weapon.")]
		public static object GetFuelState(
			[ToolParameter(Description = "Optional stable pawn id for the bearer such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "Optional stable pawn id for the actor that would perform refueling.", Required = false, DefaultValue = null)] string actorId = null,
			[ToolParameter(Description = "When checking actor refueling, pass the forced flag used by the refuel job helpers.", Required = false, DefaultValue = true)] bool forced = true)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			var actor = string.IsNullOrWhiteSpace(actorId) ? null : FindPawn(actorId);
			return new
			{
				success = true,
				pawn = DescribeThingBasic(pawn),
				weapon = DescribeThingBasic(flamethrower),
				fuel = DescribeFuelState(flamethrower),
				refuelCheck = string.IsNullOrWhiteSpace(actorId) ? null : DescribeRefuelCheck(actor, pawn, forced)
			};
		}

		[Tool("zeflammenwerfer/set_fuel_state", Description = "Set the equipped Ze Flammenwerfer fuel amount for deterministic firing/refuel tests.")]
		public static object SetFuelState(
			[ToolParameter(Description = "Target fuel amount. Clamped to the weapon fuel capacity.")] float fuel,
			[ToolParameter(Description = "Optional stable pawn id for the bearer such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "When true, also set allowAutoRefuel to the provided allowAutoRefuel value.", Required = false, DefaultValue = false)] bool setAllowAutoRefuel = false,
			[ToolParameter(Description = "Value used when setAllowAutoRefuel is true.", Required = false, DefaultValue = true)] bool allowAutoRefuel = true,
			[ToolParameter(Description = "When true, also set TargetFuelLevel to targetFuelLevel.", Required = false, DefaultValue = false)] bool setTargetFuelLevel = false,
			[ToolParameter(Description = "TargetFuelLevel value used when setTargetFuelLevel is true. Negative uses the fuel value.", Required = false, DefaultValue = -1f)] float targetFuelLevel = -1f)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			var refuelable = flamethrower.refuelable ?? flamethrower.TryGetComp<CompRefuelable>();
			if (refuelable == null)
			{
				return new
				{
					success = false,
					error = "The equipped Ze Flammenwerfer does not have CompRefuelable."
				};
			}

			var before = DescribeFuelState(flamethrower);
			var capacity = flamethrower.FuelCapacity;
			var targetFuel = Mathf.Clamp(fuel, 0f, capacity);
			FuelScaling.SetFuelLevel(refuelable, flamethrower, targetFuel);

			if (setAllowAutoRefuel)
				refuelable.allowAutoRefuel = allowAutoRefuel;
			if (setTargetFuelLevel)
				refuelable.TargetFuelLevel = Mathf.Clamp(targetFuelLevel < 0f ? targetFuel : targetFuelLevel, 0f, capacity);

			if (flamethrower.CanFireNow == false)
			{
				flamethrower.ClearManualTargetVisuals();
			}

			return new
			{
				success = true,
				pawn = DescribeThingBasic(pawn),
				before,
				after = DescribeFuelState(flamethrower)
			};
		}

		[Tool("zeflammenwerfer/probe_fire_line", Description = "Probe a Ze Flammenwerfer shot line and particle-collider blockers between a pawn and a target cell.")]
		public static object ProbeFireLine(
			[ToolParameter(Description = "Target x coordinate.")] int targetX,
			[ToolParameter(Description = "Target z coordinate.")] int targetZ,
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "Maximum Bresenham cells to return.", Required = false, DefaultValue = 60)] int maxCells = 60)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;
			if (TryGetCell(targetX, targetZ, out var targetCell, out error) == false)
				return error;

			var map = pawn.Map;
			maxCells = Mathf.Clamp(maxCells, 1, 200);
			var target = new LocalTargetInfo(targetCell);
			var verb = flamethrower.GetComp<CompEquippable>()?.PrimaryVerb;
			var canHit = verb?.CanHitTargetFrom(pawn.Position, target) ?? false;
			ShootLine shootLine = default;
			var hasShootLine = verb != null && verb.TryFindShootLineFromTo(pawn.Position, target, out shootLine, ignoreRange: false);
			var cells = GenSight.BresenhamCellsBetween(pawn.Position, targetCell)
				.Take(maxCells)
				.ToArray();
			var firstParticleBlocker = cells.FirstOrDefault(cell => map.thingGrid.MaxFillPercentFast(cell) >= 0.25f);

			return new
			{
				success = true,
				pawn = DescribeThingBasic(pawn),
				target = DescribeTarget(target),
				canHit,
				hasShootLine,
				shootLine = hasShootLine ? new
				{
					source = DescribeCell(shootLine.Source),
					dest = DescribeCell(shootLine.Dest)
				} : null,
				firstParticleBlocker = firstParticleBlocker.IsValid ? DescribeLineCell(map, firstParticleBlocker) : null,
				particleBlockerCount = cells.Count(cell => map.thingGrid.MaxFillPercentFast(cell) >= 0.25f),
				cells = cells.Select(cell => DescribeLineCell(map, cell)).ToArray()
			};
		}

		[Tool("zeflammenwerfer/get_flame_collision_state", Description = "Read Ze Flammenwerfer particle blocker colliders, pawn collision holders, and active flame projectiles for one shooter.")]
		public static object GetFlameCollisionState(
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "Maximum collider cells to return.", Required = false, DefaultValue = 80)] int maxCells = 80)
		{
			if (TryGetPawnAndWeapon(pawnId, out var pawn, out var flamethrower, out var error) == false)
				return error;

			var map = pawn.Map;
			maxCells = Mathf.Clamp(maxCells, 1, 300);
			PawnShooterTracker.trackers.TryGetValue(pawn, out var detector);
			detector?.Update(pawn);
			var comp = flamethrower.flameComp ?? flamethrower.TryGetComp<ZeFlameComp>();

			return new
			{
				success = true,
				pawn = DescribeThingBasic(pawn),
				tracker = detector == null ? null : new
				{
					exists = true,
					shooter = DescribeThingBasic(detector.shooter),
					gameObject = detector.go == null ? null : new
					{
						name = detector.go.name,
						activeSelf = detector.go.activeSelf,
						activeInHierarchy = detector.go.activeInHierarchy,
						layer = detector.go.layer
					},
					colliderCount = detector.colliders.Count,
					colliderCells = detector.colliders
						.OrderBy(pair => pawn.Position.DistanceToSquared(pair.Key))
						.ThenBy(pair => pair.Key.x)
						.ThenBy(pair => pair.Key.z)
						.Take(maxCells)
						.Select(pair => DescribeColliderCell(map, pair.Key, pair.Value))
						.ToArray()
				},
				global = new
				{
					shooterTrackerCount = PawnShooterTracker.trackers.Count,
					holderCount = ColliderHolder.holders.Count,
					flameCompCount = ZeFlameComp.allFlameComps.Count,
					particleSystemCount = ZeFlameComp.allParticleSystems.Count
				},
				pawnCollisionHolders = ColliderHolder.holders
					.Where(pair => pair.Key?.Map == map)
					.Take(50)
					.Select(pair => new
					{
						pawn = DescribeThingBasic(pair.Key),
						gameObject = pair.Value == null ? null : new
						{
							name = pair.Value.name,
							activeSelf = pair.Value.activeSelf,
							activeInHierarchy = pair.Value.activeInHierarchy,
							layer = pair.Value.layer
						}
					})
					.ToArray(),
				flameProjectiles = comp?.flames.Take(50).Select(DescribeFlameProjectile).ToArray() ?? Array.Empty<object>()
			};
		}

		[Tool("zeflammenwerfer/get_damage_state", Description = "Read fire, FireDamage, health, and hitpoint state for a pawn, thing, selected thing, or bounded cell area.")]
		public static object GetDamageState(
			[ToolParameter(Description = "Optional stable thing id such as Thing_Wall47012.", Required = false, DefaultValue = null)] string thingId = null,
			[ToolParameter(Description = "Optional stable pawn id such as Thing_Human776. Used when thingId is empty.", Required = false, DefaultValue = null)] string pawnId = null,
			[ToolParameter(Description = "Optional center x coordinate. Used when thingId and pawnId are empty.", Required = false, DefaultValue = -1)] int x = -1,
			[ToolParameter(Description = "Optional center z coordinate. Used when thingId and pawnId are empty.", Required = false, DefaultValue = -1)] int z = -1,
			[ToolParameter(Description = "Radius around x/z to inspect. Clamped to 0..12.", Required = false, DefaultValue = 0)] int radius = 0,
			[ToolParameter(Description = "Maximum things to return for cell-area reads.", Required = false, DefaultValue = 40)] int maxThings = 40)
		{
			var map = CurrentMap;
			if (map == null)
			{
				return new
				{
					success = false,
					error = "No current map is loaded."
				};
			}

			if (string.IsNullOrWhiteSpace(thingId) == false)
			{
				if (TryGetThing(thingId, out var thing, out var error) == false)
					return error;
				return new
				{
					success = true,
					mode = "thing",
					target = DescribeDamageThing(thing)
				};
			}

			if (string.IsNullOrWhiteSpace(pawnId) == false)
			{
				var pawn = FindPawn(pawnId);
				if (pawn == null)
				{
					return new
					{
						success = false,
						error = $"Pawn '{pawnId}' was not found on the current map."
					};
				}
				return new
				{
					success = true,
					mode = "pawn",
					target = DescribeDamageThing(pawn)
				};
			}

			if (x >= 0 || z >= 0)
			{
				if (TryGetCell(x, z, out var cell, out var error) == false)
					return error;
				radius = Mathf.Clamp(radius, 0, 12);
				maxThings = Mathf.Clamp(maxThings, 1, 200);
				var cells = GenRadial.RadialCellsAround(cell, radius, true)
					.Where(candidate => candidate.InBounds(map))
					.ToArray();
				var things = cells
					.SelectMany(candidate => map.thingGrid.ThingsListAtFast(candidate))
					.Distinct()
					.Take(maxThings)
					.Select(DescribeDamageThing)
					.ToArray();
				return new
				{
					success = true,
					mode = "cell-area",
					center = DescribeCell(cell),
					radius,
					cellCount = cells.Length,
					thingCount = things.Length,
					things
				};
			}

			var selected = Find.Selector.SingleSelectedThing;
			if (selected != null)
			{
				return new
				{
					success = true,
					mode = "selected",
					target = DescribeDamageThing(selected)
				};
			}

			return new
			{
				success = false,
				error = "Provide thingId, pawnId, x/z, or select one thing."
			};
		}

		[Tool("zeflammenwerfer/apply_flame_damage_probe", Description = "Apply the mod's flame-damage helper to a thing or cell for deterministic damage/fire tests, then return before/after state.")]
		public static object ApplyFlameDamageProbe(
			[ToolParameter(Description = "Flame/fire amount to apply.", Required = false, DefaultValue = 0.5f)] float amount = 0.5f,
			[ToolParameter(Description = "Optional stable thing id such as Thing_Wall47012. When supplied, applies to that thing if it has comps.", Required = false, DefaultValue = null)] string thingId = null,
			[ToolParameter(Description = "Optional target x coordinate. Used for cell fire when thingId is empty.", Required = false, DefaultValue = -1)] int x = -1,
			[ToolParameter(Description = "Optional target z coordinate. Used for cell fire when thingId is empty.", Required = false, DefaultValue = -1)] int z = -1,
			[ToolParameter(Description = "When true and x/z are used, apply flame damage to every ThingWithComps in the cell instead of only placing cell fire.", Required = false, DefaultValue = false)] bool applyToThingsInCell = false)
		{
			var map = CurrentMap;
			if (map == null)
			{
				return new
				{
					success = false,
					error = "No current map is loaded."
				};
			}

			amount = Mathf.Max(0.01f, amount);
			if (string.IsNullOrWhiteSpace(thingId) == false)
			{
				if (TryGetThing(thingId, out var thing, out var error) == false)
					return error;
				if (thing is not ThingWithComps thingWithComps)
				{
					return new
					{
						success = false,
						error = $"{thing.LabelCap} does not support ThingComp-based flame damage."
					};
				}

				var before = DescribeDamageThing(thingWithComps);
				Tools.ApplyFlameDamage(thingWithComps, amount);
				return new
				{
					success = true,
					mode = "thing",
					amount,
					before,
					after = DescribeDamageThing(thingWithComps)
				};
			}

			if (TryGetCell(x, z, out var cell, out var cellError) == false)
				return cellError;

			var beforeThings = map.thingGrid.ThingsListAtFast(cell).ToArray();
			var beforeCell = beforeThings.Select(DescribeDamageThing).ToArray();
			if (applyToThingsInCell)
			{
				foreach (var thingWithComps in beforeThings.OfType<ThingWithComps>().Where(thing => thing is not Fire).ToArray())
					Tools.ApplyFlameDamage(thingWithComps, amount);
			}
			else
			{
				Tools.ApplyCellFlame(map, amount, cell, beforeThings);
			}
			var after = map.thingGrid.ThingsListAtFast(cell).ToArray().Select(DescribeDamageThing).ToArray();
			return new
			{
				success = true,
				mode = applyToThingsInCell ? "cell-things" : "cell-fire",
				amount,
				cell = DescribeCell(cell),
				before = beforeCell,
				after
			};
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

			flamethrower.ClearManualTargetVisuals();

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
