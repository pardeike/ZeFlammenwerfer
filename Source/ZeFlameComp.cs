using BansheeGz.BGSpline.Curve;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	public class ZeFlameComp : ThingComp
	{
		const int activeGraceTicks = 120;

		public static HashSet<ZeFlameComp> allFlameComps = new();
		public GameObject fire;
		public GameObject smoke;
		public ZeFlameSound sound;
		public List<ZeFlame> flames = new();
		public bool isActive;
		public bool isPipeActive;
		public int lastShotTick = int.MinValue;
		bool renderVisible = true;

		public GameObject curveInner, curveOuter;
		public IBGCurvePointI[] pointsInner, pointsOuter;

		public static HashSet<ParticleSystem> allParticleSystems = new();

		public static readonly Vector3 vSpacer = new(0, 0.00035f, 0);
		public static readonly Vector3 p1HandleLeft = new(-0.15f, 0f, -0.08f);
		public static readonly Vector3 p1HandleRight = new(0.15f, 0f, -0.08f);
		public static readonly Vector3 p2HandleLeft = new(-0.15f, 0f, 0.07f);
		public static readonly Vector3 p2HandleRight = new(0.15f, 0f, 0.07f);
		const float tankFrontVisualLayer = PawnRenderUtility.Layer_Carried - 4f;
		const float pipeFrontVisualLayer = PawnRenderUtility.Layer_Carried - 2f;
		const float tankBackVisualLayer = PawnRenderUtility.Layer_Carried + 8f;
		const float pipeBackVisualLayer = PawnRenderUtility.Layer_Carried + 10f;

		public static Vector3[] tankOffset = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(-0.3f, 0, 0),
			new Vector3(-0.35f, 0, 0),
			new Vector3(0.3f, 0, 0)
		};

		static float CurrentEquipmentLayerFor(Rot4 facing)
		{
			return facing == Rot4.North ? PawnRenderUtility.Layer_Carried_Behind : PawnRenderUtility.Layer_Carried;
		}

		public static Vector3 MoveFromEquipmentLayer(Vector3 drawPos, Rot4 facing, float targetLayer)
		{
			return drawPos.WithYOffset(PawnRenderUtility.AltitudeForLayer(targetLayer) - PawnRenderUtility.AltitudeForLayer(CurrentEquipmentLayerFor(facing)));
		}

		public static Vector3 TankDrawPosition(Vector3 equipmentBaseDrawPos, Rot4 facing)
		{
			var layer = facing == Rot4.North ? tankBackVisualLayer : tankFrontVisualLayer;
			return MoveFromEquipmentLayer(equipmentBaseDrawPos, facing, layer) + tankOffset[facing.AsInt];
		}

		static Vector3 PipeDrawPosition(Vector3 equipmentBaseDrawPos, Rot4 facing)
		{
			var layer = facing == Rot4.North ? pipeBackVisualLayer : pipeFrontVisualLayer;
			return MoveFromEquipmentLayer(equipmentBaseDrawPos, facing, layer);
		}

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

		public static void ClearAllVisuals()
		{
			foreach (var flameComp in allFlameComps.ToArray())
				flameComp.Remove();

			allFlameComps.Clear();
			allParticleSystems = new HashSet<ParticleSystem>();
		}

		public static void RefreshRenderedMap(Map renderedMap, bool force = false)
		{
			foreach (var flameComp in allFlameComps.ToArray())
			{
				if (flameComp == null)
					continue;

				flameComp.SetRenderVisible(flameComp.RenderMap == renderedMap, force);
			}
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
			fire.GetComponent<ParticleSystem>()?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			smoke.GetComponent<ParticleSystem>()?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			SetActive(false, true);

			_ = allFlameComps.Add(this);
			_ = allParticleSystems.Add(fire.GetComponent<ParticleSystem>());
			_ = allParticleSystems.Add(smoke.GetComponent<ParticleSystem>());
			renderVisible = MapRenderState.ShouldRenderMap(RenderMap);
			ApplyRenderState(true);
		}

		public void Remove()
		{
			if (fire == null)
				return;

			_ = allFlameComps.Remove(this);
			flames.DoIf(f => f.Destroyed == false, f => f.Destroy(DestroyMode.Vanish));
			flames.Clear();

			fire.GetComponent<ParticleSystem>()?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			smoke.GetComponent<ParticleSystem>()?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			sound?.Remove();
			sound = null;

			_ = allParticleSystems.Remove(fire.GetComponent<ParticleSystem>());
			_ = allParticleSystems.Remove(smoke.GetComponent<ParticleSystem>());

			fire.SetActive(false);
			DestroyUnityObject(fire);
			fire = null;
			smoke?.SetActive(false);
			DestroyUnityObject(smoke);
			smoke = null;

			curveInner?.SetActive(false);
			DestroyUnityObject(curveInner);
			curveInner = null;
			curveOuter?.SetActive(false);
			DestroyUnityObject(curveOuter);
			curveOuter = null;
		}

		static void DestroyUnityObject(Object obj)
		{
			if (obj == null)
				return;

			if (Application.isPlaying)
				Object.Destroy(obj);
			else
				Object.DestroyImmediate(obj);
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
			ApplyRenderState(true);
		}

		public void UpdateDrawPos(Pawn pawn)
		{
			if (pawn == null)
				return;

			var equipmentLayer = CurrentEquipmentLayerFor(pawn.Rotation);
			UpdateDrawPos(pawn, pawn.DrawPos.WithYOffset(PawnRenderUtility.AltitudeForLayer(equipmentLayer)), pawn.Rotation, PawnRenderFlags.None);
		}

		public void UpdateDrawPos(Pawn pawn, Vector3 equipmentBaseDrawPos, Rot4 renderFacing, PawnRenderFlags flags)
		{
			if (pawn == null || fire == null)
				return;

			if (WeaponTool.TryGetAimingData(pawn, equipmentBaseDrawPos, flags, out var aiming, includeRecoil: true) == false)
			{
				if (isActive == false)
					SetPipeActive(false);
				return;
			}

			var orientation = renderFacing;
			Vector3 control;
			var flipped = WeaponTool.IsFlipped(aiming.AimAngle);

			curveInner.transform.position = equipmentBaseDrawPos;
			curveOuter.transform.position = equipmentBaseDrawPos;
			SetPipeActive(true);

			var tankOutputPoint = PipeDrawPosition(equipmentBaseDrawPos, orientation) + tankOffset[orientation.AsInt] + new Vector3(0, 0, 0.25f) + vSpacer;

			pointsInner[0].PositionWorld = tankOutputPoint + vSpacer;
			pointsOuter[0].PositionWorld = tankOutputPoint;
			control = tankOutputPoint + (orientation != Rot4.West ? p1HandleLeft : p1HandleRight);
			pointsInner[0].ControlSecondWorld = control + vSpacer;
			pointsOuter[0].ControlSecondWorld = control;

			var gunInputPoint = PipeDrawPosition(aiming.DrawPos, orientation) + new Vector3(-0.5f, 0, flipped ? -0.028f : 0.028f).RotatedBy(aiming.AimAngle - 90) + vSpacer;

			pointsInner[1].PositionWorld = gunInputPoint + vSpacer;
			pointsOuter[1].PositionWorld = gunInputPoint;
			control = gunInputPoint + (flipped ? p2HandleRight : p2HandleLeft);
			pointsInner[1].ControlFirstWorld = control + vSpacer;
			pointsOuter[1].ControlFirstWorld = control;
		}

		public void NotifyShot()
		{
			lastShotTick = GenTicks.TicksGame;
		}

		public bool ShouldStayActiveBetweenShots()
		{
			return lastShotTick != int.MinValue && GenTicks.TicksGame - lastShotTick <= activeGraceTicks;
		}

		public void SetActive(bool active, bool force = false)
		{
			if (isActive == active && force == false)
				return;
			isActive = active;
			var fireParticleSystem = fire.GetComponent<ParticleSystem>();
			var smokeParticleSystem = smoke.GetComponent<ParticleSystem>();
			var emission1 = fireParticleSystem.emission;
			emission1.enabled = active;
			var emission2 = smokeParticleSystem.emission;
			emission2.enabled = active;

			if (active)
			{
				fireParticleSystem.Play(true);
				smokeParticleSystem.Play(true);
				sound.Start();
			}
			else
			{
				flames.DoIf(f => f.Destroyed == false, f => f.Destroy(DestroyMode.Vanish));
				flames.Clear();
				sound.Stop();
				fireParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
				smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
			}

			ApplyRenderState(true);
			DebugTrace.Log($"SetActive(active={active}, force={force}) flames={flames.Count} firePlaying={fireParticleSystem.isPlaying} firePaused={fireParticleSystem.isPaused} fireStopped={fireParticleSystem.isStopped} smokePlaying={smokeParticleSystem.isPlaying} smokePaused={smokeParticleSystem.isPaused} smokeStopped={smokeParticleSystem.isStopped}");
		}

		public void SetRenderVisible(bool visible, bool force = false)
		{
			if (force == false && renderVisible == visible)
				return;

			renderVisible = visible;
			ApplyRenderState(true);
		}

		Map RenderMap => parent?.MapHeld ?? (parent as ZeFlammenwerfer)?.pawn?.Map;

		void ApplyRenderState(bool force = false)
		{
			if (fire == null || smoke == null || curveInner == null || curveOuter == null)
				return;

			var fireParticleSystem = fire.GetComponent<ParticleSystem>();
			var smokeParticleSystem = smoke.GetComponent<ParticleSystem>();
			if (fireParticleSystem == null || smokeParticleSystem == null)
				return;

			if (renderVisible == false)
			{
				sound?.SetPause(true);
				fireParticleSystem.Pause(true);
				smokeParticleSystem.Pause(true);
				if (force || fire.activeSelf)
					fire.SetActive(false);
				if (force || smoke.activeSelf)
					smoke.SetActive(false);
				if (force || curveInner.activeSelf)
					curveInner.SetActive(false);
				if (force || curveOuter.activeSelf)
					curveOuter.SetActive(false);
				return;
			}

			if (force || fire.activeSelf == false)
				fire.SetActive(true);
			if (force || smoke.activeSelf == false)
				smoke.SetActive(true);

			var showPipe = isPipeActive;
			if (force || curveInner.activeSelf != showPipe)
				curveInner.SetActive(showPipe);
			if (force || curveOuter.activeSelf != showPipe)
				curveOuter.SetActive(showPipe);

			if (isActive == false)
				return;

			var paused = Current.Game?.tickManager?.Paused ?? false;
			if (paused)
			{
				sound?.SetPause(true);
				fireParticleSystem.Pause(true);
				smokeParticleSystem.Pause(true);
				return;
			}

			fireParticleSystem.Play(true);
			smokeParticleSystem.Play(true);
			sound?.SetPause(false);
		}
	}

	public class ZeFlameCompProps : CompProperties
	{
		public float minimumFuelCapacity = 50f;
		public float maximumFuelCapacity = 1000f;
		public float minimumFuelConsumption = 1f;
		public float maximumFuelConsumption = 20f;

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
