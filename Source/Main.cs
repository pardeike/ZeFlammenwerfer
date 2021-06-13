using HarmonyLib;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class ZeFlammenwerferMain : Mod
	{
		public ZeFlammenwerferMain(ModContentPack content) : base(content)
		{
			Renderer.Prepare();
			PawnExtension.Subscribe(new PawnShooterTracker());
			PawnExtension.Subscribe(new PawnTargetTracker());

			var harmony = new Harmony("net.pardeike.ze.flammenwerfer");
			harmony.PatchAll();
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
