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
			Object.DontDestroyOnLoad(fire);
			Object.DontDestroyOnLoad(smoke);
			Object.DontDestroyOnLoad(curveInner);
			Object.DontDestroyOnLoad(curveOuter);
			Object.DontDestroyOnLoad(blockCube);
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
			var path = Path.Combine(GetModRootDirectory(), "Resources", "flamethrower");
			return AssetBundle.LoadFromFile(path);
		}
	}

	[DefOf]
	public static class Defs
	{
		public static ThingDef ZeFlammenwerfer;
	}
}
