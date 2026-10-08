using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 80년대 한국 건물 키트 (코드로 짓는다). Synty는 서양식이라 동네 분위기가 안 나서 만들었다.
    /// · 단층 슬레이트집: 시멘트 벽, 슬레이트·양철 박공지붕, 담장과 철 대문, 장독대, 연탄 연통, TV 안테나
    /// · 상가주택: 2~3층 시멘트 건물, 1층 가게(간판·셔터·차양), 옥상 물탱크
    /// 정면은 로컬 +Z. 판정은 몸통 콜라이더 하나.
    /// </summary>
    public static class KoreanBuildings
    {
        static readonly Color[] Plaster =
        {
            new Color(0.86f, 0.83f, 0.76f), new Color(0.8f, 0.78f, 0.72f), new Color(0.74f, 0.8f, 0.82f),
            new Color(0.88f, 0.8f, 0.68f), new Color(0.78f, 0.74f, 0.68f),
        };
        static readonly Color[] Roofs =
        {
            new Color(0.32f, 0.33f, 0.36f), // 슬레이트
            new Color(0.55f, 0.24f, 0.2f),  // 빛바랜 빨간 양철
            new Color(0.25f, 0.38f, 0.52f), // 파란 양철
            new Color(0.36f, 0.42f, 0.3f),  // 녹색 양철
        };
        static readonly Color[] Signs =
        {
            new Color(0.75f, 0.15f, 0.12f), new Color(0.15f, 0.3f, 0.6f), new Color(0.15f, 0.45f, 0.25f), new Color(0.85f, 0.65f, 0.15f), new Color(0.95f, 0.93f, 0.88f),
        };
        static readonly Color Cement = new Color(0.6f, 0.58f, 0.54f);
        static readonly Color Window = new Color(0.16f, 0.2f, 0.24f);
        static readonly Color Frame = new Color(0.45f, 0.42f, 0.38f);

        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Color c, bool collider = false) =>
            Graybox.Box(p, n, pos, size, c, collider);

        /// <summary>단층 슬레이트집. lot이 0이 아니면 담장과 대문을 두른다.</summary>
        public static GameObject House(Transform parent, Vector3 pos, float yaw, float w, float d, System.Random rng, Vector2 lot, bool rural)
        {
            var root = new GameObject(rural ? "시골집" : "슬레이트집");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var t = root.transform;
            var wall = Plaster[rng.Next(Plaster.Length)];
            var roofC = Roofs[rng.Next(Roofs.Length)];
            float wallH = 2.7f;

            Box(t, "기단", new Vector3(0, 0.2f, 0), new Vector3(w + 0.4f, 0.4f, d + 0.4f), Cement);
            Box(t, "벽", new Vector3(0, 0.4f + wallH / 2f, 0), new Vector3(w, wallH, d), wall, true);
            // 정면: 미닫이 유리문과 창
            Box(t, "현관", new Vector3(-w * 0.18f, 1.45f, d / 2f + 0.02f), new Vector3(1.6f, 2.0f, 0.06f), new Color(0.3f, 0.25f, 0.2f));
            Box(t, "현관유리", new Vector3(-w * 0.18f, 1.6f, d / 2f + 0.05f), new Vector3(1.3f, 1.1f, 0.03f), Window);
            Box(t, "창틀", new Vector3(w * 0.22f, 1.75f, d / 2f + 0.02f), new Vector3(1.7f, 1.2f, 0.06f), Frame);
            Box(t, "창", new Vector3(w * 0.22f, 1.75f, d / 2f + 0.05f), new Vector3(1.5f, 1.0f, 0.03f), Window);
            Box(t, "뒷창", new Vector3(0, 1.8f, -d / 2f - 0.02f), new Vector3(1.2f, 0.8f, 0.04f), Window);

            // 박공지붕: 양쪽 경사면 + 용마루
            float pitch = 20f;
            float half = d / 2f + 0.45f;
            float slopeLen = half / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * half;
            float eaveY = 0.4f + wallH;
            for (int s = -1; s <= 1; s += 2)
            {
                var r = Box(t, "지붕", new Vector3(0, eaveY + rise / 2f, s * half / 2f), new Vector3(w + 0.8f, 0.1f, slopeLen), roofC);
                r.transform.localRotation = Quaternion.Euler(s * pitch, 0, 0);
            }
            Box(t, "용마루", new Vector3(0, eaveY + rise + 0.04f, 0), new Vector3(w + 0.85f, 0.14f, 0.3f), roofC * 0.8f + new Color(0, 0, 0, 0.2f));
            // 박공 벽 (삼각형 대신 낮은 상자 두 겹)
            Box(t, "박공", new Vector3(0, eaveY + rise * 0.3f, 0), new Vector3(w - 0.05f, rise * 0.6f, d * 0.6f), wall);

            // 연탄 보일러 연통, TV 안테나
            var pipe = Graybox.Prim(PrimitiveType.Cylinder, t, "연통", new Vector3(w / 2f + 0.15f, 2.4f, -d * 0.25f), new Vector3(0.18f, 1.4f, 0.18f), new Color(0.35f, 0.35f, 0.36f), false);
            Graybox.Prim(PrimitiveType.Cylinder, t, "연통머리", new Vector3(w / 2f + 0.15f, 3.85f, -d * 0.25f), new Vector3(0.3f, 0.08f, 0.3f), new Color(0.3f, 0.3f, 0.3f), false);
            if (rng.NextDouble() < 0.75)
            {
                float ax = (float)(rng.NextDouble() - 0.5) * w * 0.5f;
                Graybox.Prim(PrimitiveType.Cylinder, t, "안테나대", new Vector3(ax, eaveY + rise + 1.0f, 0), new Vector3(0.05f, 1.0f, 0.05f), new Color(0.6f, 0.6f, 0.62f), false);
                for (int k = 0; k < 3; k++)
                    Box(t, "안테나살", new Vector3(ax, eaveY + rise + 1.3f + k * 0.25f, 0), new Vector3(1.2f - k * 0.3f, 0.03f, 0.03f), new Color(0.6f, 0.6f, 0.62f));
            }

            // 담장과 대문, 마당의 장독대
            if (lot.x > 0f)
            {
                float lw = lot.x / 2f, ld = lot.y / 2f;
                float wallTop = rural ? 1.3f : 1.8f;
                var wc = rural ? new Color(0.62f, 0.58f, 0.5f) : Cement;
                Box(t, "담_뒤", new Vector3(0, wallTop / 2f, -ld), new Vector3(lot.x, wallTop, 0.22f), wc, true);
                Box(t, "담_왼", new Vector3(-lw, wallTop / 2f, 0), new Vector3(0.22f, wallTop, lot.y), wc, true);
                Box(t, "담_오", new Vector3(lw, wallTop / 2f, 0), new Vector3(0.22f, wallTop, lot.y), wc, true);
                float gate = 2.4f;
                float seg = (lot.x - gate) / 2f;
                Box(t, "담_앞1", new Vector3(-lw + seg / 2f, wallTop / 2f, ld), new Vector3(seg, wallTop, 0.22f), wc, true);
                Box(t, "담_앞2", new Vector3(lw - seg / 2f, wallTop / 2f, ld), new Vector3(seg, wallTop, 0.22f), wc, true);
                if (!rural)
                {
                    // 기와 얹은 담 머리
                    Box(t, "담머리", new Vector3(0, wallTop + 0.05f, -ld), new Vector3(lot.x + 0.1f, 0.1f, 0.4f), roofC);
                    // 파란 철 대문
                    var gateC = rng.NextDouble() < 0.6 ? new Color(0.2f, 0.38f, 0.55f) : new Color(0.25f, 0.45f, 0.3f);
                    Box(t, "대문", new Vector3(0, 1.1f, ld), new Vector3(gate, 2.2f, 0.08f), gateC, true);
                    Box(t, "대문틀", new Vector3(0, 2.3f, ld), new Vector3(gate + 0.4f, 0.2f, 0.3f), Cement);
                }
                // 장독대
                var jars = new GameObject("장독대").transform;
                jars.SetParent(t, false);
                jars.localPosition = new Vector3(lw - 1.6f, 0, -ld + 1.6f);
                Box(jars, "장독대단", new Vector3(0, 0.15f, 0), new Vector3(2.2f, 0.3f, 1.6f), Cement);
                for (int j = 0; j < 4; j++)
                {
                    float s = 0.5f + (float)rng.NextDouble() * 0.35f;
                    Graybox.Prim(PrimitiveType.Sphere, jars, "독", new Vector3(-0.6f + (j % 2) * 1.1f, 0.3f + s * 0.45f, -0.35f + (j / 2) * 0.7f),
                        new Vector3(s, s * 1.1f, s), new Color(0.32f, 0.2f, 0.12f), false);
                }
            }
            return root;
        }

        /// <summary>2~3층 상가주택. 1층은 가게, 위는 살림집, 옥상엔 물탱크.</summary>
        public static GameObject ShopHouse(Transform parent, Vector3 pos, float yaw, float w, float d, int floors, string signText, System.Random rng)
        {
            var root = new GameObject("상가주택");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var t = root.transform;
            var body = rng.NextDouble() < 0.4 ? new Color(0.72f, 0.62f, 0.55f) : Plaster[rng.Next(Plaster.Length)] * 0.95f;
            body.a = 1f;
            float fh = 3f;
            float h = floors * fh;
            Box(t, "몸통", new Vector3(0, h / 2f, 0), new Vector3(w, h, d), body, true);
            // 층 띠
            for (int f = 1; f < floors; f++)
                Box(t, "층띠", new Vector3(0, f * fh, 0), new Vector3(w + 0.12f, 0.18f, d + 0.12f), Cement);
            // 1층 가게: 유리, 셔터, 차양
            float zf = d / 2f + 0.03f;
            Box(t, "가게유리", new Vector3(0, 1.3f, zf), new Vector3(w * 0.8f, 2.2f, 0.05f), new Color(0.25f, 0.3f, 0.32f));
            bool shutter = rng.NextDouble() < 0.35;
            if (shutter)
                for (int k = 0; k < 9; k++)
                    Box(t, "셔터", new Vector3(-w * 0.2f, 0.25f + k * 0.24f, zf + 0.03f), new Vector3(w * 0.38f, 0.2f, 0.04f), new Color(0.55f, 0.56f, 0.58f));
            var signC = Signs[rng.Next(Signs.Length)];
            var awning = Box(t, "차양", new Vector3(0, 2.75f, zf + 0.6f), new Vector3(w * 0.85f, 0.08f, 1.3f), signC * 0.85f + new Color(0, 0, 0, 0.15f));
            awning.transform.localRotation = Quaternion.Euler(-12f, 0, 0);
            // 간판
            Box(t, "간판", new Vector3(0, 3.5f, zf + 0.12f), new Vector3(w * 0.9f, 0.9f, 0.15f), signC);
            if (!string.IsNullOrEmpty(signText))
            {
                var textC = signC.r + signC.g + signC.b > 2.2f ? new Color(0.15f, 0.12f, 0.1f) : new Color(0.98f, 0.96f, 0.9f);
                var label = Graybox.Label(t, signText, Vector3.zero, 0.075f, textC);
                label.transform.localPosition = new Vector3(0, 3.5f, zf + 0.22f);
                label.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
            // 위층 창
            for (int f = 1; f < floors; f++)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(w / 2.6f));
                for (int k = 0; k < n; k++)
                {
                    float x = -w / 2f + (k + 0.5f) * w / n;
                    Box(t, "창틀", new Vector3(x, f * fh + 1.5f, zf), new Vector3(1.3f, 1.2f, 0.06f), Frame);
                    Box(t, "창", new Vector3(x, f * fh + 1.5f, zf + 0.03f), new Vector3(1.1f, 1.0f, 0.03f), Window);
                }
            }
            // 옥상: 난간, 물탱크, 빨랫줄
            Box(t, "난간", new Vector3(0, h + 0.45f, zf - 0.08f), new Vector3(w, 0.9f, 0.12f), Cement);
            Box(t, "난간뒤", new Vector3(0, h + 0.45f, -d / 2f + 0.06f), new Vector3(w, 0.9f, 0.12f), Cement);
            var tankC = rng.NextDouble() < 0.5 ? new Color(0.25f, 0.45f, 0.7f) : new Color(0.72f, 0.73f, 0.75f);
            Graybox.Prim(PrimitiveType.Cylinder, t, "물탱크", new Vector3(w * 0.25f, h + 0.9f, -d * 0.2f), new Vector3(1.4f, 0.9f, 1.4f), tankC, false);
            if (rng.NextDouble() < 0.6)
            {
                Box(t, "빨랫대1", new Vector3(-w * 0.35f, h + 0.9f, -d * 0.3f), new Vector3(0.06f, 1.8f, 0.06f), Frame);
                Box(t, "빨랫대2", new Vector3(-w * 0.05f, h + 0.9f, -d * 0.3f), new Vector3(0.06f, 1.8f, 0.06f), Frame);
                for (int k = 0; k < 3; k++)
                    Box(t, "빨래", new Vector3(-w * 0.3f + k * 0.45f, h + 1.4f, -d * 0.3f), new Vector3(0.35f, 0.5f, 0.02f), Signs[(k + 1) % Signs.Length]);
            }
            return root;
        }

        /// <summary>헛간: 나무 기둥에 양철 지붕.</summary>
        public static GameObject Shed(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = new GameObject("헛간");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var t = root.transform;
            var wood = new Color(0.36f, 0.28f, 0.2f);
            Box(t, "벽", new Vector3(0, 1.2f, -0.8f), new Vector3(4f, 2.4f, 0.2f), wood, true);
            Box(t, "기둥1", new Vector3(-1.9f, 1.2f, 1f), new Vector3(0.2f, 2.4f, 0.2f), wood);
            Box(t, "기둥2", new Vector3(1.9f, 1.2f, 1f), new Vector3(0.2f, 2.4f, 0.2f), wood);
            var roof = Box(t, "지붕", new Vector3(0, 2.55f, 0.1f), new Vector3(4.6f, 0.08f, 2.8f), Roofs[rng.Next(Roofs.Length)]);
            roof.transform.localRotation = Quaternion.Euler(-8f, 0, 0);
            // 장작더미
            for (int k = 0; k < 6; k++)
            {
                var log = Graybox.Prim(PrimitiveType.Cylinder, t, "장작", new Vector3(-1f + (k % 3) * 0.35f, 0.15f + (k / 3) * 0.28f, -0.3f), new Vector3(0.25f, 0.6f, 0.25f), new Color(0.45f, 0.33f, 0.22f), false);
                log.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            return root;
        }
    }
}
