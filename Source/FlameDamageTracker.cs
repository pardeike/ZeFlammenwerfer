using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public sealed class FlameDamageTracker : MapComponent
	{
		const float defaultMultiplier = 1f;
		const float maximumMultiplier = 100f;
		const float increaseFactor = 1.2f;
		const float decayFactor = 1.02f;
		const int cleanupIntervalTicks = 250;

		Dictionary<int, float> multipliers = new();

		public FlameDamageTracker(Map map) : base(map) { }

		public static void Increase(Thing thing)
		{
			var tracker = For(thing);
			if (tracker == null || TryGetThingId(thing, out var thingId) == false)
				return;

			tracker.multipliers.TryGetValue(thingId, out var multiplier);
			if (multiplier <= defaultMultiplier)
				multiplier = defaultMultiplier;
			tracker.multipliers[thingId] = Mathf.Min(maximumMultiplier, multiplier * increaseFactor);
		}

		public static float GetMultiplier(Thing thing)
		{
			return TryGetMultiplier(thing, out var multiplier) ? multiplier : defaultMultiplier;
		}

		public static bool TryGetMultiplier(Thing thing, out float multiplier)
		{
			multiplier = defaultMultiplier;
			var tracker = For(thing);
			if (tracker == null || TryGetThingId(thing, out var thingId) == false)
				return false;
			if (tracker.multipliers.TryGetValue(thingId, out multiplier) == false || multiplier <= defaultMultiplier)
			{
				multiplier = defaultMultiplier;
				return false;
			}
			return true;
		}

		public static bool IsTracked(Thing thing)
		{
			return TryGetMultiplier(thing, out _);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref multipliers, "multipliers", LookMode.Value, LookMode.Value);
			multipliers ??= new Dictionary<int, float>();
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();
			if (multipliers.Count == 0)
				return;

			var keys = multipliers.Keys.ToArray();
			for (var i = 0; i < keys.Length; i++)
			{
				var key = keys[i];
				var multiplier = Mathf.Max(defaultMultiplier, multipliers[key] / decayFactor);
				if (multiplier <= defaultMultiplier)
					multipliers.Remove(key);
				else
					multipliers[key] = multiplier;
			}

			if (Find.TickManager?.TicksGame % cleanupIntervalTicks == 0)
				RemoveMissingThings();
		}

		void RemoveMissingThings()
		{
			if (map?.listerThings == null || multipliers.Count == 0)
				return;

			var spawnedIds = new HashSet<int>(map.listerThings.AllThings.Select(thing => thing.thingIDNumber));
			foreach (var thingId in multipliers.Keys.ToArray())
				if (spawnedIds.Contains(thingId) == false)
					multipliers.Remove(thingId);
		}

		static FlameDamageTracker For(Thing thing)
		{
			return thing?.MapHeld?.GetComponent<FlameDamageTracker>();
		}

		static bool TryGetThingId(Thing thing, out int thingId)
		{
			thingId = thing?.thingIDNumber ?? 0;
			return thingId > 0;
		}
	}
}
