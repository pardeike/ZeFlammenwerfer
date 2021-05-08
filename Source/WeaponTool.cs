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
			location = Vector3.negativeInfinity;
			angle = int.MinValue;
			CalculateCenter(pawn.Drawer.renderer, pawn.DrawPos);
			return (location, angle);
		}

		public static bool IsAiming(Pawn pawn)
		{
			angle = int.MinValue;
			CheckIsAiming(pawn.Drawer.renderer, pawn.DrawPos);
			return angle != int.MinValue;
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

		public static void DoNothing(PawnRenderer _1, Thing _2, Vector3 _3, float _4)
		{
		}

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
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

		[HarmonyReversePatch(HarmonyReversePatchType.Original)]
		[HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
		public static void CheckIsAiming(PawnRenderer _1, Vector3 _2)
		{
			IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var from = AccessTools.Method(typeof(PawnRenderer), "DrawEquipmentAiming");
				var to1 = SymbolExtensions.GetMethodInfo(() => DrawEquipmentAiming(default, default, default, default));
				var to2 = SymbolExtensions.GetMethodInfo(() => DoNothing(default, default, default, default));
				var firstTime = true;
				foreach (var instruction in instructions)
				{
					if (instruction.Calls(from))
					{
						instruction.operand = firstTime ? to1 : to2;
						firstTime = false;
					}
					yield return instruction;
				}
			}

			// make compiler happy
			_ = Transpiler(default);
		}
	}
}
