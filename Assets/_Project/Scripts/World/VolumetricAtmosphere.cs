using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Manyworld
{
    /// <summary>
    /// Kronnect Volumetric Fog &amp; Mist 2 연결. 유료 에셋이라 레포에 없고 asmdef도 없어서 리플렉션으로 붙는다.
    /// 패키지가 없으면 아무것도 하지 않는다 (RenderSettings 거리 안개만 남는다).
    /// 참고: everret-unity의 AtmosphereFog. 원본 프로필은 건드리지 않고 런타임 사본을 바꾼다.
    /// </summary>
    public static class VolumetricAtmosphere
    {
        public struct Look
        {
            public float density, height, brightness, turbulence, wind, distant, ambient;
            public Color albedo;
            public bool lights; // 손전등 같은 URP 추가 조명이 안개 속 빛줄기를 만든다
        }

        static Type fogType, profileType, managerType;
        static bool resolved;

        public static bool Available
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    fogType = Find("VolumetricFogAndMist2.VolumetricFog");
                    profileType = Find("VolumetricFogAndMist2.VolumetricFogProfile");
                    managerType = Find("VolumetricFogAndMist2.VolumetricFogManager");
                }
                return fogType != null && profileType != null && managerType != null;
            }
        }

        static Type Find(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>size 크기의 상자 안개. 바닥(y=0)부터 height까지 깔린다.</summary>
        public static GameObject Create(Transform parent, Vector3 center, float width, Look look, Light sun)
        {
            if (!Available) return null;
            try
            {
                var create = managerType.GetMethod("CreateFogVolume", BindingFlags.Public | BindingFlags.Static);
                var go = create?.Invoke(null, new object[] { "VolumetricFog" }) as GameObject;
                if (go == null) return null;
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(center.x, look.height * 0.5f - 0.2f, center.z);
                go.transform.localScale = new Vector3(width, look.height, width);

                var fog = go.GetComponent(fogType);
                var profileField = fogType.GetField("profile");
                var baseProfile = profileField.GetValue(fog) as ScriptableObject;
                var profile = baseProfile != null ? Object.Instantiate(baseProfile) : ScriptableObject.CreateInstance(profileType);
                profile.name = "Manyworld Fog (runtime)";
                Set(profile, "density", look.density);
                Set(profile, "albedo", look.albedo);
                Set(profile, "brightness", look.brightness);
                Set(profile, "turbulence", look.turbulence);
                Set(profile, "windDirection", new Vector3(look.wind, 0f, look.wind * 0.35f));
                Set(profile, "noiseScale", 16f);
                Set(profile, "terrainFit", false);
                Set(profile, "dayNightCycle", true);
                Set(profile, "ambientLightMultiplier", look.ambient);
                Set(profile, "distantFog", look.distant > 0f);
                Set(profile, "distantFogStartDistance", 250f);
                Set(profile, "distantFogDistanceDensity", look.distant);
                Set(profile, "dithering", 0.2f);
                profileType.GetMethod("ValidateSettings")?.Invoke(profile, null);
                profileField.SetValue(fog, profile);
                fogType.GetField("enableNativeLights")?.SetValue(fog, look.lights);

                var manager = managerType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (manager != null)
                {
                    Set(manager, "sun", sun);
                    Set(manager, "downscaling", 2f);   // 절반 해상도
                    Set(manager, "blurPasses", 1);
                    Set(manager, "ditherStrength", 0.015f);
                }
                fogType.GetMethod("UpdateMaterialProperties", Type.EmptyTypes)?.Invoke(fog, null);
                return go;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Manyworld] 볼류메트릭 안개 연결 실패, 거리 안개로 대체: {e.Message}");
                return null;
            }
        }

        static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && (value == null || f.FieldType.IsInstanceOfType(value) || f.FieldType == value.GetType())) f.SetValue(target, value);
        }

        /// <summary>세계 규칙에 따른 안개. 독가스는 낮게 깔린 노란 안개, 밤은 푸른 밤안개 + 손전등 빛줄기.</summary>
        public static Look ForWorld(WorldSpec spec)
        {
            if (spec.rules.HasFlag(WorldRule.ToxicGas))
                return new Look { density = 0.32f, height = 6f, brightness = 1.05f, turbulence = 0.7f, wind = 0.02f, distant = 0.35f, ambient = 0.5f, albedo = new Color(0.86f, 0.78f, 0.36f), lights = true };
            if (spec.rules.HasFlag(WorldRule.EternalNight))
                return new Look { density = 0.2f, height = 7f, brightness = 0.9f, turbulence = 0.45f, wind = 0.012f, distant = 0.4f, ambient = 0.5f, albedo = new Color(0.55f, 0.63f, 0.82f), lights = true };
            if (spec.terrain == TerrainMod.Fog)
                return new Look { density = 0.22f, height = 10f, brightness = 1f, turbulence = 0.6f, wind = 0.025f, distant = 0.45f, ambient = 0.45f, albedo = new Color(0.8f, 0.84f, 0.88f) };
            return new Look { density = 0.015f, height = 2.5f, brightness = 0.9f, turbulence = 0.55f, wind = 0.018f, distant = 0f, ambient = 0.35f, albedo = new Color(0.82f, 0.88f, 0.95f) };
        }

        /// <summary>우리 동네: 노을빛 저지대 옅은 안개.</summary>
        public static Look Home => new Look { density = 0.03f, height = 2.2f, brightness = 0.8f, turbulence = 0.5f, wind = 0.015f, distant = 0f, ambient = 0.35f, albedo = new Color(1f, 0.9f, 0.8f) }; // 원거리 안개는 하늘까지 덮어서 끈다
    }
}
