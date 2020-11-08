using UnityEngine;

namespace FlameThrower
{
	public static class FlameThrower
	{
		public static GameObject CreateFire()
		{
			var go = ParticleTools.NewSystem();
			var ps = go.AddComponent<ParticleSystem>();

			ps.Configure(
				simulationSpeed: 1.5f,
				duration: 0f,
				startDelay: 0f,
				startLifetime: new[] { 3f, 3f },
				startSpeed: 1f,
				startSize: new[] { 0.6f, 0.6f },
				maxParticles: 2000
			);

			ps.AddEmission(80f);
			ps.AddShape(2f, 5f, 0f, false);
			ps.AddDynamicColor(new[] {
					new GradientColorKey(new Color(0.3066038f, 0.5203553f, 1f), 0f),
					new GradientColorKey(new Color(1f, 0.5949937f, 0f), 0.182f),
					new GradientColorKey(new Color(1f, 0.216212f, 0f), 0.4f),
					new GradientColorKey(Color.white, 1f),
				}, new[] {
					new GradientAlphaKey(100f/255f, 0f),
					new GradientAlphaKey(1f, 0.371f),
					new GradientAlphaKey(0f, 0.506f),
					new GradientAlphaKey(0f, 1f),
			});
			ps.AddDynamicSize(2f, 0.05f, 1f);
			ps.AddDynamicRotation(-5f, 5f);
			ps.AddTextureSheet(2);
			ps.AddRenderer(Assets.flamesMat, 2, 0f, 1f, false);

			return go;
		}

		public static GameObject CreateSmoke()
		{
			var go = ParticleTools.NewSystem();
			var ps = go.AddComponent<ParticleSystem>();

			ps.Configure(
				simulationSpeed: 0.5f,
				duration: 0.05f,
				startDelay: 0.2f,
				startLifetime: new[] { 3f, 5f },
				startSpeed: 0.5f,
				startSize: new[] { 0f, 1.5f },
				maxParticles: 1000
			);

			ps.AddEmission(50f);
			ps.AddShape(4f, 0.2f, 1f, true);
			ps.AddDynamicColor(new[] {
					new GradientColorKey(Color.black, 0.75f),
					new GradientColorKey(Color.white, 1f),
				}, new[] {
					new GradientAlphaKey(0f, 0.197f),
					new GradientAlphaKey(104f/255f, 0.5f),
					new GradientAlphaKey(0f, 1f),
			});
			ps.AddDynamicSize(1f, 0f, 1f);
			ps.AddDynamicRotation(-5f, 5f);
			ps.AddTextureSheet(5);
			ps.AddRenderer(Assets.smokesMat, 1, 0f, 0.1f, true);

			return go;
		}
	}
}
