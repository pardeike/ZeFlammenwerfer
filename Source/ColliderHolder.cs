using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public static class ColliderHolder
	{
		public class RimWorldPawn : MonoBehaviour
		{
			public Pawn pawn;
		}

		public static readonly Dictionary<Pawn, GameObject> holders = new Dictionary<Pawn, GameObject>();

		public static GameObject Add(Pawn pawn)
		{
			var go = new GameObject(pawn.LabelCap, typeof(PawnCollisionHandler), typeof(CapsuleCollider)) { layer = Renderer.BlockerCullingLevel };
			go.AddComponent<RimWorldPawn>().pawn = pawn;
			Object.DontDestroyOnLoad(go);
			holders.Add(pawn, go);
			return go;
		}

		public static GameObject Get(Pawn pawn, bool create = false)
		{
			var go = holders.TryGetValue(pawn);
			if (create && go == null)
				go = Add(pawn);
			return go;
		}

		public static void Remove(Pawn pawn)
		{
			Object.Destroy(holders[pawn]);
			_ = holders.Remove(pawn);
		}

		public static void ClearAll()
		{
			foreach (var pair in holders)
				Object.Destroy(pair.Value);
			holders.Clear();
		}
	}
}
