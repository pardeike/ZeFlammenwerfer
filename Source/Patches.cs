using HarmonyLib;
using System.Linq;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	[HarmonyPatch(typeof(Map))]
	[HarmonyPatch(nameof(Map.FinalizeInit))]
	public static class Map_FinalizeInit_Patch
	{
		public static GameObject fire;
		public static GameObject smoke;
		public static Pawn owner;
		public static Pawn target;

		public static void Postfix(Map __instance)
		{
			var pos = __instance.Center.ToVector3();

			fire = FlameThrower.CreateFire();
			fire.transform.localScale = new Vector3(5f, 0, 5f);

			smoke = FlameThrower.CreateSmoke();
			smoke.transform.localScale = new Vector3(5f, 0, 5f);

			owner = __instance.mapPawns.AllPawnsSpawned.First();
			target = __instance.mapPawns.AllPawnsSpawned.Skip(1).First();
		}
	}

	[HarmonyPatch(typeof(Map))]
	[HarmonyPatch(nameof(Map.MapPostTick))]
	public static class Map_MapPostTick_Patch
	{
		public static void Postfix()
		{
			var pos = Map_FinalizeInit_Patch.owner.DrawPos;
			pos.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
			Map_FinalizeInit_Patch.fire.transform.position = pos;
			Map_FinalizeInit_Patch.smoke.transform.position = pos;

			var q = Quaternion.LookRotation(Map_FinalizeInit_Patch.target.DrawPos - Map_FinalizeInit_Patch.owner.DrawPos);
			Log.Warning("" + q);
			Map_FinalizeInit_Patch.fire.transform.rotation = q;
			Map_FinalizeInit_Patch.smoke.transform.rotation = q;
		}
	}
}
