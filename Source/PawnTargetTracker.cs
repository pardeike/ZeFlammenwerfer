using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class PawnTargetTracker : IPawnSubscriber
	{
		public void Prepare() { }
		public void UpdateCell(Pawn pawn) { }
		public void Equipped(Pawn pawn) { }
		public void Unequipped(Pawn pawn) { }

		public void ClearAll() => ColliderHolder.ClearAll();

		public void NewPawn(Pawn pawn)
		{
			if (PawnShooterTracker.InRange(pawn) == false)
				return;

			var center = pawn.Position.ToVector3ShiftedWithAltitude(Tools.moteOverheadHeight);
			ColliderHolder.Register(pawn, center);
		}

		public void UpdatedDrawPosition(Pawn pawn, Vector3 center)
		{
			if (PawnShooterTracker.InRange(pawn))
				ColliderHolder.Register(pawn, center);
			else
				ColliderHolder.Unregister(pawn);
		}

		public void RemovePawn(Pawn pawn) => ColliderHolder.Unregister(pawn);
	}
}