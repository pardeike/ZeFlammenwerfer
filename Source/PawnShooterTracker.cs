﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class PawnShooterTracker : IPawnSubscriber
	{
		public static readonly Dictionary<Pawn, FlameRadiusDetector> trackers = new();

		public void Prepare() { }
		public void UpdatedDrawPosition(Pawn pawn, Vector3 center) { }

		public void ClearAll()
		{
			foreach (var pair in trackers)
				pair.Value.Cleanup();
			trackers.Clear();
		}

		void RegisterPawn(Pawn pawn)
		{
			if (pawn.NonHumanlikeOrWildMan() || pawn.HasFlameThrower() == false)
				return;

			var detector = trackers.TryGetValue(pawn);
			if (detector == null)
			{
				detector = new FlameRadiusDetector(pawn);
				trackers[pawn] = detector;
			}
			detector.Update(pawn);
		}

		void UnregisterPawn(Pawn pawn)
		{
			if (trackers.TryGetValue(pawn, out var detector))
			{
				detector.Cleanup();
				_ = trackers.Remove(pawn);
			}
		}

		public void NewPawn(Pawn pawn) => RegisterPawn(pawn);
		public void Equipped(Pawn pawn) => RegisterPawn(pawn);
		public void Unequipped(Pawn pawn) => UnregisterPawn(pawn);
		public void RemovePawn(Pawn pawn) => UnregisterPawn(pawn);

		public void UpdateCell(Pawn pawn)
		{
			if (trackers.TryGetValue(pawn, out var detector))
				detector.Update(pawn);
		}

		public static void Update(Map map, IEnumerable<IntVec3> cells)
		{
			trackers.DoIf(pair => pair.Value.AffectedByCells(map, cells), pair => pair.Value.Update(pair.Key));
		}

		public static bool InRange(Pawn pawn)
		{
			var map = pawn.Map;
			var position = pawn.drawer.tweener.tweenedPos;
			return trackers.Keys
				.AsParallel()
				.Any(
					shooter => shooter.Map == map &&
					(shooter.drawer.tweener.tweenedPos - position).MagnitudeHorizontalSquared() <= FlameRadiusDetector.maxRadiusSquared
				);
		}
	}
}