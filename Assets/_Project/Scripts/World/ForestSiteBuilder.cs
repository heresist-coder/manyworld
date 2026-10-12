using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Manyworld
{
    /// <summary>
    /// 첫 자연 월드 "숲 현장" (목표 샷 03, D17·D18).
    /// 남서쪽 초원 흙길에서 출발 → 숲 가장자리 주차 공터 → 숲길 80m → 숲속의 빈 방.
    /// 빈 방: 소나무가 직선으로 세 면의 벽을 만들고, 맨흙 바닥 가운데 의자 하나, 벽 밑엔 곰인형들.
    /// 가장자리는 높은 산으로 막고, 그 너머에 먼 산 원경을 둔다.
    /// </summary>
    public static class ForestSiteBuilder
    {
        public const float Size = 768f;
        const int Res = 513;   // 지형 해상도는 2^n+1만 된다
        const float Cell = Size / (Res - 1); // 1.5m
        public const float MaxHeight = 170f;
        public const float RoomHalf = 9f;

        // 고정 지리
        public static readonly Vector2 CarStart = new Vector2(214, 188);
        static readonly Vector2[] Road =
        {
            new Vector2(186, 160), new Vector2(250, 214), new Vector2(300, 246), new Vector2(346, 292), new Vector2(378, 346), new Vector2(400, 396),
        };
        public static readonly Vector2 Trailhead = new Vector2(404, 404);
        public static readonly Vector2 RoomCenter = new Vector2(430, 482);

        /// <summary>방의 앞(열린 면 → 안쪽) 방향. 열린 면이 숲길(주차 공터)을 본다.</summary>
        public static Vector2 RoomForward => (RoomCenter - Trailhead).normalized;
        public static Vector2 RoomEntrance => RoomCenter - RoomForward * (RoomHalf + 1.5f);

        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        public static MissionLayout Build(int seed, out TeddyRoom room)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var root = new GameObject("ForestSite").transform;
            var layout = new MissionLayout { root = root, worldSize = Size, waterLevel = -10f };
            var rng = new System.Random(seed * 7717 + 11);

            // 1) 높이
            var h = new float[Res, Res];
            for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
                h[z, x] = BaseHeight(x * Cell, z * Cell);

            // 2) 흙길과 숲길: 지형을 따라가되 부드럽게
            var roadDist = Filled(999f);
            var trailDist = Filled(999f);
            var roadH = new float[Res, Res];
            var roadPts = WorldTerrainBuilder.Densify(Road, 2f);
            roadPts.Add(Trailhead);
            var trailPts = WorldTerrainBuilder.Densify(new[] { Trailhead, Vector2.Lerp(Trailhead, RoomEntrance, 0.5f) + new Vector2(-6f, 2f), RoomEntrance }, 1.5f);
            StampLine(roadPts, h, roadDist, roadH, 11f);
            StampLine(trailPts, h, trailDist, roadH, 5f);
            for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                float d = Mathf.Min(roadDist[z, x], trailDist[z, x]);
                float r = roadDist[z, x] < trailDist[z, x] ? 11f : 5f;
                if (d > r) continue;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r * 0.4f, d));
                h[z, x] = Mathf.Lerp(h[z, x], roadH[z, x] - 0.04f, w);
            }

            // 3) 평평한 자리: 출발점, 주차 공터, 빈 방
            foreach (var (c, r) in new[] { (CarStart, 14f), (Trailhead, 16f), (RoomCenter, 24f) })
            {
                float target = Sample(h, c.x, c.y);
                ForCells(c, r, (x, z, d) => h[z, x] = Mathf.Lerp(h[z, x], target, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r * 0.55f, d))));
            }

            // 4) 지형
            var td = new TerrainData { heightmapResolution = Res, size = new Vector3(Size, MaxHeight, Size) };
            var norm = new float[Res, Res];
            for (int z = 0; z < Res; z++) for (int x = 0; x < Res; x++) norm[z, x] = Mathf.Clamp01(h[z, x] / MaxHeight);
            td.SetHeights(0, 0, norm);
            Paint(td, h, roadDist, trailDist);
            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "Terrain";
            terrainGo.transform.SetParent(root, false);
            var terrain = terrainGo.GetComponent<Terrain>();
            var terrainMat = Resources.Load<Material>("Materials/Terrain");
            if (terrainMat != null) terrain.materialTemplate = terrainMat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 400f;
            terrain.treeDistance = 850f;
            terrain.treeBillboardDistance = 850f; // 신티 나무는 빌보드·원거리 LOD가 하얗게 뜬다. LOD0 메시로만 그린다
            terrain.treeCrossFadeLength = 20f;
            terrain.detailObjectDistance = 110f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            layout.terrain = terrain;
            Vector3 G(Vector2 p) => new Vector3(p.x, terrain.SampleHeight(V3(p)), p.y);

            // 5) 자연물
            PlantTrees(td, h, roadDist, trailDist, rng);
            PlantGrass(td, roadDist, trailDist);
            ScatterRocks(root, terrain, roadDist, trailDist, rng);
            PlaceMountains(root, rng);
            terrain.Flush();

            // 6) 빈 방 (목표)
            room = BuildRoom(root, terrain, rng);

            // 7) 배치
            var roadDir = (Road[2] - Road[1]).normalized;
            layout.ticoPos = G(CarStart);
            layout.ticoRotation = Quaternion.LookRotation(V3(roadDir));
            var side = new Vector2(roadDir.y, -roadDir.x); // 오른쪽
            layout.playerSpawn = G(CarStart - side * 3.2f - roadDir * 1.5f) + Vector3.up * 0.1f;
            layout.companionSpawn = G(CarStart - side * 3.4f + roadDir * 1.5f) + Vector3.up * 0.1f;
            layout.gatePos = layout.ticoPos;
            layout.midpointPos = G(Trailhead);
            layout.objectivePos = room.chair.position;
            layout.landmarks["주차 공터"] = layout.midpointPos;

            layout.minimap = BuildMinimap(h, roadDist, trailDist);

            // 8) 네비메시: 가장자리 산을 뺀 안쪽
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(Size / 2, 60, Size / 2);
            surface.size = new Vector3(Size - 280f, 220, Size - 280f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << 2);
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.4f;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.BuildNavMesh();

            CollectSpawnPoints(layout, rng);
            Debug.Log($"[Manyworld] 숲 현장 생성: {watch.ElapsedMilliseconds}ms, 나무 {td.treeInstanceCount}, 스폰 {layout.spawnPoints.Count}+{layout.bruteSpawnPoints.Count}");
            return layout;
        }

        // ---------------- 지형 ----------------

        static float BaseHeight(float x, float z)
        {
            float hills = WorldTerrainBuilder.Fbm(x * 0.004f + 3.1f, z * 0.004f + 8.7f, 4) * 24f;
            float ne = Mathf.Clamp01((x + z - 700f) / 500f) * 9f; // 북동쪽 숲이 조금 높다
            float detail = (Mathf.PerlinNoise(x * 0.03f + 1f, z * 0.03f + 5f) - 0.5f) * 1.4f;
            float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            float rimT = Mathf.Clamp01(1f - edge / 175f);
            float rim = rimT * rimT * 100f + WorldTerrainBuilder.Fbm(x * 0.008f + 40f, z * 0.008f + 11f, 3) * 45f * rimT;
            return 10f + hills + ne + detail + rim;
        }

        /// <summary>숲 밀도 0~1. 방 주변과 북동쪽은 숲, 출발점 쪽 남서는 초원.</summary>
        public static float ForestMask(float x, float z)
        {
            float n = WorldTerrainBuilder.Fbm(x * 0.007f + 5f, z * 0.007f + 9f, 3);
            float m = Mathf.InverseLerp(0.47f, 0.58f, n);
            float room = Vector2.Distance(new Vector2(x, z), RoomCenter);
            m += Mathf.InverseLerp(170f, 50f, room);
            m -= Mathf.InverseLerp(640f, 430f, x + z) * 0.95f;
            float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            m += Mathf.InverseLerp(240f, 130f, edge) * 0.8f; // 가장자리 산은 숲으로 덮인다
            return Mathf.Clamp01(m);
        }

        static float[,] Filled(float v)
        {
            var g = new float[Res, Res];
            for (int z = 0; z < Res; z++) for (int x = 0; x < Res; x++) g[z, x] = v;
            return g;
        }

        static void StampLine(List<Vector2> pts, float[,] h, float[,] dist, float[,] hgt, float r)
        {
            var hs = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) hs[i] = Sample(h, pts[i].x, pts[i].y);
            hs = WorldTerrainBuilder.Smooth(WorldTerrainBuilder.Smooth(hs, 10), 10);
            for (int i = 0; i < pts.Count; i++)
            {
                float y = hs[i];
                ForCells(pts[i], r, (x, z, d) =>
                {
                    if (d < dist[z, x]) { dist[z, x] = d; hgt[z, x] = y; }
                });
            }
        }

        static float Sample(float[,] g, float wx, float wz)
        {
            float fx = Mathf.Clamp(wx / Cell, 0, Res - 1.001f), fz = Mathf.Clamp(wz / Cell, 0, Res - 1.001f);
            int x0 = (int)fx, z0 = (int)fz;
            float tx = fx - x0, tz = fz - z0;
            return Mathf.Lerp(Mathf.Lerp(g[z0, x0], g[z0, x0 + 1], tx), Mathf.Lerp(g[z0 + 1, x0], g[z0 + 1, x0 + 1], tx), tz);
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

        /// <summary>방 기준 로컬 좌표 (x: 오른쪽, z: 안쪽).</summary>
        public static Vector2 RoomLocal(float x, float z)
        {
            var d = new Vector2(x, z) - RoomCenter;
            var f = RoomForward;
            var r = new Vector2(f.y, -f.x);
            return new Vector2(Vector2.Dot(d, r), Vector2.Dot(d, f));
        }

        static bool InRoom(float x, float z, float pad)
        {
            var l = RoomLocal(x, z);
            return Mathf.Abs(l.x) < RoomHalf + pad && Mathf.Abs(l.y) < RoomHalf + pad;
        }

        // ---------------- 칠하기 ----------------

        static void Paint(TerrainData td, float[,] h, float[,] roadDist, float[,] trailDist)
        {
            // 바랜 세이지·올리브 초원, 짙은 숲 바닥, 황토색 길, 회색 바위, 마른 풀
            td.terrainLayers = new[]
            {
                WorldTerrainBuilder.Layer(new Color(0.43f, 0.46f, 0.29f), 7f, 1),  // 0 초원 (바랜 세이지·올리브)
                WorldTerrainBuilder.Layer(new Color(0.24f, 0.25f, 0.15f), 6f, 2),  // 1 숲 바닥
                WorldTerrainBuilder.Layer(new Color(0.6f, 0.47f, 0.31f), 5f, 3),   // 2 흙길·방 바닥
                WorldTerrainBuilder.Layer(new Color(0.4f, 0.41f, 0.38f), 9f, 4),   // 3 바위
                WorldTerrainBuilder.Layer(new Color(0.58f, 0.55f, 0.33f), 8f, 5),  // 4 마른 풀
            };
            int ar = 512;
            td.alphamapResolution = ar;
            var a = new float[ar, ar, 5];
            for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
            {
                float wx = x / (float)(ar - 1) * Size, wz = z / (float)(ar - 1) * Size;
                float e = Sample(h, wx + 2f, wz), w = Sample(h, wx - 2f, wz), n = Sample(h, wx, wz + 2f), s = Sample(h, wx, wz - 2f);
                float slope = Mathf.Atan(new Vector2((e - w) / 4f, (n - s) / 4f).magnitude) * Mathf.Rad2Deg;
                float rd = Sample(roadDist, wx, wz), td2 = Sample(trailDist, wx, wz);
                float jitter = (Mathf.PerlinNoise(wx * 0.35f, wz * 0.35f) - 0.5f) * 0.3f;
                float road = Mathf.Clamp01(Mathf.InverseLerp(3.6f, 2.2f, rd) + (rd < 4.5f ? jitter : 0f));
                float trail = Mathf.Clamp01(Mathf.InverseLerp(1.6f, 0.6f, td2) + (td2 < 2.2f ? jitter : 0f)) * 0.85f;
                float floor = 0f;
                if (InRoom(wx, wz, 0.6f))
                {
                    var l = RoomLocal(wx, wz);
                    float edge = Mathf.Max(Mathf.Abs(l.x), Mathf.Abs(l.y));
                    floor = Mathf.Clamp01(Mathf.InverseLerp(RoomHalf + 0.6f, RoomHalf - 0.8f, edge) + jitter * 0.5f);
                }
                float dirt = Mathf.Max(road, Mathf.Max(trail, floor));
                float rock = Mathf.InverseLerp(44f, 58f, slope) * (1f - dirt); // 산비탈도 대부분 숲 바닥
                float forest = ForestMask(wx, wz);
                float dry = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 0.7f, WorldTerrainBuilder.Fbm(wx * 0.012f + 31f, wz * 0.012f + 17f, 3)) + Mathf.InverseLerp(6.5f, 3.6f, rd) * 0.7f);
                float rest = Mathf.Max(0f, 1f - dirt - rock);
                a[z, x, 2] = dirt;
                a[z, x, 3] = rock;
                a[z, x, 1] = rest * Mathf.InverseLerp(0.35f, 0.75f, forest);
                float open = rest - a[z, x, 1];
                a[z, x, 4] = open * dry;
                a[z, x, 0] = open * (1f - dry);
            }
            td.SetAlphamaps(0, 0, a);
        }

        // ---------------- 자연물 ----------------

        static void PlantTrees(TerrainData td, float[,] h, float[,] roadDist, float[,] trailDist, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            bool hasArt = art != null && art.pines != null && art.pines.Length > 0 && art.pines[0] != null;
            var protos = new List<TreePrototype>();
            if (hasArt)
            {
                foreach (var t in art.pines) if (t != null) protos.Add(new TreePrototype { prefab = t });
                if (art.meadowTrees != null) foreach (var t in art.meadowTrees) if (t != null) protos.Add(new TreePrototype { prefab = t });
            }
            else protos.Add(new TreePrototype { prefab = FallbackPine() });
            int pineCount = hasArt ? art.pines.Length : 1;
            td.treePrototypes = protos.ToArray();

            var list = new List<TreeInstance>();
            const float step = 4.6f;
            for (float z = 12f; z < Size - 12f; z += step)
            for (float x = 12f; x < Size - 12f; x += step)
            {
                float px = x + (float)(rng.NextDouble() - 0.5) * step * 0.95f;
                float pz = z + (float)(rng.NextDouble() - 0.5) * step * 0.95f;
                if (Sample(roadDist, px, pz) < 8.5f || Sample(trailDist, px, pz) < 3.2f) continue;
                if (InRoom(px, pz, 4.2f)) continue; // 방의 벽은 따로 줄 세워 심는다
                var p2 = new Vector2(px, pz);
                if (Vector2.Distance(p2, CarStart) < 16f || Vector2.Distance(p2, Trailhead) < 17f) continue;
                float forest = ForestMask(px, pz);
                float hgt = Sample(h, px, pz);
                bool meadow = forest < 0.3f;
                float edgeDist = Mathf.Min(Mathf.Min(px, Size - px), Mathf.Min(pz, Size - pz));
                bool rim = edgeDist < 200f;
                double chance = meadow ? 0.018 : rim ? 0.97 : 0.25 + forest * 0.65; // 가장자리 산은 빽빽한 숲
                if (rng.NextDouble() > chance) continue;
                int proto = meadow && protos.Count > pineCount && rng.NextDouble() < 0.65
                    ? pineCount + rng.Next(protos.Count - pineCount)
                    : rng.Next(pineCount);
                float s = (rim ? 1.25f : 0.85f) + (float)rng.NextDouble() * 0.5f;
                list.Add(new TreeInstance
                {
                    position = new Vector3(px / Size, hgt / MaxHeight, pz / Size),
                    prototypeIndex = proto,
                    widthScale = s,
                    heightScale = s * (0.95f + (float)rng.NextDouble() * 0.3f),
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }
            td.SetTreeInstances(list.ToArray(), true);
        }

        static GameObject fallbackPine;
        static GameObject FallbackPine()
        {
            if (fallbackPine != null) return fallbackPine;
            fallbackPine = new GameObject("FallbackPine");
            Graybox.Prim(PrimitiveType.Cylinder, fallbackPine.transform, "Trunk", new Vector3(0, 2f, 0), new Vector3(0.5f, 2f, 0.5f), new Color(0.35f, 0.25f, 0.18f));
            Graybox.Prim(PrimitiveType.Sphere, fallbackPine.transform, "Crown", new Vector3(0, 6f, 0), new Vector3(3f, 6f, 3f), new Color(0.2f, 0.32f, 0.18f), false);
            fallbackPine.SetActive(false);
            Object.DontDestroyOnLoad(fallbackPine);
            return fallbackPine;
        }

        static void PlantGrass(TerrainData td, float[,] roadDist, float[,] trailDist)
        {
            var art = ArtCatalog.Instance;
            if (art == null || art.grass == null || art.grass.Length == 0 || art.grass[0] == null) return;
            var protos = new List<DetailPrototype>();
            foreach (var g in art.grass)
            {
                if (g == null) continue;
                protos.Add(new DetailPrototype
                {
                    prototype = g, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                    minWidth = 0.8f, maxWidth = 1.4f, minHeight = 0.8f, maxHeight = 1.5f, noiseSpread = 0.3f,
                    healthyColor = Color.white, dryColor = new Color(0.9f, 0.85f, 0.6f),
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
                    if (Sample(roadDist, wx, wz) < 3.4f || Sample(trailDist, wx, wz) < 1.2f || InRoom(wx, wz, 0.3f)) continue;
                    float forest = ForestMask(wx, wz);
                    float n = Mathf.PerlinNoise(wx * 0.04f + layer * 17f, wz * 0.04f);
                    float thr = Mathf.Lerp(0.42f, 0.62f, forest); // 숲 속은 성기게
                    if (n > thr) map[z, x] = n > thr + 0.2f ? 4 : n > thr + 0.1f ? 3 : 2;
                }
                td.SetDetailLayer(0, 0, layer, map);
            }
        }

        static void ScatterRocks(Transform root, Terrain terrain, float[,] roadDist, float[,] trailDist, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            int placed = 0;
            for (int tries = 0; tries < 1400 && placed < 190; tries++)
            {
                float x = 150f + (float)rng.NextDouble() * (Size - 300f);
                float z = 150f + (float)rng.NextDouble() * (Size - 300f);
                float rd = Sample(roadDist, x, z);
                if (rd < 6f || Sample(trailDist, x, z) < 2.5f || InRoom(x, z, 3f)) continue;
                var p2 = new Vector2(x, z);
                if (Vector2.Distance(p2, CarStart) < 12f || Vector2.Distance(p2, Trailhead) < 15f) continue;
                // 길 어깨와 숲 가장자리에 몰린다
                float forest = ForestMask(x, z);
                bool shoulder = rd < 14f;
                bool edge = forest > 0.25f && forest < 0.6f;
                if (!shoulder && !edge && rng.NextDouble() > 0.25) continue;
                var p = new Vector3(x, 0, z);
                p.y = terrain.SampleHeight(p) - 0.25f;
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
                    float s = 1f + (float)rng.NextDouble() * 2f;
                    var r = Graybox.Box(root, "Rock", p + Vector3.up * s * 0.4f, new Vector3(s, s * 0.8f, s * 1.2f), new Color(0.47f, 0.46f, 0.44f), false);
                    r.transform.rotation = rot * Quaternion.Euler(10, 0, 8);
                    b = new Bounds(r.transform.position, new Vector3(s, s * 0.8f, s * 1.2f));
                }
                if (b.size.y > 1.1f)
                {
                    var col = Graybox.Box(root, "RockCollider", b.center, b.size * 0.85f, Color.gray);
                    col.GetComponent<Renderer>().enabled = false;
                }
                placed++;
            }
        }

        /// <summary>분지 너머 먼 산. 공기 원근(거리 안개)으로 푸르게 물러난다.</summary>
        static void PlaceMountains(Transform root, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            var center = new Vector3(Size / 2, 0, Size / 2);
            // 지형 바깥 땅: 가장자리 산 높이 아래에 넓게 깔아 하늘 아래 갈색 바닥이 안 보이게
            const float outer = 2600f;
            var groundColor = new Color(0.3f, 0.36f, 0.26f);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(Size / 2, 95f, -outer / 2), new Vector3(outer * 2 + Size, 1f, outer)),
                (new Vector3(Size / 2, 95f, Size + outer / 2), new Vector3(outer * 2 + Size, 1f, outer)),
                (new Vector3(-outer / 2, 95f, Size / 2), new Vector3(outer, 1f, Size)),
                (new Vector3(Size + outer / 2, 95f, Size / 2), new Vector3(outer, 1f, Size)),
            })
            {
                var g = Graybox.Box(root, "OuterGround", pos, size, groundColor, false);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                float ang = (i + (float)rng.NextDouble() * 0.6f) / n * Mathf.PI * 2f;
                float dist = 1150f + (float)rng.NextDouble() * 500f;
                var p = center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * dist;
                float height = 260f + (float)rng.NextDouble() * 220f;
                var prefab = art != null ? ArtCatalog.Pick(art.mountains, rng.Next(100)) : null;
                GameObject go;
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, root);
                    // 원경 산은 판 모양이다. 분지 가운데를 보게 세운다
                    var toCenter = center - p;
                    toCenter.y = 0;
                    go.transform.rotation = Quaternion.LookRotation(toCenter) * Quaternion.Euler(0, (float)(rng.NextDouble() - 0.5) * 30f, 0);
                    var b = ArtCatalog.RendererBounds(go);
                    float s = height / Mathf.Max(1f, b.size.y);
                    go.transform.localScale = Vector3.one * s;
                    b = ArtCatalog.RendererBounds(go);
                    go.transform.position = p + new Vector3(0, -b.min.y - 30f, 0);
                }
                else
                {
                    go = Graybox.Prim(PrimitiveType.Sphere, root, "Mountain", p, new Vector3(height * 2.2f, height, height * 2.2f), new Color(0.35f, 0.42f, 0.5f), false);
                }
                go.name = "FarMountain";
                var far = Graybox.Mat(new Color(0.3f, 0.37f, 0.48f)); // 거리 안개가 더해져 푸르게 물러난다
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < mats.Length; k++) mats[k] = far;
                    r.sharedMaterials = mats;
                }
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ---------------- 빈 방 ----------------

        static TeddyRoom BuildRoom(Transform root, Terrain terrain, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            var f = RoomForward;
            var rot = Quaternion.LookRotation(new Vector3(f.x, 0, f.y));
            var c = new Vector3(RoomCenter.x, 0, RoomCenter.y);
            c.y = terrain.SampleHeight(c);
            var go = new GameObject("EmptyRoom");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(c, rot);
            var room = go.AddComponent<TeddyRoom>();
            room.half = RoomHalf;
            room.terrain = terrain;

            Vector3 W(float lx, float lz)
            {
                var p = c + rot * new Vector3(lx, 0, lz);
                p.y = terrain.SampleHeight(p);
                return p;
            }

            // 벽: 세 면은 두 줄로 빽빽하게 (안쪽 줄은 정확한 직선)
            GameObject Pine(Vector3 p, float scale)
            {
                var prefab = art != null ? ArtCatalog.Pick(art.pines, rng.Next(100)) : null;
                GameObject t = prefab != null ? Object.Instantiate(prefab, go.transform) : Object.Instantiate(FallbackPine(), go.transform);
                t.SetActive(true);
                t.name = "WallPine";
                t.transform.position = p - Vector3.up * 0.15f;
                t.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                t.transform.localScale = Vector3.one * scale;
                return t;
            }
            const float spacing = 1.85f;
            int count = Mathf.RoundToInt(RoomHalf * 2f / spacing) + 1;
            for (int side = 0; side < 3; side++)
            for (int row = 0; row < 2; row++)
            for (int i = 0; i < count + row; i++)
            {
                float t = -RoomHalf + i * spacing - row * spacing * 0.5f;
                float off = RoomHalf + 0.4f + row * 1.7f;
                Vector3 p = side switch
                {
                    0 => W(-off, t), // 왼쪽
                    1 => W(off, t),  // 오른쪽
                    _ => W(t, off),  // 안쪽 끝
                };
                Pine(p, 1.0f + (float)rng.NextDouble() * 0.25f);
            }
            // 네 번째 벽: 열린 면. 땅속에 묻어 두었다가 솟는다
            for (int i = 0; i < count; i++)
            {
                var p = W(-RoomHalf + i * spacing, -RoomHalf - 0.4f);
                var t = Pine(p, 1.05f + (float)rng.NextDouble() * 0.2f);
                foreach (var col in t.GetComponentsInChildren<Collider>()) col.enabled = false;
                var obstacle = t.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = 0.5f;
                obstacle.height = 6f;
                obstacle.center = new Vector3(0, 3f, 0);
                obstacle.carving = true;
                obstacle.enabled = false;
                room.AddWallTree(t.transform, p - Vector3.up * 0.15f);
            }

            // 의자: 가운데, 들어오는 사람을 본다
            var chairPos = W(0, 0.6f);
            GameObject chair;
            if (art != null && art.siteChair != null)
            {
                chair = ArtCatalog.PlaceFitted(art.siteChair, go.transform, chairPos, rot * Quaternion.Euler(0, 180f, 0), new Vector3(0.7f, 1.0f, 0.7f), out _);
            }
            else
            {
                chair = new GameObject("Chair");
                chair.transform.SetParent(go.transform, false);
                chair.transform.SetPositionAndRotation(chairPos, rot * Quaternion.Euler(0, 180f, 0));
                var wood = new Color(0.45f, 0.32f, 0.2f);
                Graybox.Box(chair.transform, "Seat", new Vector3(0, 0.46f, 0), new Vector3(0.5f, 0.06f, 0.5f), wood, false);
                Graybox.Box(chair.transform, "Back", new Vector3(0, 0.75f, 0.22f), new Vector3(0.5f, 0.55f, 0.05f), wood, false);
                foreach (var (x, z) in new[] { (-0.22f, -0.22f), (0.22f, -0.22f), (-0.22f, 0.22f), (0.22f, 0.22f) })
                    Graybox.Box(chair.transform, "Leg", new Vector3(x, 0.22f, z), new Vector3(0.05f, 0.44f, 0.05f), wood, false);
            }
            chair.name = "Chair";
            room.chair = chair.transform;
            foreach (var col in chair.GetComponentsInChildren<Collider>()) col.enabled = false;

            // 의자 위 곰인형 (목표)
            var prize = Plush(go.transform, art != null ? ArtCatalog.Pick(art.plushies, 0) : null, chairPos + Vector3.up * 0.48f, rot * Quaternion.Euler(0, 180f, 0), 0.42f, false);
            prize.name = "PrizeBear";
            room.prize = prize;

            // 벽 밑 곰인형 여섯: 나무에 기대어 방 가운데를 본다
            var spots = new[] { (-RoomHalf + 0.9f, -3.5f), (-RoomHalf + 0.9f, 4.5f), (-2.5f, RoomHalf - 0.9f), (3.2f, RoomHalf - 0.9f), (RoomHalf - 0.9f, 5.5f), (RoomHalf - 0.9f, -2.0f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = W(spots[i].Item1, spots[i].Item2);
                var face = Quaternion.LookRotation(new Vector3(chairPos.x - p.x, 0, chairPos.z - p.z));
                int pick = art != null && art.plushies != null && art.plushies.Length > 1 ? 1 + (i % 3 == 2 ? 1 + i % 2 : 0) : 0;
                var bear = Plush(go.transform, art != null ? ArtCatalog.Pick(art.plushies, pick) : null, p, face, 0.72f + (float)rng.NextDouble() * 0.12f, true);
                room.AddBear(bear);
            }
            return room;
        }

        /// <summary>봉제 인형 하나. height 높이로 맞추고, 맞을 수 있으면 콜라이더·체력을 단다.</summary>
        static GameObject Plush(Transform parent, GameObject prefab, Vector3 pos, Quaternion rot, float height, bool shootable)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent);
                foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = Vector3.one;
                var b = ArtCatalog.RendererBounds(go);
                go.transform.localScale = Vector3.one * (height / Mathf.Max(0.05f, b.size.y));
                b = ArtCatalog.RendererBounds(go);
                go.transform.position += Vector3.up * (pos.y - b.min.y);
            }
            else
            {
                go = new GameObject("Bear");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);
                var fur = new Color(0.45f, 0.32f, 0.22f);
                Graybox.Prim(PrimitiveType.Sphere, go.transform, "Body", new Vector3(0, height * 0.32f, 0), new Vector3(height * 0.55f, height * 0.6f, height * 0.5f), fur, false);
                Graybox.Prim(PrimitiveType.Sphere, go.transform, "Head", new Vector3(0, height * 0.78f, 0.02f), Vector3.one * height * 0.42f, fur, false);
                Graybox.Prim(PrimitiveType.Sphere, go.transform, "EarL", new Vector3(-height * 0.16f, height * 0.97f, 0), Vector3.one * height * 0.14f, fur, false);
                Graybox.Prim(PrimitiveType.Sphere, go.transform, "EarR", new Vector3(height * 0.16f, height * 0.97f, 0), Vector3.one * height * 0.14f, fur, false);
            }
            go.name = "Bear";
            if (shootable)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0, height * 0.5f / go.transform.localScale.y, 0);
                box.size = new Vector3(height * 0.7f, height, height * 0.6f) / go.transform.localScale.y;
                var hp = go.AddComponent<Health>();
                hp.Init(40f);
                var zone = go.AddComponent<HitZone>();
                zone.owner = hp;
            }
            return go;
        }

        // ---------------- 스폰·지도 ----------------

        static void CollectSpawnPoints(MissionLayout layout, System.Random rng)
        {
            var room = new Vector3(RoomCenter.x, 0, RoomCenter.y);
            for (int tries = 0; tries < 600 && layout.spawnPoints.Count < 36; tries++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 45f + (float)rng.NextDouble() * 150f;
                var p = room + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * dist;
                if (Vector2.Distance(new Vector2(p.x, p.z), CarStart) < 90f) continue;
                if (ForestMask(p.x, p.z) < 0.4f) continue;
                p.y = layout.terrain.SampleHeight(p);
                if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) layout.spawnPoints.Add(hit.position);
            }
            for (int tries = 0; tries < 200 && layout.bruteSpawnPoints.Count < 4; tries++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                var p = room + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (85f + (float)rng.NextDouble() * 50f);
                p.y = layout.terrain.SampleHeight(p);
                if (NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas)) layout.bruteSpawnPoints.Add(hit.position);
            }
            if (layout.bruteSpawnPoints.Count == 0 && layout.spawnPoints.Count > 0) layout.bruteSpawnPoints.Add(layout.spawnPoints[0]);
        }

        static Texture2D BuildMinimap(float[,] h, float[,] roadDist, float[,] trailDist)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float wx = (x + 0.5f) / n * Size, wz = (y + 0.5f) / n * Size;
                float hgt = Sample(h, wx, wz);
                float shade = Mathf.Clamp((hgt - Sample(h, wx + 6f, wz)) * 0.06f + (hgt - Sample(h, wx, wz + 6f)) * 0.06f, -0.25f, 0.25f);
                var c = Color.Lerp(new Color(0.6f, 0.63f, 0.43f), new Color(0.3f, 0.4f, 0.26f), ForestMask(wx, wz));
                c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.56f), Mathf.InverseLerp(60f, 110f, hgt));
                c *= 1f + shade;
                if (Sample(roadDist, wx, wz) < 3.5f) c = new Color(0.74f, 0.62f, 0.44f);
                else if (Sample(trailDist, wx, wz) < 1.8f) c = new Color(0.66f, 0.56f, 0.4f);
                if (InRoom(wx, wz, 0.5f)) c = new Color(0.55f, 0.42f, 0.3f);
                c.a = 1f;
                px[y * n + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
