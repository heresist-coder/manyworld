using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Manyworld
{
    /// <summary>
    /// 멀티버스 세계 = 1km 분지 지형 (D15: 같은 지리, 다른 역사).
    /// 산·강·도로망·마을 자리는 모든 세계에서 같다 (우리 세계의 지리). 세계 번호는 숲 밀도, 폐허, 침수, 색감을 바꾼다.
    /// 가장자리는 산이라 자연 경계가 된다. 차로 달릴 만큼 넓고, 자연물이 많고, 집은 가끔 있다.
    /// </summary>
    public static class WorldTerrainBuilder
    {
        public const float Size = 1024f;
        public const float MaxHeight = 110f;
        const int Res = 513;
        const float Cell = Size / (Res - 1);
        public const float WaterLevel = 4f;

        // 고정 지리
        public static readonly Vector3 GatePos = new Vector3(150, 0, 150);
        public static readonly Vector3 MidpointPos = new Vector3(512, 0, 500);
        public static readonly Vector3 ObjectivePos = new Vector3(850, 0, 840);

        static readonly Vector2[][] Roads =
        {
            new[] { new Vector2(150, 150), new Vector2(230, 230), new Vector2(330, 300), new Vector2(420, 390), new Vector2(512, 500) },
            new[] { new Vector2(512, 500), new Vector2(600, 590), new Vector2(700, 660), new Vector2(790, 760), new Vector2(850, 840) },
            new[] { new Vector2(512, 500), new Vector2(440, 610), new Vector2(360, 720), new Vector2(300, 820) },
            new[] { new Vector2(512, 500), new Vector2(640, 420), new Vector2(760, 330), new Vector2(860, 260) },
            new[] { new Vector2(330, 300), new Vector2(290, 410), new Vector2(250, 520) },
        };

        struct Hamlet
        {
            public string name;
            public Vector2 pos;
            public int houses;
            public Hamlet(string n, float x, float z, int h) { name = n; pos = new Vector2(x, z); houses = h; }
        }

        static readonly Hamlet[] Hamlets =
        {
            new Hamlet("장터거리", 512, 500, 5),
            new Hamlet("배나무골", 300, 820, 3),
            new Hamlet("숯골", 860, 260, 3),
            new Hamlet("새터", 250, 520, 2),
            new Hamlet("방앗간", 700, 660, 2),
        };

        /// <summary>마을 옆 밭 (고정 지리). 길 쪽을 따라 놓인 직사각형.</summary>
        struct Field
        {
            public Vector2 center;
            public float w, d, angle;
        }

        static List<Field> fields;

        static List<Field> Fields()
        {
            if (fields != null) return fields;
            fields = new List<Field>();
            var rng = new System.Random(1988);
            foreach (var hm in Hamlets)
            {
                int n = 2 + rng.Next(2);
                for (int i = 0; i < n; i++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = 50f + (float)rng.NextDouble() * 18f;
                    fields.Add(new Field
                    {
                        center = hm.pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist,
                        w = 26f + (float)rng.NextDouble() * 14f,
                        d = 16f + (float)rng.NextDouble() * 8f,
                        angle = (float)rng.NextDouble() * 180f,
                    });
                }
            }
            return fields;
        }

        /// <summary>밭 안이면 0~1 (가장자리는 부드럽게).</summary>
        static float FieldWeight(float x, float z)
        {
            float best = 0f;
            foreach (var f in Fields())
            {
                var d = new Vector2(x, z) - f.center;
                if (Mathf.Abs(d.x) > 30f || Mathf.Abs(d.y) > 30f) continue; // 멀면 건너뛴다
                float a = -f.angle * Mathf.Deg2Rad;
                float lx = d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a);
                float lz = d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a);
                float wx = Mathf.InverseLerp(f.w / 2f, f.w / 2f - 1.5f, Mathf.Abs(lx));
                float wz = Mathf.InverseLerp(f.d / 2f, f.d / 2f - 1.5f, Mathf.Abs(lz));
                best = Mathf.Max(best, Mathf.Min(wx, wz));
            }
            return best;
        }

        public static MissionLayout Build(WorldSpec spec)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var root = new GameObject($"World_{spec.number}").transform;
            var layout = new MissionLayout { root = root };
            var worldRng = new System.Random(spec.number * 7717 + 3);

            // 1) 높이 (미터)
            var h = new float[Res, Res];
            var baseH = new float[Res, Res];
            for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                float wx = x * Cell, wz = z * Cell;
                baseH[z, x] = BaseHeight(wx, wz);
                h[z, x] = CarveRiver(wx, wz, baseH[z, x]);
            }

            // 2) 도로: 원래 지형 높이를 따라가되 완만하게, 둑길로 강을 건넌다
            var roadDist = new float[Res, Res];
            var roadH = new float[Res, Res];
            for (int z = 0; z < Res; z++) for (int x = 0; x < Res; x++) roadDist[z, x] = 999f;
            var roadLines = new List<List<Vector3>>();
            foreach (var road in Roads)
            {
                var pts = Densify(road, 2f);
                var hs = new float[pts.Count];
                for (int i = 0; i < pts.Count; i++) hs[i] = SampleGrid(baseH, pts[i].x, pts[i].y);
                hs = Smooth(Smooth(hs, 12), 12);
                var line = new List<Vector3>();
                for (int i = 0; i < pts.Count; i++)
                {
                    line.Add(new Vector3(pts[i].x, hs[i], pts[i].y));
                    Stamp(roadDist, roadH, pts[i], hs[i], 12f);
                }
                roadLines.Add(line);
            }
            for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                float d = roadDist[z, x];
                if (d > 12f) continue;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 4.5f, d));
                h[z, x] = Mathf.Lerp(h[z, x], roadH[z, x] - 0.05f, w);
            }

            // 3) 마을·게이트·목표 자리는 평평하게
            var flats = new List<(Vector2 c, float r)> { (V2(GatePos), 34f), (V2(ObjectivePos), 36f) };
            foreach (var hm in Hamlets) flats.Add((hm.pos, 38f));
            foreach (var (c, r) in flats)
            {
                float target = SampleGrid(h, c.x, c.y);
                ForCells(c, r, (x, z, d) =>
                {
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r * 0.55f, d));
                    h[z, x] = Mathf.Lerp(h[z, x], target, w);
                });
            }

            long tHeights = watch.ElapsedMilliseconds;
            // 4) TerrainData
            var td = new TerrainData { heightmapResolution = Res, size = new Vector3(Size, MaxHeight, Size) };
            var norm = new float[Res, Res];
            for (int z = 0; z < Res; z++) for (int x = 0; x < Res; x++) norm[z, x] = Mathf.Clamp01(h[z, x] / MaxHeight);
            td.SetHeights(0, 0, norm);
            Paint(td, spec, roadDist, h);

            long tPaint = watch.ElapsedMilliseconds;
            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "Terrain";
            terrainGo.transform.SetParent(root, false);
            var terrain = terrainGo.GetComponent<Terrain>();
            var terrainMat = Resources.Load<Material>("Materials/Terrain");
            if (terrainMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (sh != null) terrainMat = new Material(sh);
            }
            if (terrainMat != null) terrain.materialTemplate = terrainMat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 300f;
            terrain.treeDistance = 600f;
            terrain.treeBillboardDistance = 180f;
            terrain.detailObjectDistance = 120f;
            terrain.detailObjectDensity = 1f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            layout.terrain = terrain;

            float Y(Vector3 p) => terrain.SampleHeight(p);
            Vector3 OnGround(Vector3 p) => new Vector3(p.x, Y(p), p.z);

            // 5) 흙길은 지형에 직접 칠했다 (1m 해상도). 길가엔 전봇대와 전깃줄
            foreach (var line in roadLines) PlacePowerLines(root, line, terrain);

            // 6) 물: 강과 저지대. 침수 세계는 수위가 높다
            float water = spec.terrain == TerrainMod.Flood ? 10.5f : WaterLevel;
            var waterGo = Graybox.Box(root, "Water", new Vector3(Size / 2, water - 0.25f, Size / 2), new Vector3(Size, 0.5f, Size),
                spec.rules.HasFlag(WorldRule.ToxicGas) ? new Color(0.35f, 0.38f, 0.2f, 0.7f) : new Color(0.2f, 0.32f, 0.38f, 0.65f), false);
            waterGo.layer = 2;
            var waterMat = new Material(waterGo.GetComponent<Renderer>().sharedMaterial);
            if (waterMat.HasProperty("_Smoothness")) waterMat.SetFloat("_Smoothness", 0.93f);
            waterGo.GetComponent<Renderer>().sharedMaterial = waterMat;
            waterGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            layout.waterLevel = water;
            layout.minimap = BuildMinimap(h, roadDist, water);
            layout.worldSize = Size;

            // 7) 게이트·티코·뷰로 표지판
            layout.gatePos = OnGround(GatePos);
            TownBuilder.BuildGate(root, layout.gatePos + new Vector3(-8, 0, -6), false);
            layout.ticoPos = OnGround(GatePos + new Vector3(10, 0, 10));
            layout.playerSpawn = OnGround(GatePos + new Vector3(14, 0, 6)) + Vector3.up * 0.1f;
            layout.companionSpawn = OnGround(GatePos + new Vector3(15, 0, 9)) + Vector3.up * 0.1f;
            TownBuilder.PlaceBureauSign(root, spec, OnGround(GatePos + new Vector3(22, 0, -4)));

            // 8) 마을 (가끔 있는 집). 폐허 변조는 집 일부를 무너뜨린다
            var ruinRng = new System.Random(spec.number * 31 + 5);
            for (int i = 0; i < Hamlets.Length; i++)
            {
                var hm = Hamlets[i];
                layout.landmarks[hm.name] = OnGround(new Vector3(hm.pos.x, 0, hm.pos.y));
                BuildHamlet(root, hm, i, terrain, spec, ruinRng);
            }

            // 길가 디테일: 장승, 서낭당, 버스 정류장, 이정표, 짚더미
            PlaceRoadside(root, terrain, spec);

            // 9) 목표: 공사 현장
            layout.objectivePos = OnGround(ObjectivePos);
            BuildSite(root, layout.objectivePos);
            layout.midpointPos = OnGround(MidpointPos + new Vector3(0, 0, 6));

            // 10) 자연물
            PlantTrees(td, terrain, spec, roadDist, h, water, worldRng);
            ScatterRocks(root, terrain, roadDist, h, water, worldRng);
            PlantGrass(td, roadDist, h, water);
            terrain.Flush();

            long tNature = watch.ElapsedMilliseconds;
            // 11) 네비메시: 산 가장자리를 뺀 안쪽만 굽는다
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(Size / 2, 50, Size / 2);
            surface.size = new Vector3(790, 160, 790);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << 2);
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.45f;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.BuildNavMesh();

            CollectSpawnPoints(layout, spec);
            Debug.Log($"[Manyworld] 월드 생성 제{spec.number}번: {watch.ElapsedMilliseconds}ms (높이 {tHeights}, 칠하기 {tPaint - tHeights}, 배치 {tNature - tPaint}, 네비메시 {watch.ElapsedMilliseconds - tNature}), 나무 {td.treeInstanceCount}, 스폰 {layout.spawnPoints.Count}");
            return layout;
        }

        // ---------------- 지형 ----------------

        static float BaseHeight(float x, float z)
        {
            float hills = Fbm(x * 0.0032f + 13.1f, z * 0.0032f + 7.7f, 4) * 24f;
            float detail = (Mathf.PerlinNoise(x * 0.025f + 3f, z * 0.025f + 9f) - 0.5f) * 2f;
            float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            float rimT = Mathf.Clamp01(1f - edge / 150f);
            float rim = rimT * rimT * 55f + Fbm(x * 0.011f + 50f, z * 0.011f + 20f, 3) * 30f * rimT;
            return 8f + hills + detail + rim;
        }

        public static float RiverX(float z) => 512f + Mathf.Sin(z * 0.0055f) * 200f + Mathf.Sin(z * 0.019f + 1f) * 35f;

        static float CarveRiver(float x, float z, float hgt)
        {
            float d = Mathf.Abs(x - RiverX(z));
            float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 7f, d));
            return Mathf.Lerp(hgt, 1.2f, s);
        }

        static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, z * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        static Vector2 V2(Vector3 v) => new Vector2(v.x, v.z);

        static List<Vector2> Densify(Vector2[] pts, float step)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                // 모서리를 둥글게: 이웃 점과 섞은 2차 곡선
                Vector2 a = pts[i], b = pts[i + 1];
                Vector2 a2 = i > 0 ? (a + b) * 0.5f : a;
                Vector2 c = i < pts.Length - 2 ? (b + pts[i + 2]) * 0.5f : b;
                float len = Vector2.Distance(a2, b) + Vector2.Distance(b, c);
                int n = Mathf.Max(2, Mathf.CeilToInt(len / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n;
                    var p = Vector2.Lerp(Vector2.Lerp(a2, b, t), Vector2.Lerp(b, c, t), t);
                    if (i == 0 && k == 0) list.Add(a);
                    list.Add(p);
                }
            }
            list.Add(pts[pts.Length - 1]);
            return list;
        }

        static float[] Smooth(float[] v, int radius)
        {
            var o = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float s = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, v.Length - 1);
                    s += v[j]; n++;
                }
                o[i] = s / n;
            }
            return o;
        }

        static float SampleBilinear(float[,] g, float wx, float wz)
        {
            float fx = Mathf.Clamp(wx / Cell, 0, Res - 1.001f), fz = Mathf.Clamp(wz / Cell, 0, Res - 1.001f);
            int x0 = (int)fx, z0 = (int)fz;
            float tx = fx - x0, tz = fz - z0;
            float a = Mathf.Lerp(g[z0, x0], g[z0, x0 + 1], tx);
            float b = Mathf.Lerp(g[z0 + 1, x0], g[z0 + 1, x0 + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        static float SampleGrid(float[,] g, float wx, float wz)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(wx / Cell), 0, Res - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(wz / Cell), 0, Res - 1);
            return g[z, x];
        }

        static void ForCells(Vector2 c, float r, System.Action<int, int, float> f)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - r) / Cell)), x1 = Mathf.Min(Res - 1, Mathf.CeilToInt((c.x + r) / Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.y - r) / Cell)), z1 = Mathf.Min(Res - 1, Mathf.CeilToInt((c.y + r) / Cell));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(c, new Vector2(x * Cell, z * Cell));
                if (d <= r) f(x, z, d);
            }
        }

        static void Stamp(float[,] dist, float[,] hgt, Vector2 p, float height, float r)
        {
            ForCells(p, r, (x, z, d) =>
            {
                if (d < dist[z, x]) { dist[z, x] = d; hgt[z, x] = height; }
            });
        }

        // ---------------- 칠하기 ----------------

        static Texture2D NoiseTex(Color c, float variation, int seed)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(seed);
            var px = new Color[64 * 64];
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % 64, y = i / 64;
                float n = Mathf.PerlinNoise(x * 0.15f + seed, y * 0.15f) * 0.7f + (float)rng.NextDouble() * 0.3f;
                px[i] = Color.Lerp(c * (1f - variation), c * (1f + variation), n);
                px[i].a = 0.06f; // URP 지형은 알파를 광택으로 쓴다. 1이면 하늘을 반사해 하얗게 번들거린다
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>밭고랑: 줄무늬 흙.</summary>
        static TerrainLayer FieldLayer(Color c)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[64 * 64];
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % 64, y = i / 64;
                float furrow = 0.5f + 0.5f * Mathf.Sin(y / 64f * Mathf.PI * 2f * 4f);
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.25f;
                var col = Color.Lerp(c * 0.75f, c * 1.15f, furrow * 0.8f + n);
                col.a = 0.05f;
                px[i] = col;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6f, 6f), smoothness = 0f };
        }

        static TerrainLayer Layer(Color c, float tile, int seed)
        {
            return new TerrainLayer { diffuseTexture = NoiseTex(c, 0.12f, seed), tileSize = new Vector2(tile, tile), smoothness = 0.05f, metallic = 0f };
        }

        static void Paint(TerrainData td, WorldSpec spec, float[,] roadDist, float[,] h)
        {
            // 노이즈는 2m 격자에 한 번만 계산하고 1m 칠하기에서 보간한다 (월드 생성 시간)
            var forestGrid = new float[Res, Res];
            var dryGrid = new float[Res, Res];
            var slopeGrid = new float[Res, Res];
            for (int gz = 0; gz < Res; gz++)
            for (int gx = 0; gx < Res; gx++)
            {
                float wx0 = gx * Cell, wz0 = gz * Cell;
                forestGrid[gz, gx] = Fbm(wx0 * 0.006f + 5f, wz0 * 0.006f + 9f, 3);
                dryGrid[gz, gx] = Fbm(wx0 * 0.012f + 31f, wz0 * 0.012f + 17f, 3);
                int xa = Mathf.Max(0, gx - 1), xb = Mathf.Min(Res - 1, gx + 1), za = Mathf.Max(0, gz - 1), zb = Mathf.Min(Res - 1, gz + 1);
                float dx = (h[gz, xb] - h[gz, xa]) / ((xb - xa) * Cell), dz = (h[zb, gx] - h[za, gx]) / ((zb - za) * Cell);
                slopeGrid[gz, gx] = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
            }
            // 멀티버스는 같은 구도인데 차갑고 뒤틀린 톤 (brainstorm-01 3장)
            Color tint = spec.rules.HasFlag(WorldRule.ToxicGas) ? new Color(0.72f, 0.68f, 0.38f) : spec.skyTint;
            Color Mix(Color c) => Color.Lerp(c, tint, 0.22f);
            td.terrainLayers = new[]
            {
                Layer(Mix(new Color(0.36f, 0.46f, 0.24f)), 7f, 1),   // 0 풀밭
                Layer(Mix(new Color(0.22f, 0.3f, 0.17f)), 6f, 2),    // 1 숲 바닥
                Layer(Mix(new Color(0.5f, 0.41f, 0.29f)), 5f, 3),    // 2 흙길
                Layer(Mix(new Color(0.44f, 0.43f, 0.41f)), 9f, 4),   // 3 바위
                Layer(Mix(new Color(0.33f, 0.29f, 0.23f)), 6f, 5),   // 4 진흙 (물가)
                Layer(Mix(new Color(0.55f, 0.52f, 0.3f)), 8f, 6),    // 5 마른 풀 (겨울 오후)
                FieldLayer(Mix(new Color(0.42f, 0.33f, 0.24f))),      // 6 밭고랑
            };
            int ar = 768; // 1.33m. 1024는 생성이 2초 넘게 걸렸다
            td.alphamapResolution = ar;
            var a = new float[ar, ar, 7];
            for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
            {
                float wx = x / (float)(ar - 1) * Size, wz = z / (float)(ar - 1) * Size;
                float hgt = SampleBilinear(h, wx, wz);
                float slope = SampleBilinear(slopeGrid, wx, wz);
                float rd = SampleBilinear(roadDist, wx, wz);
                // 흙길: 가운데는 진하게, 어깨는 마른 풀로 번진다
                float road = Mathf.InverseLerp(3.8f, 2.4f, rd) + (rd < 4.5f ? (Mathf.PerlinNoise(wx * 0.4f, wz * 0.4f) - 0.5f) * 0.25f : 0f);
                road = Mathf.Clamp01(road);
                float shoulder = Mathf.InverseLerp(6.5f, 3.8f, rd) * (1f - road);
                float rock = Mathf.InverseLerp(24f, 38f, slope);
                float mud = Mathf.InverseLerp(WaterLevel + 2.5f, WaterLevel + 0.3f, hgt);
                float field = FieldWeight(wx, wz);
                float forest = Mathf.InverseLerp(0.52f, 0.62f, SampleBilinear(forestGrid, wx, wz));
                float dry = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 0.7f, SampleBilinear(dryGrid, wx, wz)) + shoulder * 0.8f);
                // 우선순위: 길 > 바위 > 진흙 > 밭 > 숲 > 마른 풀 > 풀
                float wRoad = road;
                float wRock = rock * (1 - wRoad);
                float wMud = mud * (1 - wRoad - wRock);
                float wField = field * Mathf.Max(0f, 1 - wRoad - wRock - wMud);
                float rest = Mathf.Max(0f, 1f - wRoad - wRock - wMud - wField);
                a[z, x, 2] = wRoad;
                a[z, x, 3] = wRock;
                a[z, x, 4] = Mathf.Max(0f, wMud);
                a[z, x, 6] = wField;
                a[z, x, 1] = rest * forest;
                a[z, x, 5] = rest * (1f - forest) * dry;
                a[z, x, 0] = rest * (1f - forest) * (1f - dry);
            }
            td.SetAlphamaps(0, 0, a);
        }

        // ---------------- 지도 ----------------

        /// <summary>미니맵: 높이 음영 + 물 + 흙길 + 숲. 공사 측량도 같은 바랜 색.</summary>
        static Texture2D BuildMinimap(float[,] h, float[,] roadDist, float water)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float wx = (x + 0.5f) / n * Size, wz = (y + 0.5f) / n * Size;
                float hgt = SampleGrid(h, wx, wz);
                float east = SampleGrid(h, wx + 6f, wz), north = SampleGrid(h, wx, wz + 6f);
                float shade = Mathf.Clamp((hgt - east) * 0.08f + (hgt - north) * 0.08f, -0.25f, 0.25f);
                Color c;
                if (hgt < water) c = new Color(0.3f, 0.42f, 0.5f);
                else
                {
                    float forest = Mathf.InverseLerp(0.5f, 0.62f, Fbm(wx * 0.006f + 5f, wz * 0.006f + 9f, 3));
                    c = Color.Lerp(new Color(0.55f, 0.6f, 0.42f), new Color(0.32f, 0.42f, 0.28f), forest);
                    c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.56f), Mathf.InverseLerp(40f, 70f, hgt)); // 산
                    c *= 1f + shade;
                    if (SampleGrid(roadDist, wx, wz) < 4f) c = new Color(0.72f, 0.6f, 0.42f);
                }
                c.a = 1f;
                px[y * n + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---------------- 도로 ----------------

        static void BuildRoadMesh(Transform root, List<Vector3> line, Terrain terrain)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float half = 3.2f, v = 0f;
            for (int i = 0; i < line.Count; i++)
            {
                var p = line[i];
                var fwd = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]);
                fwd.y = 0;
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var l = p - right * half;
                var r = p + right * half;
                l.y = terrain.SampleHeight(l) + 0.06f;
                r.y = terrain.SampleHeight(r) + 0.06f;
                verts.Add(l); verts.Add(r);
                if (i > 0) v += Vector3.Distance(line[i], line[i - 1]) / 6f;
                uvs.Add(new Vector2(0, v)); uvs.Add(new Vector2(1, v));
                if (i > 0)
                {
                    int b = verts.Count - 4;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Road", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.layer = 2;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Graybox.Mat(new Color(0.46f, 0.38f, 0.28f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------- 길가 디테일 ----------------

        /// <summary>마을 어귀 장승과 서낭당, 버스 정류장, 갈림길 이정표, 밭의 짚더미.</summary>
        static void PlaceRoadside(Transform root, Terrain terrain, WorldSpec spec)
        {
            var rng = new System.Random(2024);
            Vector3 G(float x, float z) { var p = new Vector3(x, 0, z); p.y = terrain.SampleHeight(p); return p; }
            var wood = new Color(0.4f, 0.3f, 0.2f);
            var faded = spec.rules != WorldRule.None; // 멀티버스: 빛바래고 쓰러진 것도 있다

            // 장승: 각 마을로 들어오는 길에 한 쌍
            for (int i = 0; i < Hamlets.Length; i++)
            {
                var hm = Hamlets[i];
                Vector2 dir = Vector2.zero;
                foreach (var road in Roads)
                    for (int k = 0; k < road.Length; k++)
                        if (Vector2.Distance(road[k], hm.pos) < 1f)
                        {
                            var other = k > 0 ? road[k - 1] : road[k + 1];
                            dir = (other - hm.pos).normalized;
                        }
                if (dir == Vector2.zero) continue;
                var c = hm.pos + dir * 40f;
                var side = new Vector2(-dir.y, dir.x);
                for (int s = -1; s <= 1; s += 2)
                {
                    var pp = c + side * (4.6f * s);
                    var p = G(pp.x, pp.y);
                    var post = Graybox.Prim(PrimitiveType.Cylinder, root, "장승", p + Vector3.up * 1.4f, new Vector3(0.45f, 1.4f, 0.45f), wood, false);
                    Graybox.Box(post.transform, "얼굴", new Vector3(0, 0.62f, 0.38f), new Vector3(0.9f, 0.3f, 0.3f), new Color(0.6f, 0.2f, 0.15f), false);
                    Graybox.Box(post.transform, "모자", new Vector3(0, 0.98f, 0), new Vector3(1.1f, 0.08f, 1.1f), new Color(0.15f, 0.12f, 0.1f), false);
                    post.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y)) * Quaternion.Euler(faded && rng.NextDouble() < 0.3 ? 14f : 0f, 0, 0);
                    var label = Graybox.Label(root, s < 0 ? "天\n下\n大\n將\n軍" : "地\n下\n女\n將\n軍", p + Vector3.up * 1.45f + new Vector3(dir.x, 0, dir.y) * 0.26f, 0.022f, new Color(0.12f, 0.08f, 0.06f));
                    label.transform.rotation = Quaternion.LookRotation(-new Vector3(dir.x, 0, dir.y));
                    var col = Graybox.Box(root, "장승콜라이더", p + Vector3.up * 1.2f, new Vector3(0.5f, 2.4f, 0.5f), Color.gray);
                    col.GetComponent<Renderer>().enabled = false;
                }
                // 서낭당 돌무더기 (장승 옆)
                if (i % 2 == 0)
                {
                    var cp = c + side * 8f;
                    var cairn = G(cp.x, cp.y);
                    for (int r = 0; r < 9; r++)
                    {
                        float lvl = r < 5 ? 0 : r < 8 ? 1 : 2;
                        float rad = (2 - lvl) * 0.45f;
                        float a = r * 1.3f;
                        Graybox.Prim(PrimitiveType.Sphere, root, "돌", cairn + new Vector3(Mathf.Cos(a) * rad, 0.25f + lvl * 0.4f, Mathf.Sin(a) * rad), Vector3.one * (0.6f - lvl * 0.1f), new Color(0.5f, 0.49f, 0.46f), false);
                    }
                }
                // 버스 정류장: 마을 앞 길가
                var bp = hm.pos + dir * 24f - side * 6.5f;
                var bus = G(bp.x, bp.y);
                Graybox.Box(root, "정류장기둥", bus + Vector3.up * 1.3f, new Vector3(0.1f, 2.6f, 0.1f), new Color(0.5f, 0.5f, 0.52f));
                var signGo = Graybox.Prim(PrimitiveType.Cylinder, root, "정류장표지", bus + Vector3.up * 2.6f, new Vector3(0.7f, 0.03f, 0.7f), new Color(0.2f, 0.4f, 0.75f), false);
                signGo.transform.rotation = Quaternion.LookRotation(Vector3.up, new Vector3(dir.x, 0, dir.y));
                var busLabel = Graybox.Label(root, $"{hm.name}\n버스 정류장", bus + Vector3.up * 3.15f, 0.025f, Color.white);
                busLabel.gameObject.AddComponent<Billboard>();
                Graybox.Box(root, "정류장의자", bus + new Vector3(0, 0.45f, 0) + new Vector3(side.x, 0, side.y) * -1.2f, new Vector3(1.8f, 0.1f, 0.5f), wood);
            }

            // 갈림길 이정표 (장터거리, 새터 갈림길)
            void SignPost(Vector2 at, params (string text, Vector2 toward)[] arms)
            {
                var p = G(at.x + 6f, at.y + 6f);
                Graybox.Box(root, "이정표기둥", p + Vector3.up * 1.4f, new Vector3(0.15f, 2.8f, 0.15f), wood);
                for (int k = 0; k < arms.Length; k++)
                {
                    var d = (arms[k].toward - at).normalized;
                    var dir3 = new Vector3(d.x, 0, d.y);
                    var arm = Graybox.Box(root, "이정표판", p + Vector3.up * (2.4f - k * 0.35f) + dir3 * 0.6f, new Vector3(0.08f, 0.28f, 1.3f), new Color(0.85f, 0.82f, 0.72f));
                    arm.transform.rotation = Quaternion.LookRotation(dir3);
                    var lbl = Graybox.Label(root, arms[k].text, arm.transform.position, 0.022f, new Color(0.2f, 0.15f, 0.1f));
                    lbl.gameObject.AddComponent<Billboard>();
                }
            }
            SignPost(new Vector2(512, 500), ("배나무골 →", new Vector2(300, 820)), ("숯골 →", new Vector2(860, 260)), ("게이트 →", new Vector2(150, 150)));
            SignPost(new Vector2(330, 300), ("새터 →", new Vector2(250, 520)), ("장터거리 →", new Vector2(512, 500)));

            // 짚더미: 밭마다 몇 개
            foreach (var f in Fields())
            {
                int n = 2 + rng.Next(4);
                for (int k = 0; k < n; k++)
                {
                    var off = new Vector2((float)(rng.NextDouble() - 0.5) * f.w * 0.7f, (float)(rng.NextDouble() - 0.5) * f.d * 0.7f);
                    float a = f.angle * Mathf.Deg2Rad;
                    var pp = f.center + new Vector2(off.x * Mathf.Cos(a) - off.y * Mathf.Sin(a), off.x * Mathf.Sin(a) + off.y * Mathf.Cos(a));
                    var p = G(pp.x, pp.y);
                    var straw = faded ? new Color(0.5f, 0.45f, 0.32f) : new Color(0.72f, 0.62f, 0.38f);
                    Graybox.Prim(PrimitiveType.Cylinder, root, "짚더미", p + Vector3.up * 0.6f, new Vector3(1.6f, 0.6f, 1.6f), straw, false);
                    Graybox.Prim(PrimitiveType.Sphere, root, "짚더미머리", p + Vector3.up * 1.25f, new Vector3(1.7f, 1.2f, 1.7f), straw * 0.92f + new Color(0, 0, 0, 0.08f), false);
                }
            }
        }

        // ---------------- 전봇대 ----------------

        /// <summary>길 따라 전봇대와 처진 전깃줄 (80년대 시골길).</summary>
        public static void PlacePowerLines(Transform root, List<Vector3> line, Terrain terrain, float offset = 5.2f)
        {
            var wood = Graybox.Mat(new Color(0.26f, 0.2f, 0.15f));
            var wireMat = Graybox.Mat(new Color(0.08f, 0.08f, 0.08f));
            Vector3? prevA = null, prevB = null;
            float acc = 0f;
            for (int i = 1; i < line.Count; i++)
            {
                acc += Vector3.Distance(line[i], line[i - 1]);
                if (acc < 36f) continue;
                acc = 0f;
                var fwd = line[i] - line[i - 1];
                fwd.y = 0;
                fwd = fwd.normalized;
                var right = Vector3.Cross(Vector3.up, fwd);
                var p = line[i] + right * offset;
                if (terrain != null)
                {
                    p.y = terrain.SampleHeight(p);
                    if (p.y < WaterLevel + 0.5f || NearAny(p.x, p.z, -20f)) { prevA = prevB = null; continue; }
                }
                else p.y = 0f; // 우리 동네: 평지

                var pole = Graybox.Prim(PrimitiveType.Cylinder, root, "PowerPole", p + Vector3.up * 4f, new Vector3(0.24f, 4f, 0.24f), Color.gray, false);
                pole.GetComponent<Renderer>().sharedMaterial = wood;
                pole.transform.rotation = Quaternion.Euler(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f)); // 조금씩 기울었다
                var bar = Graybox.Box(root, "CrossArm", p + Vector3.up * 7.4f, new Vector3(1.6f, 0.12f, 0.12f), Color.gray, false);
                bar.GetComponent<Renderer>().sharedMaterial = wood;
                bar.transform.rotation = Quaternion.LookRotation(right) * Quaternion.Euler(0, 90, 0);
                var col = Graybox.Box(root, "PoleCollider", p + Vector3.up * 2f, new Vector3(0.3f, 4f, 0.3f), Color.gray);
                col.GetComponent<Renderer>().enabled = false;

                var a = p + Vector3.up * 7.5f - fwd * 0f + bar.transform.right * 0.7f;
                var b = p + Vector3.up * 7.5f - bar.transform.right * 0.7f;
                if (prevA.HasValue)
                {
                    Wire(root, prevA.Value, a, wireMat);
                    Wire(root, prevB.Value, b, wireMat);
                }
                prevA = a;
                prevB = b;
            }
        }

        static void Wire(Transform root, Vector3 a, Vector3 b, Material mat)
        {
            var go = new GameObject("Wire");
            go.transform.SetParent(root, false);
            go.layer = 2;
            var lr = go.AddComponent<LineRenderer>();
            const int n = 9;
            lr.positionCount = n;
            float sag = Vector3.Distance(a, b) * 0.035f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                lr.SetPosition(i, p);
            }
            lr.startWidth = lr.endWidth = 0.035f;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------- 마을·현장 ----------------

        static void BuildHamlet(Transform root, Hamlet hm, int index, Terrain terrain, WorldSpec spec, System.Random ruinRng)
        {
            var art = ArtCatalog.Instance;
            var rng = new System.Random(4000 + index * 97); // 집 배치는 모든 세계에서 같다
            for (int k = 0; k < hm.houses; k++)
            {
                float ang = (k / (float)hm.houses) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f + index;
                float dist = 17f + (float)rng.NextDouble() * 8f;
                var p = new Vector3(hm.pos.x + Mathf.Cos(ang) * dist, 0, hm.pos.y + Mathf.Sin(ang) * dist);
                p.y = terrain.SampleHeight(p);
                // 마당 쪽(마을 중심)을 본다
                var face = Quaternion.LookRotation(new Vector3(hm.pos.x - p.x, 0, hm.pos.y - p.z));
                int pick = rng.Next(100);
                bool ruined = spec.terrain == TerrainMod.Collapse && ruinRng.NextDouble() < 0.5;
                if (ruined)
                {
                    for (int r = 0; r < 5; r++)
                    {
                        var rb = Graybox.Box(root, "Ruin", p + new Vector3((float)ruinRng.NextDouble() * 8 - 4, 0.7f, (float)ruinRng.NextDouble() * 8 - 4),
                            new Vector3(2.5f, 1.4f, 2f), new Color(0.36f, 0.33f, 0.3f));
                        rb.transform.rotation = Quaternion.Euler((float)ruinRng.NextDouble() * 25, (float)ruinRng.NextDouble() * 360, (float)ruinRng.NextDouble() * 25);
                    }
                    continue;
                }
                // 80년대 시골: 슬레이트·양철 지붕 집, 헛간. 장터거리는 간판 단 상가주택이 섞인다
                float yaw = face.eulerAngles.y;
                if (index == 0 && k % 2 == 0)
                {
                    string[] shops = { "장터 국밥", "철물점", "정미소", "다방", "구판장" };
                    KoreanBuildings.ShopHouse(root, p, yaw, 9f, 7.5f, 2, shops[(k / 2) % shops.Length], rng);
                }
                else if (pick % 4 == 3)
                    KoreanBuildings.Shed(root, p, yaw, rng);
                else
                    KoreanBuildings.House(root, p, yaw, 8f, 5.5f, rng, new Vector2(13f, 11f), true);
            }
            // 우물, 돌담, 울타리 (마을 둘레. 길이 들어오는 곳은 비운다)
            if (art != null && art.hamletProps != null && art.hamletProps.Length > 0)
            {
                var well = ArtCatalog.Pick(art.hamletProps, 0);
                if (well != null)
                {
                    var wp = new Vector3(hm.pos.x - 6f, 0, hm.pos.y + 5f);
                    wp.y = terrain.SampleHeight(wp);
                    ArtCatalog.PlaceNatural(well, root, wp, Quaternion.Euler(0, index * 40f, 0), out _);
                }
            }
            if (art != null && art.fences != null && art.fences.Length > 0)
            {
                var fence = ArtCatalog.Pick(art.fences, index);
                if (fence != null)
                {
                    float radius = 31f;
                    float seg = 3.2f;
                    int count = Mathf.FloorToInt(2f * Mathf.PI * radius / seg);
                    for (int k = 0; k < count; k++)
                    {
                        float ang = k / (float)count * Mathf.PI * 2f;
                        var fp = new Vector3(hm.pos.x + Mathf.Cos(ang) * radius, 0, hm.pos.y + Mathf.Sin(ang) * radius);
                        if (RoadNear(fp.x, fp.z, 9f)) continue;
                        if (rng.NextDouble() < 0.18) continue; // 군데군데 무너졌다
                        fp.y = terrain.SampleHeight(fp);
                        var rot = Quaternion.LookRotation(new Vector3(-Mathf.Sin(ang), 0, Mathf.Cos(ang))) * Quaternion.Euler(0, 90, 0);
                        ArtCatalog.PlaceNatural(fence, root, fp, rot, out _);
                    }
                }
            }

            // 마을 이름 표지
            var sp = new Vector3(hm.pos.x + 7f, 0, hm.pos.y - 9f);
            sp.y = terrain.SampleHeight(sp);
            Graybox.Box(root, "VillageSignPost", sp + new Vector3(0, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f), new Color(0.35f, 0.28f, 0.2f));
            var label = Graybox.Label(root, hm.name, sp + new Vector3(0, 2.6f, 0), 0.06f, new Color(0.95f, 0.9f, 0.75f));
            label.gameObject.AddComponent<Billboard>();
        }

        static void BuildSite(Transform root, Vector3 c)
        {
            var fence = new Color(0.6f, 0.5f, 0.2f);
            Graybox.Box(root, "SiteFence_A", c + new Vector3(-12, 1, 0), new Vector3(0.3f, 2, 20), fence);
            Graybox.Box(root, "SiteFence_B", c + new Vector3(0, 1, 12), new Vector3(20, 2, 0.3f), fence);
            Graybox.Box(root, "SiteFence_C", c + new Vector3(12, 1, -4), new Vector3(0.3f, 2, 12), fence);
            Graybox.Box(root, "Container", c + new Vector3(5, 1.3f, 6), new Vector3(6, 2.6f, 2.4f), new Color(0.45f, 0.25f, 0.2f));
            Graybox.Box(root, "Container2", c + new Vector3(-6, 1.3f, -6), new Vector3(2.4f, 2.6f, 6), new Color(0.25f, 0.35f, 0.45f));
            Graybox.Box(root, "Shed", c + new Vector3(-6, 1.6f, 7), new Vector3(5, 3.2f, 4), new Color(0.5f, 0.5f, 0.52f));
            var label = Graybox.Label(root, "○○개발공사 측량 현장", c + new Vector3(0, 3.6f, -10), 0.05f, UIStyle.Amber);
            label.gameObject.AddComponent<Billboard>();
        }

        // ---------------- 자연물 ----------------

        static bool RoadNear(float x, float z, float within)
        {
            var p = new Vector2(x, z);
            foreach (var road in Roads)
                for (int i = 0; i < road.Length - 1; i++)
                {
                    var a = road[i]; var b = road[i + 1];
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    if (Vector2.Distance(p, a + ab * t) < within) return true;
                }
            return false;
        }

        static bool NearAny(float x, float z, float pad)
        {
            if (Vector2.Distance(new Vector2(x, z), V2(GatePos)) < 34f + pad) return true;
            if (Vector2.Distance(new Vector2(x, z), V2(ObjectivePos)) < 36f + pad) return true;
            foreach (var hm in Hamlets) if (Vector2.Distance(new Vector2(x, z), hm.pos) < 36f + pad) return true;
            return false;
        }

        static GameObject fallbackTree, fallbackDeadTree;

        /// <summary>아트가 없을 때 쓰는 그레이박스 나무. 터레인 나무는 루트에 메시 하나가 있어야 해서 합친다.</summary>
        static GameObject FallbackTree(bool dead)
        {
            ref GameObject cache = ref dead ? ref fallbackDeadTree : ref fallbackTree;
            if (cache != null) return cache;
            var tmp = new GameObject("tmp");
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tmp.transform);
            trunk.transform.localScale = new Vector3(0.4f, 2f, 0.4f);
            trunk.transform.localPosition = new Vector3(0, 2f, 0);
            var crown = GameObject.CreatePrimitive(dead ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
            crown.transform.SetParent(tmp.transform);
            crown.transform.localScale = dead ? new Vector3(0.15f, 1.2f, 0.15f) : new Vector3(3.2f, 4f, 3.2f);
            crown.transform.localPosition = new Vector3(0.3f, dead ? 4.6f : 5.2f, 0);
            var combine = new[]
            {
                new CombineInstance { mesh = trunk.GetComponent<MeshFilter>().sharedMesh, transform = trunk.transform.localToWorldMatrix },
                new CombineInstance { mesh = crown.GetComponent<MeshFilter>().sharedMesh, transform = crown.transform.localToWorldMatrix },
            };
            var mesh = new Mesh();
            mesh.CombineMeshes(combine, false, true);
            Object.Destroy(tmp);
            cache = new GameObject(dead ? "GrayDeadTree" : "GrayTree", typeof(MeshFilter), typeof(MeshRenderer));
            cache.GetComponent<MeshFilter>().sharedMesh = mesh;
            cache.GetComponent<MeshRenderer>().sharedMaterials = new[]
            {
                Graybox.Mat(new Color(0.32f, 0.24f, 0.17f)),
                Graybox.Mat(dead ? new Color(0.3f, 0.26f, 0.22f) : new Color(0.24f, 0.38f, 0.2f)),
            };
            var cap = cache.AddComponent<CapsuleCollider>();
            cap.radius = 0.35f; cap.height = 5f; cap.center = new Vector3(0, 2.5f, 0);
            cache.SetActive(false);
            Object.DontDestroyOnLoad(cache);
            return cache;
        }

        static void PlantTrees(TerrainData td, Terrain terrain, WorldSpec spec, float[,] roadDist, float[,] h, float water, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            bool hasArt = ArtCatalog.HasNature;
            var living = hasArt ? art.trees : new[] { FallbackTree(false) };
            var dead = hasArt && art.deadTrees != null && art.deadTrees.Length > 0 ? art.deadTrees : new[] { FallbackTree(true) };

            var protos = new List<TreePrototype>();
            foreach (var t in living) if (t != null) protos.Add(new TreePrototype { prefab = t });
            int deadStart = protos.Count;
            foreach (var t in dead) if (t != null) protos.Add(new TreePrototype { prefab = t });
            td.treePrototypes = protos.ToArray();

            // 세계마다: 어두운 세계는 죽은 나무가 많고, 독가스 세계는 숲이 성기다
            float deadRatio = spec.rules.HasFlag(WorldRule.EternalNight) || spec.rules.HasFlag(WorldRule.ToxicGas) ? 0.6f : 0.08f;
            float density = spec.rules.HasFlag(WorldRule.ToxicGas) ? 0.6f : 1f + (spec.number % 5) * 0.08f;

            var list = new List<TreeInstance>();
            const float step = 7.5f;
            for (float z = 20f; z < Size - 20f; z += step)
            for (float x = 20f; x < Size - 20f; x += step)
            {
                float px = x + (float)(rng.NextDouble() - 0.5) * step * 0.9f;
                float pz = z + (float)(rng.NextDouble() - 0.5) * step * 0.9f;
                if (SampleGrid(roadDist, px, pz) < 8f || NearAny(px, pz, 6f) || FieldWeight(px, pz) > 0f) continue;
                float hgt = SampleGrid(h, px, pz);
                if (hgt < water + 0.6f) continue;
                float forest = Fbm(px * 0.006f + 5f, pz * 0.006f + 9f, 3);
                float chance = Mathf.InverseLerp(0.45f, 0.62f, forest) * 0.85f + 0.03f;
                float steep = td.GetSteepness(px / Size, pz / Size);
                if (steep > 34f) chance *= 0.25f;
                if (rng.NextDouble() > chance * density) continue;
                bool isDead = rng.NextDouble() < deadRatio;
                int proto = isDead ? deadStart + rng.Next(protos.Count - deadStart) : rng.Next(Mathf.Max(1, deadStart));
                float s = 0.8f + (float)rng.NextDouble() * 0.55f;
                list.Add(new TreeInstance
                {
                    position = new Vector3(px / Size, hgt / MaxHeight, pz / Size),
                    prototypeIndex = proto,
                    widthScale = s,
                    heightScale = s * (0.9f + (float)rng.NextDouble() * 0.25f),
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }
            // 덤불: 숲 가장자리와 길가에 (콜라이더 없음)
            var bushes = hasArt && art.bushes != null && art.bushes.Length > 0 ? art.bushes : null;
            if (bushes != null)
            {
                int bushStart = protos.Count;
                foreach (var b in bushes) if (b != null) protos.Add(new TreePrototype { prefab = b });
                td.treePrototypes = protos.ToArray();
                for (int i = 0; i < 2600; i++)
                {
                    float px = 40f + (float)rng.NextDouble() * (Size - 80f);
                    float pz = 40f + (float)rng.NextDouble() * (Size - 80f);
                    float rd = SampleGrid(roadDist, px, pz);
                    if (rd < 5f || NearAny(px, pz, 0f) || FieldWeight(px, pz) > 0f) continue;
                    float hgt = SampleGrid(h, px, pz);
                    if (hgt < water + 0.3f) continue;
                    float forest = Fbm(px * 0.006f + 5f, pz * 0.006f + 9f, 3);
                    bool edge = forest > 0.47f && forest < 0.56f;  // 숲 가장자리
                    bool roadside = rd < 11f;
                    if (!edge && !roadside && rng.NextDouble() > 0.15) continue;
                    float sc = 0.7f + (float)rng.NextDouble() * 0.6f;
                    list.Add(new TreeInstance
                    {
                        position = new Vector3(px / Size, hgt / MaxHeight, pz / Size),
                        prototypeIndex = bushStart + rng.Next(protos.Count - bushStart),
                        widthScale = sc, heightScale = sc,
                        rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                        color = Color.white, lightmapColor = Color.white,
                    });
                }
            }
            td.SetTreeInstances(list.ToArray(), true);
        }

        static void ScatterRocks(Transform root, Terrain terrain, float[,] roadDist, float[,] h, float water, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            int placed = 0;
            for (int tries = 0; tries < 900 && placed < 160; tries++)
            {
                float x = 60f + (float)rng.NextDouble() * (Size - 120f);
                float z = 60f + (float)rng.NextDouble() * (Size - 120f);
                if (SampleGrid(roadDist, x, z) < 7f || NearAny(x, z, 2f) || SampleGrid(h, x, z) < water + 0.3f || FieldWeight(x, z) > 0f) continue;
                var p = new Vector3(x, 0, z);
                p.y = terrain.SampleHeight(p) - 0.2f;
                var rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                var prefab = art != null ? ArtCatalog.Pick(art.rocks, rng.Next(100)) : null;
                Bounds b;
                if (prefab != null)
                {
                    var inst = ArtCatalog.PlaceNatural(prefab, root, p, rot, out b);
                    inst.name = "Rock";
                }
                else
                {
                    float s = 1f + (float)rng.NextDouble() * 2.5f;
                    var r = Graybox.Box(root, "Rock", p + Vector3.up * s * 0.4f, new Vector3(s, s * 0.8f, s * 1.2f), new Color(0.45f, 0.44f, 0.42f), false);
                    r.transform.rotation = rot * Quaternion.Euler(10, 0, 8);
                    b = new Bounds(r.transform.position, new Vector3(s, s * 0.8f, s * 1.2f));
                }
                // 큰 바위만 엄폐물이 된다
                if (b.size.y > 1.2f)
                {
                    var col = Graybox.Box(root, "RockCollider", b.center, b.size * 0.85f, Color.gray);
                    col.GetComponent<Renderer>().enabled = false;
                }
                placed++;
            }
        }

        static void PlantGrass(TerrainData td, float[,] roadDist, float[,] h, float water)
        {
            var art = ArtCatalog.Instance;
            if (art == null || art.grass == null || art.grass.Length == 0 || art.grass[0] == null) return;
            var protos = new List<DetailPrototype>();
            foreach (var g in art.grass)
            {
                if (g == null) continue;
                protos.Add(new DetailPrototype
                {
                    prototype = g,
                    usePrototypeMesh = true,
                    renderMode = DetailRenderMode.VertexLit,
                    useInstancing = true,
                    minWidth = 0.8f, maxWidth = 1.3f, minHeight = 0.8f, maxHeight = 1.4f,
                    noiseSpread = 0.3f,
                    healthyColor = Color.white, dryColor = new Color(0.85f, 0.8f, 0.65f),
                });
            }
            td.detailPrototypes = protos.ToArray();
            int dr = 512;
            td.SetDetailResolution(dr, 32);
            for (int layer = 0; layer < protos.Count; layer++)
            {
                var map = new int[dr, dr];
                for (int z = 0; z < dr; z++)
                for (int x = 0; x < dr; x++)
                {
                    float wx = x / (float)(dr - 1) * Size, wz = z / (float)(dr - 1) * Size;
                    if (SampleGrid(roadDist, wx, wz) < 4.5f || SampleGrid(h, wx, wz) < water + 0.4f || FieldWeight(wx, wz) > 0.2f) continue;
                    float n = Mathf.PerlinNoise(wx * 0.03f + layer * 17f, wz * 0.03f);
                    if (n > 0.46f) map[z, x] = n > 0.72f ? 4 : n > 0.6f ? 3 : 2;
                }
                td.SetDetailLayer(0, 0, layer, map);
            }
        }

        // ---------------- 스폰 ----------------

        static void CollectSpawnPoints(MissionLayout layout, WorldSpec spec)
        {
            var rng = new System.Random(spec.number * 13 + 7);
            void Around(Vector3 c, float radius, int count, List<Vector3> into)
            {
                int tries = 0;
                while (count > 0 && tries++ < count * 30)
                {
                    var p = c + new Vector3((float)(rng.NextDouble() * 2 - 1) * radius, 0, (float)(rng.NextDouble() * 2 - 1) * radius);
                    if (NavMesh.SamplePosition(new Vector3(p.x, layout.terrain.SampleHeight(p), p.z), out var hit, 4f, NavMesh.AllAreas))
                    {
                        into.Add(hit.position);
                        count--;
                    }
                }
            }
            Around(layout.midpointPos, 70f, 16, layout.spawnPoints);
            Around(layout.objectivePos, 75f, 16, layout.spawnPoints);
            foreach (var kv in layout.landmarks) Around(kv.Value, 45f, 3, layout.spawnPoints);
            Around(layout.objectivePos, 16f, 6, layout.bruteSpawnPoints);
            if (layout.bruteSpawnPoints.Count == 0) layout.bruteSpawnPoints.Add(layout.objectivePos);
        }
    }
}
