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
        public float baseExposure, baseSaturation;
        public float exposureDrop = 0.6f, saturationDrop = 7f; // 12는 02 하늘 틈·줄기가 회색으로 죽었다 (it70). 노출 −0.7은 신티 숲 때 값: 각진 소나무 숲은 이미 어두워 03 바닥이 목표의 절반(62 vs 122)이었다 (it97)
        // 빈 방 둘레만 다시 밝힌다: 세 면 나무 벽의 그늘로 바닥이 목표 샷 03의 70%였다 (85 vs 122, 숲길은 오히려 밝았다 134 vs 100)
        public float roomLift = 0.95f;
        float w = -1f, rw = -1f;

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
            color.postExposure.Override(baseExposure - exposureDrop * w + roomLift * rw);
            color.saturation.Override(baseSaturation - saturationDrop * w);
        }
    }
}
