using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Manyworld
{
    /// <summary>미션 맵의 주요 지점.</summary>
    public class MissionLayout
    {
        public Transform root;
        public Vector3 playerSpawn;
        public Vector3 companionSpawn;
        public Vector3 ticoPos;
        public Vector3 midpointPos;
        public Vector3 objectivePos;
        public Vector3 gatePos;
        public Terrain terrain;          // 큰 월드일 때
        public Texture2D minimap;        // 큰 월드 지도 (북쪽이 위)
        public float worldSize;
        public float waterLevel = -10f;
        public readonly Dictionary<string, Vector3> landmarks = new Dictionary<string, Vector3>();
        public readonly List<Vector3> spawnPoints = new List<Vector3>();
        public readonly List<Vector3> bruteSpawnPoints = new List<Vector3>();
    }

    /// <summary>
    /// "같은 동네, 다른 번호" — 동네 기본 맵 하나를 늘 같은 배치로 짓고,
    /// 세계 번호에 따라 변조 레이어(안개·침수·붕괴)를 덮는다 (brainstorm-01 3장, brainstorm-02 4장).
    /// </summary>
    public static class TownBuilder
    {
        const int Blocks = 4;
        const float BlockSize = 22f;
        const float RoadWidth = 8f;
        const float Pitch = BlockSize + RoadWidth;       // 30
        const float Half = (Blocks * Pitch + RoadWidth) / 2f; // 64

        static readonly string[] Landmarks = { "기사식당", "문방구", "다방", "연탄", "동사무소", "약국", "복덕방", "이발소", "전파사", "슈퍼", "목욕탕", "교회" };

        /// <summary>가장 가까운 동서 도로 위의 점.</summary>
        public static Vector3 NearestRoadPoint(Vector3 p)
        {
            float best = 0f, bestD = float.MaxValue;
            for (int i = 0; i <= Blocks; i++)
            {
                float z = -Half + RoadWidth / 2f + i * Pitch;
                if (Mathf.Abs(z - p.z) < bestD) { bestD = Mathf.Abs(z - p.z); best = z; }
            }
            return new Vector3(p.x, 0, best);
        }

        static float BlockCenter(int i) => -Half + RoadWidth + BlockSize / 2f + i * Pitch;

        public static MissionLayout Build(WorldSpec spec, bool isHome = false)
        {
            var root = new GameObject(isHome ? "Town_Home" : $"Town_{spec.number}").transform;
            var layout = new MissionLayout { root = root };

            // 우리 세계는 따뜻하고, 멀티버스는 같은 구도인데 차갑고 뒤틀린 톤
            // 우리 동네: 흙 마당, 아스팔트 골목, 시멘트 연석
            Color ground = isHome ? new Color(0.42f, 0.4f, 0.32f) : new Color(0.32f, 0.34f, 0.35f);
            Color road = isHome ? new Color(0.24f, 0.24f, 0.25f) : new Color(0.20f, 0.21f, 0.22f);
            Color curb = new Color(0.58f, 0.56f, 0.52f);
            Color wallA = isHome ? new Color(0.78f, 0.70f, 0.58f) : new Color(0.55f, 0.57f, 0.58f);
            Color wallB = isHome ? new Color(0.70f, 0.60f, 0.52f) : new Color(0.46f, 0.48f, 0.50f);
            if (spec != null && spec.rules.HasFlag(WorldRule.ToxicGas))
            {
                wallA = Color.Lerp(wallA, new Color(0.7f, 0.65f, 0.3f), 0.35f);
                wallB = Color.Lerp(wallB, new Color(0.6f, 0.55f, 0.25f), 0.35f);
            }

            Graybox.Box(root, "Ground", new Vector3(0, -0.5f, 0), new Vector3(Half * 2 + 20, 1, Half * 2 + 20), ground);
            if (isHome) // 담 밖까지 땅이 이어진다 (원경 아래 허공이 안 보이게)
                Graybox.Box(root, "OuterGround", new Vector3(0, -0.55f, 0), new Vector3(700f, 1f, 700f), new Color(0.38f, 0.36f, 0.3f), false);
            for (int i = 0; i <= Blocks; i++)
            {
                float p = -Half + RoadWidth / 2f + i * Pitch;
                Graybox.Box(root, "RoadX", new Vector3(0, 0.01f, p), new Vector3(Half * 2, 0.02f, RoadWidth), road, false);
                Graybox.Box(root, "RoadZ", new Vector3(p, 0.012f, 0), new Vector3(RoadWidth, 0.02f, Half * 2), road, false);
                if (isHome)
                {
                    // 가운데 점선 (빛바랜 노란 페인트)
                    for (float t = -Half + 6f; t < Half - 6f; t += 7f)
                    {
                        Graybox.Box(root, "Lane", new Vector3(t, 0.025f, p), new Vector3(2.4f, 0.01f, 0.18f), new Color(0.75f, 0.65f, 0.35f), false);
                        Graybox.Box(root, "Lane", new Vector3(p, 0.026f, t), new Vector3(0.18f, 0.01f, 2.4f), new Color(0.75f, 0.65f, 0.35f), false);
                    }
                }
            }

            // 외곽 담장
            float wallH = isHome ? 2.4f : 4f;
            Color fence = isHome ? new Color(0.55f, 0.53f, 0.49f) : new Color(0.25f, 0.25f, 0.27f); // 우리 동네: 시멘트 담
            Graybox.Box(root, "Fence_N", new Vector3(0, wallH / 2, Half + 1), new Vector3(Half * 2 + 4, wallH, 1), fence);
            Graybox.Box(root, "Fence_S", new Vector3(0, wallH / 2, -Half - 1), new Vector3(Half * 2 + 4, wallH, 1), fence);
            Graybox.Box(root, "Fence_E", new Vector3(Half + 1, wallH / 2, 0), new Vector3(1, wallH, Half * 2 + 4), fence);
            Graybox.Box(root, "Fence_W", new Vector3(-Half - 1, wallH / 2, 0), new Vector3(1, wallH, Half * 2 + 4), fence);

            // 기본 배치는 고정 시드. 변조는 세계 번호 시드.
            var baseRng = new System.Random(1987);
            var korRng = new System.Random(1987 * 7 + 1); // 한국식 건물 세부 (배치 난수와 따로)
            var modRng = new System.Random((spec?.number ?? 0) * 31 + 5);
            int landmarkIndex = 0;

            for (int bx = 0; bx < Blocks; bx++)
            for (int bz = 0; bz < Blocks; bz++)
            {
                var center = new Vector3(BlockCenter(bx), 0, BlockCenter(bz));
                bool gateLot = bx == 0 && bz == 0;
                bool objectiveLot = bx == Blocks - 1 && bz == Blocks - 1;
                if (gateLot || objectiveLot) continue;

                for (int lx = 0; lx < 2; lx++)
                for (int lz = 0; lz < 2; lz++)
                {
                    double roll = baseRng.NextDouble();
                    float h = 3f + (float)baseRng.NextDouble() * 9f;
                    float w = 7f + (float)baseRng.NextDouble() * 3.5f;
                    float d = 7f + (float)baseRng.NextDouble() * 3.5f;
                    bool tint = baseRng.NextDouble() < 0.5;
                    if (roll < 0.18)
                    {
                        // 빈 필지: 풀밭과 나무 한두 그루 (우리 동네만)
                        if (isHome) PlantLot(root, center + new Vector3((lx - 0.5f) * BlockSize / 2f, 0, (lz - 0.5f) * BlockSize / 2f), baseRng);
                        continue;
                    }

                    var lotCenter = center + new Vector3((lx - 0.5f) * BlockSize / 2f, 0, (lz - 0.5f) * BlockSize / 2f);

                    // 붕괴 변조: 일부 건물이 무너져 잔해만 남는다
                    if (!isHome && spec.terrain == TerrainMod.Collapse && modRng.NextDouble() < 0.35)
                    {
                        for (int r = 0; r < 4; r++)
                        {
                            var rubble = Graybox.Box(root, "Rubble",
                                lotCenter + new Vector3((float)modRng.NextDouble() * 6 - 3, 0.6f, (float)modRng.NextDouble() * 6 - 3),
                                new Vector3(2 + (float)modRng.NextDouble() * 2, 1.2f, 1.5f + (float)modRng.NextDouble() * 2), Color.Lerp(wallB, Color.black, 0.2f));
                            rubble.transform.rotation = Quaternion.Euler((float)modRng.NextDouble() * 20, (float)modRng.NextDouble() * 360, (float)modRng.NextDouble() * 20);
                        }
                        continue;
                    }

                    bool isLandmark = landmarkIndex < Landmarks.Length && baseRng.NextDouble() < 0.6;
                    bool facesNorth = lz == 1; // 가까운 도로 쪽을 본다

                    // 우리 동네: 80년대 한국 건물 (KoreanBuildings)
                    if (isHome)
                    {
                        float yaw = facesNorth ? 0f : 180f;
                        string name = null;
                        if (isLandmark)
                        {
                            name = Landmarks[landmarkIndex++];
                            layout.landmarks[name] = lotCenter;
                        }
                        if (name != null || korRng.NextDouble() < 0.22)
                        {
                            int floors = name == null ? 2 + korRng.Next(2) : 2 + korRng.Next(2);
                            KoreanBuildings.ShopHouse(root, lotCenter, yaw, Mathf.Min(w, 10f), Mathf.Min(d, 9f) * 0.9f, floors, name, korRng);
                        }
                        else
                        {
                            var lot = new Vector2(BlockSize / 2f - 0.8f, BlockSize / 2f - 0.8f);
                            KoreanBuildings.House(root, lotCenter, yaw, Mathf.Min(w, 9f) * 0.85f, Mathf.Min(d, 8f) * 0.7f, korRng, lot, false);
                        }
                        continue;
                    }

                    var b = Graybox.Box(root, "Building", lotCenter + new Vector3(0, h / 2f, 0), new Vector3(w, h, d), tint ? wallA : wallB);
                    // 슬레이트 지붕 느낌
                    Graybox.Box(b.transform, "Roof", new Vector3(0, 0.5f + 0.02f, 0), new Vector3(1.04f, 0.04f, 1.04f), new Color(0.3f, 0.32f, 0.38f), false);

                    float frontZ = facesNorth ? lotCenter.z + d / 2f : lotCenter.z - d / 2f;
                    float signY = 2.8f;
                    if (ArtCatalog.HasTown)
                    {
                        var bounds = DressBuilding(root, b, lotCenter, w, d, facesNorth, isLandmark, bx * 16 + bz * 4 + lx * 2 + lz);
                        frontZ = facesNorth ? bounds.max.z : bounds.min.z;
                        signY = Mathf.Clamp(bounds.size.y * 0.45f, 2.2f, 3.4f);
                    }

                    if (isLandmark)
                    {
                        string name = Landmarks[landmarkIndex++];
                        layout.landmarks[name] = lotCenter;
                        var sign = Graybox.Label(root, name, new Vector3(lotCenter.x, signY, frontZ + (facesNorth ? 0.08f : -0.08f)), 0.09f,
                            isHome ? new Color(0.9f, 0.85f, 0.7f) : new Color(0.6f, 0.65f, 0.7f));
                        sign.transform.rotation = facesNorth ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                    }
                }
            }

            // 게이트 공터 (남서쪽)
            var gateCenter = new Vector3(BlockCenter(0), 0, BlockCenter(0));
            layout.gatePos = gateCenter + new Vector3(-5, 0, -4);
            BuildGate(root, layout.gatePos, isHome);
            layout.ticoPos = gateCenter + new Vector3(3, 0, 2);
            if (!isHome) BuildTico(root, layout.ticoPos, Quaternion.Euler(0, 45, 0)); // 우리 동네에선 직접 몰고 온다
            layout.playerSpawn = gateCenter + new Vector3(5.5f, 0.1f, 4.5f);
            layout.companionSpawn = gateCenter + new Vector3(4.8f, 0.1f, 6.8f); // 기사 왼쪽 앞. 카메라 시야를 가리지 않게

            // 목표 공터 (북동쪽): 공사 현장
            var objCenter = new Vector3(BlockCenter(Blocks - 1), 0, BlockCenter(Blocks - 1));
            layout.objectivePos = objCenter;
            Graybox.Box(root, "SiteFence_A", objCenter + new Vector3(-9, 1, 0), new Vector3(0.3f, 2, 14), new Color(0.6f, 0.5f, 0.2f));
            Graybox.Box(root, "SiteFence_B", objCenter + new Vector3(0, 1, 9), new Vector3(14, 2, 0.3f), new Color(0.6f, 0.5f, 0.2f));
            Graybox.Box(root, "Container", objCenter + new Vector3(4, 1.3f, 5), new Vector3(6, 2.6f, 2.4f), new Color(0.45f, 0.25f, 0.2f));
            Graybox.Box(root, "Container2", objCenter + new Vector3(-5, 1.3f, -5), new Vector3(2.4f, 2.6f, 6), new Color(0.25f, 0.35f, 0.45f));

            layout.midpointPos = new Vector3(0, 0, 0);
            if (ArtCatalog.HasTown) PlaceStreetProps(root, layout);
            if (isHome)
            {
                BuildCurbs(root, curb);
                BuildSkyline(root);
                // 골목 전봇대와 처진 전깃줄
                for (int i = 1; i < Blocks; i++)
                {
                    float line = -Half + RoadWidth / 2f + i * Pitch;
                    var pts = new System.Collections.Generic.List<Vector3>();
                    for (float t = -Half + 2f; t <= Half - 2f; t += 4f) pts.Add(new Vector3(t, 0, line));
                    WorldTerrainBuilder.PlacePowerLines(root, pts, null, 4.4f); // 연석 위
                }
            }

            if (!isHome)
            {
                ApplyFlood(root, spec);
                PlaceBureauSign(root, spec, gateCenter + new Vector3(8, 0, -8));
            }

            // 네비메시: 지형만 굽는다 (배우들은 Ignore Raycast 레이어라 빠진다)
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << 2);
            surface.BuildNavMesh();

            if (!isHome) CollectSpawnPoints(layout, spec);
            return layout;
        }

        /// <summary>담 너머 원경: 동네가 계속 이어지는 것처럼 보이게 (Synty 배경 건물).</summary>
        static void BuildSkyline(Transform root)
        {
            var art = ArtCatalog.Instance;
            if (art == null || art.skyline == null || art.skyline.Length == 0) return;
            var rng = new System.Random(77);
            for (int i = 0; i < 40; i++)
            {
                float ang = i / 40f * Mathf.PI * 2f;
                float dist = Half + 120f + (float)rng.NextDouble() * 80f; // 멀리, 안개 속 서울 원경
                var p = new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                var prefab = ArtCatalog.Pick(art.skyline, rng.Next(100));
                if (prefab == null) continue;
                var face = Quaternion.LookRotation(new Vector3(-p.x, 0, -p.z));
                var b = ArtCatalog.PlaceNatural(prefab, root, p, face, out _);
                ArtCatalog.SetLayer(b, 2);
            }
        }

        /// <summary>길가 연석 (인도 턱). 차가 살짝 넘을 수 있는 높이.</summary>
        static void BuildCurbs(Transform root, Color curb)
        {
            for (int i = 0; i <= Blocks; i++)
            {
                float p = -Half + RoadWidth / 2f + i * Pitch;
                foreach (float side in new[] { -1f, 1f })
                {
                    float off = side * (RoadWidth / 2f + 0.4f);
                    for (int b = 0; b < Blocks; b++)
                    {
                        float c = BlockCenter(b);
                        Graybox.Box(root, "Curb", new Vector3(c, 0.06f, p + off), new Vector3(BlockSize, 0.12f, 0.8f), curb, false);
                        Graybox.Box(root, "Curb", new Vector3(p + off, 0.061f, c), new Vector3(0.8f, 0.12f, BlockSize), curb, false);
                    }
                }
            }
        }

        /// <summary>빈 필지에 풀밭과 나무.</summary>
        static void PlantLot(Transform root, Vector3 c, System.Random rng)
        {
            Graybox.Box(root, "Grass", c + new Vector3(0, 0.015f, 0), new Vector3(BlockSize / 2f - 1f, 0.02f, BlockSize / 2f - 1f), new Color(0.36f, 0.42f, 0.26f), false);
            var art = ArtCatalog.Instance;
            if (art == null || art.trees == null || art.trees.Length == 0) return;
            int n = 1 + rng.Next(2);
            for (int k = 0; k < n; k++)
            {
                var prefab = ArtCatalog.Pick(art.trees, rng.Next(100));
                if (prefab == null) continue;
                var p = c + new Vector3((float)rng.NextDouble() * 6f - 3f, 0, (float)rng.NextDouble() * 6f - 3f);
                var t = ArtCatalog.PlaceNatural(prefab, root, p, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), out var b);
                // 너무 큰 나무는 줄인다
                if (b.size.y > 9f) t.transform.localScale *= 9f / b.size.y;
                var col = Graybox.Box(root, "TreeCollider", new Vector3(p.x, 1.5f, p.z), new Vector3(0.5f, 3f, 0.5f), Color.gray);
                col.GetComponent<Renderer>().enabled = false;
            }
        }

        /// <summary>Synty 건물 앞쪽 축 보정 (프리팹 정면이 +Z가 아니면 여기서 돌린다).</summary>
        const float SyntyFrontYaw = 0f;

        /// <summary>그레이박스 건물 자리에 Synty 건물을 맞춰 넣고, 콜라이더 상자를 실제 크기에 맞춘다.</summary>
        static Bounds DressBuilding(Transform root, GameObject box, Vector3 lotCenter, float w, float d, bool facesNorth, bool shop, int lotIndex)
        {
            var art = ArtCatalog.Instance;
            var prefab = shop ? ArtCatalog.Pick(art.shops, lotIndex) : ArtCatalog.Pick(art.houses, lotIndex);
            if (prefab == null) prefab = ArtCatalog.Pick(art.houses, lotIndex);
            var rot = Quaternion.Euler(0, (facesNorth ? 0f : 180f) + SyntyFrontYaw, 0);
            var inst = ArtCatalog.PlaceFitted(prefab, root, lotCenter, rot, new Vector3(w + 1.5f, 14f, d + 1.5f), out var bounds);
            inst.name = "Art_" + prefab.name;
            box.transform.position = bounds.center;
            box.transform.localScale = bounds.size;
            foreach (var r in box.GetComponentsInChildren<Renderer>()) r.enabled = false;
            return bounds;
        }

        /// <summary>길가의 주차된 차, 소화전, 쓰레기통, 벤치. 차는 엄폐물이 된다.</summary>
        static void PlaceStreetProps(Transform root, MissionLayout layout)
        {
            var art = ArtCatalog.Instance;
            var rng = new System.Random(77);
            var avoid = new[] { layout.gatePos, layout.ticoPos, layout.objectivePos, layout.midpointPos, layout.playerSpawn };
            bool Clear(Vector3 p)
            {
                foreach (var a in avoid) if (Vector3.Distance(p, a) < 11f) return false;
                return true;
            }

            int cars = 0, props = 0;
            for (int tries = 0; tries < 200 && (cars < 10 || props < 34); tries++)
            {
                bool alongX = rng.NextDouble() < 0.5;
                int road = rng.Next(Blocks + 1);
                float line = -Half + RoadWidth / 2f + road * Pitch;
                float along = (float)rng.NextDouble() * (Half * 2f - 10f) - (Half - 5f);
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                bool car = cars < 10 && rng.NextDouble() < 0.35;
                float offset = car ? 2.6f : 3.7f;
                var p = alongX ? new Vector3(along, 0, line + side * offset) : new Vector3(line + side * offset, 0, along);
                if (!Clear(p)) continue;
                var yaw = alongX ? (side > 0 ? 90f : -90f) : (side > 0 ? 0f : 180f);
                if (car)
                {
                    var prefab = ArtCatalog.Pick(art.parkedCars, rng.Next(100));
                    if (prefab == null) continue;
                    var inst = ArtCatalog.PlaceFitted(prefab, root, p, Quaternion.Euler(0, yaw + (alongX ? 0 : 0), 0), new Vector3(4.6f, 3f, 4.6f), out var bounds);
                    inst.name = "ParkedCar";
                    var col = Graybox.Box(root, "ParkedCarCollider", bounds.center, bounds.size, Color.gray);
                    col.GetComponent<Renderer>().enabled = false;
                    cars++;
                }
                else
                {
                    var prefab = ArtCatalog.Pick(art.streetProps, rng.Next(100));
                    if (prefab == null) continue;
                    var inst = ArtCatalog.PlaceFitted(prefab, root, p, Quaternion.Euler(0, yaw + 90f, 0), new Vector3(2.2f, 2.2f, 2.2f), out _);
                    inst.name = "StreetProp";
                    ArtCatalog.SetLayer(inst, 2); // 소품은 길을 막지 않는다
                    props++;
                }
            }
        }

        public static void BuildGate(Transform root, Vector3 pos, bool home)
        {
            // 논두렁 너머에 반쯤 묻힌 거대한 링 — 테일즈 프롬 더 루프풍
            var ring = new GameObject("Gate");
            ring.transform.SetParent(root, false);
            ring.transform.localPosition = pos;
            const int segments = 28;
            const float radius = 6.5f;
            float centerY = radius * 0.55f; // 아래쪽은 땅에 묻혔다
            var metal = Graybox.Mat(new Color(0.3f, 0.29f, 0.27f));
            var rust = Graybox.Mat(new Color(0.38f, 0.25f, 0.18f));
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius + centerY, 0);
                if (p.y < -0.8f) continue;
                var seg = Graybox.Prim(PrimitiveType.Cylinder, ring.transform, "Seg", p, new Vector3(1.5f, radius * Mathf.PI / segments * 1.1f, 1.5f), Color.gray, false);
                seg.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                seg.GetComponent<Renderer>().sharedMaterial = i % 5 == 0 ? rust : metal;
                // 리벳 띠
                if (i % 2 == 0)
                {
                    var band = Graybox.Box(ring.transform, "Band", p, new Vector3(0.25f, 0.5f, 1.8f), Color.gray, false);
                    band.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                    band.GetComponent<Renderer>().sharedMaterial = rust;
                }
            }
            var color = home ? new Color(0.35f, 0.65f, 1f, 0.28f) : new Color(0.75f, 0.35f, 1f, 0.28f);
            var membrane = Graybox.Prim(PrimitiveType.Cylinder, ring.transform, "Membrane", new Vector3(0, centerY, 0), new Vector3(radius * 1.8f, 0.04f, radius * 1.8f), color, false);
            membrane.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var mat = new Material(membrane.GetComponent<Renderer>().sharedMaterial);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 0.45f);
            membrane.GetComponent<Renderer>().sharedMaterial = mat;
            var glow = new GameObject("GateGlow").AddComponent<Light>();
            glow.transform.SetParent(ring.transform, false);
            glow.transform.localPosition = new Vector3(0, centerY, 1.5f);
            glow.type = LightType.Point;
            glow.range = 18f;
            glow.intensity = 3f;
            glow.color = new Color(color.r, color.g, color.b);
            var shimmer = membrane.AddComponent<GateShimmer>();
            shimmer.mat = mat;
            shimmer.glow = glow;
            shimmer.baseColor = new Color(color.r, color.g, color.b);
            ring.transform.localRotation = Quaternion.Euler(0, 40, 0);
        }

        public static GameObject BuildTico(Transform root, Vector3 pos, Quaternion rot)
        {
            var car = new GameObject("Tico");
            car.transform.SetParent(root, false);
            car.transform.localPosition = pos;
            car.transform.localRotation = rot;
            var body = new Color(0.82f, 0.82f, 0.78f);
            Graybox.Box(car.transform, "Body", new Vector3(0, 0.65f, 0), new Vector3(1.5f, 0.8f, 3.3f), body);
            Graybox.Box(car.transform, "Cabin", new Vector3(0, 1.3f, -0.2f), new Vector3(1.4f, 0.6f, 1.9f), new Color(0.35f, 0.45f, 0.5f));
            for (int i = 0; i < 4; i++)
            {
                var w = Graybox.Prim(PrimitiveType.Cylinder, car.transform, "Wheel",
                    new Vector3(i % 2 == 0 ? -0.75f : 0.75f, 0.3f, i < 2 ? 1.05f : -1.05f), new Vector3(0.55f, 0.12f, 0.55f), Color.black, false);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            var label = Graybox.Label(car.transform, "티코", new Vector3(0, 2.1f, 0), 0.06f, Color.white);
            label.gameObject.AddComponent<Billboard>();

            // Synty 소형차를 티코로 쓴다. 판정은 그레이박스 상자 그대로
            var art = ArtCatalog.Instance;
            var bodyPrefab = art != null ? (art.ticoBody != null ? art.ticoBody : art.tico) : null;
            if (bodyPrefab != null)
            {
                foreach (var r in car.GetComponentsInChildren<MeshRenderer>())
                    if (r.GetComponent<TextMesh>() == null) r.enabled = false;
                // 돌아간 상태로 맞추면 AABB가 커지니, 정면을 본 상태에서 맞추고 다시 돌린다
                var savedRot = car.transform.rotation;
                car.transform.rotation = Quaternion.identity;
                var inst = ArtCatalog.PlaceFitted(bodyPrefab, car.transform, car.transform.position + Vector3.up * 0.2f, Quaternion.identity, new Vector3(1.7f, 1.6f, 3.5f), out _);
                inst.name = "Art_Tico";
                car.transform.rotation = savedRot;
            }
            return car;
        }

        static void ApplyFlood(Transform root, WorldSpec spec)
        {
            if (spec.terrain != TerrainMod.Flood) return;
            var water = Graybox.Box(root, "Floodwater", new Vector3(0, 0.25f, 0), new Vector3(Half * 2, 0.5f, Half * 2), new Color(0.2f, 0.35f, 0.4f, 0.55f), false);
            water.layer = 2;
        }

        /// <summary>뷰로 표지판. 규칙 힌트 역할 (brainstorm-03 7장). 오래되어 틀릴 수도 있다.</summary>
        public static void PlaceBureauSign(Transform root, WorldSpec spec, Vector3 pos)
        {
            string warning = spec.rules.HasFlag(WorldRule.SoundReactive) ? "소음 금지 구역"
                : spec.rules.HasFlag(WorldRule.ToxicGas) ? "저지대 가스 고임 주의"
                : spec.rules.HasFlag(WorldRule.EternalNight) ? "등화 관제 구역"
                : "출입 민원인 유의";
            Graybox.Box(root, "SignPost", pos + new Vector3(0, 1.1f, 0), new Vector3(0.12f, 2.2f, 0.12f), new Color(0.3f, 0.3f, 0.3f));
            Graybox.Box(root, "SignBoard", pos + new Vector3(0, 2.2f, 0), new Vector3(2.4f, 1.3f, 0.06f), new Color(0.85f, 0.83f, 0.75f), false);
            var t = Graybox.Label(root, $"{spec.Title}\n{warning}\nDIMENSION BUREAU", pos + new Vector3(0, 2.2f, -0.05f), 0.035f, new Color(0.15f, 0.15f, 0.2f));
            t.transform.rotation = Quaternion.identity;
        }

        static void CollectSpawnPoints(MissionLayout layout, WorldSpec spec)
        {
            var rng = new System.Random(spec.number * 13 + 7);
            int tries = 0;
            while (layout.spawnPoints.Count < 40 && tries++ < 500)
            {
                var p = new Vector3((float)rng.NextDouble() * Half * 2 - Half, 0, (float)rng.NextDouble() * Half * 2 - Half);
                if (Vector3.Distance(p, layout.playerSpawn) < 30f) continue;
                if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas))
                    layout.spawnPoints.Add(hit.position);
            }
            // 덩치는 목표 근처에 자리 잡는다
            for (int i = 0; i < 6; i++)
            {
                var p = layout.objectivePos + new Vector3((float)rng.NextDouble() * 16 - 8, 0, (float)rng.NextDouble() * 16 - 8);
                if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas))
                    layout.bruteSpawnPoints.Add(hit.position);
            }
            if (layout.bruteSpawnPoints.Count == 0) layout.bruteSpawnPoints.Add(layout.objectivePos);
        }
    }
}
