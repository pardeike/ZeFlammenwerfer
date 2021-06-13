using BansheeGz.BGSpline.Curve;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.AccessTools;

namespace ZeFlammenwerfer
{
	public static class Tools
	{
		public static readonly float moteOverheadHeight = AltitudeLayer.MoteOverheadLow.AltitudeFor();
		public static readonly FieldRef<ThingWithComps, List<ThingComp>> compsRef = FieldRefAccess<ThingWithComps, List<ThingComp>>("comps");
		public static readonly Action<Fire> d_DoComplexCalcs = MethodDelegate<Action<Fire>>(Method(typeof(Fire), "DoComplexCalcs"));

		public static string GetAssetsPath(string folder, string fileName)
		{
			var root = LoadedModManager.GetMod<ZeFlammenwerferMain>()?.Content.RootDir ?? "";
			return Path.Combine(root, folder, fileName);
		}

		public static void Log(string _)
		{
			// FileLog.Log(log);
		}

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
			return pawn.equipment?.Primary?.def == Defs.ZeFlammenwerfer;
		}

		public static void AttachFire(ThingWithComps thing, float amount)
		{
			if (!(thing is Pawn pawn) || pawn.RaceProps.IsMechanoid == false)
			{
				thing.TryAttachFire(amount);
				return;
			}
			if (pawn.Destroyed || pawn.HasAttachment(ThingDefOf.Fire)) return;

			var attachBase = pawn.TryGetComp<CompAttachBase>();
			if (attachBase != null)
			{
				var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire, null);
				fire.fireSize = amount;
				fire.AttachTo(pawn);
				_ = GenSpawn.Spawn(fire, pawn.Position, pawn.Map, Rot4.North, WipeMode.Vanish, false);
				pawn.records.Increment(RecordDefOf.TimesOnFire);
			}
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
			}

			AttachFire(thing, amount);
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

		public static BGCurvePointI[] AddPoints(this BGCurve curve)
		{
			var p1 = new BGCurvePoint(curve, Vector3.zero, BGCurvePoint.ControlTypeEnum.BezierIndependant, Vector3.zero, Vector3.zero);
			var p2 = new BGCurvePoint(curve, Vector3.zero, BGCurvePoint.ControlTypeEnum.BezierIndependant, Vector3.zero, Vector3.zero);
			return new[] { curve.AddPoint(p1), curve.AddPoint(p2) };
		}
	}
}
