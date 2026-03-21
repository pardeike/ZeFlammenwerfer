using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	public static class WeaponTool
	{
		public static (Vector3, float) GetAimingCenter(Pawn pawn)
		{
			return TryGetAimingData(pawn, out var drawPos, out var aimAngle)
				? (drawPos, aimAngle)
				: (Vector3.negativeInfinity, int.MinValue);
		}

		public static bool IsAiming(Pawn pawn)
		{
			return TryGetAimingData(pawn, out _, out _);
		}

		public static bool IsFlipped(float aimAngle)
		{
			return aimAngle > 200f && aimAngle < 340f;
		}

		static bool TryGetAimingData(Pawn pawn, out Vector3 drawPos, out float aimAngle)
		{
			drawPos = Vector3.negativeInfinity;
			aimAngle = int.MinValue;

			if (pawn?.equipment?.Primary == null)
				return false;
			if (pawn.equipment.Primary is not ZeFlammenwerfer flamethrower)
				return false;
			if (flamethrower.CanFireNow == false)
				return false;

			var curJob = pawn.CurJob;
			if (curJob == null || curJob.def?.neverShowWeapon == true)
				return false;

			LocalTargetInfo target;
			if (flamethrower.HasManualTarget)
			{
				target = flamethrower.CurrentAimTarget;
			}
			else
			{
				if (pawn.stances?.curStance is not Stance_Busy stance_Busy || stance_Busy.neverAimWeapon || stance_Busy.focusTarg.IsValid == false)
					return false;
				target = stance_Busy.focusTarg;
			}

			if (target.IsValid == false)
				return false;

			var targetPos = target.HasThing ? target.Thing.DrawPos : target.Cell.ToVector3Shifted();
			var targetVector = targetPos - pawn.DrawPos;
			if (targetVector.MagnitudeHorizontalSquared() <= 0.001f)
				return false;

			aimAngle = targetVector.AngleFlat();
			var currentEffectiveVerb = pawn.CurrentEffectiveVerb;
			if (currentEffectiveVerb?.AimAngleOverride.HasValue == true)
				aimAngle = currentEffectiveVerb.AimAngleOverride.Value;

			var equipmentDrawDistanceFactor = pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
			drawPos = pawn.DrawPos
				+ new Vector3(0f, 0f, 0.4f + pawn.equipment.Primary.def.equippedDistanceOffset).RotatedBy(aimAngle)
				* equipmentDrawDistanceFactor;
			return true;
		}
	}
}
