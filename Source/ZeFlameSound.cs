using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	[StaticConstructorOnStartup]
	public class ZeFlameSound
	{
		static readonly AudioClip start = ContentFinder<AudioClip>.Get($"Flaming-start");
		static readonly AudioClip middle = ContentFinder<AudioClip>.Get($"Flaming-middle");
		static readonly AudioClip end = ContentFinder<AudioClip>.Get($"Flaming-end");
		readonly AudioSource startEndSource;
		readonly AudioSource loopSource;
		bool started;

		public AudioSource Create(GameObject go)
		{
			var audioSource = go.AddComponent<AudioSource>();
			audioSource.rolloffMode = AudioRolloffMode.Linear;
			audioSource.dopplerLevel = 0f;
			audioSource.playOnAwake = false;
			return audioSource;
		}

		public ZeFlameSound(GameObject fire)
		{
			startEndSource = Create(fire);
			startEndSource.volume = 1;
			startEndSource.pitch = 1f;
			startEndSource.minDistance = 1f;
			startEndSource.maxDistance = 100f;
			startEndSource.spatialBlend = 1f;
			loopSource = Create(fire);
			loopSource.volume = 1;
			loopSource.pitch = 1f;
			loopSource.minDistance = 1f;
			loopSource.maxDistance = 100f;
			loopSource.spatialBlend = 1f;
			loopSource.loop = true;
		}

		public void Start()
		{
			if (started)
				return;

			startEndSource.clip = start;
			startEndSource.PlayScheduled(AudioSettings.dspTime + 0.25f);
			loopSource.clip = middle;
			loopSource.PlayScheduled(AudioSettings.dspTime + 0.25f + start.length);

			started = true;
		}

		public void Stop()
		{
			if (started == false)
				return;

			startEndSource.clip = end;
			startEndSource.Play();
			loopSource.Stop();

			started = false;
		}
	}
}
