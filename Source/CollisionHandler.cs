using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FlameThrower
{
	public class PawnCollisionHandler : MonoBehaviour
	{
		public List<ParticleCollisionEvent> collisionEvents;

		public void Start()
		{
			collisionEvents = new List<ParticleCollisionEvent>();
		}

		public void OnParticleCollision(GameObject other)
		{
			try
			{
				var particleSystem = other.GetComponent<ParticleSystem>();
				if (particleSystem == null) return;
				var launcher = other.GetComponent<FlamethrowerOwner>()?.launcher;

				_ = particleSystem.GetCollisionEvents(gameObject, collisionEvents);
				foreach (var collision in collisionEvents)
				{
					var collider = collision.colliderComponent as Collider;
					var targetPawn = collider?.gameObject.GetComponent<ColliderHolder.RimWorldPawn>()?.pawn;
					if (targetPawn == null || targetPawn == launcher) return;

					var v = collision.velocity;
					var skill = Mathf.Clamp(launcher?.skills.GetSkill(SkillDefOf.Shooting).Level ?? 1, 0, 20);
					var amount = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z)) / (21 - skill);

					Tools.ApplyFlameDamage(targetPawn, amount);
				}
			}
			catch (Exception ex)
			{
				Log.Warning(ex.ToString());
			}
		}
	}

	public class ThingCollisionHandler : MonoBehaviour
	{
		public FlameRadiusDetector flameRadiusDetector;
		public List<ParticleCollisionEvent> collisionEvents;

		public void Start()
		{
			collisionEvents = new List<ParticleCollisionEvent>();
		}

		public void OnParticleCollision(GameObject other)
		{
			try
			{
				var pawn = flameRadiusDetector?.shooter;
				var map = pawn?.Map;
				if (map == null) return;

				_ = other.GetComponent<ParticleSystem>().GetCollisionEvents(gameObject, collisionEvents);
				foreach (var collision in collisionEvents)
				{
					var v = collision.velocity;
					var skill = Mathf.Clamp(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, 0, 20);
					var amount = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z)) / (21 - skill);

					var itemCollider = collision.colliderComponent as BoxCollider;
					var p = itemCollider.center;
					var cell = new IntVec3((int)p.x, 0, (int)p.z);
					if (cell.InBounds(map))
					{
						var things = map.thingGrid.ThingsAt(cell).Where(thing => thing as Pawn == null);
						things.OfType<ThingWithComps>().Do(thing => Tools.ApplyFlameDamage(thing, amount));
						Tools.ApplyCellFlame(map, amount, cell, things);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning(ex.ToString());
			}
		}
	}
}
