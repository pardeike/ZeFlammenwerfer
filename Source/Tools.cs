using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public static class Tools
	{
		public static readonly float moteOverheadHeight = AltitudeLayer.MoteOverheadLow.AltitudeFor();
		public static readonly AccessTools.FieldRef<ThingWithComps, List<ThingComp>> compsRef = AccessTools.FieldRefAccess<ThingWithComps, List<ThingComp>>("comps");
		public static readonly Action<Fire> d_DoComplexCalcs = AccessTools.MethodDelegate<Action<Fire>>(AccessTools.Method(typeof(Fire), "DoComplexCalcs"));

		public static Vector3 WithHeight(this Vector3 vector, float height)
		{
			vector.y = height;
			return vector;
		}

		public static bool BlocksFlamethrower(this Map map, IntVec2 vec2)
		{
			var cell = vec2.ToIntVec3;
			if (cell.InBounds(map) == false) return false;
			var things = map.thingGrid.ThingsListAt(cell).Where(t => (t as Pawn) == null);
			if (things.Count() == 0) return false;
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

		public static void ApplyFlameDamage(ThingWithComps thing, float amount)
		{
			var fireDamageComp = thing.GetComp<FireDamage>();
			if (fireDamageComp == null)
			{
				fireDamageComp = (FireDamage)Activator.CreateInstance(typeof(FireDamage));
				fireDamageComp.parent = thing;
				if (compsRef(thing) == null)
					compsRef(thing) = new List<ThingComp>() { fireDamageComp };
				else
					compsRef(thing).Add(fireDamageComp);

				thing.TryAttachFire(amount);
			}
			fireDamageComp.Increase();

			var compAttachBase = thing.TryGetComp<CompAttachBase>();
			var fire = compAttachBase?.attachments?.OfType<Fire>().FirstOrDefault();
			if (fire != null) d_DoComplexCalcs(fire);
		}

		public static void ApplyCellFlame(Map map, float amount, IntVec3 cell, IEnumerable<Thing> things)
		{
			var cellFire = things.OfType<Fire>().FirstOrDefault();
			if (cellFire == null)
			{
				cellFire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
				cellFire.fireSize = amount;
				_ = GenSpawn.Spawn(cellFire, cell, map, Rot4.North, WipeMode.Vanish, false);
			}
			else
			{
				if (cellFire.fireSize < amount)
					cellFire.fireSize = amount;
			}
		}
	}
}
