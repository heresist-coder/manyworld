using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>
    /// 숲 현장용 직접 만든 자연물: 각진 로우폴리 바위, 이음매 없는 지형 질감 (목표 샷 01~03의 그림체).
    /// </summary>
    public static class NatureKit
    {
        // ---------------- 바위 ----------------

        static Mesh[] boulders;
        static Material[] rockMats;

        /// <summary>놓은 바위 자리 (x, z, 반지름). 풀이 바위 속에서 솟지 않게 거른다.</summary>
        public static readonly FootprintSet Footprints = new FootprintSet();

        public class FootprintSet
        {
            const float CellSize = 4f;
            readonly Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();
            public void Clear() => cells.Clear();

            public void Add(float x, float z, float r)
            {
                var key = new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Vector3>();
                list.Add(new Vector3(x, z, r));
            }

            public bool Contains(float x, float z)
            {
                int cx = Mathf.FloorToInt(x / CellSize), cz = Mathf.FloorToInt(z / CellSize);
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetValue(new Vector2Int(cx + dx, cz + dz), out var list)) continue;
                    foreach (var f in list)
                        if ((f.x - x) * (f.x - x) + (f.y - z) * (f.y - z) < f.z * f.z) return true;
                }
                return false;
            }
        }

        public static bool UnderRock(float x, float z) => Footprints.Contains(x, z);

        /// <summary>정이십면체를 한 번 쪼개 울퉁불퉁하게 밀고, 면마다 법선을 따로 줘서 각진 면이 보이게 한다. 높이 1, 바닥 평평.</summary>
        static Mesh BoulderMesh(int seed)
        {
            var rng = new System.Random(seed);
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                v.Add((v[a] + v[b]) * 0.5f);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            var f2 = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                f2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            // 울퉁불퉁: 꼭짓점마다 반지름을 흔들고 납작하게, 바닥은 잘라 평평하게
            float sx = 1f + (float)rng.NextDouble() * 0.25f, sy = 0.8f + (float)rng.NextDouble() * 0.18f, sz = 0.92f + (float)rng.NextDouble() * 0.16f; // 둥근 바위 (판처럼 납작하지 않게)
            for (int i = 0; i < v.Count; i++)
            {
                var p = v[i].normalized * (0.84f + (float)rng.NextDouble() * 0.26f);
                p = new Vector3(p.x * sx, p.y * sy, p.z * sz);
                if (p.y < -0.3f) p.y = -0.3f - (p.y + 0.3f) * 0.15f;
                v[i] = p;
            }
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in v) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            float hgt = maxY - minY;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < f2.Count; i += 3)
            {
                var a = (v[f2[i]] - Vector3.up * minY) / hgt;
                var b = (v[f2[i + 1]] - Vector3.up * minY) / hgt;
                var c = (v[f2[i + 2]] - Vector3.up * minY) / hgt;
                var n = Vector3.Cross(b - a, c - a).normalized;
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            var mesh = new Mesh { name = "Boulder" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 각진 바위 하나. ground는 땅 위 점, height는 땅 위로 보이는 높이.
        /// collide면 1m 넘는 바위에 볼록 메시 충돌을 단다.
        /// </summary>
        public static GameObject Boulder(Transform parent, Vector3 ground, float height, System.Random rng, bool collide, string name = "Rock")
        {
            if (boulders == null)
            {
                boulders = new Mesh[8];
                for (int i = 0; i < boulders.Length; i++) boulders[i] = BoulderMesh(101 + i * 37);
                rockMats = new[]
                {
                    RockMat(new Color(0.48f, 0.48f, 0.5f)),
                    RockMat(new Color(0.43f, 0.43f, 0.45f)),
                    RockMat(new Color(0.5f, 0.48f, 0.46f)),
                };
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var mesh = boulders[rng.Next(boulders.Length)];
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = rockMats[rng.Next(rockMats.Length)];
            float sink = 0.12f + (float)rng.NextDouble() * 0.12f; // 땅에 조금 묻힌다
            float total = height / (1f - sink);
            float wide = total * (0.95f + (float)rng.NextDouble() * 0.35f);
            go.transform.SetPositionAndRotation(ground - Vector3.up * total * sink,
                Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 12f, (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() - 0.5) * 12f));
            go.transform.localScale = new Vector3(wide, total, wide);
            Footprints.Add(ground.x, ground.z, wide * 0.55f);
            if (collide && height > 1f)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            return go;
        }

        static Material RockMat(Color c)
        {
            var m = new Material(Graybox.Mat(c)) { name = "Boulder" };
            m.SetFloat("_Smoothness", 0f);
            m.enableInstancing = true;
            return m;
        }

        // ---------------- 지형 질감 ----------------

        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xffffff) / (float)0xffffff;

        /// <summary>주기 period로 감기는 값 노이즈 (질감이 타일 경계에서 이어진다).</summary>
        static float PNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int a) => ((a % period) + period) % period;
            float a00 = Rand(Wrap(x0), Wrap(y0), seed), a10 = Rand(Wrap(x0 + 1), Wrap(y0), seed);
            float a01 = Rand(Wrap(x0), Wrap(y0 + 1), seed), a11 = Rand(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }

        static float PFbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * PNoise(u * period, v * period, period, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        public enum Ground { Meadow, Lush, ForestFloor, Dirt, Rut, Rock, Dry }

        const int TexSize = 256;

        /// <summary>질감 하나: 바탕 노이즈 + 붓질(풀잎·솔잎) 또는 자갈 점.</summary>
        static readonly Dictionary<(Ground, Color, int), Texture2D> texCache = new Dictionary<(Ground, Color, int), Texture2D>();

        public static TerrainLayer Layer(Ground kind, Color c, float tile, int seed)
        {
            // 질감은 한 번만 만든다 (다시 현장에 갈 때 생성 시간)
            if (texCache.TryGetValue((kind, c, seed), out var cached) && cached != null)
                return new TerrainLayer { diffuseTexture = cached, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f };
            var rng = new System.Random(seed);
            var px = new Color[TexSize * TexSize];
            float variation = kind == Ground.Rock ? 0.16f : kind == Ground.Dirt || kind == Ground.Rut ? 0.1f : 0.14f;
            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                float u = x / (float)TexSize, v = y / (float)TexSize;
                float n = PFbm(u, v, 4, 4, seed);
                var col = Color.Lerp(c * (1f - variation), c * (1f + variation), n);
                if (kind == Ground.Meadow || kind == Ground.Dry || kind == Ground.Lush)
                {
                    // 햇볕에 바랜 노란 얼룩
                    float warm = Mathf.InverseLerp(0.55f, 0.75f, PFbm(u, v, 2, 3, seed + 5));
                    col = Color.Lerp(col, new Color(col.r * 1.15f, col.g * 1.08f, col.b * 0.8f), warm * 0.6f);
                }
                if (kind == Ground.Rock)
                {
                    float crack = Mathf.Abs(PFbm(u, v, 8, 3, seed + 9) - 0.5f);
                    if (crack < 0.025f) col *= 0.75f;
                }
                px[y * TexSize + x] = col;
            }

            void Dot(int cx, int cy, int r, Color tint, float strength)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    int i = (((cy + dy) % TexSize + TexSize) % TexSize) * TexSize + ((cx + dx) % TexSize + TexSize) % TexSize;
                    px[i] = Color.Lerp(px[i], tint, strength);
                }
            }

            void Stroke(float x, float y, float ang, float len, Color tint, float strength)
            {
                for (float s = 0; s < len; s += 0.5f)
                {
                    int cx = Mathf.RoundToInt(x + Mathf.Cos(ang) * s), cy = Mathf.RoundToInt(y + Mathf.Sin(ang) * s);
                    int i = ((cy % TexSize + TexSize) % TexSize) * TexSize + (cx % TexSize + TexSize) % TexSize;
                    px[i] = Color.Lerp(px[i], tint, strength * (1f - s / len * 0.5f));
                }
            }

            switch (kind)
            {
                case Ground.Meadow:
                case Ground.Lush:
                case Ground.Dry:
                    // 짧은 풀잎 붓질: 밝은 끝·어두운 그늘이 섞인다
                    for (int i = 0; i < 2600; i++)
                    {
                        bool light = rng.NextDouble() < 0.55;
                        var tint = light ? new Color(c.r * 1.35f, c.g * 1.3f, c.b * 1.05f) : new Color(c.r * 0.62f, c.g * 0.7f, c.b * 0.6f);
                        float ang = Mathf.PI * 0.5f + (float)(rng.NextDouble() - 0.5) * 1.4f;
                        Stroke((float)rng.NextDouble() * TexSize, (float)rng.NextDouble() * TexSize, ang, 3f + (float)rng.NextDouble() * 6f, tint, 0.45f);
                    }
                    break;
                case Ground.ForestFloor:
                    // 솔잎·마른 가지: 사방으로 흩어진 가는 갈색 선
                    for (int i = 0; i < 2200; i++)
                    {
                        var tint = rng.NextDouble() < 0.5 ? new Color(0.36f, 0.27f, 0.17f) : new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f);
                        Stroke((float)rng.NextDouble() * TexSize, (float)rng.NextDouble() * TexSize, (float)rng.NextDouble() * Mathf.PI * 2f, 3f + (float)rng.NextDouble() * 5f, tint, 0.5f);
                    }
                    break;
                case Ground.Dirt:
                case Ground.Rut:
                    // 자갈: 밝은 돌 위에 그늘 한 점
                    for (int i = 0; i < 520; i++)
                    {
                        int x = rng.Next(TexSize), y = rng.Next(TexSize), r = rng.Next(1, 3);
                        Dot(x + 1, y - 1, r, c * 0.62f, 0.6f);
                        Dot(x, y, r, rng.NextDouble() < 0.6 ? c * 1.22f : new Color(0.58f, 0.56f, 0.52f), 0.7f);
                    }
                    for (int i = 0; i < 900; i++)
                        Dot(rng.Next(TexSize), rng.Next(TexSize), 0, rng.NextDouble() < 0.5 ? c * 0.8f : c * 1.12f, 0.5f);
                    break;
                case Ground.Rock:
                    for (int i = 0; i < 400; i++)
                        Dot(rng.Next(TexSize), rng.Next(TexSize), rng.Next(0, 2), rng.NextDouble() < 0.5 ? c * 0.78f : c * 1.15f, 0.5f);
                    break;
            }

            for (int i = 0; i < px.Length; i++) px[i].a = 0.04f; // URP 지형은 알파를 광택으로 쓴다
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "Ground_" + kind };
            tex.SetPixels(px);
            tex.Apply(true);
            texCache[(kind, c, seed)] = tex;
            return new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f };
        }
    }
}
