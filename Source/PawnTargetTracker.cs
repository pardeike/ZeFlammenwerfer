using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class PawnTargetTracker : IPawnSubscriber
	{
		public void Prepare() { }
		public void NewPosition(Pawn pawn) { }

		public void ClearAll()
		{
			ColliderHolder.ClearAll();
		}

		public void NewPawn(Pawn pawn)
		{
			var collider = ColliderHolder.Get(pawn, true).GetComponent<CapsuleCollider>();
			collider.name = pawn.LabelCap;
			collider.direction = 1; // y-axis
			collider.height = 5f;
			collider.radius = 0.8f;
			collider.center = pawn.Position.ToVector3ShiftedWithAltitude(Tools.moteOverheadHeight);
		}

		public void UpdatedCenter(Pawn pawn, Vector3 center)
		{
			var collider = ColliderHolder.Get(pawn).GetComponent<CapsuleCollider>();
			if (collider.center.x != center.x || collider.center.z != center.z)
			{
				center.y = Tools.moteOverheadHeight;
				collider.center = center;
			}
		}

		public void RemovePawn(Pawn pawn)
		{
			ColliderHolder.Remove(pawn);
		}
	}
}
