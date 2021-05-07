using BansheeGz.BGSpline.Curve;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class FlamethrowerCompProps : CompProperties
	{
		public FlamethrowerCompProps()
		{
			compClass = typeof(FlamethrowerComp);
		}
	}

	public class FlamethrowerOwner : MonoBehaviour
	{
		public Pawn launcher;
	}

	public class FlamethrowerComp : ThingComp
	{
		public GameObject fire;
		public GameObject smoke;

		public GameObject curveInner, curveOuter;
		public BGCurvePointI[] pointsInner, pointsOuter;

		public static HashSet<ParticleSystem> allParticleSystems = new HashSet<ParticleSystem>();

		public static readonly Vector3 vSpacer = new Vector3(0, 0.01f, 0);
		public static readonly Vector3 p1HandleLeft = new Vector3(-0.15f, 0f, -0.08f);
		public static readonly Vector3 p1HandleRight = new Vector3(0.15f, 0f, -0.08f);
		public static readonly Vector3 p2HandleLeft = new Vector3(-0.15f, 0f, 0.07f);
		public static readonly Vector3 p2HandleRight = new Vector3(0.15f, 0f, 0.07f);

		public static Vector3[] tankOffset = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(-0.3f, 0, 0),
			new Vector3(-0.35f, 0, 0),
			new Vector3(0.3f, 0, 0)
		};

		public override void Initialize(CompProperties props)
		{
			var maxRange = parent.def.Verbs[0].range;
			LineRenderer lineRenderer;

			fire = Object.Instantiate(Assets.fire);
			_ = fire.AddComponent(typeof(FlamethrowerOwner));
			fire.transform.localScale = new Vector3(maxRange, 1, maxRange);

			smoke = Object.Instantiate(Assets.smoke);
			smoke.transform.localScale = new Vector3(maxRange, 1, maxRange);

			curveInner = Object.Instantiate(Assets.curveInner);
			curveInner.transform.localScale = new Vector3(1, 1, 1);
			lineRenderer = curveInner.GetComponent<LineRenderer>();
			lineRenderer.numCapVertices = 8;
			lineRenderer.widthCurve = AnimationCurve.Constant(0, 1, 0.05f);
			var curve1 = curveInner.GetComponent<BGCurve>();
			pointsInner = curve1.AddPoints();

			curveOuter = Object.Instantiate(Assets.curveOuter);
			curveOuter.transform.localScale = new Vector3(1, 1, 1);
			lineRenderer = curveOuter.GetComponent<LineRenderer>();
			lineRenderer.numCapVertices = 8;
			lineRenderer.widthCurve = AnimationCurve.Constant(0, 1, 0.09f);
			var curve2 = curveOuter.GetComponent<BGCurve>();
			pointsOuter = curve2.AddPoints();

			UpdateDrawPos(parent as Pawn);
			SetActive(false);

			_ = allParticleSystems.Add(fire.GetComponent<ParticleSystem>());
			_ = allParticleSystems.Add(smoke.GetComponent<ParticleSystem>());

			base.Initialize(props);
		}

		~FlamethrowerComp()
		{
			_ = allParticleSystems.Remove(fire.GetComponent<ParticleSystem>());
			_ = allParticleSystems.Remove(smoke.GetComponent<ParticleSystem>());

			Object.DestroyImmediate(fire);
			Object.DestroyImmediate(smoke);

			Object.DestroyImmediate(curveInner);
			Object.DestroyImmediate(curveOuter);

			allParticleSystems = new HashSet<ParticleSystem>();
		}

		public void Update(Vector3 from, Vector3 to)
		{
			from.y = Tools.moteOverheadHeight;
			to.y = Tools.moteOverheadHeight;

			fire.transform.position = from;
			smoke.transform.position = from;

			var q = Quaternion.LookRotation(to - from);
			fire.transform.rotation = q;
			smoke.transform.rotation = q;
		}

		public void UpdateDrawPos(Pawn pawn)
		{
			if (pawn == null) return;

			var drawPos = pawn.DrawPos;
			var orientation = pawn.Rotation;
			Vector3 control;

			var (center, angle) = WeaponTool.GetAimingCenter(pawn);
			var flipped = WeaponTool.IsFlipped(angle);

			curveInner.transform.position = drawPos;
			curveOuter.transform.position = drawPos;
			curveInner.SetActive(pawn.Rotation != Rot4.North);
			curveOuter.SetActive(pawn.Rotation != Rot4.North);

			var tankOutputPoint = drawPos + tankOffset[pawn.Rotation.AsInt] + new Vector3(0, 0, 0.25f) + vSpacer;
			tankOutputPoint.y += PawnRenderer_RenderPawnInternal_Patch.magicOffset + (orientation == Rot4.North ? Altitudes.AltInc : -Altitudes.AltInc / 12f);

			pointsInner[0].PositionWorld = tankOutputPoint + vSpacer;
			pointsOuter[0].PositionWorld = tankOutputPoint;
			control = tankOutputPoint + (pawn.Rotation != Rot4.West ? p1HandleLeft : p1HandleRight);
			pointsInner[0].ControlSecondWorld = control + vSpacer;
			pointsOuter[0].ControlSecondWorld = control;

			var gunInputPoint = center + new Vector3(-0.5f, 0, flipped ? -0.028f : 0.028f).RotatedBy(angle - 90) + vSpacer;

			pointsInner[1].PositionWorld = gunInputPoint + vSpacer;
			pointsOuter[1].PositionWorld = gunInputPoint;
			control = gunInputPoint + (flipped ? p2HandleRight : p2HandleLeft);
			pointsInner[1].ControlFirstWorld = control + vSpacer;
			pointsOuter[1].ControlFirstWorld = control;
		}

		public void SetActive(bool active)
		{
			var emission1 = fire.GetComponent<ParticleSystem>().emission;
			emission1.enabled = active;
			var emission2 = smoke.GetComponent<ParticleSystem>().emission;
			emission2.enabled = active;
		}
	}
}
