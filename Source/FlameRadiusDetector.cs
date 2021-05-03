using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class FlameRadiusDetector
	{
		public Pawn shooter;
		public GameObject go;
		public static readonly int maxRadius = 8;
		public static readonly Dictionary<IntVec2, BoxCollider> colliders = new Dictionary<IntVec2, BoxCollider>();

		public FlameRadiusDetector(Pawn pawn)
		{
			shooter = pawn;

			go = new GameObject(pawn.LabelCap, typeof(ThingCollisionHandler)) { layer = Renderer.BlockerCullingLevel };
			go.GetComponent<ThingCollisionHandler>().flameRadiusDetector = this;
			Object.DontDestroyOnLoad(go);
		}

		public void Cleanup()
		{
			Object.Destroy(go);
			go = null;
			colliders.Clear();
		}

		public void Update(Pawn shooter)
		{
			this.shooter = shooter;
			if (shooter == null) return;
			var shooterVec2 = shooter.Position.ToIntVec2;

			var map = shooter.Map;
			if (map == null) return;

			if (shooter.Dead || shooter.Destroyed || shooter.Spawned == false) return;

			var thingGrid = map.thingGrid;

			var blockedCells = new HashSet<IntVec2>();
			if (shooter.HasFlameThrower())
				blockedCells = GenRadial.RadialCellsAround(shooter.Position, maxRadius, true)
					.Select(cell => cell.ToIntVec2)
					.Where(cell => cell != shooterVec2 && map.BlocksFlamethrower(cell))
					.ToHashSet();
			var oldCells = colliders.Keys.ToArray();
			oldCells.Do(cell =>
			{
				if (blockedCells.Contains(cell) == false)
				{
					var collider = colliders[cell];
					Object.Destroy(collider);
					_ = colliders.Remove(cell);
				}
			});
			blockedCells.Do(cell =>
			{
				var f = thingGrid.ThingsListAtFast(cell.ToIntVec3).MaxFillPercent() * 1.1f;

				if (colliders.TryGetValue(cell, out var collider) == false)
				{
					collider = go.AddComponent<BoxCollider>();
					collider.name = $"{cell.x}x{cell.z}";
					collider.size = new Vector3(f, 5f, f);
					collider.center = cell.ToIntVec3.ToVector3ShiftedWithAltitude(Tools.moteOverheadHeight);
					Log.Warning($"new {collider.center} {collider.size}");
					colliders[cell] = collider;
				}
				else if (collider.size.x != f || collider.size.z != f)
				{
					Log.Warning($"size {collider.center} {collider.size}");
					collider.size = new Vector3(f, 5f, f);
				}
			});
		}

		public bool AffectedByCells(Map map, IEnumerable<IntVec2> vec2s)
		{
			if (map != shooter.Map) return false;
			var pos = shooter.Position;
			var radius = maxRadius * maxRadius;
			return vec2s.Any(vec2 => pos.DistanceToSquared(vec2.ToIntVec3) <= radius);
		}
	}
}
