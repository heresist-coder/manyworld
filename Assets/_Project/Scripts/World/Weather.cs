using UnityEngine;

namespace Manyworld
{
    public enum WeatherKind { Clear, Overcast, Rain, Snow, MorningFog }
    public enum TimeOfDay { Morning, Afternoon, Dusk }

    /// <summary>
    /// 날씨와 시간대. 우리 동네는 날짜로, 다른 세계는 세계 번호 + 날짜로 정해진다 (같은 날 다시 가면 같은 날씨).
    /// 팔레트·안개를 고치고, 비·눈 입자와 빗소리를 카메라에 붙인다.
    /// </summary>
    public struct WeatherState
    {
        public WeatherKind kind;
        public TimeOfDay time;

        public string Label
        {
            get
            {
                string t = time switch { TimeOfDay.Morning => "아침", TimeOfDay.Dusk => "해 질 녘", _ => "오후" };
                string w = kind switch
                {
                    WeatherKind.Overcast => "흐림",
                    WeatherKind.Rain => "비",
                    WeatherKind.Snow => "눈",
                    WeatherKind.MorningFog => "안개",
                    _ => "맑음",
                };
                return $"{w} · {t}";
            }
        }

        /// <summary>빗소리가 발소리·총소리를 묻는다.</summary>
        public float NoiseMultiplier => kind == WeatherKind.Rain ? 0.75f : kind == WeatherKind.Snow ? 0.85f : 1f;

        public static WeatherState ForHome(int day, int month)
        {
            var rng = new System.Random(day * 977 + 13);
            double r = rng.NextDouble();
            bool winter = month <= 3 || month >= 11;
            WeatherKind k = r < 0.45 ? WeatherKind.Clear
                : r < 0.68 ? WeatherKind.Overcast
                : r < 0.84 ? (winter ? WeatherKind.Snow : WeatherKind.Rain)
                : WeatherKind.MorningFog;
            var t = (TimeOfDay)rng.Next(3);
            if (k == WeatherKind.MorningFog) t = TimeOfDay.Morning;
            return new WeatherState { kind = k, time = t };
        }

        public static WeatherState ForWorld(WorldSpec spec, int day)
        {
            var rng = new System.Random(spec.number * 4211 + day * 17 + 5);
            double r = rng.NextDouble();
            // 규칙이 있는 세계는 그 규칙이 하늘을 정한다. 비만 가끔
            if (spec.rules.HasFlag(WorldRule.ToxicGas))
                return new WeatherState { kind = WeatherKind.Clear, time = TimeOfDay.Afternoon };
            if (spec.rules.HasFlag(WorldRule.EternalNight))
                return new WeatherState { kind = r < 0.3 ? WeatherKind.Snow : WeatherKind.Clear, time = TimeOfDay.Afternoon };
            if (spec.terrain == TerrainMod.Fog)
                return new WeatherState { kind = r < 0.4 ? WeatherKind.Rain : WeatherKind.Overcast, time = (TimeOfDay)rng.Next(3) };
            WeatherKind k = r < 0.35 ? WeatherKind.Clear
                : r < 0.6 ? WeatherKind.Overcast
                : r < 0.78 ? WeatherKind.Rain
                : r < 0.9 ? WeatherKind.Snow
                : WeatherKind.MorningFog;
            var t = (TimeOfDay)rng.Next(3);
            if (k == WeatherKind.MorningFog) t = TimeOfDay.Morning;
            return new WeatherState { kind = k, time = t };
        }

        /// <summary>시간대와 날씨로 팔레트를 고친다. 영원한 밤은 시간대를 무시한다.</summary>
        public WorldLook.Palette Modify(WorldLook.Palette p, bool night = false)
        {
            if (!night)
            {
                switch (time)
                {
                    case TimeOfDay.Morning:
                        p.sunYaw += 150f; p.sunPitch = Mathf.Clamp(p.sunPitch, 12f, 24f);
                        p.sunColor = Color.Lerp(p.sunColor, new Color(1f, 0.9f, 0.78f), 0.6f);
                        p.horizon = Color.Lerp(p.horizon, new Color(0.85f, 0.85f, 0.88f), 0.5f);
                        p.highlightsTint = Color.Lerp(p.highlightsTint, new Color(0.95f, 0.95f, 1f), 0.5f);
                        p.saturation -= 4f;
                        break;
                    case TimeOfDay.Dusk:
                        p.sunPitch = 7f;
                        p.sunColor = Color.Lerp(p.sunColor, new Color(1f, 0.6f, 0.38f), 0.7f);
                        p.sunIntensity *= 0.85f;
                        p.horizon = Color.Lerp(p.horizon, new Color(1f, 0.6f, 0.4f), 0.6f);
                        p.skyTint = Color.Lerp(p.skyTint, new Color(0.55f, 0.48f, 0.6f), 0.4f);
                        p.ambientSky *= 0.82f; p.ambientEquator = Color.Lerp(p.ambientEquator, new Color(0.6f, 0.42f, 0.32f), 0.4f);
                        p.highlightsTint = Color.Lerp(p.highlightsTint, new Color(1f, 0.8f, 0.6f), 0.5f);
                        p.atmosphere += 0.3f;
                        break;
                }
            }
            switch (kind)
            {
                case WeatherKind.Overcast:
                case WeatherKind.Rain:
                case WeatherKind.Snow:
                    float heavy = kind == WeatherKind.Overcast ? 0.6f : 0.8f;
                    var grey = kind == WeatherKind.Snow ? new Color(0.7f, 0.72f, 0.76f) : new Color(0.55f, 0.58f, 0.6f);
                    p.skyTint = Color.Lerp(p.skyTint, grey, heavy);
                    p.horizon = Color.Lerp(p.horizon, grey, heavy);
                    p.sunIntensity *= kind == WeatherKind.Overcast ? 0.45f : 0.3f;
                    p.atmosphere += 0.6f;
                    p.skyExposure *= kind == WeatherKind.Snow ? 0.95f : 0.8f;
                    // 해가 가려지면 하늘빛이 고르게 내려온다
                    p.ambientSky = Color.Lerp(p.ambientSky, grey * 1.05f, 0.6f);
                    p.ambientEquator = Color.Lerp(p.ambientEquator, grey * 0.9f, 0.6f);
                    p.saturation -= kind == WeatherKind.Rain ? 10f : 6f;
                    p.contrast -= 2f;
                    p.shadowsTint = Color.Lerp(p.shadowsTint, new Color(0.4f, 0.45f, 0.52f), 0.5f);
                    break;
                case WeatherKind.MorningFog:
                    p.sunIntensity *= 0.6f;
                    p.ambientSky = Color.Lerp(p.ambientSky, new Color(0.7f, 0.72f, 0.74f), 0.4f);
                    p.saturation -= 6f;
                    p.bloom += 0.15f;
                    break;
            }
            return p;
        }

