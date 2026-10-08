using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Manyworld
{
    /// <summary>
    /// 하늘·환경광·후처리 (brainstorm-01 3장 아트 방향).
    /// "바랜 필름, 겨울 오후의 비스듬한 빛, 안개 낀 새벽, 노을. 필름 그레인."
    /// 우리 세계는 따뜻하고 그리운 톤, 멀티버스는 같은 구도인데 차갑고 뒤틀린 톤.
    /// </summary>
    public static class WorldLook
    {
        public struct Palette
        {
            public Color skyTint, ground, horizon, sunColor, ambientSky, ambientEquator, ambientGround;
            public float sunIntensity, sunPitch, sunYaw, atmosphere, skyExposure;
            public float postExposure, contrast, saturation, grain, vignette, bloom;
            public Color filter, shadowsTint, highlightsTint;
        }

        /// <summary>우리 동네: 노을 진 겨울 오후. 따뜻하고 바랬다.</summary>
        public static Palette Home => new Palette
        {
            // 하늘 위는 푸르고, 낮게 깔린 해가 지평선만 노을빛으로 물들인다
            skyTint = new Color(0.52f, 0.57f, 0.68f), ground = new Color(0.42f, 0.36f, 0.3f), horizon = new Color(0.95f, 0.72f, 0.5f),
            sunColor = new Color(1f, 0.8f, 0.58f), sunIntensity = 1.4f, sunPitch = 14f, sunYaw = -60f,
            ambientSky = new Color(0.62f, 0.55f, 0.5f), ambientEquator = new Color(0.55f, 0.44f, 0.36f), ambientGround = new Color(0.25f, 0.2f, 0.17f),
            atmosphere = 0.75f, skyExposure = 1.15f,
            postExposure = 0.45f, contrast = 6f, saturation = -4f, grain = 0.2f, vignette = 0.25f, bloom = 0.35f,
            filter = new Color(1f, 0.97f, 0.93f), shadowsTint = new Color(0.45f, 0.45f, 0.58f), highlightsTint = new Color(1f, 0.9f, 0.75f),
        };

        /// <summary>멀티버스: 규칙에 따라. 같은 구도인데 차갑다.</summary>
        public static Palette ForWorld(WorldSpec spec)
        {
            var rng = new System.Random(spec.number * 211 + 9);
            float jitter = (float)rng.NextDouble();
            if (spec.rules.HasFlag(WorldRule.EternalNight))
                return new Palette
                {
                    skyTint = new Color(0.1f, 0.12f, 0.25f), ground = new Color(0.02f, 0.02f, 0.03f), horizon = new Color(0.05f, 0.06f, 0.12f),
                    // 달빛: 어둡지만 윤곽은 보인다. 손전등·전조등이 의미 있게
                    sunColor = new Color(0.5f, 0.58f, 0.9f), sunIntensity = 0.28f, sunPitch = 35f, sunYaw = 20f,
                    ambientSky = new Color(0.13f, 0.15f, 0.24f), ambientEquator = new Color(0.09f, 0.1f, 0.15f), ambientGround = new Color(0.04f, 0.04f, 0.06f),
                    atmosphere = 0.4f, skyExposure = 0.08f,
                    postExposure = 0.6f, contrast = 12f, saturation = -22f, grain = 0.3f, vignette = 0.4f, bloom = 0.6f,
                    filter = new Color(0.85f, 0.9f, 1f), shadowsTint = new Color(0.3f, 0.35f, 0.6f), highlightsTint = new Color(0.75f, 0.8f, 1f),
                };
            if (spec.rules.HasFlag(WorldRule.ToxicGas))
                return new Palette
                {
                    skyTint = new Color(0.75f, 0.7f, 0.35f), ground = new Color(0.3f, 0.28f, 0.15f), horizon = new Color(0.78f, 0.72f, 0.4f),
                    sunColor = new Color(1f, 0.9f, 0.55f), sunIntensity = 0.95f, sunPitch = 30f, sunYaw = -30f,
                    ambientSky = new Color(0.6f, 0.58f, 0.36f), ambientEquator = new Color(0.5f, 0.47f, 0.3f), ambientGround = new Color(0.2f, 0.19f, 0.12f),
                    atmosphere = 2.6f, skyExposure = 0.9f,
                    postExposure = 0.4f, contrast = 6f, saturation = -10f, grain = 0.25f, vignette = 0.32f, bloom = 0.3f,
                    filter = new Color(1f, 0.97f, 0.82f), shadowsTint = new Color(0.4f, 0.42f, 0.3f), highlightsTint = new Color(1f, 0.92f, 0.65f),
                };
            if (spec.rules.HasFlag(WorldRule.SoundReactive))
                return new Palette
                {
                    // 소리가 금기인 세계: 창백하고 고요하다
                    skyTint = new Color(0.55f, 0.6f, 0.65f), ground = new Color(0.3f, 0.31f, 0.32f), horizon = new Color(0.78f, 0.8f, 0.82f),
                    sunColor = new Color(0.9f, 0.93f, 1f), sunIntensity = 0.9f, sunPitch = 22f + jitter * 10f, sunYaw = -40f,
                    ambientSky = new Color(0.55f, 0.58f, 0.62f), ambientEquator = new Color(0.45f, 0.47f, 0.5f), ambientGround = new Color(0.22f, 0.23f, 0.24f),
                    atmosphere = 0.9f, skyExposure = 1f,
                    postExposure = 0.5f, contrast = 5f, saturation = -20f, grain = 0.22f, vignette = 0.28f, bloom = 0.25f,
                    filter = new Color(0.93f, 0.96f, 1f), shadowsTint = new Color(0.4f, 0.45f, 0.55f), highlightsTint = new Color(0.9f, 0.93f, 1f),
                };
            // 특이 사항 없음: 흐린 겨울 오후. 번호마다 해 높이가 조금 다르다
            return new Palette
            {
                skyTint = Color.Lerp(new Color(0.45f, 0.55f, 0.7f), new Color(0.6f, 0.55f, 0.6f), jitter), ground = new Color(0.3f, 0.3f, 0.28f), horizon = new Color(0.75f, 0.78f, 0.8f),
                sunColor = new Color(0.95f, 0.9f, 0.82f), sunIntensity = 1.05f, sunPitch = 20f + jitter * 18f, sunYaw = -35f + jitter * 40f,
                ambientSky = new Color(0.5f, 0.55f, 0.62f), ambientEquator = new Color(0.45f, 0.46f, 0.45f), ambientGround = new Color(0.22f, 0.21f, 0.19f),
                atmosphere = 1.1f, skyExposure = 1f,
                postExposure = 0.45f, contrast = 7f, saturation = -10f, grain = 0.2f, vignette = 0.28f, bloom = 0.3f,
                filter = new Color(0.95f, 0.98f, 1f), shadowsTint = new Color(0.35f, 0.42f, 0.55f), highlightsTint = new Color(1f, 0.93f, 0.82f),
            };
        }

        /// <summary>하늘·해·환경광·후처리를 적용한다. parent가 사라지면 후처리도 같이 사라진다.</summary>
        public static void Apply(Palette p, Light sun, Transform parent, Camera cam)
        {
            // 해: 겨울 오후의 비스듬한 빛
            sun.transform.rotation = Quaternion.Euler(p.sunPitch, p.sunYaw, 0f);
            sun.color = p.sunColor;
            sun.intensity = p.sunIntensity;
            RenderSettings.sun = sun;

            // 하늘
            var template = Resources.Load<Material>("Materials/Sky");
            Material sky = template != null ? new Material(template) : null;
            if (sky == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh != null) sky = new Material(sh);
            }
            if (sky != null)
            {
                sky.SetColor("_SkyTint", p.skyTint);
                sky.SetColor("_GroundColor", p.ground);
                sky.SetFloat("_AtmosphereThickness", p.atmosphere);
                sky.SetFloat("_Exposure", p.skyExposure);
                sky.SetFloat("_SunSize", 0.035f);
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = p.horizon;
            }

            // 환경광: 하늘·지평선·땅 세 색
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            if (RenderSettings.fog) RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, p.horizon, 0.5f);

            // 후처리: 톤매핑 + 바랜 색 + 필름 그레인 + 비네트
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            var old = parent.Find("PostFX");
            if (old != null) Object.Destroy(old.gameObject);
            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(p.postExposure);
            color.contrast.Override(p.contrast);
            color.saturation.Override(p.saturation);
            color.colorFilter.Override(p.filter);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(p.shadowsTint);
            split.highlights.Override(p.highlightsTint);
            split.balance.Override(10f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(p.vignette);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(p.grain);
            grain.response.Override(0.75f);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(p.bloom);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);
        }
    }
}
