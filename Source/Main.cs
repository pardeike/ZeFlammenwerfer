using Brrainz;
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

			CrossPromotion.Install(76561197973010050);
		}

		public override void DoSettingsWindowContents(Rect inRect) => base.DoSettingsWindowContents(inRect);

		public override string SettingsCategory() => base.SettingsCategory();
	}
}
