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
			flameComp.NotifyShot();
			DebugTrace.Log($"Configure launcher={launcher?.LabelShortCap ?? "null"} activeBefore={flameComp.isActive} existingFlames={flameComp.flames.Count}");
			flameComp.SetActive(true);
			flameComp.flames.Insert(0, this);
		}

		public override void ExposeData()
		{
			base.ExposeData();
		}

		public override void Impact(Thing hitThing, bool blockedByShield = false)
		{
			// do not call base
			if (flameComp == null)
				return;
			Destroy(DestroyMode.Vanish);
			_ = flameComp.flames.Remove(this);
			DebugTrace.Log($"Impact hit={hitThing?.LabelCap ?? "cell"} remainingFlames={flameComp.flames.Count}");
			flameComp = null;
		}

		public override void DrawAt(Vector3 drawLoc, bool flip) { }
		public override void Print(SectionLayer layer) { }
		public override void DrawGUIOverlay() { }
		public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(IEnumerable<Pawn> selPawns) { yield break; }
		public override string GetInspectString() { return ""; }
	}
}
