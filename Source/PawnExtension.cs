using HarmonyLib;
using System;
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
		void UpdatedCenter(Pawn pawn, Vector3 center);
		void NewPosition(Pawn pawn);
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
			subscribers.ForEach((sub) => sub.ClearAll());
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PawnTweener), MethodType.Constructor, new Type[] { typeof(Pawn) })]
		public static void Constructor(Pawn pawn)
		{
			subscribers.ForEach((sub) => sub.NewPawn(pawn));
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
		public static void FinalizeInit(Map __instance)
		{
			foreach (var pawn in __instance.mapPawns.AllPawnsSpawned)
				subscribers.ForEach((sub) => sub.NewPosition(pawn));
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.PreDrawPosCalculation))]
		public static void PreDrawPosCalculation(Pawn ___pawn, Vector3 ___tweenedPos)
		{
			subscribers.ForEach((sub) => sub.UpdatedCenter(___pawn, ___tweenedPos));
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PawnTweener), nameof(PawnTweener.ResetTweenedPosToRoot))]
		public static void ResetTweenedPosToRoot(Pawn ___pawn, Vector3 ___tweenedPos)
		{
			subscribers.ForEach((sub) => sub.UpdatedCenter(___pawn, ___tweenedPos));
		}

		[HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Setter)]
		public static void Position(Thing __instance)
		{
			if (__instance is Pawn pawn && pawn.Map != null)
				subscribers.ForEach((sub) => sub.NewPosition(pawn));
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
		public static void DeSpawn(Pawn __instance)
		{
			subscribers.ForEach((sub) => sub.RemovePawn(__instance));
		}
	}
}
