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
        public const float RoomHalf = 7.5f;

        // 고정 지리
        public static readonly Vector2 CarStart = new Vector2(238, 220); // 흙길 위 (가장자리 산비탈을 피해 초원 안쪽)
        static readonly Vector2[] Road =
        {
            // 차 앞(Road[1]→[2])은 그대로, 그 너머 40m에서 오른쪽으로 27° 꺾였다가 왼쪽으로 크게 휘어 숲길 입구로 (목표 샷 01의 S자 흙길:
            // 예전 점들은 휨이 130m 너머라 01 구도에서 곧은 길로 읽혔다)
            new Vector2(214, 196), new Vector2(262, 240), new Vector2(305, 262), new Vector2(348, 264), new Vector2(382, 290), new Vector2(397, 340), new Vector2(400, 396),
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
            NatureKit.Footprints.Clear();
            var laps = new System.Text.StringBuilder();
            long lastLap = 0;
            void Lap(string name) { laps.Append($" {name} {watch.ElapsedMilliseconds - lastLap}"); lastLap = watch.ElapsedMilliseconds; }

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
            Lap("높이");
            Paint(td, h, roadDist, trailDist);
            Lap("칠하기");
            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "Terrain";
            terrainGo.transform.SetParent(root, false);
            var terrain = terrainGo.GetComponent<Terrain>();
            var terrainMat = Resources.Load<Material>("Materials/Terrain");
            if (terrainMat != null)
            {
                terrainMat.EnableKeyword("_NORMALMAP"); // 질감 법선 맵 (NatureKit.Layer)
                terrain.materialTemplate = terrainMat;
            }
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
            PlantTrees(td, h, roadDist, trailDist, rng); Lap("PlantTrees");
            ScatterRocks(root, terrain, roadDist, trailDist, rng); Lap("ScatterRocks");
            PlaceMountains(root, rng);
            PlaceClouds(root, rng);
            PlaceRoadside(root, terrain, roadPts, rng); Lap("PlaceRoadside");
            PlaceLeaningForest(root, terrain, rng); Lap("PlaceLeaningForest");
            PlaceFloatingHouse(root, terrain, rng);
            terrain.Flush();
            // 나무는 GPU 인스턴싱으로 그린다 (지형이 한 그루씩 그리면 프레임 절반). 에디터 굽기는 지형 나무 자리를 직접 읽는다
            if (Application.isPlaying) TreeField.FromTerrain(terrain);

            // 6) 빈 방 (목표)
            room = BuildRoom(root, terrain, rng);
            Lap("방");
            PlaceMeadowAccents(root, terrain, roadPts, roadDist, seed); Lap("PlaceMeadowAccents");
            ScatterClutter(root, terrain, roadDist, trailDist, seed); Lap("ScatterClutter"); // 따로 된 rng라 다른 배치는 그대로. 통나무 자리를 풀이 피하게 풀보다 먼저
            ScatterGrass(root, h, roadDist, trailDist, rng); Lap("ScatterGrass"); // 바위를 다 놓은 뒤 (바위 속에 풀이 나지 않게)

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
            Debug.Log($"[Manyworld] 숲 현장 생성: {watch.ElapsedMilliseconds}ms, 나무 {td.treeInstanceCount}, 스폰 {layout.spawnPoints.Count}+{layout.bruteSpawnPoints.Count} |{laps} 나머지 {watch.ElapsedMilliseconds - lastLap}");
            return layout;
        }

        // ---------------- 지형 ----------------

        static float BaseHeight(float x, float z)
        {
            float hills = WorldTerrainBuilder.Fbm(x * 0.004f + 3.1f, z * 0.004f + 8.7f, 4) * 24f;
            float ne = Mathf.Clamp01((x + z - 700f) / 500f) * 9f; // 북동쪽 숲이 조금 높다
            float detail = (Mathf.PerlinNoise(x * 0.03f + 1f, z * 0.03f + 5f) - 0.5f) * 1.4f;
            float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            float rimT = Mathf.Clamp01(1f - edge / 240f); // 넓고 완만하게: 가파르면 바위색으로 칠해져 산이 회색이 된다
            float rim = Mathf.Pow(rimT, 1.6f) * 95f + WorldTerrainBuilder.Fbm(x * 0.008f + 40f, z * 0.008f + 11f, 3) * 40f * rimT;
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
            m -= Mathf.InverseLerp(30f, 14f, LeaningBandDistance(x, z)) * 0.75f; // 기운 숲은 볕 드는 성긴 숲
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
                NatureKit.Layer(NatureKit.Ground.Meadow, new Color(0.54f, 0.5f, 0.3f), 5f, 1),         // 0 초원 (햇빛 받은 올리브, 목표 샷 01은 황토 쪽 134,119,71)
                NatureKit.Layer(NatureKit.Ground.ForestFloor, new Color(0.17f, 0.17f, 0.1f), 4f, 2),  // 1 숲 바닥 (먼 산비탈 나무 사이가 밝게 비치지 않게 어둡게)
                NatureKit.Layer(NatureKit.Ground.Dirt, new Color(0.74f, 0.58f, 0.42f), 4f, 3),        // 2 흙길·방 바닥 (밝은 황토, 자갈)
                NatureKit.Layer(NatureKit.Ground.Rock, new Color(0.42f, 0.43f, 0.4f), 8f, 4),         // 3 바위
                NatureKit.Layer(NatureKit.Ground.Dry, new Color(0.55f, 0.51f, 0.33f), 6f, 5),         // 4 마른 풀 (0.62,0.57,0.34는 01 오른쪽 언덕이 노랗게 떴다)
                NatureKit.Layer(NatureKit.Ground.Lush, new Color(0.42f, 0.46f, 0.26f), 5f, 6),        // 5 짙은 풀 얼룩
                NatureKit.Layer(NatureKit.Ground.Rut, new Color(0.6f, 0.46f, 0.33f), 4f, 7),          // 6 바퀴 자국·길 가장자리 (짙은 흙)
            };
            // 노이즈 마스크는 지형 격자(1.5m)에서 한 번만 계산하고 칠할 땐 보간한다 (생성 시간)
            var forestG = new float[Res, Res];
            var dryG = new float[Res, Res];
            var lushG = new float[Res, Res];
            var slopeG = new float[Res, Res];
            for (int gz = 0; gz < Res; gz++)
            for (int gx = 0; gx < Res; gx++)
            {
                float wx = gx * Cell, wz = gz * Cell;
                forestG[gz, gx] = ForestMask(wx, wz);
                dryG[gz, gx] = WorldTerrainBuilder.Fbm(wx * 0.012f + 31f, wz * 0.012f + 17f, 3);
                lushG[gz, gx] = WorldTerrainBuilder.Fbm(wx * 0.02f + 71f, wz * 0.02f + 3f, 3);
                int xa = Mathf.Max(0, gx - 1), xb = Mathf.Min(Res - 1, gx + 1), za = Mathf.Max(0, gz - 1), zb = Mathf.Min(Res - 1, gz + 1);
                float dx = (h[gz, xb] - h[gz, xa]) / ((xb - xa) * Cell), dz = (h[zb, gx] - h[za, gx]) / ((zb - za) * Cell);
                slopeG[gz, gx] = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
            }
            int ar = 768; // 1m. 바퀴 자국이 보이게
            td.alphamapResolution = ar;
            var a = new float[ar, ar, 7];
            for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
            {
                float wx = x / (float)(ar - 1) * Size, wz = z / (float)(ar - 1) * Size;
                float slope = Sample(slopeG, wx, wz);
                float rd = Sample(roadDist, wx, wz), td2 = Sample(trailDist, wx, wz);
                float jitter = (Mathf.PerlinNoise(wx * 0.35f, wz * 0.35f) - 0.5f) * 0.3f;
                float road = Mathf.Clamp01(Mathf.InverseLerp(5f, 3.9f, rd) + (rd < 5.6f ? jitter * 0.6f : 0f));
                float trail = Mathf.Clamp01(Mathf.InverseLerp(2.4f, 1.2f, td2) + (td2 < 3f ? jitter : 0f)) * 0.9f;
                float floor = 0f, roomEdge = 0f;
                if (InRoom(wx, wz, 0.6f))
                {
                    var l = RoomLocal(wx, wz);
                    float edge = Mathf.Max(Mathf.Abs(l.x), Mathf.Abs(l.y));
                    floor = Mathf.Clamp01(Mathf.InverseLerp(RoomHalf + 0.6f, RoomHalf - 0.8f, edge) + jitter * 0.5f);
                    roomEdge = Mathf.InverseLerp(RoomHalf - 3f, RoomHalf - 0.3f, edge) * floor; // 목표 샷 03: 나무 밑 가장자리는 솔잎 깔린 짙은 흙
                }
                // 기운 숲 바닥: 모래흙이 드러난 자리와 마른 풀 (목표 샷 02)
                float band = Mathf.InverseLerp(30f, 14f, LeaningBandDistance(wx, wz));
                float sandy = band * Mathf.InverseLerp(0.5f, 0.68f, Mathf.PerlinNoise(wx * 0.09f + 13f, wz * 0.09f + 41f)) * 0.5f;
                float dirt = Mathf.Max(Mathf.Max(road, sandy), Mathf.Max(trail, floor));
                float rock = Mathf.InverseLerp(50f, 64f, slope) * (1f - dirt); // 산비탈도 대부분 숲 바닥
                float forest = Sample(forestG, wx, wz);
                float dry = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 0.7f, Sample(dryG, wx, wz)) + Mathf.InverseLerp(6.5f, 3.6f, rd) * 0.7f + band * 0.45f);
                float rest = Mathf.Max(0f, 1f - dirt - rock);
                // 바퀴 자국 두 줄과 닳은 길 가장자리
                float rut = road * (Mathf.Exp(-(rd - 1.35f) * (rd - 1.35f) * 4f) * 0.6f + Mathf.InverseLerp(3.1f, 4.6f, rd) * 0.45f);
                a[z, x, 6] = rut + floor * 0.55f - roomEdge * 0.55f; // 가장자리 몫은 숲 바닥(1)으로 옮긴다 (합 1 유지) // 방 바닥: 주황빛 짙은 흙 + 솔잎 (목표 샷 03)
                a[z, x, 2] = dirt - floor * 0.8f - rut;
                a[z, x, 3] = rock;
                a[z, x, 1] = rest * Mathf.InverseLerp(0.35f, 0.75f, forest) + floor * 0.25f + roomEdge * 0.55f;
                float open = rest - a[z, x, 1];
                a[z, x, 4] = open * dry;
                float lush = Mathf.InverseLerp(0.45f, 0.65f, Sample(lushG, wx, wz));
                a[z, x, 5] = open * (1f - dry) * lush;
                a[z, x, 0] = open * (1f - dry) * (1f - lush);
            }
            td.SetAlphamaps(0, 0, a);

            // 풀 컬러맵: 칠한 지형 색을 위에서 내려다본 텍스처 (풀 밑동이 바로 아래 땅 색을 따른다, Stylized Grass Shader)
            var layerColors = new[] { new Color(0.54f, 0.5f, 0.3f), new Color(0.17f, 0.17f, 0.1f), new Color(0.74f, 0.58f, 0.42f), new Color(0.42f, 0.43f, 0.4f), new Color(0.55f, 0.51f, 0.33f), new Color(0.42f, 0.46f, 0.26f), new Color(0.6f, 0.46f, 0.33f) };
            const int cm = 512;
            var cpx = new Color[cm * cm];
            for (int z = 0; z < cm; z++)
            for (int x = 0; x < cm; x++)
            {
                int az = z * (ar - 1) / (cm - 1), ax = x * (ar - 1) / (cm - 1);
                var c = Color.black;
                for (int k = 0; k < layerColors.Length; k++) c += layerColors[k] * a[az, ax, k];
                c.a = 1f;
                cpx[z * cm + x] = c;
            }
            var colorMap = new Texture2D(cm, cm, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "ForestColorMap" };
            colorMap.SetPixels(cpx);
            colorMap.Apply(true);
            Shader.SetGlobalTexture("_ColorMap", colorMap);
            Shader.SetGlobalVector("_ColorMapUV", new Vector4(0f, 0f, 1f / Size, 0f));
            Shader.SetGlobalVector("_ColorMapParams", new Vector4(1f, 0f, 0f, 0f));
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
            int meadowEnd = protos.Count;
            if (hasArt && art.pinesDark != null) foreach (var t in art.pinesDark) if (t != null) protos.Add(new TreePrototype { prefab = t });
            int darkCount = protos.Count - meadowEnd;
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
                if (InLeaningBand(px, pz, 2f)) continue; // 기운 숲은 따로 심는다
                var p2 = new Vector2(px, pz);
                if (Vector2.Distance(p2, CarStart) < 16f || Vector2.Distance(p2, Trailhead) < 17f) continue;
                float forest = ForestMask(px, pz);
                float hgt = Sample(h, px, pz);
                bool meadow = forest < 0.3f;
                float edgeDist = Mathf.Min(Mathf.Min(px, Size - px), Mathf.Min(pz, Size - pz));
                bool rim = edgeDist < 200f;
                double chance = meadow ? 0.026 : rim ? 0.97 : 0.25 + forest * 0.65; // 가장자리 산은 빽빽한 숲
                if (rng.NextDouble() > chance) continue;
                int proto = meadow && meadowEnd > pineCount && rng.NextDouble() < 0.65
                    ? pineCount + rng.Next(meadowEnd - pineCount)
                    : rng.Next(pineCount);
                // 멀리 보이는 가장자리 산은 짙은 소나무로: 햇빛 받은 잎이 희끗하게 뜨지 않게
                if (!meadow && darkCount > 0 && Vector2.Distance(new Vector2(px, pz), new Vector2(Size / 2, Size / 2)) > 250f)
                    proto = meadowEnd + rng.Next(darkCount);
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

        /// <summary>풀: 초원은 빽빽하게, 숲 속은 성기게, 길·숲길·방 바닥은 비운다. GrassField가 인스턴싱으로 그린다.</summary>
        static void ScatterGrass(Transform root, float[,] h, float[,] roadDist, float[,] trailDist, System.Random rng)
        {
            // 풀 종류: Stylized Grass Shader가 있으면 그 풀, 없으면 직접 만든 덤불
            var kinds = GrassField.Kinds();
            float total = 0f;
            foreach (var k in kinds) total += k.weight;
            var lists = new List<Matrix4x4>[kinds.Count];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<Matrix4x4>();
            const float step = 1.25f, lo = 135f, hi = Size - 135f;
            for (float z = lo; z < hi; z += step)
            for (float x = lo; x < hi; x += step)
            {
                float px = x + (float)(rng.NextDouble() - 0.5) * step, pz = z + (float)(rng.NextDouble() - 0.5) * step;
                float rd = Sample(roadDist, px, pz);
                // 방 안은 비우되 벽 밑 1.5m에는 풀이 붙는다
                if (rd < 4.6f || Sample(trailDist, px, pz) < 2f || InRoom(px, pz, -1.5f) || NatureKit.UnderRock(px, pz)) continue;
                float forest = ForestMask(px, pz);
                // 풀은 고르게 심긴 밭이 아니라 무더기로 뭉친다. 길가 어깨는 빽빽하고 키가 크다
                float big = Mathf.PerlinNoise(px * 0.04f + 3f, pz * 0.04f + 7f);
                float small = Mathf.PerlinNoise(px * 0.22f + 11f, pz * 0.22f + 5f);
                float verge = Mathf.InverseLerp(11f, 5f, rd);
                // 기운 숲 숲길 어깨도 풀이 빽빽하다 (목표 샷 02: 길 양옆 낮은 풀과 바위)
                // (방 둘레는 빼고: 03 방 안쪽까지 덤불이 번졌다)
                float awayFromRoom = Mathf.InverseLerp(RoomHalf + 4f, RoomHalf + 10f, Vector2.Distance(new Vector2(px, pz), RoomCenter));
                verge = Mathf.Max(verge, Mathf.InverseLerp(9f, 3f, Sample(trailDist, px, pz)) * Mathf.InverseLerp(30f, 14f, LeaningBandDistance(px, pz)) * 0.8f * awayFromRoom);
                float clumped = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.6f, big)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.62f, small));
                float chance = Mathf.Lerp(0.95f, 0.1f, forest) * Mathf.Clamp01(clumped * 1.5f + verge * 0.7f);
                if (rng.NextDouble() > chance) continue;
                int count = 1 + (rng.NextDouble() < 0.55 ? 1 : 0) + (rng.NextDouble() < 0.3 ? 1 : 0);
                for (int c = 0; c < count; c++)
                {
                    double pick = rng.NextDouble() * total;
                    int kind = 0;
                    for (; kind < kinds.Count - 1; kind++) { pick -= kinds[kind].weight; if (pick <= 0) break; }
                    float qx = px + (float)(rng.NextDouble() - 0.5) * 0.9f, qz = pz + (float)(rng.NextDouble() - 0.5) * 0.9f;
                    float y = Sample(h, qx, qz);
                    float s = kinds[kind].scale * (0.65f + (float)rng.NextDouble() * 0.75f) * (1f + verge * 0.15f) * (rng.NextDouble() < 0.06 ? 1.6f : 1f); // 가끔 키 큰 덤불. 목표 샷 01의 길가는 낮은 풀 (예전 ×1.5는 길을 가렸다)
                    lists[kind].Add(Matrix4x4.TRS(new Vector3(qx, y - 0.05f, qz), Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * s));
                }
            }
            var field = root.gameObject.AddComponent<GrassField>();
            for (int i = 0; i < kinds.Count; i++) field.Add(kinds[i].mesh, kinds[i].mat, lists[i]);
            Debug.Log($"[Manyworld] 풀 {field.InstanceCount}포기");
        }

        /// <summary>
        /// 땅 위 잔것: 잔가지·낙엽·자갈 무더기·고사리·버섯·그루터기·쓰러진 통나무 (목표 샷 02·03의 숲 바닥).
        /// 방 둘레·숲길 가·기운 숲·주차 공터 주변에만 (보이는 곳). 다른 배치와 rng를 나누지 않는다 (고정 세계가 그대로).
        /// 작은 것은 그림자를 끄고 정적 배칭으로 묶는다.
        /// </summary>
        static void ScatterClutter(Transform root, Terrain terrain, float[,] roadDist, float[,] trailDist, int seed)
        {
            var art = ArtCatalog.Instance;
            if (art == null || art.clutterSmall == null || art.clutterSmall.Length == 0) return;
            var rng = new System.Random(seed * 131 + 5);
            var group = new GameObject("GroundClutter").transform;
            group.SetParent(root, false);
            int count = 0;
            GameObject Put(GameObject prefab, float x, float z, float scale, bool shadows)
            {
                if (prefab == null) return null;
                var p = new Vector3(x, 0, z);
                p.y = terrain.SampleHeight(p) - 0.02f;
                var n = terrain.terrainData.GetInterpolatedNormal(x / Size, z / Size);
                var go = Object.Instantiate(prefab, p, Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), group);
                go.transform.localScale *= scale;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Kill(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                count++;
                return go;
            }
            GameObject PickOf(GameObject[] a) => a != null && a.Length > 0 ? a[rng.Next(a.Length)] : null;
            bool Blocked(float x, float z) => Sample(roadDist, x, z) < 4.2f || Sample(trailDist, x, z) < 1.1f || NatureKit.UnderRock(x, z);

            // 0) 방 입구 양옆: 쓰러진 통나무와 고사리 무리 (목표 샷 03의 앞쪽 전경). 입구 길(숲길)은 비운다
            var across = new Vector2(RoomForward.y, -RoomForward.x);
            foreach (float side in new[] { -1f, 1f })
            {
                var logAt = RoomEntrance - RoomForward * (1.5f + (float)rng.NextDouble() * 2f) + across * side * (4.2f + (float)rng.NextDouble() * 1.5f);
                var log = Put(art.clutterLogs != null && art.clutterLogs.Length > 0 ? art.clutterLogs[rng.Next(Mathf.Min(2, art.clutterLogs.Length))] : null, logAt.x, logAt.y, 1.1f + (float)rng.NextDouble() * 0.3f, true);
                if (log != null)
                {
                    // 방 앞면과 나란히 눕힌다 (입구를 가로막지 않게)
                    log.transform.rotation = Quaternion.LookRotation(new Vector3(across.x, 0, across.y)) * Quaternion.Euler(0, 90f + (float)(rng.NextDouble() - 0.5) * 30f, 0);
                    NatureKit.Footprints.Add(logAt.x, logAt.y, 1.5f);
                }
                for (int k = 0; k < 7; k++)
                {
                    var f = RoomEntrance - RoomForward * ((float)rng.NextDouble() * 5f - 1f) + across * side * (2.6f + (float)rng.NextDouble() * 4f);
                    if (InRoom(f.x, f.y, 0.3f) || Sample(trailDist, f.x, f.y) < 1.4f) continue;
                    Put(PickOf(k < 5 ? art.clutterFerns : art.clutterPebbles), f.x, f.y, 0.8f + (float)rng.NextDouble() * 0.6f, k < 5);
                }
            }

            // 1) 방: 벽(나무) 밑 띠에 낙엽·잔가지가 쌓이고 가운데는 드문드문 (목표 샷 03)
            for (int i = 0; i < 260; i++)
            {
                float lx = ((float)rng.NextDouble() * 2f - 1f) * (RoomHalf + 1.2f), lz = ((float)rng.NextDouble() * 2f - 1f) * (RoomHalf + 1.2f);
                float edge = Mathf.Max(Mathf.Abs(lx), Mathf.Abs(lz));
                if (edge < RoomHalf - 2.5f && rng.NextDouble() > 0.18) continue;
                var w = RoomCenter + RoomForward * lz + new Vector2(RoomForward.y, -RoomForward.x) * lx;
                if (Vector2.Distance(w, RoomEntrance) < 2.2f || NatureKit.UnderRock(w.x, w.y)) continue;
                Put(PickOf(edge > RoomHalf - 1.2f && rng.NextDouble() < 0.4 ? art.clutterLeaves : art.clutterSmall), w.x, w.y, 0.6f + (float)rng.NextDouble() * 0.4f, false);
            }

            // 2) 숲길·기운 숲·방 둘레의 숲 바닥, 주차 공터 길가
            for (int i = 0; i < 9000 && count < 1600; i++)
            {
                float x = 150f + (float)rng.NextDouble() * (Size - 300f), z = 150f + (float)rng.NextDouble() * (Size - 300f);
                var p2 = new Vector2(x, z);
                float td = Sample(trailDist, x, z), rd = Sample(roadDist, x, z);
                float room = Vector2.Distance(p2, RoomCenter);
                bool nearTrail = td < 9f, lean = LeaningBandDistance(x, z) < 26f, nearRoom = room < 32f, nearCar = Vector2.Distance(p2, CarStart) < 40f && rd < 12f;
                if (!(nearTrail || lean || nearRoom || nearCar) || InRoom(x, z, 0.5f) || Blocked(x, z)) continue;
                float forest = ForestMask(x, z);
                double r = rng.NextDouble();
                if (nearCar && forest < 0.3f)
                {
                    // 초원 길가: 자갈 무더기와 잔가지만
                    if (r < 0.5) Put(PickOf(art.clutterPebbles), x, z, 0.8f + (float)rng.NextDouble() * 0.6f, false);
                    continue;
                }
                if (r < 0.42) Put(PickOf(art.clutterSmall), x, z, 0.8f + (float)rng.NextDouble() * 0.6f, false);
                else if (r < 0.62) Put(PickOf(art.clutterLeaves), x, z, 0.6f + (float)rng.NextDouble() * 0.4f, false);
                else if (r < 0.86 && td > 2f) Put(PickOf(art.clutterFerns), x, z, 0.7f + (float)rng.NextDouble() * 0.7f, true);
                else if (r < 0.93) Put(PickOf(art.clutterPebbles), x, z, 0.8f + (float)rng.NextDouble() * 0.6f, false);
                else if (r < 0.965 && td > 2.5f) Put(PickOf(art.clutterMushrooms), x, z, 0.8f + (float)rng.NextDouble() * 0.5f, false);
                else if (td > 3.5f && room > RoomHalf + 3f)
                {
                    // 그루터기·쓰러진 통나무: 드물게, 그림자를 드리운다
                    var big = Put(PickOf(art.clutterLogs), x, z, 0.9f + (float)rng.NextDouble() * 0.4f, true);
                    if (big != null) NatureKit.Footprints.Add(x, z, 1.2f);
                }
            }
            if (Application.isPlaying) StaticBatchingUtility.Combine(group.gameObject); // 에디터 굽기(APV)는 정적 배칭을 받지 않는다
            Debug.Log($"[Manyworld] 땅 잔것 {count}개");
        }

        /// <summary>
        /// 목표 샷 01의 초원: 출발점에서 길 왼쪽 앞 30~170m에 둥근 활엽수가 서너 그루씩 서고, 회색 바위가 무리 지어 박혀 있다.
        /// 지형 나무(드문 확률)만으로는 그 구도에 나무가 하나밖에 없었다. 따로 된 rng라 다른 배치는 그대로.
        /// </summary>
        static void PlaceMeadowAccents(Transform root, Terrain terrain, List<Vector2> road, float[,] roadDist, int seed)
        {
            var art = ArtCatalog.Instance;
            var rng = new System.Random(seed * 977 + 31);
            Vector3 G(Vector2 p) => new Vector3(p.x, terrain.SampleHeight(new Vector3(p.x, 0, p.y)), p.y);
            var dir = (Road[2] - Road[1]).normalized;
            var left = new Vector2(-dir.y, dir.x);
            bool Free(Vector2 p) => Sample(roadDist, p.x, p.y) > 9f && ForestMask(p.x, p.y) < 0.35f && !NatureKit.UnderRock(p.x, p.y);
            int trees = 0;
            for (int i = 0; i < 40 && trees < 11; i++)
            {
                // 길이 앞에서 왼쪽으로 휘므로 멀리 갈수록 더 왼쪽으로 (길 위 먼 곳에 몰리지 않게)
                float ahead = 25f + (float)rng.NextDouble() * 85f;
                var p = CarStart + dir * ahead + left * (22f + ahead * 0.35f + (float)rng.NextDouble() * 55f);
                if (!Free(p) || Sample(roadDist, p.x, p.y) < 18f || art == null || art.meadowTrees == null || art.meadowTrees.Length == 0) continue;
                var prefab = ArtCatalog.Pick(art.meadowTrees, rng.Next(100));
                if (prefab == null) continue;
                var t = Object.Instantiate(prefab, root);
                t.name = "MeadowTree";
                t.transform.SetPositionAndRotation(G(p) - Vector3.up * 0.2f, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
                t.transform.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.5f);
                NatureKit.Footprints.Add(p.x, p.y, 2.5f);
                trees++;
            }
            for (int g = 0; g < 9; g++)
            {
                var c = CarStart + dir * (20f + (float)rng.NextDouble() * 120f) + left * (12f + (float)rng.NextDouble() * 50f);
                if (!Free(c)) continue;
                int n = 2 + rng.Next(3);
                for (int k = 0; k < n; k++)
                {
                    var p = c + new Vector2((float)(rng.NextDouble() - 0.5) * 5f, (float)(rng.NextDouble() - 0.5) * 5f);
                    NatureKit.Boulder(root, G(p), (k == 0 ? 0.9f : 0.4f) + (float)rng.NextDouble() * 0.8f, rng, k == 0, "MeadowRock");
                }
            }
        }

        static void PlantGrass(TerrainData td, float[,] roadDist, float[,] trailDist)
        {
            var art = ArtCatalog.Instance;
            var source = art != null && art.fieldGrass != null && art.fieldGrass.Length > 0 ? art.fieldGrass : art?.grass;
            if (source == null || source.Length == 0 || source[0] == null) return;
            var protos = new List<DetailPrototype>();
            foreach (var g in source)
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
                    float thr = Mathf.Lerp(0.34f, 0.6f, forest); // 숲 속은 성기게
                    if (n > thr) map[z, x] = n > thr + 0.2f ? 7 : n > thr + 0.1f ? 5 : 3;
                }
                td.SetDetailLayer(0, 0, layer, map);
            }
        }

        static void ScatterRocks(Transform root, Terrain terrain, float[,] roadDist, float[,] trailDist, System.Random rng)
        {
            // 각진 회색 바위 (목표 샷 01·03). 길 어깨와 숲 가장자리에 몰린다
            int placed = 0;
            for (int tries = 0; tries < 1400 && placed < 190; tries++)
            {
                float x = 150f + (float)rng.NextDouble() * (Size - 300f);
                float z = 150f + (float)rng.NextDouble() * (Size - 300f);
                float rd = Sample(roadDist, x, z);
                if (rd < 6f || Sample(trailDist, x, z) < 2.5f || InRoom(x, z, 3f)) continue;
                var p2 = new Vector2(x, z);
                if (Vector2.Distance(p2, CarStart) < 12f || Vector2.Distance(p2, Trailhead) < 15f) continue;
                float forest = ForestMask(x, z);
                bool shoulder = rd < 14f;
                bool edge = forest > 0.25f && forest < 0.6f;
                if (!shoulder && !edge && rng.NextDouble() > 0.25) continue;
                var p = new Vector3(x, 0, z);
                p.y = terrain.SampleHeight(p);
                float r = (float)rng.NextDouble();
                NatureKit.Boulder(root, p, 0.5f + r * r * 2f, rng, true);
                // 가끔 작은 돌이 곁에 붙는다
                if (rng.NextDouble() < 0.4)
                {
                    var q = p + new Vector3((float)(rng.NextDouble() - 0.5) * 3f, 0, (float)(rng.NextDouble() - 0.5) * 3f);
                    q.y = terrain.SampleHeight(q);
                    NatureKit.Boulder(root, q, 0.25f + (float)rng.NextDouble() * 0.4f, rng, false, "Pebble");
                }
                placed++;
            }

            // 잔돌: 초원과 숲길 가에 흩어진 무릎 아래 돌 (충돌 없음)
            int pebbles = 0;
            for (int tries = 0; tries < 6000 && pebbles < 420; tries++)
            {
                float x = 160f + (float)rng.NextDouble() * (Size - 320f);
                float z = 160f + (float)rng.NextDouble() * (Size - 320f);
                float rd = Sample(roadDist, x, z), td = Sample(trailDist, x, z);
                if (rd < 5.2f || td < 2.4f || InRoom(x, z, 2f)) continue;
                bool near = rd < 22f || (td < 9f && LeaningBandDistance(x, z) < 28f);
                if (!near && (ForestMask(x, z) > 0.4f || rng.NextDouble() > 0.35)) continue;
                var p = new Vector3(x, 0, z);
                p.y = terrain.SampleHeight(p);
                var peb = NatureKit.Boulder(root, p, 0.18f + (float)rng.NextDouble() * 0.5f, rng, false, "Pebble");
                peb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pebbles++;
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
            // 먼 산: 능선 띠 두 겹. 가까운 겹은 짙은 푸른색, 먼 겹은 하늘에 묻히는 옅은 색 (목표 샷 01)
            var backdrop = Resources.Load<Material>("Materials/Backdrop");
            if (backdrop != null)
            {
                // 목표 샷 01은 세 겹: 숲 덮인 가까운 언덕(푸른 초록) → 짙은 푸른 산 → 하늘에 묻히는 먼 봉우리.
                // (비탈 명암은 띠가 세로 기둥이라 커튼 줄무늬로 보여서 끈다: shade 0)
                RidgeRing(root, backdrop, 2050f, 560f, 1000f, new Color(0.46f, 0.53f, 0.65f), new Color(0.62f, 0.67f, 0.75f), 5);
                RidgeRing(root, backdrop, 1500f, 380f, 700f, new Color(0.3f, 0.37f, 0.5f), new Color(0.55f, 0.6f, 0.7f), 9);
                RidgeRing(root, backdrop, 1020f, 190f, 360f, new Color(0.17f, 0.25f, 0.24f), new Color(0.36f, 0.44f, 0.5f), 13);
                return;
            }
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                float ang = (i + (float)rng.NextDouble() * 0.6f) / n * Mathf.PI * 2f;
                float dist = 1150f + (float)rng.NextDouble() * 500f;
                var p = center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * dist;
                float height = 420f + (float)rng.NextDouble() * 320f;
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
                foreach (var c in go.GetComponentsInChildren<Collider>()) Kill(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// 분지를 둘러싼 능선 띠 한 겹. 뾰족한 봉우리 = 1-|sin|을 여러 주기로 겹친다 (한 바퀴에 이어지는 정수 주기).
        /// shade: 비탈이 해를 보는 정도로 밝기를 바꾸는 세기 (정점색). 세로 얼룩은 커튼처럼 보여서 뺐다.
        /// </summary>
        static void RidgeRing(Transform root, Material mat, float radius, float minH, float maxH, Color top, Color haze, int seed, float shade = 0f)
        {
            var rng = new System.Random(seed);
            int[] freqs = { 3, 5, 9, 17, 31 };
            float[] amps = { 1f, 0.6f, 0.4f, 0.22f, 0.12f };
            var phase = new float[freqs.Length];
            for (int k = 0; k < phase.Length; k++) phase[k] = (float)rng.NextDouble() * Mathf.PI * 2f;
            const int seg = 960;
            const int rows = 5;
            var center = new Vector3(Size / 2, 0, Size / 2);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            const float baseY = 40f;
            float Height(float a)
            {
                float n = 0f, norm = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float r = 1f - Mathf.Abs(Mathf.Sin(freqs[k] * a * 0.5f + phase[k]));
                    n += amps[k] * r * r;
                    norm += amps[k];
                }
                return n / norm;
            }
            var field = WorldLook.Field;
            var toSun = -(Quaternion.Euler(field.sunPitch, field.sunYaw, 0f) * Vector3.forward);
            float step = Mathf.PI * 2f / seg;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * step;
                float n = Height(a);
                float hgt = Mathf.Lerp(minH, maxH, Mathf.Clamp01(n * 1.6f - 0.1f));
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                // 비탈 법선: 분지 쪽(안쪽)을 보고, 능선이 오르는 쪽으로 기운다
                // 비탈은 넓게 (±8칸) 재서 잔 봉우리마다 명암이 뒤집혀 줄무늬가 지지 않게
                float h0 = Mathf.Lerp(minH, maxH, Mathf.Clamp01(Height(a - step * 8f) * 1.6f - 0.1f));
                float h1 = Mathf.Lerp(minH, maxH, Mathf.Clamp01(Height(a + step * 8f) * 1.6f - 0.1f));
                float slope = (h1 - h0) / (radius * step * 16f);
                var normal = (-dir * 0.8f - tangent * slope * 1.6f + Vector3.up * 0.45f).normalized;
                float lit = 1f + shade * (Vector3.Dot(normal, toSun) * 1.6f - 0.35f);
                var p = center + dir * radius;
                for (int r = 0; r < rows; r++)
                {
                    float v = r / (float)(rows - 1);
                    // 아래쪽은 안개에 묻히고 위로 갈수록 산 색. 꼭대기 가까이 해 그늘 차이가 가장 크다
                    float y = baseY + (hgt - baseY) * v;
                    var c = Color.Lerp(haze, Color.Lerp(top, haze, 0.15f * (1f - n)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.9f, v)));
                    float k = Mathf.Lerp(1f, lit, Mathf.InverseLerp(0.1f, 0.8f, v));
                    c = new Color(c.r * k, c.g * k, c.b * k);
                    verts.Add(p + Vector3.up * y);
                    cols.Add(c.linear); // 정점색은 감마 변환 없이 그대로 쓰여서 선형 색으로 넣는다
                }
                if (i > 0)
                {
                    int b0 = (i - 1) * rows, c0 = i * rows;
                    for (int r = 0; r < rows - 1; r++)
                        tris.AddRange(new[] { b0 + r, c0 + r, b0 + r + 1, b0 + r + 1, c0 + r, c0 + r + 1 });
                }
            }
            var mesh = new Mesh { name = "RidgeRing", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("FarMountain", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>뭉게구름: 분지 위와 먼 하늘에 낮게 (목표 샷 01).</summary>
        static void PlaceClouds(Transform root, System.Random rng)
        {
            // 부드러운 뭉게구름 돔 (셰이더). 없으면 메시 구름으로
            var cloudMat = Resources.Load<Material>("Materials/CloudDome");
            if (cloudMat != null)
            {
                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name = "CloudDome";
                Object.DestroyImmediate(dome.GetComponent<Collider>());
                dome.transform.SetParent(root, false);
                dome.transform.position = new Vector3(Size / 2, 0, Size / 2);
                dome.transform.localScale = Vector3.one * 4800f;
                var mat = new Material(cloudMat);
                var toSun = -(Quaternion.Euler(WorldLook.Field.sunPitch, WorldLook.Field.sunYaw, 0) * Vector3.forward);
                mat.SetVector("_SunDir", toSun);
                mat.SetFloat("_Cover", 0.46f);    // 뭉게구름이 하늘 절반쯤
                mat.SetFloat("_Softness", 0.07f); // 가장자리가 또렷하게
                mat.SetFloat("_Scale", 1.6f);
                var r = dome.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                return;
            }
            var art = ArtCatalog.Instance;
            if (art == null || art.clouds == null || art.clouds.Length == 0) return;
            var center = new Vector3(Size / 2, 0, Size / 2);
            for (int i = 0; i < 40; i++)
            {
                var prefab = ArtCatalog.Pick(art.clouds, rng.Next(100));
                if (prefab == null) continue;
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 350f + (float)rng.NextDouble() * 1300f;
                var p = center + new Vector3(Mathf.Cos(ang) * dist, 190f + (float)rng.NextDouble() * 150f, Mathf.Sin(ang) * dist);
                var go = Object.Instantiate(prefab, root);
                go.name = "Cloud";
                go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                var b = ArtCatalog.RendererBounds(go);
                float width = 170f + (float)rng.NextDouble() * 200f;
                go.transform.localScale = Vector3.one * (width / Mathf.Max(1f, Mathf.Max(b.size.x, b.size.z)));
                go.transform.position = p;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Kill(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }

        /// <summary>길가: 어깨에 늘어선 바위, 한쪽 나무 울타리, 키 큰 풀 덤불 (목표 샷 01).</summary>
        static void PlaceRoadside(Transform root, Terrain terrain, List<Vector2> road, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            Vector3 G(Vector2 p) => new Vector3(p.x, terrain.SampleHeight(new Vector3(p.x, 0, p.y)), p.y);

            // 출발점 오른쪽 전경: 둥근 바위 무더기와 키 큰 풀 (목표 샷 01의 앞쪽)
            var startDir = (Road[2] - Road[1]).normalized;
            var startRight = new Vector2(startDir.y, -startDir.x);
            for (int k = 0; k < 7; k++)
            {
                var p2 = CarStart + startDir * (-3f + k * 2.1f + (float)rng.NextDouble() * 1.2f) + startRight * (6.2f + (float)rng.NextDouble() * 3.5f);
                NatureKit.Boulder(root, G(p2), 0.8f + (float)rng.NextDouble() * (k % 3 == 0 ? 1.1f : 0.6f), rng, true, "RoadRock");
            }
            for (int k = 0; k < 4; k++)
            {
                var p2 = CarStart + startDir * (-2f + (float)rng.NextDouble() * 14f) + startRight * (5.4f + (float)rng.NextDouble() * 1.5f);
                NatureKit.Boulder(root, G(p2), 0.3f + (float)rng.NextDouble() * 0.35f, rng, false, "Pebble");
            }
            for (int k = 0; k < 90; k++)
            {
                var p2 = CarStart + startDir * (-5f + (float)rng.NextDouble() * 22f) + startRight * (4.9f + (float)rng.NextDouble() * 7f);
                if (!NatureKit.UnderRock(p2.x, p2.y))
                    GrassField.ClumpObject(root, G(p2) - Vector3.up * 0.05f, 1.2f + (float)rng.NextDouble() * 1.1f, rng);
            }
            float since = 0f;
            for (int i = 1; i < road.Count - 1; i++)
            {
                var dir = (road[i + 1] - road[i - 1]).normalized;
                var right = new Vector2(dir.y, -dir.x);
                since += Vector2.Distance(road[i], road[i - 1]);
                if (Vector2.Distance(road[i], Trailhead) < 20f || Vector2.Distance(road[i], CarStart) < 10f) continue;

                // 바위: 길 어깨에 듬성듬성, 가끔 두셋이 모여
                if (since > 7f && rng.NextDouble() < 0.4)
                {
                    since = 0f;
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    int n = rng.NextDouble() < 0.3 ? 2 + rng.Next(2) : 1;
                    for (int k = 0; k < n; k++)
                    {
                        var p2 = road[i] + right * side * (5.2f + (float)rng.NextDouble() * 3f) + dir * (k * 1.8f);
                        // 무릎~가슴 높이 각진 바위 (목표 샷 01). 큰 바위 무더기는 길을 가린다
                        NatureKit.Boulder(root, G(p2), (k == 0 ? 0.9f : 0.5f) + (float)rng.NextDouble() * 1.1f, rng, true, "RoadRock");
                    }
                }

                // 둥근 덤불: 길가 초원에 듬성듬성 (목표 샷 01)
                if (art != null && art.fieldBushes != null && art.fieldBushes.Length > 0 && rng.NextDouble() < 0.12)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var p2 = road[i] + right * side * (7f + (float)rng.NextDouble() * 12f);
                    var bush = ArtCatalog.Pick(art.fieldBushes, rng.Next(100));
                    if (bush != null)
                    {
                        var inst = Object.Instantiate(bush, root);
                        inst.transform.SetPositionAndRotation(G(p2) - Vector3.up * 0.1f, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
                        inst.transform.localScale = Vector3.one * (0.9f + (float)rng.NextDouble() * 0.8f);
                        Brighten(inst, new Color(0.82f, 0.92f, 0.7f)); // 아틀라스 연두가 볕에서 형광처럼 뜬다. 짙은 덤불로
                    }
                }

                // 키 큰 풀 덤불: 어깨를 따라 촘촘히
                for (int k = 0; k < 2; k++)
                {
                    if (rng.NextDouble() > 0.7) continue;
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var p2 = road[i] + right * side * (4.8f + (float)rng.NextDouble() * 3.5f);
                    if (!NatureKit.UnderRock(p2.x, p2.y))
                        GrassField.ClumpObject(root, G(p2) - Vector3.up * 0.05f, 1.1f + (float)rng.NextDouble() * 0.7f, rng);
                }
            }

            // 나무 울타리: 길 왼쪽을 따라 기둥 + 가로대 두 줄 (군데군데 끊기고 기운다). 신티 울타리는 기둥뿐이라 직접 짠다
            var wood = Graybox.Mat(new Color(0.36f, 0.27f, 0.19f));
            Vector3 prev = Vector3.zero;
            bool havePrev = false;
            float acc = 0f;
            for (int i = 6; i < Mathf.Min(road.Count - 2, 80); i++)
            {
                acc += Vector2.Distance(road[i], road[i - 1]);
                if (acc < 2.6f) continue;
                acc = 0f;
                var dir = (road[i + 1] - road[i - 1]).normalized;
                var left = new Vector2(-dir.y, dir.x);
                var p = G(road[i] + left * 8.5f);
                if (rng.NextDouble() < 0.06) { havePrev = false; continue; } // 빠진 칸
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Fence";
                post.transform.SetParent(root, false);
                post.transform.position = p + Vector3.up * 0.55f;
                post.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 6f, (float)rng.NextDouble() * 20f, (float)(rng.NextDouble() - 0.5) * 6f);
                post.transform.localScale = new Vector3(0.16f, 1.2f, 0.16f);
                post.GetComponent<Renderer>().sharedMaterial = wood;
                Kill(post.GetComponent<Collider>());
                if (havePrev)
                {
                    for (int r = 0; r < 2; r++)
                    {
                        var a = prev + Vector3.up * (0.45f + r * 0.42f);
                        var b = p + Vector3.up * (0.45f + r * 0.42f);
                        var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rail.name = "FenceRail";
                        rail.transform.SetParent(root, false);
                        rail.transform.position = (a + b) / 2f;
                        rail.transform.rotation = Quaternion.LookRotation(b - a);
                        rail.transform.localScale = new Vector3(0.07f, 0.11f, Vector3.Distance(a, b) + 0.2f);
                        rail.GetComponent<Renderer>().sharedMaterial = wood;
                        Kill(rail.GetComponent<Collider>());
                    }
                }
                prev = p;
                havePrev = true;
            }
        }

        // ---------------- 떠 있는 집과 길을 건너는 그림자 (목표 샷 01) ----------------

        /// <summary>
        /// 초원 위 공중에 떠 있는 목조 주택. 출발점에서 왼쪽 앞으로 보인다. 천천히 오르내린다.
        /// 그리고 그 집과 다른 모양의 큰 그림자가 흙길을 천천히 가로지른다 (그림자를 만드는 몸은 보이지 않는다).
        /// </summary>
        static void PlaceFloatingHouse(Transform root, Terrain terrain, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            var roadDir = (Road[2] - Road[1]).normalized;
            var left = new Vector2(-roadDir.y, roadDir.x);
            var at2 = CarStart + roadDir * 75f + left * 42f;
            var ground = new Vector3(at2.x, 0, at2.y);
            ground.y = terrain.SampleHeight(ground);
            GameObject house;
            var prefab = art != null && art.farmBuildings != null && art.farmBuildings.Length > 0 ? art.farmBuildings[0] : null;
            if (prefab != null)
            {
                house = Object.Instantiate(prefab, root);
                foreach (var c in house.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c); // 네비메시를 같은 프레임에 굽는다
                house.transform.rotation = Quaternion.LookRotation(new Vector3(-roadDir.x, 0, -roadDir.y)) * Quaternion.Euler(0, 25f, 0);
                var b = ArtCatalog.RendererBounds(house);
                house.transform.localScale = Vector3.one * 1.25f;
                b = ArtCatalog.RendererBounds(house);
                house.transform.position += new Vector3(ground.x - b.center.x, ground.y + 19f - b.min.y, ground.z - b.center.z);
            }
            else
            {
                house = Graybox.Box(root, "House", ground + Vector3.up * 28f, new Vector3(9f, 7f, 7f), new Color(0.8f, 0.75f, 0.62f), false);
            }
            house.name = "FloatingHouse";
            house.AddComponent<FloatingBob>().amplitude = 0.8f;

            // 길을 건너는 그림자: 보이지 않는 몸 (그림자만 드리운다). 집과 다른, 길쭉하고 울퉁불퉁한 모양
            var body = new GameObject("CrossingShadow");
            body.transform.SetParent(root, false);
            for (int i = 0; i < 6; i++)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(body.transform, false);
                part.transform.localPosition = new Vector3((i - 2.5f) * 5.5f, (float)rng.NextDouble() * 2f, (float)(rng.NextDouble() - 0.5) * 6f);
                part.transform.localScale = new Vector3(9f + (float)rng.NextDouble() * 5f, 3f, 6f + (float)rng.NextDouble() * 6f);
                var r = part.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                r.sharedMaterial = Graybox.Mat(Color.black);
            }
            var cross = CarStart + roadDir * 70f;
            var from = new Vector3(cross.x, 0, cross.y) + new Vector3(left.x, 0, left.y) * 60f;
            var to = new Vector3(cross.x, 0, cross.y) - new Vector3(left.x, 0, left.y) * 60f;
            from.y = to.y = terrain.SampleHeight(new Vector3(cross.x, 0, cross.y)) + 40f;
            var drift = body.AddComponent<DriftingShadow>();
            drift.from = from;
            drift.to = to;
            drift.period = 46f;
            body.transform.rotation = Quaternion.LookRotation(new Vector3(roadDir.x, 0, roadDir.y));
        }

        /// <summary>플레이 중엔 Destroy, 에디터에서 조명을 구울 땐 DestroyImmediate.</summary>
        static void Kill(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        static MaterialPropertyBlock mpb;

        static void Brighten(GameObject go, Color c)
        {
            mpb ??= new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb);
            }
        }

        // ---------------- 기운 숲 (목표 샷 02) ----------------

        /// <summary>숲길 양옆 띠. 주차 공터 → 방 입구 사이.</summary>
        static float LeaningBandDistance(float x, float z)
        {
            var a = Trailhead + RoomForward * 14f;
            var b = RoomEntrance - RoomForward * 6f;
            var p = new Vector2(x, z);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static bool InLeaningBand(float x, float z, float pad)
        {
            var a = Trailhead + RoomForward * 14f;
            var b = RoomEntrance - RoomForward * 6f;
            var p = new Vector2(x, z);
            var ab = b - a;
            float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
            float d = Vector2.Distance(p, a + ab * Mathf.Clamp01(t));
            return d < 24f + pad && t > 0f && t < 1f;
        }

        /// <summary>
        /// 숲길 양옆의 나무가 전부 같은 방향(방 쪽)으로 기울어 있다. 바깥의 곧은 숲과 비교해 읽힌다.
        /// 터레인 나무는 Y로만 돌아서 게임오브젝트로 심는다.
        /// </summary>
        static void PlaceLeaningForest(Transform root, Terrain terrain, System.Random rng)
        {
            var art = ArtCatalog.Instance;
            var go = new GameObject("LeaningForest");
            go.transform.SetParent(root, false);
            var lean = new Vector3(RoomForward.x, 0, RoomForward.y);
            var axis = Vector3.Cross(Vector3.up, lean);
            int leaning = 0;
            // 목표 샷 02는 굵은 줄기가 2~3m 간격으로 길가에 붙어 터널을 만든다 (4.1m 격자·6.5m 비움은 듬성한 공터로 읽혔다)
            // (3.2m는 줄기가 벽처럼 막아 하늘이 안 보였다 → 4.3m)
            for (float x = RoomCenter.x - 120f; x < RoomCenter.x + 60f; x += 4.3f)
            for (float z = RoomCenter.y - 140f; z < RoomCenter.y + 20f; z += 4.3f)
            {
                float px = x + (float)(rng.NextDouble() - 0.5) * 3f, pz = z + (float)(rng.NextDouble() - 0.5) * 3f;
                if (!InLeaningBand(px, pz, 0f) || InRoom(px, pz, 4f)) continue;
                var p2 = new Vector2(px, pz);
                if (Vector2.Distance(p2, Trailhead) < 16f) continue;
                // 숲길 자체는 비운다
                var a = Trailhead; var b = RoomEntrance; var ab = b - a;
                float tt = Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / ab.sqrMagnitude);
                var onPath = a + ab * tt;
                float side = Vector2.Distance(p2, onPath);
                if (side < 6.2f) continue; // 길(폭 약 4m)과 어깨를 비운다. 기운 수관이 길 위로 모인다 (4.6m에선 줄기가 길을 가로질러 A자 들보가 됐다)
                if (rng.NextDouble() < (side < 12f ? 0.12 : 0.35)) continue; // 길가 두 줄은 빽빽하게, 안쪽은 성기게
                // 맨 줄기가 길게 드러난 키 큰 소나무 (목표 샷 02). 길가 줄이 가장 크다
                float height = Mathf.Lerp(18f, 13f, Mathf.InverseLerp(6f, 18f, side)) + (float)rng.NextDouble() * 3f;
                var t = NatureKit.TallPine(go.transform, height, rng);
                var p = new Vector3(px, 0, pz);
                p.y = terrain.SampleHeight(p) - 0.2f;
                // 길 쪽으로 기울고 조금은 방 쪽으로 (목표 샷 02: 길 위로 V자로 모인다)
                var inward = (onPath - p2).normalized;
                var dir2 = (inward + RoomForward * 0.45f).normalized;
                var tiltAxis = Vector3.Cross(Vector3.up, new Vector3(dir2.x, 0, dir2.y));
                float tilt = Mathf.Lerp(20f, 13f, Mathf.InverseLerp(6f, 20f, side)) + (float)rng.NextDouble() * 5f; // 길가 줄기가 가장 많이 기운다 (목표 샷 02는 15~20°)
                t.transform.SetPositionAndRotation(p, Quaternion.AngleAxis(tilt, tiltAxis) * Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
                leaning++;
            }
            // 숲길 가장자리 바위 (목표 샷 02: 길 양옆에 회색 바위가 줄지어 있다)
            int rocks = 0;
            var bendPt = Vector2.Lerp(Trailhead, RoomEntrance, 0.5f) + new Vector2(-6f, 2f); // 숲길 꺾임 (BuildTerrain의 trailPts와 같다)
            var legs = new[] { (Trailhead + (bendPt - Trailhead).normalized * 12f, bendPt), (bendPt, RoomEntrance - RoomForward * 3f) };
            foreach (var (pa, pb) in legs)
            for (float d = 0f; d < Vector2.Distance(pa, pb); d += 3.5f)
            {
                if (rng.NextDouble() < 0.45) continue;
                var fwd = (pb - pa).normalized;
                var pathRight = new Vector2(fwd.y, -fwd.x);
                float sideSign = rng.NextDouble() < 0.5 ? -1f : 1f;
                var q = pa + fwd * d + pathRight * sideSign * (3.4f + (float)rng.NextDouble() * 3f);
                var ground = new Vector3(q.x, 0, q.y);
                ground.y = terrain.SampleHeight(ground);
                NatureKit.Boulder(go.transform, ground, 0.35f + (float)rng.NextDouble() * (rng.NextDouble() < 0.25 ? 1.1f : 0.5f), rng, true, "PathRock");
                rocks++;
            }
            Debug.Log($"[Manyworld] 기운 숲 {leaning}그루, 길가 바위 {rocks}");
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
                // 벽은 줄기가 긴 큰 소나무로: 아래는 줄기 기둥, 위는 잎 (목표 샷 03)
                var prefab = art != null && art.pines != null && art.pines.Length > 2 ? art.pines[2] : art != null ? ArtCatalog.Pick(art.pines, rng.Next(100)) : null;
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
                Pine(p, 1.15f + (float)rng.NextDouble() * 0.3f);
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

            // 입구 양옆: 둥근 바위·쓰러진 통나무·키 큰 풀 (목표 샷 03의 앞쪽)
            for (int k = 0; k < 9; k++)
            {
                float side = k % 2 == 0 ? -1f : 1f;
                // 입구 양옆을 액자처럼: 가운데 길(±3.5m)은 비운다 (목표 샷 03)
                var p = W(side * (3.8f + (float)rng.NextDouble() * (RoomHalf + 1f)), -RoomHalf - 1f - (float)rng.NextDouble() * 8f);
                if (k < 6) NatureKit.Boulder(go.transform, p, 0.6f + (float)rng.NextDouble() * 1.0f, rng, false);
                for (int q = 0; q < 3; q++)
                {
                    var cp = p + new Vector3((float)rng.NextDouble() * 3f - 1.5f, -0.05f, (float)rng.NextDouble() * 3f - 1.5f);
                    if (!NatureKit.UnderRock(cp.x, cp.z)) GrassField.ClumpObject(go.transform, cp, 1.3f + (float)rng.NextDouble() * 0.8f, rng);
                }
            }
            var logPos = W(RoomHalf + 1.5f, -RoomHalf - 3.5f);
            var log = Graybox.Prim(PrimitiveType.Cylinder, go.transform, "FallenLog", logPos + Vector3.up * 0.3f, new Vector3(0.6f, 2.6f, 0.6f), new Color(0.33f, 0.24f, 0.17f), false);
            log.transform.rotation = rot * Quaternion.Euler(0, 35f, 90f);

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
                var bear = Plush(go.transform, art != null ? ArtCatalog.Pick(art.plushies, pick) : null, p, face, 0.95f + (float)rng.NextDouble() * 0.15f, true); // 아이 키만 한 곰인형
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
                foreach (var col in go.GetComponentsInChildren<Collider>()) Kill(col);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = Vector3.one;
                var b = ArtCatalog.RendererBounds(go);
                go.transform.localScale = Vector3.one * (height / Mathf.Max(0.05f, b.size.y));
                b = ArtCatalog.RendererBounds(go);
                go.transform.position += Vector3.up * (pos.y - b.min.y);
                Brighten(go, new Color(1.3f, 1.22f, 1.1f)); // 흙바닥에 묻히지 않게 밝은 곰인형으로
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
