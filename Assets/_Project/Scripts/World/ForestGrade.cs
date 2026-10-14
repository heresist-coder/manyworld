using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Manyworld
{
    /// <summary>
    /// 숲 현장 색보정: 카메라가 숲(숲길·기운 숲·방) 안에 들어가면 노출과 채도를 내린다.
    /// 목표 샷 02·03은 01(볕 든 초원)보다 한 단 가까이 어둡고 차분하다 — 같은 노출이면 숲 바닥이 1.4~2배 밝았다
    /// (it58 샘플: 03 바닥 143,113,82 / 목표 103,83,72, 02 숲길 153,130,90 / 목표 99,84,65).
    /// 숲길·방을 잇는 축에서 60m 안이 최대, 140m 밖(초원·출발점)은 그대로.
    /// </summary>
    public class ForestGrade : MonoBehaviour
    {
        public ColorAdjustments color;
        public float baseExposure, baseSaturation, baseContrast;
        public float exposureDrop = 0f, saturationDrop = 20f; // (0.6은 잎을 밝힌 뒤에도 02 숲 그늘이 목표의 60~80%였다, it159) // 12는 02 하늘 틈·줄기가 회색으로 죽었다 (it70). 노출 −0.7은 신티 숲 때 값: 각진 소나무 숲은 이미 어두워 03 바닥이 목표의 절반(62 vs 122)이었다 (it97)
        // 빈 방 둘레만 다시 밝힌다: 세 면 나무 벽의 그늘로 바닥이 목표 샷 03의 70%였다 (85 vs 122, 숲길은 오히려 밝았다 134 vs 100)
        public float roomLift = 0.1f; // 방 둘레는 해를 높여(LakeDusk) 바닥에 볕이 들어 덜 밝힌다 (0.4는 밝기 92 vs 목표 76)
        // 철제 나무 길가: 차 세우는 어깨가 비탈 숲 그늘이라 목표 샷 04보다 한 단 어두웠다 (밝기 94 vs 112, it174)
        public float ironLift = 0.2f;
        // 숲길 대비: 목표 샷 02는 볕 든 길과 짙은 줄기의 명암차가 크다 (밝기 표준편차 30 vs 39, it178). 방은 이미 목표보다 대비가 커서 뺀다
        public float contrastBoost = 26f; // (대비를 올리면 어두운 쪽이 더 내려가 평균이 떨어진다 → 숲 노출 내림을 그만큼 줄였다)
        // 호수(노을)는 대비를 낮춘다: 목표 샷 05는 해가 진 뒤의 부드러운 빛 (대비 47 vs 37, it184)
        public float lakeContrastDrop = 12f; // 대비를 내리면 채도도 빠져(93→83) lakeSaturation으로 되돌린다
        public float lakeSaturation = 12f;
        // 방 둘레 대비 내림 (03 대비 40 vs 34), 철제 나무 길가 채도 내림 (04 채도 81 vs 73). it192
        public float roomContrastDrop = 0f, ironSaturationDrop = 8f; // (방 대비는 밝은 바닥·짙은 줄기 구도에서 나온다: 6을 내려도 40→39에 평균만 밝아졌다)
        // 숲길만 채도를 더 내린다 (02 채도 78 vs 68. 방은 이미 목표와 같다)
        public float pathSaturationDrop = 8f;
        float w = -1f, rw = -1f, iw = -1f, lw = -1f;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (color == null || cam == null) return;
            var p = new Vector2(cam.transform.position.x, cam.transform.position.z);
            var a = ForestSiteBuilder.Trailhead;
            var b = ForestSiteBuilder.RoomCenter;
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = Vector2.Distance(p, a + ab * t);
            float target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(140f, 60f, d));
            // 순간이동(캡처·워프)에는 바로, 걸을 땐 천천히
            w = w < 0f || Mathf.Abs(target - w) > 0.5f ? target : Mathf.MoveTowards(w, target, Time.deltaTime * 0.5f);
            float roomTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(45f, 22f, Vector2.Distance(p, b)));
            rw = rw < 0f || Mathf.Abs(roomTarget - rw) > 0.5f ? roomTarget : Mathf.MoveTowards(rw, roomTarget, Time.deltaTime * 0.5f);
            float ironTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(90f, 50f, Vector2.Distance(p, ForestSiteBuilder.IronTree)));
            iw = iw < 0f || Mathf.Abs(ironTarget - iw) > 0.5f ? ironTarget : Mathf.MoveTowards(iw, ironTarget, Time.deltaTime * 0.5f);
            color.postExposure.Override(baseExposure - exposureDrop * w + roomLift * rw + ironLift * iw);
            float lakeTarget = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 105f, Vector2.Distance(p, ForestSiteBuilder.LakeCenter)));
            lw = lw < 0f || Mathf.Abs(lakeTarget - lw) > 0.5f ? lakeTarget : Mathf.MoveTowards(lw, lakeTarget, Time.deltaTime * 0.5f);
            float path = w * (1f - rw) * (1f - iw); // 숲길 (방 둘레·철제 나무 길가는 뺀다: 04 대비가 목표보다 커졌다 61 vs 53)
            color.saturation.Override(baseSaturation - saturationDrop * w * (1f - iw) - pathSaturationDrop * path - ironSaturationDrop * iw + lakeSaturation * lw); // 철제 나무 길가는 숲 채도 내림 대신 따로
            color.contrast.Override(baseContrast + contrastBoost * path - lakeContrastDrop * lw - roomContrastDrop * rw);
        }
    }
}
