using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public static class ColliderHolder
	{
		public class RimWorldPawn : MonoBehaviour
		{
			public Pawn pawn;
		}

		public static readonly Dictionary<Pawn, GameObject> holders = new();

		public static void Register(Pawn pawn, Vector3 center)
		{
			var go = holders.TryGetValue(pawn);
			CapsuleCollider collider;
			if (go != null)
				collider = go.GetComponent<CapsuleCollider>();
			else
			{
				collider = Add(pawn).GetComponent<CapsuleCollider>();
				collider.name = pawn.ThingID;
				collider.direction = 1; // y-axis
				collider.height = 5f;
				collider.radius = 0.8f;
			}
			center.y = Tools.moteOverheadHeight;
			collider.center = center;
			//PawnShooterTracker.trackers.TryGetValue(pawn)?.SetDirty();
		}

		public static void Unregister(Pawn pawn) => Remove(pawn);

		static GameObject Add(Pawn pawn)
		{
			var go = new GameObject(pawn.ThingID, typeof(PawnCollisionHandler), typeof(CapsuleCollider)) { layer = Renderer.BlockerCullingLevel };
			go.AddComponent<RimWorldPawn>().pawn = pawn;
			Object.DontDestroyOnLoad(go);
			holders[pawn] = go;
			return go;
		}

		static void Remove(Pawn pawn)
		{
			if (holders.TryGetValue(pawn, out var holder))
			{
				Object.Destroy(holder);
				_ = holders.Remove(pawn);
			}
		}

		public static void ClearAll()
		{
			foreach (var pair in holders)
				Object.Destroy(pair.Value);
			holders.Clear();
		}
	}
}
