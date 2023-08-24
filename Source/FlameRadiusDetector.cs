using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class FlameRadiusDetector : ICellBoolGiver
	{
		public Pawn shooter;
		public GameObject go;
		public static readonly int maxRadius = 9;
		public static readonly Dictionary<IntVec3, BoxCollider> colliders = new();

		public Color color = GenColor.RandomColorOpaque();
		public CellBoolDrawer currentMapDrawer;
		public Map currentDrawerMap;
		public bool currentMapDirty;

		public FlameRadiusDetector(Pawn pawn)
		{
			shooter = pawn;
			color = GenColor.RandomColorOpaque();

			go = new GameObject(pawn.ThingID, typeof(ThingCollisionHandler)) { layer = Renderer.BlockerCullingLevel };
			go.GetComponent<ThingCollisionHandler>().flameRadiusDetector = this;
			Object.DontDestroyOnLoad(go);
			Tools.Log($"SHOO ADD {shooter.ThingID}");
		}

		public void Cleanup()
		{
			Tools.Log($"SHOO DEL {shooter.ThingID}");
			Object.Destroy(go);
			go = null;
			colliders.Clear();
		}

		public void Update(Pawn shooter)
		{
			if (shooter.HasFlameThrower() == false && colliders.Count > 0)
			{
				foreach (var collider in colliders.Values)
				{
					Tools.Log($"CELL DEL {collider.name} {collider.center}");
					Object.Destroy(collider);
				}
				colliders.Clear();
				currentMapDirty = true;
				return;
			}

			Tools.Log($"UPDATE {shooter}");

			this.shooter = shooter;
			if (shooter == null)
				return;
			var shooterPosition = shooter.Position;

			var map = shooter.Map;
			if (map == null)
				return;

			if (shooter.Dead || shooter.Destroyed || shooter.Spawned == false)
				return;

			var thingGrid = map.thingGrid;

			var newCells = new Dictionary<IntVec3, float>();
			if (shooter.HasFlameThrower())
				newCells = GenRadial.RadialCellsAround(shooter.Position, maxRadius, true)
					.Where(cell => cell != shooterPosition && cell.InBounds(map))
					.Select(cell => (cell, fill: thingGrid.MaxFillPercentFast(cell)))
					.Where(pair => pair.fill >= 0.25f)
					.ToDictionary(pair => pair.cell, cell => cell.fill);
			var oldCells = colliders.Keys.ToArray();
			oldCells.Do(cell =>
			{
				if (newCells.ContainsKey(cell) == false)
				{
					var collider = colliders[cell];
					Tools.Log($"CELL DEL {collider.name} {collider.center}");
					Object.Destroy(collider);
					_ = colliders.Remove(cell);
					currentMapDirty = true;
				}
			});
			newCells.Do(pair =>
			{
				var cell = pair.Key;
				var f = pair.Value * 1.1f;

				if (colliders.TryGetValue(pair.Key, out var collider) == false)
				{
					collider = go.AddComponent<BoxCollider>();
					collider.name = $"{cell.x}x{cell.z}";
					collider.size = new Vector3(f, 5f, f);
					collider.center = cell.ToVector3ShiftedWithAltitude(Tools.moteOverheadHeight);
					Tools.Log($"CELL ADD {collider.name} {collider.center} f={f}");
					colliders[cell] = collider;
					currentMapDirty = true;
				}
				else if (collider.size.x != f || collider.size.z != f)
				{
					Tools.Log($"CELL SET {collider.name} {collider.center} f={f}");
					collider.size = new Vector3(f, 5f, f);
					currentMapDirty = true;
				}
			});
		}

		public bool AffectedByCells(Map map, IEnumerable<IntVec3> cells)
		{
			if (map != shooter.Map)
				return false;
			var pos = shooter.Position;
			var radius = maxRadius * maxRadius;
			return cells.Any(cell => pos.DistanceToSquared(cell) <= radius);
		}

		public Color Color => color;

		public bool GetCellBool(int index)
		{
			if (currentDrawerMap == null || currentDrawerMap.fogGrid.IsFogged(index))
				return false;

			var map = shooter.Map;
			if (shooter.Map != currentDrawerMap)
				return false;

			var cell = map.cellIndices.IndexToCell(index);
			return colliders.ContainsKey(cell);
		}

		public Color GetCellExtraColor(int index) => color;

		public void DrawerUpdate()
		{
			var map = Find.CurrentMap;
			if (currentDrawerMap != map)
			{
				currentMapDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3640, 0.5f);
				currentDrawerMap = map;
				currentMapDirty = true;
			}

			//var tickManager = Find.TickManager;
			if (currentMapDirty/* && (tickManager.TicksGame % 60 == 30 || tickManager.Paused)*/)
			{
				currentMapDirty = false;
				currentMapDrawer.SetDirty();
			}

			currentMapDrawer.CellBoolDrawerUpdate();
			currentMapDrawer.MarkForDraw();
		}
	}
}
