using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FlameThrower
{
	public static class Tools
	{
		public static bool BlocksFlamethrower(this Map map, IntVec3 cell)
		{
			if (cell.InBounds(map) == false) return false;
			var things = map.thingGrid.ThingsListAt(cell);
			if (things.Count() == 0) return false;
			if (things.OfType<Pawn>().Any()) return true;
			return things.Max(thing => thing.def.fillPercent) >= 0.25f;
		}

		public static float MaxFillPercent(this IEnumerable<Thing> things)
		{
			if (things.Count() == 0) return 0;
			return things.Max(thing => thing is Pawn ? 0.75f : thing.def.fillPercent);
		}

		public static bool HasFlameThrower(this Pawn pawn)
		{
			return pawn.equipment?.Primary?.def == Defs.Flamethrower;
		}
	}
}
