using HarmonyLib;
using RimWorld;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public static class FuelScaling
	{
		const float defaultMinimumFuelCapacity = 50f;
		const float defaultMaximumFuelCapacity = 1000f;
		const float defaultMinimumFuelConsumption = 1f;
			const float defaultMaximumFuelConsumption = 20f;
		const float maximumShootingSkill = 20f;

		static readonly FieldInfo fuelField = AccessTools.Field(typeof(CompRefuelable), "fuel");
		static readonly FieldInfo configuredTargetFuelLevelField = AccessTools.Field(typeof(CompRefuelable), "configuredTargetFuelLevel");

		public static bool TryGetFlamethrower(CompRefuelable refuelable, out ZeFlammenwerfer flamethrower)
		{
			flamethrower = refuelable?.parent as ZeFlammenwerfer;
			return flamethrower != null;
		}

		public static float CapacityFor(ZeFlammenwerfer flamethrower)
		{
			var props = flamethrower?.FlameProps;
			var min = Mathf.Max(0f, props?.minimumFuelCapacity ?? defaultMinimumFuelCapacity);
			var max = Mathf.Max(0f, props?.maximumFuelCapacity ?? defaultMaximumFuelCapacity);
			NormalizeRange(ref min, ref max);

			var quality = flamethrower?.TryGetComp<CompQuality>()?.Quality ?? QualityCategory.Normal;
			return Mathf.Clamp(max * CapacityFactor(quality), min, max);
		}

		public static float ConsumptionFor(ZeFlammenwerfer flamethrower)
		{
			var props = flamethrower?.FlameProps;
			var min = Mathf.Max(0f, props?.minimumFuelConsumption ?? defaultMinimumFuelConsumption);
			var max = Mathf.Max(0f, props?.maximumFuelConsumption ?? defaultMaximumFuelConsumption);
			NormalizeRange(ref min, ref max);

			var skill = Mathf.Clamp(flamethrower?.pawn?.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, 0, maximumShootingSkill);
			return Mathf.Lerp(max, min, skill / maximumShootingSkill);
		}

		public static float TargetFuelLevelFor(CompRefuelable refuelable, ZeFlammenwerfer flamethrower)
		{
			var capacity = CapacityFor(flamethrower);
			var configured = ConfiguredTargetFuelLevel(refuelable);
			return configured >= 0f ? Mathf.Clamp(configured, 0f, capacity) : capacity;
		}

		public static int FuelCountToFullyRefuel(CompRefuelable refuelable, ZeFlammenwerfer flamethrower)
		{
			var multiplier = refuelable.Props?.FuelMultiplierCurrentDifficulty ?? 1f;
			if (multiplier <= 0f)
				return 0;

			if (refuelable.Props?.atomicFueling == true)
				return Mathf.CeilToInt(CapacityFor(flamethrower) / multiplier);

			return Mathf.Max(Mathf.CeilToInt((TargetFuelLevelFor(refuelable, flamethrower) - refuelable.Fuel) / multiplier), 1);
		}

		public static void Refuel(CompRefuelable refuelable, ZeFlammenwerfer flamethrower, float amount)
		{
			var multiplier = refuelable.Props?.FuelMultiplierCurrentDifficulty ?? 1f;
			SetFuel(refuelable, Mathf.Min(Fuel(refuelable) + amount * multiplier, CapacityFor(flamethrower)));
			refuelable.parent.BroadcastCompSignal(CompRefuelable.RefueledSignal);
		}

		public static void SetTargetFuelLevel(CompRefuelable refuelable, ZeFlammenwerfer flamethrower, float value)
		{
			configuredTargetFuelLevelField.SetValue(refuelable, Mathf.Clamp(value, 0f, CapacityFor(flamethrower)));
		}

		public static void SetFuelLevel(CompRefuelable refuelable, ZeFlammenwerfer flamethrower, float value)
		{
			SetFuel(refuelable, Mathf.Clamp(value, 0f, CapacityFor(flamethrower)));
		}

		public static void ClampFuelToCapacity(CompRefuelable refuelable, ZeFlammenwerfer flamethrower)
		{
			var capacity = CapacityFor(flamethrower);
			if (refuelable.Fuel > capacity)
				SetFuel(refuelable, capacity);
		}

		public static void MigrateLegacyTargetFuelLevel(CompRefuelable refuelable, ZeFlammenwerfer flamethrower)
		{
			var capacity = CapacityFor(flamethrower);
			var legacyCapacity = refuelable.Props?.fuelCapacity ?? capacity;
			if (capacity <= legacyCapacity)
				return;

			if (Mathf.Abs(ConfiguredTargetFuelLevel(refuelable) - legacyCapacity) < 0.01f)
				SetTargetFuelLevel(refuelable, flamethrower, capacity);
		}

		public static void InitializeFuel(CompRefuelable refuelable, ZeFlammenwerfer flamethrower)
		{
			SetFuel(refuelable, CapacityFor(flamethrower) * (refuelable.Props?.initialFuelPercent ?? 0f));
		}

		static float Fuel(CompRefuelable refuelable)
		{
			return (float)fuelField.GetValue(refuelable);
		}

		static void SetFuel(CompRefuelable refuelable, float value)
		{
			fuelField.SetValue(refuelable, Mathf.Max(0f, value));
		}

		static float ConfiguredTargetFuelLevel(CompRefuelable refuelable)
		{
			return (float)configuredTargetFuelLevelField.GetValue(refuelable);
		}

		static float CapacityFactor(QualityCategory quality)
		{
			return quality switch
			{
				QualityCategory.Awful => 0f,
				QualityCategory.Poor => 0.10f,
				QualityCategory.Normal => 0.25f,
				QualityCategory.Good => 0.50f,
				QualityCategory.Excellent => 0.75f,
				QualityCategory.Masterwork => 0.90f,
				QualityCategory.Legendary => 1f,
				_ => 0.25f
			};
		}

		static void NormalizeRange(ref float min, ref float max)
		{
			if (min <= max)
				return;

			(min, max) = (max, min);
		}
	}
}
