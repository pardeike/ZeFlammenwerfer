using System;
using BansheeGz.BGSpline.Components;
using BansheeGz.BGSpline.Curve;
using RimWorld;
using System.IO;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	[StaticConstructorOnStartup]
	public static class Assets
	{
		const string AssetBundleBaseName = "flamethrower";

		public static readonly Color lineForeground = new(137f / 255f, 44f / 255f, 31f / 255f);
		public static readonly Color lineBackground = Color.black;

		public static readonly AssetBundle assets = LoadAssetBundle();
		public static readonly GameObject fire = assets.LoadAsset<GameObject>("Fire");
		public static readonly GameObject smoke = assets.LoadAsset<GameObject>("Smoke");
		public static readonly GameObject curveInner = CreateCurve(lineForeground, 0.2f, 20);
		public static readonly GameObject curveOuter = CreateCurve(lineBackground, 0.35f, 20);
		public static readonly GameObject blockCube = assets.LoadAsset<GameObject>("BlockCube"); // visually blocks flames
		public static readonly Material tank = MaterialPool.MatFrom("Tank", ShaderDatabase.Cutout);

		static Assets()
		{
			UnityEngine.Object.DontDestroyOnLoad(fire);
			UnityEngine.Object.DontDestroyOnLoad(smoke);
			UnityEngine.Object.DontDestroyOnLoad(curveInner);
			UnityEngine.Object.DontDestroyOnLoad(curveOuter);
			UnityEngine.Object.DontDestroyOnLoad(blockCube);
		}

		public static GameObject CreateCurve(Color color, float width, int sections)
		{
			var result = new GameObject();
			var curve = result.AddComponent<BGCurve>();
			var math = result.AddComponent<BGCcMath>();
			result.AddComponent<BGCcVisualizationLineRenderer>();
			var lineRenderer = result.GetComponent<LineRenderer>();
			lineRenderer.sharedMaterial = SolidColorMaterials.SimpleSolidColorMaterial(color);
			lineRenderer.startWidth = lineRenderer.endWidth = width;
			math.SectionParts = sections;
			curve.AddPoint(new BGCurvePoint(curve, Vector3.zero, BGCurvePoint.ControlTypeEnum.Absent, Vector3.zero, Vector3.zero));
			curve.AddPoint(new BGCurvePoint(curve, Vector3.zero, BGCurvePoint.ControlTypeEnum.Absent, Vector3.zero, Vector3.zero));
			return result;
		}

		public static string GetModRootDirectory()
		{
			var me = LoadedModManager.GetMod<ZeFlammenwerferMain>();
			return me.Content.RootDir;
		}

		public static AssetBundle LoadAssetBundle()
		{
			var resourcesDirectory = Path.Combine(GetModRootDirectory(), "Resources");
			var candidateBundleNames = GetCandidateBundleNames();

			for (var i = 0; i < candidateBundleNames.Length; i++)
			{
				var bundleName = candidateBundleNames[i];
				var path = Path.Combine(resourcesDirectory, bundleName);
				if (File.Exists(path) == false)
					continue;

				var bundle = AssetBundle.LoadFromFile(path);
				if (bundle != null)
				{
					if (i > 0)
						Log.Warning($"Loaded legacy asset bundle '{bundleName}'. Build platform-specific bundles to avoid cross-platform asset issues.");
					return bundle;
				}

				Log.Error($"Failed to load asset bundle at '{path}'.");
			}

			throw new FileNotFoundException(
				$"No compatible asset bundle found for platform {Application.platform}. Tried: {string.Join(", ", Array.ConvertAll(candidateBundleNames, name => Path.Combine(resourcesDirectory, name)))}");
		}

		static string[] GetCandidateBundleNames()
		{
			var platformBundleName = GetPlatformBundleName(Application.platform);
			if (platformBundleName == AssetBundleBaseName)
				return new[] { AssetBundleBaseName };

			return new[] { platformBundleName, AssetBundleBaseName };
		}

		static string GetPlatformBundleName(RuntimePlatform platform)
		{
			switch (platform)
			{
				case RuntimePlatform.OSXEditor:
				case RuntimePlatform.OSXPlayer:
					return $"{AssetBundleBaseName}-mac";
				case RuntimePlatform.WindowsEditor:
				case RuntimePlatform.WindowsPlayer:
					return $"{AssetBundleBaseName}-win";
				case RuntimePlatform.LinuxEditor:
				case RuntimePlatform.LinuxPlayer:
					return $"{AssetBundleBaseName}-linux";
				default:
					return AssetBundleBaseName;
			}
		}
	}

	[DefOf]
	public static class Defs
	{
		public static ThingDef ZeFlammenwerfer;
	}
}
