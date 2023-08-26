using System.Collections.Generic;
using Verse;

namespace ZeFlammenwerfer
{
	public class ZeFlame : Projectile_Explosive
	{
		public ZeOwner owner;
		public ZeFlameComp flameComp;

		public void Configure(Pawn launcher, ZeFlameComp comp)
		{
			flameComp = comp;
			owner = flameComp.fire.GetComponent<ZeOwner>();
			owner.launcher = launcher;
			flameComp.SetActive(true);
			flameComp.flames.Insert(0, this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
		}

		public override void Impact(Thing hitThing, bool blockedByShield = false)
		{
			if (blockedByShield) // shield does not block
				return;

			// do not call base
			if (flameComp == null)
				return;
			Destroy(DestroyMode.Vanish);
			flameComp.SetActive(false);
			_ = flameComp.flames.Remove(this);
			flameComp = null;
		}

		public override void Draw() { }
		public override void Print(SectionLayer layer) { }
		public override void DrawGUIOverlay() { }
		public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(List<Pawn> selPawns) { yield break; }
		public override string GetInspectString() { return ""; }
	}
}
