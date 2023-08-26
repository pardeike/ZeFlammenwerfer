using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public interface IPawnSubscriber
	{
		void Prepare();
		void ClearAll();
		void NewPawn(Pawn pawn);
		void Equipped(Pawn pawn);
		void Unequipped(Pawn pawn);
		void UpdatedDrawPosition(Pawn pawn, Vector3 center);
		void UpdateCell(Pawn pawn);
		void RemovePawn(Pawn pawn);
	}

	[HarmonyPatch]
	public static class PawnExtension
	{
		public static readonly List<IPawnSubscriber> subscribers = new();

		public static void Subscribe(IPawnSubscriber sub)
		{
			sub.Prepare();
			subscribers.Add(sub);
		}

		public static void Unsubscribe(IPawnSubscriber sub)
		{
			_ = subscribers.Remove(sub);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
		public static void LoadGame()
		{
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].ClearAll();
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
		public static void SpawnSetup(Pawn __instance)
		{
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].NewPawn(__instance);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.Notify_Equipped))]
		public static void Notify_Equipped(ThingWithComps __instance, Pawn pawn)
		{
			if (pawn == null || __instance.def != Defs.ZeFlammenwerfer)
				return;
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].Equipped(pawn);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.Notify_Unequipped))]
		public static void Notify_Unequipped(Pawn __instance, Pawn pawn)
		{
			if (pawn == null || __instance.def != Defs.ZeFlammenwerfer)
				return;
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].Unequipped(pawn);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
		public static void FinalizeInit(Map __instance)
		{
			var pawns = __instance.mapPawns.AllPawnsSpawned;
			var m = pawns.Count;
			var n = subscribers.Count;
			for (var j = 0; j < m; j++)
				for (var i = 0; i < n; i++)
					subscribers[i].UpdateCell(pawns[j]);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Setter)]
		public static void Position(Thing __instance, IntVec3 value)
		{
			if (value == __instance.positionInt)
				return;
			if (__instance is not Pawn pawn || pawn.Map == null)
				return;
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].UpdateCell(pawn);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.PreDrawPosCalculation))]
		public static void PreDrawPosCalculation(Pawn ___pawn, Vector3 ___tweenedPos)
		{
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].UpdatedDrawPosition(___pawn, ___tweenedPos);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.ResetTweenedPosToRoot))]
		public static void ResetTweenedPosToRoot(Pawn ___pawn, Vector3 ___tweenedPos)
		{
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].UpdatedDrawPosition(___pawn, ___tweenedPos);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
		public static void DeSpawn(Pawn __instance)
		{
			var n = subscribers.Count;
			for (var i = 0; i < n; i++)
				subscribers[i].RemovePawn(__instance);
		}
	}
}
