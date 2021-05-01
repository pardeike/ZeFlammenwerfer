using RimWorld;
using System.IO;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	[StaticConstructorOnStartup]
	public static class Assets
	{
		static readonly AssetBundle assets = LoadAssetBundle();
		public static readonly GameObject fire = assets.LoadAsset<GameObject>("Fire");
		public static readonly GameObject smoke = assets.LoadAsset<GameObject>("Smoke");
		public static readonly GameObject blockCube = assets.LoadAsset<GameObject>("BlockCube");
		//public static readonly GameObject target = assets.LoadAsset<GameObject>("Target");

		static Assets()
		{
			Object.DontDestroyOnLoad(fire);
			Object.DontDestroyOnLoad(smoke);
			Object.DontDestroyOnLoad(blockCube);
			//Object.DontDestroyOnLoad(target);
		}

		static string GetModRootDirectory()
		{
			var me = LoadedModManager.GetMod<FlameThrowerMain>();
			return me.Content.RootDir;
		}

		static AssetBundle LoadAssetBundle()
		{
			var path = Path.Combine(GetModRootDirectory(), "Resources", "flamethrower");
			return AssetBundle.LoadFromFile(path);
		}
	}

	[DefOf]
	public static class Defs
	{
		public static ThingDef Flamethrower;
	}
}