        public VolumetricAtmosphere.Look Modify(VolumetricAtmosphere.Look l)
        {
            switch (kind)
            {
                case WeatherKind.MorningFog:
                    l.density = Mathf.Max(l.density, 0.12f); l.height = Mathf.Max(l.height, 7f); l.albedo = Color.Lerp(l.albedo, Color.white, 0.5f);
                    break;
                case WeatherKind.Rain:
                    l.density = Mathf.Max(l.density, 0.05f); l.height = Mathf.Max(l.height, 5f); l.wind *= 2f;
                    l.albedo = Color.Lerp(l.albedo, new Color(0.7f, 0.74f, 0.78f), 0.5f);
                    break;
                case WeatherKind.Snow:
                    l.density = Mathf.Max(l.density, 0.04f); l.height = Mathf.Max(l.height, 4f);
                    l.albedo = Color.Lerp(l.albedo, Color.white, 0.6f);
                    break;
                case WeatherKind.Overcast:
                    l.density = Mathf.Max(l.density, 0.025f);
                    break;
            }
            return l;
        }

        /// <summary>비·눈 입자와 빗소리. parent가 사라지면 같이 사라진다.</summary>
        public GameObject AttachFx(Transform parent)
        {
            var old = parent.Find("WeatherFx");
            if (old != null) Object.Destroy(old.gameObject);
            if (kind != WeatherKind.Rain && kind != WeatherKind.Snow) return null;
            var go = new GameObject("WeatherFx");
            go.transform.SetParent(parent, false);
            go.AddComponent<WeatherFx>().Init(kind);
            return go;
        }
    }

    /// <summary>카메라를 따라다니는 강수 입자.</summary>
    public class WeatherFx : MonoBehaviour
    {
        static Texture2D dot;
        ParticleSystem ps;
        AudioSource rain;

        public void Init(WeatherKind kind)
        {
            bool snow = kind == WeatherKind.Snow;
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = snow ? 7f : 1.1f;
            main.startSpeed = 0f;
            main.startSize = snow ? new ParticleSystem.MinMaxCurve(0.05f, 0.11f) : new ParticleSystem.MinMaxCurve(0.025f, 0.035f);
            main.startColor = snow ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.75f, 0.8f, 0.88f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = snow ? 4000 : 6000;
            main.gravityModifier = 0f;

            var em = ps.emission;
            em.rateOverTime = snow ? 520f : 4200f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 1f, 44f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = snow ? new ParticleSystem.MinMaxCurve(-0.4f, 0.6f) : new ParticleSystem.MinMaxCurve(1.2f, 1.6f);
            vel.y = snow ? new ParticleSystem.MinMaxCurve(-1.6f, -1.1f) : new ParticleSystem.MinMaxCurve(-21f, -17f);
            vel.z = snow ? new ParticleSystem.MinMaxCurve(-0.3f, 0.3f) : new ParticleSystem.MinMaxCurve(0.3f, 0.5f);

            if (snow)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.5f;
                noise.frequency = 0.4f;
            }

            var r = GetComponent<ParticleSystemRenderer>();
            var template = Resources.Load<Material>("Materials/Particle");
            Material mat = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            if (dot == null) dot = MakeDot();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", dot);
            mat.mainTexture = dot;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (snow) r.renderMode = ParticleSystemRenderMode.Billboard;
            else
            {
                // 빗줄기: 속도 방향으로 늘린다
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1f;
            }
            Follow();
            ps.Play();
            ps.Simulate(snow ? 6f : 1f, true, false); // 시작부터 내리고 있게
            ps.Play();

            if (!snow) rain = Sfx.Loop("rain_loop", transform, 0.45f, false);
        }

        static Texture2D MakeDot()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "WeatherDot" };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            t.Apply();
            return t;
        }

        void LateUpdate() => Follow();

        void Follow()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var p = cam.transform.position;
            // 진행 방향 앞쪽에 조금 더 뿌린다 (운전 중에도 비가 앞에서 맞는다)
            transform.position = p + Vector3.up * 14f + Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) * 8f;
            transform.rotation = Quaternion.identity;
        }
    }
}
