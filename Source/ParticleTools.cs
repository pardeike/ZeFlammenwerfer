using UnityEngine;

namespace FlameThrower
{
	public static class ParticleTools
	{
		static int counter = 0;
		public static GameObject NewSystem()
		{
			return new GameObject("particle system " + ++counter);
		}

		public static void Configure(this ParticleSystem ps,
			float simulationSpeed,
			float duration,
			float startDelay,
			float[] startLifetime,
			float startSpeed,
			float[] startSize,
			int maxParticles
		)
		{
			var main = ps.main;
			main.duration = duration;
			main.startDelay = startDelay;
			main.loop = true;
			main.startDelay = 0f;
			main.startLifetime = Tools.Between(startLifetime);
			main.startSpeed = startSpeed;
			main.startSize = Tools.Between(startSize);
			main.startColor = Color.white;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.simulationSpeed = simulationSpeed;
			main.scalingMode = ParticleSystemScalingMode.Hierarchy;
			main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Rigidbody;
			main.maxParticles = maxParticles;
			main.playOnAwake = true;
		}

		public static void AddEmission(this ParticleSystem ps, float rateOverTime)
		{
			var em = ps.emission;
			em.enabled = true;
			em.rateOverTime = rateOverTime;
		}

		public static void AddShape(this ParticleSystem ps, float angle, float length, float radiusThickness, bool useVolume)
		{
			var sh = ps.shape;
			sh.enabled = true;
			sh.shapeType = useVolume ? ParticleSystemShapeType.ConeVolume : ParticleSystemShapeType.Cone;
			sh.angle = angle;
			sh.radius = 0.0001f;
			sh.radiusThickness = radiusThickness;
			sh.arc = 360f;
			sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
			sh.arcSpread = 0f;
			sh.length = length;
		}

		public static void AddDynamicColor(this ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alphas)
		{
			var col = ps.colorOverLifetime;
			col.enabled = true;
			col.color = Tools.NewGradient(colors, alphas);
		}

		public static void AddDynamicSize(this ParticleSystem ps, float sizeMultiplier, float min, float max)
		{
			var sol = ps.sizeOverLifetime;
			sol.separateAxes = false;
			sol.enabled = true;
			sol.size = Tools.Curve(sizeMultiplier, min, max);
			sol.sizeMultiplier = 1f;
		}

		public static void AddDynamicRotation(this ParticleSystem ps, float min, float max)
		{
			var rol = ps.rotationOverLifetime;
			rol.enabled = true;
			rol.separateAxes = true;
			rol.x = 0; //Tools.Between(min, max);
			rol.y = 0; //Tools.Between(min, max);
			rol.z = Tools.Between(min, max);
		}

		public static void AddTextureSheet(this ParticleSystem ps, int dim)
		{
			var tsa = ps.textureSheetAnimation;
			tsa.enabled = true;
			tsa.mode = ParticleSystemAnimationMode.Grid;
			tsa.numTilesX = dim;
			tsa.numTilesY = dim;
			tsa.animation = ParticleSystemAnimationType.WholeSheet;
			tsa.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
			tsa.frameOverTime = Tools.Curve(dim * dim, 1f, 1f);
			tsa.frameOverTimeMultiplier = 1f;
			tsa.startFrame = Tools.Between(0f, dim * dim - 0.0001f);
			tsa.cycleCount = 1;
		}

		public static void AddRenderer(this ParticleSystem ps, Material mat, int sortingOrder, float minParticleSize, float maxParticleSize, bool sortByDistance)
		{
			var pr = ps.GetComponent<ParticleSystemRenderer>();
			pr.enabled = true;
			pr.sortingOrder = sortingOrder;
			pr.renderMode = ParticleSystemRenderMode.Billboard;
			pr.sortMode = sortByDistance ? ParticleSystemSortMode.Distance : ParticleSystemSortMode.None;
			pr.normalDirection = 1f;
			pr.minParticleSize = minParticleSize;
			pr.maxParticleSize = maxParticleSize;
			pr.material = mat;
		}
	}
}
