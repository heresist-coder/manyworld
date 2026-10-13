using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Manyworld
{
    /// <summary>
    /// Kronnect Radiant Global Illumination 연결 (화면 공간 실시간 간접광, D27).
    /// 구운 APV가 큰 빛(하늘·지형 반사)을 맡고, 이건 화면에 보이는 밝은 면의 빛을 가까운 그늘로 번지게 한다
    /// (볕 받은 흙바닥이 나무 밑동·인물·바위 아랫면을 따뜻하게 물들인다).
    /// 유료 에셋이라 레포에 없어 리플렉션으로 붙는다. 패키지가 없으면 아무것도 하지 않는다. F10으로 끄고 켠다.
    /// 참고: everret-unity Docs/RADIANT-GI-GUIDE.md (downsampling 1, 시간 누적이라 캡처는 몇십 프레임 뒤에).
    /// </summary>
    public static class ScreenSpaceGI
    {
        public static bool Enabled = true;
        static VolumeComponent current;
        static Type type;
        static bool resolved;

        public static bool Available
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        if ((type = asm.GetType("RadiantGI.Universal.RadiantGlobalIllumination")) != null) break;
                }
                return type != null;
            }
        }

        /// <summary>후처리 프로필에 간접광을 붙인다. intensity 0이면 붙이지 않는다.</summary>
        public static VolumeComponent Add(VolumeProfile profile, float intensity)
        {
            current = null;
            if (intensity <= 0f || !Available) return null;
            try
            {
                var c = profile.Add(type, true);
                Set(c, "indirectIntensity", intensity);
                Set(c, "indirectMaxSourceBrightness", 6f);  // 볕 받은 흙이 하얗게 번지지 않게
                Set(c, "indirectDistanceAttenuation", 0.35f);
                Set(c, "rayCount", 1);
                Set(c, "raytracerAccuracy", 6);
                Set(c, "rayMaxLength", 8f);
                Set(c, "rayMaxSamples", 24);
                Set(c, "thickness", 0.6f);
                Set(c, "rayReuse", 0.35f);
                Set(c, "downsampling", 1f);                 // 2면 점무늬 잡음이 남는다 (everret)
                Set(c, "nearFieldObscurance", 0.3f);        // 바위·밑동이 땅에 붙는 가까운 그늘
                Set(c, "unityAmbientLighting", 1f);         // 구운 APV·환경광은 그대로
                Set(c, "saturation", 1.1f);
                c.active = Enabled;
                current = c;
                return c;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Manyworld] 화면 공간 간접광 연결 실패: {e.Message}");
                return null;
            }
        }

        public static void Toggle()
        {
            Enabled = !Enabled;
            if (current != null) current.active = Enabled;
        }

        static void Set(VolumeComponent c, string field, object value)
        {
            if (!(c.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)?.GetValue(c) is VolumeParameter p)) return;
            var prop = p.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.PropertyType.IsInstanceOfType(value)) return;
            prop.SetValue(p, value);
            p.overrideState = true;
        }
    }
}
