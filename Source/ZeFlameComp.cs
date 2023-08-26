using BansheeGz.BGSpline.Curve;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class ZeFlameComp : ThingComp
	{
		public GameObject fire;
		public GameObject smoke;
		public ZeFlameSound sound;
		public List<ZeFlame> flames = new();
		public bool isActive;
		public bool isPipeActive;

		public GameObject curveInner, curveOuter;
		public IBGCurvePointI[] pointsInner, pointsOuter;

		public static HashSet<ParticleSystem> allParticleSystems = new();

		public static readonly Vector3 vSpacer = new(0, 0.01f, 0);
		public static readonly Vector3 p1HandleLeft = new(-0.15f, 0f, -0.08f);
		public static readonly Vector3 p1HandleRight = new(0.15f, 0f, -0.08f);
		public static readonly Vector3 p2HandleLeft = new(-0.15f, 0f, 0.07f);
		public static readonly Vector3 p2HandleRight = new(0.15f, 0f, 0.07f);

		public static Vector3[] tankOffset = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(-0.3f, 0, 0),
			new Vector3(-0.35f, 0, 0),
			new Vector3(0.3f, 0, 0)
		};

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			Create();
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			Create();
		}

		public override void PostPostMake()
		{
			base.PostPostMake();
			Create();
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			base.PostDestroy(mode, previousMap);
			Remove();
		}

		public void Create()
		{
			if (fire != null)
				return;

			var maxRange = parent.def.Verbs[0].range;
			LineRenderer lineRenderer;

			fire = Object.Instantiate(Assets.fire);
			_ = fire.AddComponent(typeof(ZeOwner));
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
			sound = new ZeFlameSound(fire);
			SetActive(false, true);

			_ = allParticleSystems.Add(fire.GetComponent<ParticleSystem>());
			_ = allParticleSystems.Add(smoke.GetComponent<ParticleSystem>());
		}

		public void Remove()
		{
			if (fire == null)
				return;

			sound.Remove();
			sound = null;

			_ = allParticleSystems.Remove(fire.GetComponent<ParticleSystem>());
			_ = allParticleSystems.Remove(smoke.GetComponent<ParticleSystem>());

			Object.DestroyImmediate(fire);
			fire = null;
			Object.DestroyImmediate(smoke);
			smoke = null;

			Object.DestroyImmediate(curveInner);
			curveInner = null;
			Object.DestroyImmediate(curveOuter);
			curveOuter = null;
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

		public void SetPipeActive(bool active)
		{
			if (isPipeActive == active)
				return;
			isPipeActive = active;
			curveInner.SetActive(active);
			curveOuter.SetActive(active);
		}

		public void UpdateDrawPos(Pawn pawn)
		{
			if (pawn == null || fire == null)
				return;

			var (center, angle) = WeaponTool.GetAimingCenter(pawn);
			if (angle == int.MinValue)
			{
				SetPipeActive(false);
				return;
			}

			var drawPos = pawn.DrawPos;
			var orientation = pawn.Rotation;
			Vector3 control;
			var flipped = WeaponTool.IsFlipped(angle);

			curveInner.transform.position = drawPos;
			curveOuter.transform.position = drawPos;
			SetPipeActive(pawn.Rotation != Rot4.North);

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

		public void SetActive(bool active, bool force = false)
		{
			if (isActive == active && force == false)
				return;
			isActive = active;

			if (active)
				sound.Start();
			else
			{
				flames.DoIf(f => f.Destroyed == false, f => f.Destroy(DestroyMode.Vanish));
				flames.Clear();
				sound.Stop();
			}

			var emission1 = fire.GetComponent<ParticleSystem>().emission;
			emission1.enabled = active;
			var emission2 = smoke.GetComponent<ParticleSystem>().emission;
			emission2.enabled = active;
		}
	}

	public class ZeFlameCompProps : CompProperties
	{
		public ZeFlameCompProps()
		{
			compClass = typeof(ZeFlameComp);
		}
	}

	public class ZeOwner : MonoBehaviour
	{
		public Pawn launcher;
	}
}