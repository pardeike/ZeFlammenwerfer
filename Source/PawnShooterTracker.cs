using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class PawnShooterTracker : IPawnSubscriber
	{
		public static readonly Dictionary<Pawn, FlameRadiusDetector> pawns = new Dictionary<Pawn, FlameRadiusDetector>();

		public void Prepare() { }
		public void UpdatedCenter(Pawn pawn, Vector3 center) { }

		public void ClearAll()
		{
			foreach (var pair in pawns)
				pair.Value.Cleanup();
			pawns.Clear();
		}

		public void NewPawn(Pawn pawn)
		{
			if (pawn.HasFlameThrower() == false) return;
			var detector = pawns.TryGetValue(pawn);
			if (detector == null)
			{
				detector = new FlameRadiusDetector(pawn);
				pawns[pawn] = detector;
			}
			detector.Update(pawn);
		}

		public void NewPosition(Pawn pawn)
		{
			if (pawns.TryGetValue(pawn, out var detector))
				detector.Update(pawn);

		}

		public void RemovePawn(Pawn pawn)
		{
			if (pawns.TryGetValue(pawn, out var detector))
			{
				detector.Cleanup();
				_ = pawns.Remove(pawn);
			}
		}

		// extra

		public static void RemoveThing(Map map, IEnumerable<IntVec2> vec2s)
		{
			pawns
				.Where(pair => pair.Value.AffectedByCells(map, vec2s))
				.Do(pair => pair.Value.Update(pair.Key));
		}
	}
}
