using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class FireDamage : ThingComp
	{
		public float multiplier = 1;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref multiplier, "multiplier", 1, false);
		}

		public void Increase()
		{
			multiplier = Mathf.Min(100f, multiplier * 1.2f);
		}

		public override void CompTick()
		{
			base.CompTick();
			multiplier = Mathf.Max(1f, multiplier / 1.02f);
		}
	}
}
