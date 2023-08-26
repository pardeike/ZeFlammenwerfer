using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
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
			var particleSystem = other.GetComponent<ParticleSystem>();
			if (particleSystem == null)
				return;
			var launcher = other.GetComponent<ZeOwner>()?.launcher;

			_ = particleSystem.GetCollisionEvents(gameObject, collisionEvents);
			var n = collisionEvents.Count;
			for (var i = 0; i < n; i++)
			{
				var collision = collisionEvents[i];
				var collider = collision.colliderComponent as Collider;
				var targetPawn = collider?.gameObject.GetComponent<ColliderHolder.RimWorldPawn>()?.pawn;
				if (targetPawn == null || targetPawn == launcher)
					continue;

				var v = collision.velocity;
				var skill = Mathf.Clamp(launcher?.skills.GetSkill(SkillDefOf.Shooting).Level ?? 1, 0, 20);
				var amount = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z)) / (21 - skill);

				Tools.ApplyFlameDamage(targetPawn, amount);
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
			var pawn = flameRadiusDetector?.shooter;
			var map = pawn?.Map;
			if (map == null)
				return;
			var thingGrid = map.thingGrid;

			var particleSystem = other.GetComponent<ParticleSystem>();
			if (particleSystem == null)
				return;

			_ = particleSystem.GetCollisionEvents(gameObject, collisionEvents);
			var n = collisionEvents.Count;
			for (var i = 0; i < n; i++)
			{
				var collision = collisionEvents[i];
				var v = collision.velocity;
				var skill = Mathf.Clamp(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, 0, 20);
				var amount = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.z)) / (21 - skill);

				var itemCollider = collision.colliderComponent as BoxCollider;
				if (itemCollider == null)
					continue;

				var p = itemCollider.center;
				var cell = new IntVec3((int)p.x, 0, (int)p.z);
				if (cell.InBounds(map))
				{
					var things = thingGrid.ThingsAt(cell).Where(thing => thing as Pawn == null);
					things.OfType<ThingWithComps>().Do(thing => Tools.ApplyFlameDamage(thing, amount));
					Tools.ApplyCellFlame(map, amount, cell, things);
				}
			}
		}
	}
}