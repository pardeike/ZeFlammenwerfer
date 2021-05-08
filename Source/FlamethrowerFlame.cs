using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class FlamethrowerFlame : Projectile_Explosive
	{
		public delegate bool CarryWeaponOpenly(PawnRenderer renderer);

		public FlamethrowerOwner owner;
		public FlamethrowerComp flamethrower;
		public static CarryWeaponOpenly carryWeaponOpenly = AccessTools.MethodDelegate<CarryWeaponOpenly>(AccessTools.Method(typeof(PawnRenderer), "CarryWeaponOpenly"));

		public void Configure(Pawn launcher, FlamethrowerComp flamethrowerComp)
		{
			flamethrower = flamethrowerComp;
			owner = flamethrower.fire.GetComponent<FlamethrowerOwner>();
			owner.launcher = launcher;
			Update();
			flamethrower.SetActive(true);
			flamethrower.flames.Insert(0, this);
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
			Update();
		}

		public void Update()
		{
			var from = launcher.DrawPos.WithHeight(0);
			var to = destination.WithHeight(0);
			var vector = to - from;
			var startOffset = vector.magnitude > 1f ? vector.normalized : Vector3.zero;
			flamethrower.Update(from + startOffset, to);
		}

		protected override void Impact(Thing hitThing)
		{
			if (flamethrower == null) return;
			Destroy(DestroyMode.Vanish);
			flamethrower.SetActive(false);
			_ = flamethrower.flames.Remove(this);
		}

		public override void Draw() { }
		public override void Print(SectionLayer layer) { }
		public override void DrawGUIOverlay() { }
		public override IEnumerable<FloatMenuOption> GetMultiSelectFloatMenuOptions(List<Pawn> selPawns) { yield break; }
		public override string GetInspectString() { return ""; }
	}
}
