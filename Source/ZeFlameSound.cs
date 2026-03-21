using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZeFlammenwerfer
{
	[StaticConstructorOnStartup]
	public class ZeFlameSound
	{
		public static HashSet<ZeFlameSound> allFlameSounds = new();

		static readonly AudioClip start = ContentFinder<AudioClip>.Get($"Flaming-start");
		static readonly AudioClip middle = ContentFinder<AudioClip>.Get($"Flaming-middle");
		static readonly AudioClip end = ContentFinder<AudioClip>.Get($"Flaming-end");
		AudioSource startEndSource;
		AudioSource loopSource;
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
			startEndSource.volume = 1f;
			startEndSource.pitch = 1f;
			startEndSource.minDistance = 1f;
			startEndSource.maxDistance = 100f;
			startEndSource.spatialBlend = 1f;
			loopSource = Create(fire);
			loopSource.volume = 1f;
			loopSource.pitch = 1f;
			loopSource.minDistance = 1f;
			loopSource.maxDistance = 100f;
			loopSource.spatialBlend = 1f;
			loopSource.loop = true;
			allFlameSounds.Add(this);
		}

		public void Remove()
		{
			allFlameSounds.Remove(this);
			started = false;

			if (startEndSource != null)
			{
				startEndSource.Stop();
				startEndSource.clip = null;
				DestroyUnityObject(startEndSource);
			}
			startEndSource = null;
			if (loopSource != null)
			{
				loopSource.Stop();
				loopSource.clip = null;
				DestroyUnityObject(loopSource);
			}
			loopSource = null;
		}

		static void DestroyUnityObject(Object obj)
		{
			if (obj == null)
				return;

			if (Application.isPlaying)
				UnityEngine.Object.Destroy(obj);
			else
				UnityEngine.Object.DestroyImmediate(obj);
		}

		public void Start()
		{
			if (started || startEndSource == null || loopSource == null)
				return;

			startEndSource.clip = start;
			startEndSource.PlayScheduled(AudioSettings.dspTime + 0.25f);
			loopSource.clip = middle;
			loopSource.PlayScheduled(AudioSettings.dspTime + 0.25f + start.length);

			started = true;
		}

		public void Stop()
		{
			if (started == false || startEndSource == null || loopSource == null)
			{
				started = false;
				return;
			}

			startEndSource.clip = end;
			startEndSource.Play();
			loopSource.Stop();

			started = false;
		}

		public void SetPause(bool paused)
		{
			if (started == false || startEndSource == null || loopSource == null)
			{
				started = false;
				return;
			}

			if (paused)
			{
				startEndSource.Pause();
				loopSource.Pause();
			}
			else
			{
				startEndSource.UnPause();
				loopSource.UnPause();
			}

		}
	}
}
