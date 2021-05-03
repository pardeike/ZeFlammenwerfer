using System.Collections.Generic;
using UnityEngine;

public class MyCollider: BoxCollider
{
}

public class TargetScript : MonoBehaviour
{
	public List<ParticleCollisionEvent> collisionEvents;

	public void Start()
	{
		collisionEvents = new List<ParticleCollisionEvent>();
		Debug.Log($"script started!");
	}

	public void OnParticleCollision(GameObject system)
	{
		var particleSystem = system.GetComponent<ParticleSystem>();
		int numCollisionEvents = particleSystem.GetCollisionEvents(gameObject, collisionEvents);
		int i = 0;
		while (i < numCollisionEvents)
		{
			var pos = collisionEvents[i].intersection;
			var force = collisionEvents[i].velocity * 10;
			Debug.Log($"pos={pos} force={force}");
			i++;
		}
	}
}
