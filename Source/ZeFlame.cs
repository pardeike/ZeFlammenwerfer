using System.Collections.Generic;
using UnityEngine;
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
			Update();
			flameComp.SetActive(true);
			flameComp.flames.Insert(0, this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
		}

		public override void Tick()
		{
			base.Tick();
			if (flameComp != null)
				Update();
		}

		public void Update()
		{
			var from = launcher.DrawPos.WithHeight(0);
			var job = owner.launcher.CurJob;
			var to = (job?.targetA != null ? job.targetA.Cell.ToVector3Shifted() : destination).WithHeight(0);
			var vector = to - from;
			var startOffset = vector.magnitude > 1f ? vector.normalized : Vector3.zero;
			flameComp.Update(from + startOffset, to);
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
		}

		public override void Draw() { }
		public override void Print(SectionLayer layer) { }
		public override void DrawGUIOverlay() { }
		public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(List<Pawn> selPawns) { yield break; }
		public override string GetInspectString() { return ""; }
	}
}
