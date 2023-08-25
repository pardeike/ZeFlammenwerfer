using BansheeGz.BGSpline.Curve;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public static class Tools
	{
		public static readonly float moteOverheadHeight = AltitudeLayer.MoteOverheadLow.AltitudeFor();

		public static string GetAssetsPath(string folder, string fileName)
		{
			var root = LoadedModManager.GetMod<ZeFlammenwerferMain>()?.Content.RootDir ?? "";
			return Path.Combine(root, folder, fileName);
		}

		public static Vector3 WithHeight(this Vector3 vector, float height)
		{
			vector.y = height;
			return vector;
		}

		public static float MaxFillPercentFast(this ThingGrid thingGrid, IntVec3 cell)
		{
			var list = thingGrid.ThingsListAtFast(cell);
			var max = 0f;
			for (var i = 0; i < list.Count; i++)
			{
				var thing = list[i];
				if (thing is not Pawn)
				{
					var fillPercent = thing.def.fillPercent;
					if (fillPercent > max)
						max = fillPercent;
				}
			}
			return max;
		}

		public static bool HasFlameThrower(this Pawn pawn)
		{
			return pawn.equipment?.Primary?.def == Defs.ZeFlammenwerfer;
		}

		public static void AttachFire(ThingWithComps thing, float amount)
		{
			if (thing is not Pawn pawn || pawn.RaceProps.IsMechanoid == false)
			{
				thing.TryAttachFire(amount);
				return;
			}
			if (pawn.Destroyed || pawn.HasAttachment(ThingDefOf.Fire))
				return;

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
				if (thing.comps == null)
					thing.comps = new List<ThingComp>() { fireDamageComp };
				else
					thing.comps.Add(fireDamageComp);
			}

			AttachFire(thing, amount);
			fireDamageComp.Increase();

			thing.TryGetComp<CompAttachBase>()?.attachments?.OfType<Fire>().FirstOrDefault()?.DoComplexCalcs();
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

		public static IBGCurvePointI[] AddPoints(this BGCurve curve)
		{
			var p1 = new BGCurvePoint(curve, Vector3.zero, BGCurvePoint.ControlTypeEnum.BezierIndependant, Vector3.zero, Vector3.zero);
			var p2 = new BGCurvePoint(curve, Vector3.zero, BGCurvePoint.ControlTypeEnum.BezierIndependant, Vector3.zero, Vector3.zero);
			return new[] { curve.AddPoint(p1), curve.AddPoint(p2) };
		}
	}
}
