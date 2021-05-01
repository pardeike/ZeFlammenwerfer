using System.Collections.Generic;
using Verse;

namespace FlameThrower
{
	public class Flamethrower_Flame : Projectile_Explosive
	{
		FlamethrowerComp flamethrower;

		public void Configure(FlamethrowerComp flamethrower)
		{
			this.flamethrower = flamethrower;
			flamethrower.Update(launcher.DrawPos, usedTarget.CenterVector3);
			flamethrower.SetActive(true);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			// TODO: implement or load/save will be broken
		}

		public override void Tick()
		{
			base.Tick();
			if (flamethrower == null) return;

			var from = launcher.DrawPos;
			var to = usedTarget.CenterVector3;
			flamethrower.Update(from, to);
		}

		protected override void Impact(Thing hitThing)
		{
			if (flamethrower == null) return;

			Destroy(DestroyMode.Vanish);
			flamethrower.SetActive(false);
		}

		public override void Draw() { }
		public override void Print(SectionLayer layer) { }
		public override void DrawGUIOverlay() { }
		public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(List<Pawn> selPawns) { yield break; }
		public override string GetInspectString() { return ""; }
	}
}
