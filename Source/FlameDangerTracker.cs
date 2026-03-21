using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	static class FlameDangerTracker
	{
		const bool renderDebugCells = false;
		const ushort flameCoreCellCost = 10000;
		const ushort dangerousCellCost = 600;
		const float blockerFillPercent = 0.25f;
		const int repathProbeCellCount = 3;
		static readonly Color debugDangerCellColor = new Color(0.1f, 0.45f, 1f, 0.2f);

		public static bool RenderDebugCellsEnabled => renderDebugCells;

		sealed class ShooterState
		{
			public Map map;
			public Dictionary<int, ushort> cells = new();
		}

		sealed class MapState : IDisposable
		{
			public readonly Map map;
			public readonly int[] softRefCounts;
			public readonly int[] hardRefCounts;
			public readonly NativeArray<ushort> routeCosts;
			public readonly HashSet<int> activeCells = new();
			public int activeCellCount;

			public MapState(Map map)
			{
				this.map = map;
				softRefCounts = new int[map.cellIndices.NumGridCells];
				hardRefCounts = new int[map.cellIndices.NumGridCells];
				routeCosts = new NativeArray<ushort>(map.cellIndices.NumGridCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
			}

			public void Dispose()
			{
				if (routeCosts.IsCreated)
					routeCosts.Dispose();
			}
		}

		static readonly Dictionary<Pawn, ShooterState> shooterStates = new();
		static readonly Dictionary<Map, MapState> mapStates = new();

		public static bool TryGetRouteCostGrid(Map map, out NativeArray<ushort>.ReadOnly grid)
		{
			if (map != null && mapStates.TryGetValue(map, out var state) && state.activeCellCount > 0)
			{
				grid = state.routeCosts.AsReadOnly();
				return true;
			}

			grid = default;
			return false;
		}

		public static void ResetPathIfUpcomingDanger(Pawn_PathFollower pather)
		{
			if (pather?.pawn?.Map == null || pather.Moving == false || pather.Destination.IsValid == false)
				return;
			if (ShouldIgnorePathDanger(pather.pawn))
				return;
			if (UpcomingPathContainsDanger(pather) == false)
				return;

			pather.ResetToCurrentPosition();
		}

		public static void DrawDebugCells(Map map)
		{
			if (renderDebugCells == false || map == null)
				return;
			if (mapStates.TryGetValue(map, out var state) == false || state.activeCellCount <= 0)
				return;

			var debugDangerCellMaterial = SolidColorMaterials.SimpleSolidColorMaterial(debugDangerCellColor);
			var cellIndices = map.cellIndices;
			foreach (var cellIndex in state.activeCells)
				CellRenderer.RenderCell(cellIndices.IndexToCell(cellIndex), debugDangerCellMaterial);
		}

		public static bool ShouldIgnorePathDanger(Pawn pawn)
		{
			return pawn?.equipment?.Primary is ZeFlammenwerfer flamethrower
				&& (flamethrower.HasManualTarget || (flamethrower.flameComp?.isActive == true && flamethrower.CurrentAimTarget.IsValid));
		}

		public static void ClearAll()
		{
			var shooters = shooterStates.Keys.ToArray();
			for (var i = 0; i < shooters.Length; i++)
				Clear(shooters[i]);

			foreach (var state in mapStates.Values.ToArray())
				state.Dispose();

			shooterStates.Clear();
			mapStates.Clear();
		}

		public static void Clear(Pawn shooter)
		{
			if (shooter == null)
				return;
			if (shooterStates.TryGetValue(shooter, out var state) == false)
				return;

			ApplyDelta(state.map, state.cells, remove: true);
			shooterStates.Remove(shooter);
		}

		public static void Update(Pawn shooter, LocalTargetInfo target)
		{
			if (shooter == null || shooter.Spawned == false || shooter.Map == null || target.IsValid == false)
			{
				Clear(shooter);
				return;
			}

			var newCells = BuildDangerCells(shooter, target.Cell);
			if (newCells.Count == 0)
			{
				Clear(shooter);
				return;
			}

			if (shooterStates.TryGetValue(shooter, out var state) == false)
			{
				state = new ShooterState();
				shooterStates[shooter] = state;
			}

			if (state.map != null && state.map != shooter.Map)
			{
				ApplyDelta(state.map, state.cells, remove: true);
				state.cells.Clear();
			}

			state.map = shooter.Map;

			var removed = state.cells
				.Where(entry => newCells.TryGetValue(entry.Key, out var cost) == false || cost != entry.Value)
				.ToArray();
			var added = newCells
				.Where(entry => state.cells.TryGetValue(entry.Key, out var cost) == false || cost != entry.Value)
				.ToArray();

			ApplyDelta(state.map, removed, remove: true);
			ApplyDelta(state.map, added, remove: false);

			state.cells.Clear();
			foreach (var entry in newCells)
				state.cells.Add(entry.Key, entry.Value);
		}

		static Dictionary<int, ushort> BuildDangerCells(Pawn shooter, IntVec3 targetCell)
		{
			var map = shooter.Map;
			var result = new Dictionary<int, ushort>();
			if (map == null)
				return result;
			var cellIndices = map.cellIndices;

			foreach (var cell in GenSight.BresenhamCellsBetween(shooter.Position, targetCell))
			{
				if (cell.InBounds(map) == false)
					continue;
				if (cell == shooter.Position)
					continue;

				AddDangerCost(result, cellIndices.CellToIndex(cell), flameCoreCellCost);

				foreach (var adjacent in GenAdj.AdjacentCellsAndInside)
				{
					var dangerousCell = cell + adjacent;
					if (dangerousCell.InBounds(map))
						AddDangerCost(result, cellIndices.CellToIndex(dangerousCell), dangerousCellCost);
				}

				var blocksFlame = cell.GetEdifice(map) != null || map.thingGrid.MaxFillPercentFast(cell) >= blockerFillPercent;
				if (blocksFlame)
					break;
			}

			result.Remove(cellIndices.CellToIndex(shooter.Position));
			return result;
		}

		static void AddDangerCost(Dictionary<int, ushort> cells, int cellIndex, ushort cost)
		{
			if (cells.TryGetValue(cellIndex, out var existingCost) && existingCost >= cost)
				return;
			cells[cellIndex] = cost;
		}

		static void ApplyDelta(Map map, IEnumerable<KeyValuePair<int, ushort>> cells, bool remove)
		{
			if (map == null)
				return;
			if (mapStates.TryGetValue(map, out var state) == false)
			{
				if (remove)
					return;
					state = new MapState(map);
					mapStates[map] = state;
			}
			var softRefCounts = state.softRefCounts;
			var hardRefCounts = state.hardRefCounts;
			var routeCosts = state.routeCosts;
			var cellIndices = map.cellIndices;

			foreach (var entry in cells)
			{
				var cellIndex = entry.Key;
				if ((uint)cellIndex >= (uint)softRefCounts.Length)
					continue;

				var isHard = entry.Value >= flameCoreCellCost;
				var wasActive = hardRefCounts[cellIndex] > 0 || softRefCounts[cellIndex] > 0;

				if (isHard)
				{
					var currentCount = hardRefCounts[cellIndex];
					hardRefCounts[cellIndex] = remove ? Mathf.Max(0, currentCount - 1) : currentCount + 1;
				}
				else
				{
					var currentCount = softRefCounts[cellIndex];
					softRefCounts[cellIndex] = remove ? Mathf.Max(0, currentCount - 1) : currentCount + 1;
				}

				var isActive = hardRefCounts[cellIndex] > 0 || softRefCounts[cellIndex] > 0;
				if (wasActive == false && isActive)
					state.activeCellCount++;
				else if (wasActive && isActive == false)
					state.activeCellCount--;

				if (hardRefCounts[cellIndex] > 0)
				{
					routeCosts[cellIndex] = flameCoreCellCost;
					state.activeCells.Add(cellIndex);
				}
				else if (softRefCounts[cellIndex] > 0)
				{
					routeCosts[cellIndex] = dangerousCellCost;
					state.activeCells.Add(cellIndex);
				}
				else
				{
					routeCosts[cellIndex] = 0;
					state.activeCells.Remove(cellIndex);
				}

				map.pathing?.RecalculatePerceivedPathCostAt(cellIndices.IndexToCell(cellIndex));
			}

			if (state.activeCellCount == 0)
			{
				state.Dispose();
				mapStates.Remove(map);
			}
		}

		static bool UpcomingPathContainsDanger(Pawn_PathFollower pather)
		{
			if (pather?.pawn?.Map == null)
				return false;
			if (mapStates.TryGetValue(pather.pawn.Map, out var state) == false || state.activeCellCount <= 0)
				return false;

			var cellIndices = pather.pawn.Map.cellIndices;
			if (CellIsDangerous(state, cellIndices, pather.nextCell))
				return true;

			var path = pather.curPath;
			if (path == null || path.Finished)
				return false;

			var additionalProbeCount = Mathf.Min(Mathf.Max(path.NodesLeftCount - 1, 0), repathProbeCellCount - 1);
			if (additionalProbeCount <= 0)
				return false;

			var upcomingCells = path.PeekNextCells(additionalProbeCount, 1);
			for (var i = 0; i < upcomingCells.Count; i++)
			{
				if (CellIsDangerous(state, cellIndices, upcomingCells[i]))
					return true;
			}

			return false;
		}

		static bool CellIsDangerous(MapState state, CellIndices cellIndices, IntVec3 cell)
		{
			if (cell.IsValid == false || cell.InBounds(state.map) == false)
				return false;
			return state.routeCosts[cellIndices.CellToIndex(cell)] > 0;
		}
	}
}
