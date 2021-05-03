using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class FireDamage : ThingComp
	{
		public float multiplier = 1;

		public override void PostExposeData()
		{
			Scribe_Values.Look(ref multiplier, "multiplier", 1, false);
		}

		public void Increase()
		{
			multiplier = Mathf.Min(100f, multiplier * 1.2f);
		}

		public override void CompTick()
		{
			multiplier = Mathf.Max(1f, multiplier / 1.02f);
		}
	}
}
