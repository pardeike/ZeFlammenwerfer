using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class TargetScript : MonoBehaviour
	{
		public FireBlocker blocker;
		public List<ParticleCollisionEvent> collisionEvents;

		public void Start()
		{
			collisionEvents = new List<ParticleCollisionEvent>();
		}

		static void TryAttachFire(Thing t, float fireSize)
		{
			if (!t.CanEverAttachFire()) return;
			var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire, null);
			fire.fireSize = fireSize;
			fire.AttachTo(t);
			_ = GenSpawn.Spawn(fire, t.Position, t.Map, Rot4.North, WipeMode.Vanish, false);
			if (t is Pawn pawn)
			{
				if (pawn.IsColonist) pawn.jobs.StopAll(false, true);
				pawn.records.Increment(RecordDefOf.TimesOnFire);
			}
		}

		public void OnParticleCollision(GameObject system)
		{
			var pawn = blocker?.pawn;
			var map = pawn?.Map;
			if (map == null) return;

			var particleSystem = system.GetComponent<ParticleSystem>();
			_ = particleSystem.GetCollisionEvents(gameObject, collisionEvents);
			foreach (var collision in collisionEvents)
			{
				var p = (collision.colliderComponent as BoxCollider).center;
				var cell = new IntVec3((int)p.x, 0, (int)p.z);
				var v = collision.velocity;
				var skill = Mathf.Clamp(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, 0, 20);
				var amount = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z)) / (11 - skill / 2);
				if (cell.InBounds(map))
				{
					var things = map.thingGrid.ThingsAt(cell);

					things.OfType<Pawn>().Do(targetPawn =>
					{
						var attachBase = targetPawn.TryGetComp<CompAttachBase>();
						if (attachBase?.attachments == null) return;
						if (targetPawn.FlammableNow == false) return;
						if (attachBase.attachments.Count(at => at.def == ThingDefOf.Fire) >= 8) return;

						var fire1 = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
						fire1.fireSize = amount * 10;
						fire1.AttachTo(targetPawn);

						targetPawn.jobs.StopAll(false, true);
						targetPawn.records.Increment(RecordDefOf.TimesOnFire);
					});

					if (things.Count(t => t.def == ThingDefOf.Fire) < 8)
					{
						var fire2 = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
						fire2.fireSize = amount;
						_ = GenSpawn.Spawn(fire2, cell, map, Rot4.North, WipeMode.Vanish, false);
					}
				}
			}
		}
	}

	public static class FirePawnTracker
	{
		public static readonly Dictionary<Pawn, FireBlocker> pawns = new Dictionary<Pawn, FireBlocker>();

		public static void UpdateShooter(Pawn pawn)
		{
			var blocker = pawns.TryGetValue(pawn);
			if (blocker == null)
			{
				blocker = new FireBlocker(pawn);
				pawns[pawn] = blocker;
			}
			blocker.Update(pawn);
		}

		public static void UpdateFromTarget(Thing thing)
		{
			var cell = thing.Position;
			pawns.DoIf(pair => pair.Key != thing && pair.Value.Contains(cell), pair => pair.Value.Update(pair.Key));
		}

		public static void Remove(Map map, IntVec3 cell)
		{
			if (map == null) return;
			if (map.BlocksFlamethrower(cell))
				pawns.DoIf(pair => pair.Key.Map == map, pair => pair.Value.Remove(cell));
		}

		public static void Remove(Pawn pawn)
		{
			var blocker = pawns.TryGetValue(pawn);
			if (blocker == null) return;
			blocker.Cleanup();
			_ = pawns.Remove(pawn);
		}
	}

	public class FireBlocker
	{
		public Pawn pawn;
		public GameObject blockerBase;
		public static readonly float moteOverheadHeight = AltitudeLayer.MoteOverheadLow.AltitudeFor();
		public static readonly int maxRadius = 8;
		public static readonly Dictionary<IntVec3, BoxCollider> blockers = new Dictionary<IntVec3, BoxCollider>();

		public FireBlocker(Pawn pawn)
		{
			this.pawn = pawn;
			var obj = new GameObject() { layer = Renderer.BlockerCullingLevel };
			_ = obj.AddComponent(typeof(TargetScript)) as TargetScript;
			blockerBase = UnityEngine.Object.Instantiate(obj);
			UnityEngine.Object.DontDestroyOnLoad(blockerBase);
			var script2 = blockerBase.GetComponent<TargetScript>();
			script2.blocker = this;
		}

		public void Cleanup()
		{
			UnityEngine.Object.Destroy(blockerBase);
			blockerBase = null;
			blockers.Clear();
		}

		public bool Contains(IntVec3 cell) => pawn.Position.DistanceTo(cell) <= maxRadius;

		public void Update(Pawn pawn)
		{
			this.pawn = pawn;
			var map = pawn.Map;
			var thingGrid = map.thingGrid;
			var blockedCells = GenRadial.RadialCellsAround(pawn.Position, maxRadius, true)
				.Where(cell => cell != pawn.Position && map.BlocksFlamethrower(cell))
				.ToHashSet();
			var oldCells = blockers.Keys.ToArray();
			oldCells.Do(cell =>
			{
				if (blockedCells.Contains(cell) == false)
				{
					var collider = blockers[cell];
					UnityEngine.Object.Destroy(collider);
					_ = blockers.Remove(cell);
				}
			});
			blockedCells.Do(cell =>
			{
				var pos = cell.ToVector3() + new Vector3(0.5f, 0, 0.5f);
				pos.y = moteOverheadHeight;

				if (blockers.TryGetValue(cell, out var blocker) == false)
				{
					blocker = blockerBase.AddComponent<BoxCollider>();
					blocker.center = pos;
					blockers[cell] = blocker;
				}

				var f = thingGrid.ThingsListAtFast(cell).MaxFillPercent() * 1.25f;
				blocker.size = new Vector3(f, 5f, f);
			});
		}

		public void Remove(IntVec3 cell)
		{
			if (blockers.TryGetValue(cell, out var collider))
			{
				UnityEngine.Object.Destroy(collider);
				_ = blockers.Remove(cell);
			}
		}
	}
}
