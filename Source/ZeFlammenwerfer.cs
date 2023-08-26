using RimWorld;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public partial class ZeFlammenwerfer : ThingWithComps
	{
		public Pawn pawn;

		public CompRefuelable refuelable;
		public ZeFlameComp flameComp;

		public void Setup()
		{
			refuelable = GetComp<CompRefuelable>();
			flameComp = GetComp<ZeFlameComp>();
		}

		public override void Tick()
		{
			base.Tick();
			if (pawn == null || (flameComp.isActive && WeaponTool.IsAiming(pawn) == false))
			{
				flameComp.SetActive(false);
				return;
			}
			if (pawn.stances.curStance is Stance_Busy stance_Busy && stance_Busy.focusTarg.IsValid)
			{
				var from = pawn.DrawPos.WithHeight(0);
				var to = stance_Busy.focusTarg.HasThing ? stance_Busy.focusTarg.Thing.DrawPos : stance_Busy.focusTarg.Cell.ToVector3Shifted();
				var vector = to - from;
				var startOffset = vector.magnitude > 1f ? vector.normalized : Vector3.zero;
				flameComp.Update(from + 1.75f * startOffset, to);
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref pawn, "pawn");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
				Setup();
		}

		/*public override string DescriptionDetailed
		{
			get
			{
				var builder = new StringBuilder();
				_ = builder.Append("FlamethrowerDesc".Translate());
				if (refuelable.HasFuel)
					_ = builder.Append("FlamethrowerDescFuel".Translate(Mathf.RoundToInt(refuelable.FuelPercentOfMax * 100)));
				else
					_ = builder.Append($"{"FlamethrowerDescNoFuel".Translate()}");
				return builder.ToString();
			}
		}*/

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			Setup();
		}

		public override void Notify_Equipped(Pawn pawn)
		{
			this.pawn = pawn;
			base.Notify_Equipped(pawn);
		}

		public override void Notify_Unequipped(Pawn _)
		{
			pawn = null;
			base.Notify_Unequipped(pawn);
		}

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			pawn = null;
			base.DeSpawn(mode);
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			pawn = null;
			base.Destroy(mode);
		}
	}
}