using Verse;

namespace FlameThrower
{
	public static class Renderer
	{
		public static readonly int BlockerCullingLevel = 20;

		public static void Prepare()
		{
			// our blocker layer must be visible
			Find.Camera.cullingMask |= 1 << BlockerCullingLevel;
		}
	}
}
