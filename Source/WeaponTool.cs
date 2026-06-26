using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZeFlammenwerfer
{
	public static class WeaponTool
	{
		public sealed class AimingData
		{
			public Vector3 DrawPos;
			public Vector3 TargetPos;
			public LocalTargetInfo Target;
			public float AimAngle;
			public bool FromManualTarget;
		}

		public static (Vector3, float) GetAimingCenter(Pawn pawn)
		{
			return TryGetAimingData(pawn, pawn?.DrawPos ?? Vector3.zero, PawnRenderFlags.None, out var data, includeRecoil: true)
				? (data.DrawPos, data.AimAngle)
				: (Vector3.negativeInfinity, int.MinValue);
		}

		public static bool IsAiming(Pawn pawn)
		{
			return TryGetAimingData(pawn, pawn?.DrawPos ?? Vector3.zero, PawnRenderFlags.None, out _, includeRecoil: false);
		}

		public static bool IsFlipped(float aimAngle)
		{
			return aimAngle > 200f && aimAngle < 340f;
		}

		public static bool TryGetAimingData(Pawn pawn, Vector3 equipmentBaseDrawPos, PawnRenderFlags flags, out AimingData data, bool includeRecoil)
		{
			data = null;

			if (pawn?.equipment?.Primary == null)
				return false;
			if (pawn.equipment.Primary is not ZeFlammenwerfer flamethrower)
				return false;
			if (flamethrower.CanFireNow == false)
				return false;
			if (flags.HasFlag(PawnRenderFlags.NeverAimWeapon))
				return false;

			var curJob = pawn.CurJob;
			if (curJob?.def?.neverShowWeapon == true)
				return false;

			LocalTargetInfo target;
			if (flamethrower.HasManualTarget)
			{
				target = flamethrower.CurrentAimTarget;
			}
			else
			{
				if (curJob == null)
					return false;
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

			var aimAngle = targetVector.AngleFlat();
			var currentEffectiveVerb = pawn.CurrentEffectiveVerb;
			if (currentEffectiveVerb?.AimAngleOverride.HasValue == true)
				aimAngle = currentEffectiveVerb.AimAngleOverride.Value;

			var equipmentDrawDistanceFactor = pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
			var drawPos = equipmentBaseDrawPos
				+ new Vector3(0f, 0f, 0.4f + pawn.equipment.Primary.def.equippedDistanceOffset).RotatedBy(aimAngle)
				* equipmentDrawDistanceFactor;
			if (includeRecoil && pawn.equipment.Primary.TryGetComp<CompEquippable>() is { } compEquippable)
			{
				EquipmentUtility.Recoil(pawn.equipment.Primary.def, EquipmentUtility.GetRecoilVerb(compEquippable.AllVerbs), out var drawOffset, out _, aimAngle);
				drawPos += drawOffset;
			}

			data = new AimingData
			{
				DrawPos = drawPos,
				TargetPos = targetPos,
				Target = target,
				AimAngle = aimAngle,
				FromManualTarget = flamethrower.HasManualTarget
			};
			return true;
		}
	}
}
