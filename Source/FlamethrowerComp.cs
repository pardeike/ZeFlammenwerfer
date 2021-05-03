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

		public static HashSet<ParticleSystem> allParticleSystems = new HashSet<ParticleSystem>();

		public override void Initialize(CompProperties props)
		{
			var maxRange = parent.def.Verbs[0].range;

			fire = Object.Instantiate(Assets.fire);
			_ = fire.AddComponent(typeof(FlamethrowerOwner));
			fire.transform.localScale = new Vector3(maxRange, 1, maxRange);

			smoke = Object.Instantiate(Assets.smoke);
			smoke.transform.localScale = new Vector3(maxRange, 1, maxRange);

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

		public void SetActive(bool active)
		{
			var emission1 = fire.GetComponent<ParticleSystem>().emission;
			emission1.enabled = active;
			var emission2 = smoke.GetComponent<ParticleSystem>().emission;
			emission2.enabled = active;
		}
	}
}
