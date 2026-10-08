using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 효과음. Resources/Audio의 WAV(Agent Audio로 생성)를 이름으로 튼다.
    /// 마스터 볼륨·음소거는 PlayerPrefs에 저장한다 (M 키, 일시 정지 메뉴).
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> LastPlayed = new Dictionary<string, float>();
        static readonly List<AudioSource> Pool = new List<AudioSource>();
        static GameObject host;
        static float master = -1f;
        static bool muted;

        public static float Master
        {
            get
            {
                if (master < 0f)
                {
                    master = PlayerPrefs.GetFloat("mw_master_volume", 0.8f);
                    muted = PlayerPrefs.GetInt("mw_muted", 0) == 1;
                }
                return master;
            }
            set
            {
                master = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("mw_master_volume", master);
                Apply();
            }
        }

        public static bool Muted
        {
            get { _ = Master; return muted; }
            set
            {
                muted = value;
                PlayerPrefs.SetInt("mw_muted", muted ? 1 : 0);
                Apply();
            }
        }

        static void Apply() => AudioListener.volume = Muted ? 0f : Master;

        public static void Init() => Apply();

        public static AudioClip Clip(string name)
        {
            if (Clips.TryGetValue(name, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) Debug.LogWarning($"[Sfx] 없는 클립: {name}");
            Clips[name] = c;
            return c;
        }

        static AudioSource Source()
        {
            if (host == null)
            {
                host = new GameObject("Sfx");
                Object.DontDestroyOnLoad(host);
                Pool.Clear();
                Apply();
            }
            foreach (var s in Pool)
                if (s != null && !s.isPlaying) return s;
            var go = new GameObject("SfxVoice");
            go.transform.SetParent(host.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            Pool.Add(src);
            return src;
        }

        /// <summary>같은 소리가 minInterval 안에 겹치면 건너뛴다.</summary>
        static bool Throttle(string name, float minInterval)
        {
            if (minInterval <= 0f) return false;
            if (LastPlayed.TryGetValue(name, out var t) && Time.unscaledTime - t < minInterval) return true;
            LastPlayed[name] = Time.unscaledTime;
            return false;
        }

        public static void Play(string name, float volume = 1f, float pitchJitter = 0.05f, float minInterval = 0f)
        {
            if (Throttle(name, minInterval)) return;
            var clip = Clip(name);
            if (clip == null) return;
            var s = Source();
            s.transform.localPosition = Vector3.zero;
            s.spatialBlend = 0f;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume);
        }

        public static void PlayAt(string name, Vector3 pos, float volume = 1f, float maxDistance = 60f, float pitchJitter = 0.06f, float minInterval = 0f)
        {
            if (Throttle(name, minInterval)) return;
            var clip = Clip(name);
            if (clip == null) return;
            var s = Source();
            s.transform.position = pos;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume);
        }

        /// <summary>오브젝트에 붙는 루프. 오브젝트가 사라지면 같이 사라진다.</summary>
        public static AudioSource Loop(string name, Transform parent, float volume, bool spatial, float maxDistance = 25f)
        {
            var clip = Clip(name);
            var go = new GameObject("Loop_" + name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            if (clip != null)
            {
                s.time = Random.Range(0f, clip.length * 0.9f);
                s.Play();
            }
            return s;
        }
    }
}
