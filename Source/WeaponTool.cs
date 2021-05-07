using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	[HarmonyPatch]
	public static class WeaponTool
	{
		static Vector3 location;
		static float angle;

		public static (Vector3, float) GetAimingCenter(Pawn pawn)
		{
			CalculateCenter(pawn.Drawer.renderer, pawn.DrawPos);
			return (location, angle);
		}

		public static bool IsFlipped(float aimAngle)
		{
			return aimAngle > 200f && aimAngle < 340f;
		}

		public static void DrawEquipmentAiming(PawnRenderer _1, Thing _2, Vector3 drawLoc, float aimAngle)
		{
			location = drawLoc;
			angle = aimAngle;
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
		[HarmonyDebug]
		public static void CalculateCenter(PawnRenderer _1, Vector3 _2)
		{
			IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var from = AccessTools.Method(typeof(PawnRenderer), "DrawEquipmentAiming");
				var to = SymbolExtensions.GetMethodInfo(() => DrawEquipmentAiming(default, default, default, default));
				return instructions.MethodReplacer(from, to);
			}

			// make compiler happy
			_ = Transpiler(default);
		}
	}
}
