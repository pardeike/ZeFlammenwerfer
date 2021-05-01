using HarmonyLib;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class FlameThrowerMain : Mod
	{
		public FlameThrowerMain(ModContentPack content) : base(content)
		{
			var harmony = new Harmony("net.pardeike.flamethrower");
			harmony.PatchAll();

			Renderer.Prepare();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return base.SettingsCategory();
		}

		public override string ToString()
		{
			return base.ToString();
		}

		public override void WriteSettings()
		{
			base.WriteSettings();
		}
	}
}
