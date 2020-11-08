using UnityEngine;

namespace FlameThrower
{
	public static class Tools
	{
		public static ParticleSystem.MinMaxCurve Between(float min, float max)
		{
			if (min == max)
				return min;

			return new ParticleSystem.MinMaxCurve
			{
				constantMin = min,
				constantMax = max,
				mode = ParticleSystemCurveMode.TwoConstants
			};
		}

		public static ParticleSystem.MinMaxCurve Between(float[] minmax)
		{
			if (minmax[0] == minmax[1])
				return minmax[0];

			return new ParticleSystem.MinMaxCurve
			{
				constantMin = minmax[0],
				constantMax = minmax[1],
				mode = ParticleSystemCurveMode.TwoConstants
			};
		}

		public static ParticleSystem.MinMaxCurve Curve(float size, float min, float max)
		{
			var curve = new AnimationCurve();
			_ = curve.AddKey(0f, min);
			_ = curve.AddKey(1f, max);
			return new ParticleSystem.MinMaxCurve(size, curve);
		}

		public static Gradient NewGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
		{
			var g = new Gradient();
			g.SetKeys(colors, alphas);
			g.mode = GradientMode.Blend;
			return g;
		}
	}
}
