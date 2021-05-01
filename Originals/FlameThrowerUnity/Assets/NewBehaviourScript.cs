using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
	public List<ParticleCollisionEvent> collisionEvents;
	public object thing;

	public void Start()
	{
		Debug.Log($"Start");

		collisionEvents = new List<ParticleCollisionEvent>();
	}

	public void OnParticleCollision(GameObject system)
	{
		Debug.Log($"system={system}");

		var particleSystem = system.GetComponent<ParticleSystem>();
		int numCollisionEvents = particleSystem.GetCollisionEvents(gameObject, collisionEvents);
		int i = 0;
		while (i < numCollisionEvents)
		{
			var pos = collisionEvents[i].intersection;
			var force = collisionEvents[i].velocity * 10;
			Debug.Log($"thing={thing} pos={pos} force={force}");
			i++;
		}
	}
}