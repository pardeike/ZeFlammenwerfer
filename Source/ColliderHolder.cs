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

		public static readonly Dictionary<Pawn, GameObject> holders = new Dictionary<Pawn, GameObject>();

		public static GameObject Add(Pawn pawn)
		{
			var go = new GameObject(pawn.ThingID, typeof(PawnCollisionHandler), typeof(CapsuleCollider)) { layer = Renderer.BlockerCullingLevel };
			go.AddComponent<RimWorldPawn>().pawn = pawn;
			Object.DontDestroyOnLoad(go);
			if (holders.TryAdd(pawn, go))
			{
				Tools.Log($"PAWN ADD {pawn.ThingID}");
				return go;
			}
			else
			{
				Tools.Log($"PAWN DUP {pawn.ThingID}");
				Object.Destroy(go);
				return Get(pawn);
			}
		}

		public static GameObject Get(Pawn pawn)
		{
			return holders.TryGetValue(pawn);
		}

		public static void Remove(Pawn pawn)
		{
			if (holders.TryGetValue(pawn, out var holder))
			{
				Tools.Log($"PAWN DEL {holder.GetComponent<RimWorldPawn>().pawn.ThingID}");
				Object.Destroy(holder);
				_ = holders.Remove(pawn);
			}
		}

		public static void ClearAll()
		{
			foreach (var pair in holders)
			{
				Tools.Log($"PAWN DEL {pair.Value.GetComponent<RimWorldPawn>().pawn.ThingID}");
				Object.Destroy(pair.Value);
			}
			holders.Clear();
		}
	}
}
