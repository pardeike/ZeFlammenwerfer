using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
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
			var collider = ColliderHolder.Add(pawn).GetComponent<CapsuleCollider>();
			collider.name = pawn.ThingID;
			collider.direction = 1; // y-axis
			collider.height = 5f;
			collider.radius = 0.8f;
			collider.center = pawn.Position.ToVector3ShiftedWithAltitude(Tools.moteOverheadHeight);
		}

		public void UpdatedCenter(Pawn pawn, Vector3 center)
		{
			var go = ColliderHolder.Get(pawn);
			if (go == null)
				return;
			center.y = Tools.moteOverheadHeight;
			go.GetComponent<CapsuleCollider>().center = center;
		}

		public void RemovePawn(Pawn pawn)
		{
			ColliderHolder.Remove(pawn);
		}
	}
}
