using RimWorld;
using System.IO;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	[StaticConstructorOnStartup]
	public static class Assets
	{
		public static readonly AssetBundle assets = LoadAssetBundle();
		public static readonly GameObject fire = assets.LoadAsset<GameObject>("Fire");
		public static readonly GameObject smoke = assets.LoadAsset<GameObject>("Smoke");
		public static readonly GameObject curveInner = assets.LoadAsset<GameObject>("CurveInner");
		public static readonly GameObject curveOuter = assets.LoadAsset<GameObject>("CurveOuter");
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
