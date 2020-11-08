using System.IO;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	[StaticConstructorOnStartup]
	public static class Assets
	{
		static readonly AssetBundle assets = LoadAssetBundle();
		public static readonly Material flamesMat = assets.LoadAsset<Material>("Mat_Fire");
		public static readonly Material smokesMat = assets.LoadAsset<Material>("Mat_Smoke");

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
}
