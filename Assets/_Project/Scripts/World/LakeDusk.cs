using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 호수 둘레만 해 질 녘 (목표 샷 05). 카메라가 호수에 다가가면 해가 동남동 지평 가까이 낮아지고
    /// 하늘·구름·안개·환경광이 노을빛으로 바뀐다. 호수 중심 170m 밖은 숲 현장의 한낮 그대로, 105m 안은 완전한 노을.
    /// (방은 호수에서 약 176m라 영향이 없다)
    /// </summary>
    public class LakeDusk : MonoBehaviour
    {
        public Light sun;
        Material sky, clouds;
        Quaternion sunRot0;
        Color sunColor0, fog0, amb0Sky, amb0Eq, amb0Ground;
        float sunIntensity0;
        Color sky0Zenith, sky0Mid, sky0Horizon, sky0Glow;
        float sky0GlowSize, sky0GlowStrength;
        Color cloud0Lit, cloud0Shade, cloud0Horizon;
        Vector4 cloud0Sun;
        float w = -1f, rw = -1f;
        Quaternion roomSun;

        static readonly Quaternion DuskSun = Quaternion.Euler(8f, 290f, 0f); // 동남동에서 낮게 (서쪽 물가에서 동쪽을 보면 오른쪽 앞)

        void Start()
        {
            if (sun != null) { sunRot0 = sun.transform.rotation; sunColor0 = sun.color; sunIntensity0 = sun.intensity; }
            // 빈 방 둘레는 해를 높인다 (같은 방위, 고도 40°): 22° 해는 세 면 벽 소나무 수관에 막혀 방 바닥이 거의 그늘이었다.
            // 목표 샷 03은 바닥에 햇빛 조각과 긴 나무 그림자가 선명하다
            var e = sunRot0.eulerAngles;
            roomSun = Quaternion.Euler(40f, e.y, e.z);
            fog0 = RenderSettings.fogColor;
            amb0Sky = RenderSettings.ambientSkyColor; amb0Eq = RenderSettings.ambientEquatorColor; amb0Ground = RenderSettings.ambientGroundColor;
            sky = RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_ZenithColor") ? RenderSettings.skybox : null;
            if (sky != null)
            {
                sky0Zenith = sky.GetColor("_ZenithColor"); sky0Mid = sky.GetColor("_MidColor"); sky0Horizon = sky.GetColor("_HorizonColor"); sky0Glow = sky.GetColor("_SunGlow");
                sky0GlowSize = sky.GetFloat("_SunGlowSize"); sky0GlowStrength = sky.GetFloat("_SunGlowStrength");
            }
            var dome = GameObject.Find("CloudDome");
            clouds = dome != null ? dome.GetComponent<Renderer>().sharedMaterial : null;
            if (clouds != null)
            {
                cloud0Lit = clouds.GetColor("_LitColor"); cloud0Shade = clouds.GetColor("_ShadeColor"); cloud0Horizon = clouds.GetColor("_HorizonColor");
                cloud0Sun = clouds.GetVector("_SunDir");
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var lc = ForestSiteBuilder.LakeCenter;
            float d = Vector2.Distance(new Vector2(cam.transform.position.x, cam.transform.position.z), lc);
            float target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 105f, d));
            float dr = Vector2.Distance(new Vector2(cam.transform.position.x, cam.transform.position.z), ForestSiteBuilder.RoomCenter);
            float roomTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 35f, dr));
            if (Mathf.Abs(target - w) < 0.002f && Mathf.Abs(roomTarget - rw) < 0.002f) return;
            w = target;
            rw = roomTarget;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Slerp(sunRot0, roomSun, rw), DuskSun, w);
                sun.color = Color.Lerp(sunColor0, new Color(1f, 0.62f, 0.4f), w);
                sun.intensity = Mathf.Lerp(sunIntensity0, 0.8f, w); // 목표 샷 05는 해가 산 너머로 진 뒤: 굳은 그림자 없이 하늘빛이 주 (1.35는 전망대 흙 마당이 화면 아래를 밝게 채웠다 145~153 vs 66~102)
            }
            RenderSettings.fogColor = Color.Lerp(fog0, new Color(0.52f, 0.5f, 0.66f), w); // 먼 산이 보랏빛 파랑으로
            RenderSettings.ambientSkyColor = Color.Lerp(amb0Sky, new Color(0.56f, 0.56f, 0.72f), w);
            RenderSettings.ambientEquatorColor = Color.Lerp(amb0Eq, new Color(0.64f, 0.5f, 0.46f), w);
            RenderSettings.ambientGroundColor = Color.Lerp(amb0Ground, new Color(0.2f, 0.17f, 0.17f), w);
            if (sky != null)
            {
                sky.SetColor("_ZenithColor", Color.Lerp(sky0Zenith, new Color(0.27f, 0.35f, 0.6f), w));
                sky.SetColor("_MidColor", Color.Lerp(sky0Mid, new Color(0.62f, 0.58f, 0.72f), w));
                sky.SetColor("_HorizonColor", Color.Lerp(sky0Horizon, new Color(0.99f, 0.74f, 0.54f), w));
                sky.SetColor("_SunGlow", Color.Lerp(sky0Glow, new Color(1f, 0.6f, 0.34f), w));
                sky.SetFloat("_SunGlowSize", Mathf.Lerp(sky0GlowSize, 5f, w));
                sky.SetFloat("_SunGlowStrength", Mathf.Lerp(sky0GlowStrength, 1.1f, w));
            }
            if (clouds != null)
            {
                clouds.SetColor("_LitColor", Color.Lerp(cloud0Lit, new Color(1f, 0.74f, 0.56f), w));
                clouds.SetColor("_ShadeColor", Color.Lerp(cloud0Shade, new Color(0.5f, 0.45f, 0.62f), w));
                clouds.SetColor("_HorizonColor", Color.Lerp(cloud0Horizon, new Color(0.96f, 0.74f, 0.6f), w));
                var toSun = -(DuskSun * Vector3.forward);
                clouds.SetVector("_SunDir", Vector4.Lerp(cloud0Sun, toSun, w));
            }
        }
    }
}
