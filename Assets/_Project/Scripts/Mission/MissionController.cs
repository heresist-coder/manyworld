using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// 콜 한 건의 진행. 진입 → 중도금 지점 → 목표 → 티코로 귀환 (or 철수 / 전멸).
    /// </summary>
    public class MissionController : MonoBehaviour
    {
        public static MissionController Instance { get; private set; }

        public CallContract Contract { get; private set; }
        public WorldSpec Spec => Contract.spec;
        public WorldState State { get; private set; }
        public CallLedger Ledger { get; } = new CallLedger();
        public Evolution Evolution { get; } = new Evolution();
        public MissionLayout Layout { get; private set; }
        public PlayerController Player { get; private set; }
        public Companion Companion { get; private set; }
        public TicoCar Car { get; private set; }
        public bool PlayerInCar => Player != null && Player.InVehicle;
        float engineNoiseTimer;
        public readonly List<Monster> Monsters = new List<Monster>();
        public readonly List<Health> Targets = new List<Health>();

        public bool Paused { get; private set; }
        public bool Ended { get; private set; }
        public bool InputEnabled => !Paused && !Ended;
        public bool SimRunning => !Paused && !Ended;
        public bool MidReached => Ledger.midReached;
        public bool ObjectiveDone => Ledger.objectiveDone;
        public float Elapsed { get; private set; }
        public float FilterLeft { get; private set; }
        public WorldRule Discovered { get; private set; }

        // 상호작용 (E 길게 누르기)
        public string Prompt { get; private set; }
        public float HoldProgress { get; private set; }
        string holdKey;
        float holdTime;

        public int PlayerKnockouts { get; private set; }
        public int CompanionKnockouts { get; private set; }

        public readonly List<(string text, float time)> Toasts = new List<(string, float)>();

        // 은주의 마이마이에 녹음되는 소리들 (카세트 단서)
        readonly List<(float time, string text)> audible = new List<(float, string)>();
        public readonly List<KnockoutCase> NewCases = new List<KnockoutCase>();
        public readonly List<GasPocket> GasPockets = new List<GasPocket>();
        public float LastPlayerShotTime { get; private set; } = -100f;

        // 사인 도감이 해금한 경고
        public string CodexWarning { get; private set; }
        public bool ThreatBehind { get; private set; }
        float warnTimer;
        AudioSource heartbeat;
        AudioLowPassFilter lowPass;

        GameObject worldRoot;
        Light sun;
        float gasTick, wipeTimer = -1f;
        const float FilterDuration = 90f;

        public static MissionController Begin(CallContract contract, WorldState state, PlayerCamera cam, bool companionFocused = false)
        {
            var go = new GameObject("Mission");
            var mc = go.AddComponent<MissionController>();
            Instance = mc;
            mc.Setup(contract, state, cam);
            if (companionFocused) mc.Companion.GiveFocus(120f);
            go.AddComponent<MissionHUD>();
            return mc;
        }

        void Setup(CallContract contract, WorldState state, PlayerCamera cam)
        {
            Contract = contract;
            State = state;
            Noise.Reset();
            Ledger.depositPaid = true;

            Layout = WorldTerrainBuilder.Build(Spec);
            worldRoot = Layout.root.gameObject;
            Layout.root.SetParent(transform, true);

            // 중도금 지점: 교차로의 깃대
            var flag = Graybox.Box(Layout.root, "MidpointPole", Layout.midpointPos + new Vector3(0, 2.5f, 0), new Vector3(0.15f, 5f, 0.15f), new Color(0.9f, 0.9f, 0.9f), false);
            Graybox.Box(flag.transform, "Flag", new Vector3(2.5f, 0.4f, 0), new Vector3(8f, 0.12f, 0.1f), new Color(0.9f, 0.3f, 0.2f), false);
            var midLabel = Graybox.Label(Layout.root, "중도금 지점", Layout.midpointPos + new Vector3(0, 5.6f, 0), 0.06f, UIStyle.Amber);
            midLabel.gameObject.AddComponent<Billboard>();

            // 목표: 측량 말뚝
            var stake = Graybox.Box(Layout.root, "Objective", Layout.objectivePos + new Vector3(0, 0.6f, 0), new Vector3(0.3f, 1.2f, 0.3f), new Color(1f, 0.85f, 0.2f), false);
            stake.GetComponent<Renderer>().sharedMaterial = Graybox.Mat(new Color(1f, 0.8f, 0.2f), true);
            var objLabel = Graybox.Label(Layout.root, Contract.task, Layout.objectivePos + new Vector3(0, 2.2f, 0), 0.045f, UIStyle.Amber);
            objLabel.gameObject.AddComponent<Billboard>();

            Player = PlayerController.Create(Layout.playerSpawn, cam);
            Player.transform.SetParent(transform, true);
            Player.health.Downed += OnPlayerDowned;
            Player.health.Revived += () => SetDownedAudio(false);
            // 티코: 세계 안에서도 몬다 (brainstorm-01 10장). 중도금 지점 쪽 길을 본다
            var toMid = Layout.midpointPos - Layout.ticoPos;
            toMid.y = 0;
            Car = TicoCar.Create(transform, Layout.ticoPos, Quaternion.LookRotation(toMid), false);
            Car.controllable = false;

            Companion = Companion.Spawn(Layout.companionSpawn);
            Companion.transform.SetParent(transform, true);
            Companion.health.Downed += _ =>
            {
                CompanionKnockouts++;
                LogAudible("(은주가 쓰러지는 소리. 워크맨이 바닥에 구른다)");
            };
            Targets.Add(Player.health);
            Targets.Add(Companion.health);

            ApplyRules();
            SpawnMonsters();
            SpawnGasPockets();

            Sfx.Loop("ambience_world", transform, 0.55f, false).pitch = 0.85f + (Spec.number % 7) * 0.04f;
            lowPass = cam.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = cam.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
            Sfx.Play("gate_whoosh", 0.8f);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Toast($"{Spec.Title} 진입. 공사 추정 위험도 {Spec.estimatedDanger}");
            Toast("티코 옆에서 E: 타기 · 중도금 지점은 길 따라 북동쪽 장터거리");
            cam.GetComponent<Camera>().farClipPlane = 1000f;
            Toast($"{Companion.Name}: \"{EntryLine()}\"");
        }

        string EntryLine()
        {
            if (Spec.rules.HasFlag(WorldRule.ToxicGas)) return "…이 냄새, 그날 사격장이랑 똑같아요.";
            if (Spec.rules.HasFlag(WorldRule.SoundReactive)) return "여기선 볼륨 0이에요. 이어폰만 낄게요.";
            if (Spec.rules.HasFlag(WorldRule.EternalNight)) return "어둡네요. 불 켜시면… 저쪽도 우릴 봐요.";
            if (Contract.grade == CallGrade.Skull) return "해골 콜이네요. …머리가 크면, 맞히기는 쉬워요.";
            return "한 발이면 돼요. 두 발 쏘면 기사님 돈이 아깝잖아요.";
        }

        void ApplyRules()
        {
            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, false);
            sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(28f, -35f, 0f); // 겨울 오후의 비스듬한 빛
            sun.color = new Color(0.82f, 0.88f, 1f);
            sun.intensity = 1.1f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Spec.skyTint * 0.9f;
            RenderSettings.fog = false;
            var cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Spec.skyTint;

            if (Spec.terrain == TerrainMod.Fog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Spec.skyTint;
                RenderSettings.fogDensity = 0.025f;
            }
            if (Spec.rules.HasFlag(WorldRule.ToxicGas))
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.66f, 0.6f, 0.3f);
                RenderSettings.fogDensity = 0.04f;
                cam.backgroundColor = RenderSettings.fogColor;
                sun.color = new Color(1f, 0.9f, 0.55f);
                FilterLeft = FilterDuration;
                Ledger.filtersUsed = 1;
                Ledger.Charge("방독면 필터", CallLedger.FilterCost);
            }
            if (Spec.rules.HasFlag(WorldRule.EternalNight))
            {
                sun.intensity = 0.04f;
                sun.color = new Color(0.4f, 0.45f, 0.7f);
                RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.1f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.02f);
                RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.03f);
                cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
                Toast("F: 손전등");
            }
            if (Spec.rules.HasFlag(WorldRule.SoundReactive)) Noise.WorldMultiplier = 2.5f;

            // 볼류메트릭 안개(있으면). 거리 안개는 겹치지 않게 절반으로
            if (VolumetricAtmosphere.Create(transform, new Vector3(WorldTerrainBuilder.Size / 2, 0, WorldTerrainBuilder.Size / 2), WorldTerrainBuilder.Size + 80f, VolumetricAtmosphere.ForWorld(Spec), sun) != null)
                RenderSettings.fogDensity *= 0.5f;
            // 침수는 지형의 수위로 표현한다 (물에 들어가면 느려진다)
        }

        void SpawnMonsters()
        {
            var rng = new System.Random(Spec.number * 31 + State.generation * 7 + State.visits);
            int crawlers = Mathf.Min(State.crawlers.Count, Mathf.RoundToInt(Spec.monsterCount * Spec.crawlerRatio));
            int brutes = Mathf.Min(State.brutes.Count, Mathf.Max(1, Spec.monsterCount - crawlers));

            var pool = new List<Genome>(State.crawlers);
            var points = new List<Vector3>(Layout.spawnPoints);
            for (int i = 0; i < crawlers && pool.Count > 0 && points.Count > 0; i++)
            {
                var g = pool[rng.Next(pool.Count)];
                pool.Remove(g);
                var p = points[rng.Next(points.Count)];
                points.Remove(p);
                Monsters.Add(Monster.Spawn(Species.Crawler, g, p, transform));
            }
            var bpool = new List<Genome>(State.brutes);
            for (int i = 0; i < brutes && bpool.Count > 0; i++)
            {
                var g = bpool[rng.Next(bpool.Count)];
                bpool.Remove(g);
                var p = Layout.bruteSpawnPoints[i % Layout.bruteSpawnPoints.Count];
                Monsters.Add(Monster.Spawn(Species.Brute, g, p, transform));
            }
        }

        public bool KnowsCause(string id) => GameManager.Instance != null && GameManager.Instance.Save.HasCodex(id);

        /// <summary>녹음기에 남을 만한 소리. 기절 순간 직전 것들이 테이프가 된다.</summary>
        public void LogAudible(string text)
        {
            audible.Add((Elapsed, text));
            if (audible.Count > 40) audible.RemoveAt(0);
        }

        public void OnPlayerShot()
        {
            LastPlayerShotTime = Time.time;
            LogAudible("(탕)");
            if (Spec.rules.HasFlag(WorldRule.SoundReactive) && KnowsCause("gunfire"))
            {
                float r = 35f * Noise.WorldMultiplier;
                int n = 0;
                foreach (var m in Monsters)
                    if (m != null && !m.health.IsDown && Vector3.Distance(m.transform.position, Player.transform.position) < r) n++;
                if (n > 0) Toast($"[사인 #06] 총성 반경 {r:0}m — 들을 수 있는 개체 {n}마리");
            }
        }

        void OnPlayerDowned(DamageInfo info)
        {
            PlayerKnockouts++;
            Toast("기사가 쓰러졌다… (기억이 흐려진다)");
            SetDownedAudio(true);
            if (NewCases.Count >= 2) return;

            var causeId = DeathCauses.Diagnose(this, info);
            var cause = DeathCauses.Get(causeId);
            var save = GameManager.Instance.Save;
            var kc = new KnockoutCase { id = save.nextCaseId++, worldNumber = Spec.number, day = save.day, causeId = causeId };
            kc.traces.Add(cause.trace);
            kc.traces.Add($"쓰러진 곳: {PlaceName(Player.transform.position)}. 진입 {Mathf.FloorToInt(Elapsed / 60)}분 {Mathf.FloorToInt(Elapsed % 60)}초 뒤.");
            if (Player.FlashlightOn) kc.traces.Add("손전등은 켜져 있었다.");
            if (Time.time - LastPlayerShotTime < 15f) kc.traces.Add($"총구가 아직 따뜻했다. 카빈 {Ledger.playerShots}발 소모.");

            // 은주가 서 있었다면 마이마이에 녹음이 남는다
            kc.hasTape = !Companion.health.IsDown;
            if (kc.hasTape)
            {
                float t0 = Elapsed - 25f;
                foreach (var (time, text) in audible)
                    if (time >= t0) kc.tape.Add($"[{Clock(time)}] {text}");
                while (kc.tape.Count > 6) kc.tape.RemoveAt(0);
                kc.tape.Add($"[{Clock(Elapsed)}] {cause.tapeLine}");
                kc.tape.Add($"[{Clock(Elapsed + 2f)}] 은주: \"기사님? …기사님!\"");
                kc.tape.Add("(치직─ 녹음 끝)");
            }
            kc.choices = DeathCauses.Choices(causeId, Spec, new System.Random(kc.id * 97 + Spec.number));
            NewCases.Add(kc);
        }

        string PlaceName(Vector3 p)
        {
            if (Vector3.Distance(p, Layout.midpointPos) < 15f) return "장터거리 교차로";
            if (Vector3.Distance(p, Layout.objectivePos) < 25f) return "북동쪽 측량 현장";
            if (Vector3.Distance(p, Layout.gatePos) < 30f) return "게이트 공터";
            string best = "숲속";
            float bd = 50f;
            foreach (var kv in Layout.landmarks)
            {
                float d = Vector3.Distance(p, kv.Value);
                if (d < bd) { bd = d; best = $"{kv.Key} 근처"; }
            }
            return best;
        }

        static string Clock(float t)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(t));
            return $"{s / 60:00}:{s % 60:00}";
        }

        void SetDownedAudio(bool down)
        {
            if (lowPass != null) lowPass.cutoffFrequency = down ? 650f : 22000f;
            if (down && heartbeat == null) heartbeat = Sfx.Loop("heartbeat", transform, 0.9f, false);
            if (!down && heartbeat != null) { Destroy(heartbeat.gameObject); heartbeat = null; }
        }

        void SpawnGasPockets()
        {
            if (!Spec.rules.HasFlag(WorldRule.ToxicGas)) return;
            var rng = new System.Random(Spec.number * 53 + 9);
            int count = 4 + Spec.trueDanger / 2;
            bool known = KnowsCause("gaspocket");
            // 저지대 가스 고임: 낮은 곳부터 고른다
            var candidates = new List<Vector3>(Layout.spawnPoints);
            candidates.Sort((a, b) => a.y.CompareTo(b.y));
            if (candidates.Count > count * 2) candidates.RemoveRange(count * 2, candidates.Count - count * 2);
            while (GasPockets.Count < count && candidates.Count > 0)
            {
                var p = candidates[rng.Next(candidates.Count)];
                candidates.Remove(p);
                if (Vector3.Distance(p, Layout.midpointPos) < 8f || Vector3.Distance(p, Layout.objectivePos) < 8f || Vector3.Distance(p, Layout.playerSpawn) < 18f)
                    continue;
                GasPockets.Add(GasPocket.Create(transform, p, 3.5f + (float)rng.NextDouble() * 2.5f, known));
            }
        }

        void UpdateWarnings()
        {
            warnTimer -= Time.deltaTime;
            if (warnTimer > 0f) return;
            warnTimer = 0.2f;
            CodexWarning = null;
            ThreatBehind = false;
            if (Player.IsDown) return;

            var pp = Player.transform.position;
            var look = Quaternion.Euler(0, Player.cam.yaw, 0) * Vector3.forward;
            int packCount = 0;
            bool anyHunting = false;
            foreach (var m in Monsters)
            {
                if (m == null || m.health.IsDown || !m.IsHunting) continue;
                anyHunting = true;
                var to = m.transform.position - pp;
                to.y = 0;
                if (KnowsCause("behind") && to.magnitude < 10f && Vector3.Angle(look, to) > 110f) ThreatBehind = true;
                if (m.species == Species.Crawler && to.magnitude < 14f) packCount++;
            }
            if (KnowsCause("pack") && packCount >= 3) CodexWarning = $"[사인 #02] 포위 주의 — 기는 놈 {packCount}마리";
            if (KnowsCause("light") && Spec.rules.HasFlag(WorldRule.EternalNight) && (Player.FlashlightOn || Car.HeadlightsOn) && anyHunting)
                CodexWarning = "[사인 #05] 불빛을 보고 온다 — F로 끄기";
            if (KnowsCause("gaspocket"))
                foreach (var g in GasPockets)
                    if (Vector3.Distance(new Vector3(pp.x, g.transform.position.y, pp.z), g.transform.position) < g.radius + 3f)
                        CodexWarning = "[사인 #04] 가스 고임 — 들어가지 마라";
        }

        public void OnMonsterKilled(Monster m, DamageInfo info)
        {
            if (info.source == DamageSource.Companion && m.species == Species.Brute)
                Toast($"{Companion.Name}: \"됐어요. 한 발이에요.\"");
        }

        public void Toast(string text)
        {
            Toasts.Add((text, Time.time));
            if (Toasts.Count > 6) Toasts.RemoveAt(0);
        }

        void Discover(WorldRule rule, string text)
        {
            if (Discovered.HasFlag(rule)) return;
            Discovered |= rule;
            Toast($"[세계 수첩] 확인: {text}");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !Ended) SetPaused(!Paused);
            if (!SimRunning)
            {
                if (wipeTimer >= 0f) UpdateWipe();
                return;
            }

            Elapsed += Time.deltaTime;
            if (PlayerInCar)
            {
                // 엔진 소리 = 소음. 소리 반응 세계에선 차가 몬스터를 부른다
                engineNoiseTimer -= Time.deltaTime;
                if (engineNoiseTimer <= 0f)
                {
                    engineNoiseTimer = 0.5f;
                    Noise.Emit(Car.transform.position, 10f + Car.SpeedKmh * 0.4f);
                    // 영원한 밤: 전조등 불빛 = 어그로. 멀리서 보고 몰려온다
                    if (Car.HeadlightsOn && Spec.rules.HasFlag(WorldRule.EternalNight))
                        Noise.Emit(Car.transform.position + Car.transform.forward * 15f, 45f / Noise.WorldMultiplier);
                }
            }
            UpdateRules();
            UpdateWarnings();
            UpdateInteractions(kb);
            CheckEnd();
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            Cursor.lockState = p ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = p;
        }

        void UpdateRules()
        {
            if (Spec.rules.HasFlag(WorldRule.ToxicGas))
            {
                FilterLeft -= Time.deltaTime;
                if (FilterLeft <= 0f)
                {
                    // 필터는 자동 교체. 돈이 든다
                    FilterLeft = FilterDuration;
                    Ledger.filtersUsed++;
                    Ledger.Charge("방독면 필터", CallLedger.FilterCost);
                    Toast("방독면 필터 교체 (-₩4,000)");
                }
                gasTick += Time.deltaTime;
                if (gasTick > 8f) Discover(WorldRule.ToxicGas, "독가스 (필터 소모)");
            }
            if (Spec.rules.HasFlag(WorldRule.EternalNight) && Elapsed > 3f) Discover(WorldRule.EternalNight, "영원한 밤 (빛 = 어그로)");
            if (Spec.rules.HasFlag(WorldRule.SoundReactive) && Ledger.playerShots + Ledger.companionShots > 0)
                Discover(WorldRule.SoundReactive, "소리 반응 (총소리가 멀리 퍼진다)");

            if (!MidReached && !Player.IsDown && Vector3.Distance(Player.transform.position, Layout.midpointPos) < 5f)
            {
                Ledger.midReached = true;
                Toast($"중도금 지점 도착 — 중도금 {UIStyle.Won(Contract.midPayment)} 확정");
            }
        }

        void UpdateInteractions(Keyboard kb)
        {
            Prompt = null;
            string key = null;
            float need = 0f;
            System.Action onDone = null;

            if (PlayerInCar)
            {
                bool atGate = Vector3.Distance(Car.transform.position, Layout.gatePos) < 16f;
                if (atGate && ObjectiveDone) { key = "return"; need = 1f; Prompt = "E: 게이트로 귀환"; onDone = () => Finish(CallOutcome.Success); }
                else if (Car.SpeedKmh < 8f) { key = "exit"; need = 0.05f; Prompt = "E: 내리기"; onDone = ExitCar; }
            }
            else if (!Player.IsDown)
            {
                var pp = Player.transform.position;
                bool nearCar = Vector3.Distance(pp, Car.transform.position) < 3.8f;
                if (nearCar && Car.IsFlipped)
                {
                    key = "unflip"; need = 2.5f; Prompt = "E 길게: 티코 밀어 세우기 (가벼워서 둘이면 된다)";
                    onDone = () => { Car.Unflip(); Toast("영차─ 티코를 세웠다"); };
                }
                else if (Companion.health.IsDown && Vector3.Distance(pp, Companion.transform.position) < 2f)
                {
                    key = "revive"; need = 3f; Prompt = $"E 길게: {Companion.Name} 깨우기 (각성제 ₩3,000)";
                    onDone = () =>
                    {
                        Companion.health.Revive(0.4f);
                        Ledger.Charge("각성제", 3000);
                    };
                }
                else if (!ObjectiveDone && Vector3.Distance(pp, Layout.objectivePos) < 2.8f)
                {
                    key = "objective"; need = 2.5f; Prompt = $"E 길게: {Contract.task}";
                    onDone = () =>
                    {
                        Ledger.objectiveDone = true;
                        Toast("목표 확보! 티코로 돌아가자 — 잔금은 귀환해야 받는다");
                    };
                }
                else if (Vector3.Distance(pp, Layout.gatePos) < 14f && ObjectiveDone)
                {
                    key = "return"; need = 1f; Prompt = "E: 게이트로 귀환";
                    onDone = () => Finish(CallOutcome.Success);
                }
                else if (nearCar)
                {
                    key = "enter"; need = 0.05f; Prompt = "E: 티코 타기";
                    onDone = EnterCar;
                }
                else if (Vector3.Distance(pp, Layout.gatePos) < 14f)
                {
                    key = "withdraw"; need = 2f;
                    Prompt = MidReached ? "E 길게: 철수 (계약금 + 중도금만)" : "E 길게: 철수 (계약금만)";
                    onDone = () => Finish(CallOutcome.Withdrawn);
                }
            }

            if (key != null && kb != null && kb.eKey.isPressed)
            {
                if (holdKey != key) { holdKey = key; holdTime = 0f; }
                holdTime += Time.deltaTime;
                if (key == "objective" && Mathf.Repeat(holdTime, 0.5f) < Time.deltaTime) Noise.Emit(Layout.objectivePos, 10f);
                HoldProgress = holdTime / need;
                if (holdTime >= need)
                {
                    holdKey = null;
                    holdTime = 0f;
                    HoldProgress = 0f;
                    onDone?.Invoke();
                }
            }
            else
            {
                holdKey = null;
                holdTime = 0f;
                HoldProgress = 0f;
            }

            // 동료 단독 진행: 은주가 목표를 줍고 티코로 돌아오면 콜 성공
            if (Player.IsDown && Companion.order == CompanionOrder.GoAhead && !Companion.health.IsDown)
            {
                if (!ObjectiveDone && Vector3.Distance(Companion.transform.position, Layout.objectivePos) < 2.5f)
                {
                    Ledger.objectiveDone = true;
                }
                else if (ObjectiveDone && Vector3.Distance(Companion.transform.position, Layout.gatePos) < 14f)
                {
                    Finish(CallOutcome.CompanionSolo);
                }
            }
        }

        public void EnterCar()
        {
            bool takeCompanion = !Companion.health.IsDown && Vector3.Distance(Companion.transform.position, Car.transform.position) < 15f;
            Player.SetInVehicle(true, Car.transform, Vector3.zero);
            Car.controllable = true;
            Sfx.Play("car_door", 0.8f);
            if (takeCompanion)
            {
                Companion.SetInVehicle(true, Vector3.zero, Car.transform);
                Toast($"{Companion.Name}: \"이 곡 끝나기 전에 도착하죠.\"");
            }
            else Toast($"{Companion.Name}이(가) 아직 안 탔다 (가까이 와야 같이 탄다)");
            if (Spec.rules.HasFlag(WorldRule.EternalNight)) Toast("L: 전조등 (빛 = 어그로)");
        }

        public void ExitCar()
        {
            var t = Car.transform;
            var left = t.position - t.right * 2.2f + Vector3.up * 0.5f;
            var right = t.position + t.right * 2.2f + Vector3.up * 0.5f;
            if (Layout.terrain != null)
            {
                left.y = Layout.terrain.SampleHeight(left) + 0.1f;
                right.y = Layout.terrain.SampleHeight(right) + 0.1f;
            }
            Car.controllable = false;
            Car.Stop();
            Player.SetInVehicle(false, Car.transform, left);
            Player.cam.yaw = t.eulerAngles.y;
            if (Companion.InVehicle) Companion.SetInVehicle(false, right);
            Sfx.Play("car_door", 0.8f);
        }

        void CheckEnd()
        {
            if (Player.IsDown && Companion.health.IsDown && wipeTimer < 0f)
            {
                Ended = true;
                wipeTimer = 0f;
                Toast("…");
                Sfx.Play("typewriter", 0.8f, 0f);
            }
        }

        void UpdateWipe()
        {
            float before = wipeTimer;
            wipeTimer += Time.unscaledDeltaTime;
            if (before < 1.9f && wipeTimer >= 1.9f) Sfx.Play("stamp", 1f, 0f);
            if (wipeTimer > 2.5f)
            {
                wipeTimer = -1f;
                Finish(CallOutcome.Wiped);
            }
        }

        public float WipeFade => wipeTimer < 0f ? 0f : Mathf.Clamp01(wipeTimer / 2f);

        public void Finish(CallOutcome outcome)
        {
            Ended = true;
            Time.timeScale = 1f;
            var s = new Settlement
            {
                contract = Contract,
                outcome = outcome,
                elapsed = Elapsed,
                playerKnockouts = PlayerKnockouts,
                companionKnockouts = CompanionKnockouts,
            };

            int income = Contract.deposit;
            s.lines.Add($"계약금|+{Contract.deposit:N0}");
            if (MidReached)
            {
                income += Contract.midPayment;
                s.lines.Add($"중도금|+{Contract.midPayment:N0}");
            }
            else s.lines.Add("중도금 (미도달)|0");
            if (outcome == CallOutcome.Success || outcome == CallOutcome.CompanionSolo)
            {
                income += Contract.balance;
                s.lines.Add($"잔금|+{Contract.balance:N0}");
            }
            else s.lines.Add("잔금 (미지급)|0");

            s.income = income;
            s.shotCost = Ledger.ShotSpend;
            s.filterCost = Ledger.filtersUsed * CallLedger.FilterCost;
            int other = Ledger.Spent - s.shotCost - s.filterCost;
            s.companionShare = Mathf.RoundToInt(income * 0.25f / 100f) * 100; // 지분 계약 25%

            s.lines.Add($"카빈 일반탄 {Ledger.playerShots}발 × {CallLedger.PlayerShotCost}|-{Ledger.playerShots * CallLedger.PlayerShotCost:N0}");
            s.lines.Add($"은주 경기탄 {Ledger.companionShots}발 × {CallLedger.CompanionShotCost}|-{Ledger.companionShots * CallLedger.CompanionShotCost:N0}");
            if (s.filterCost > 0) s.lines.Add($"방독면 필터 {Ledger.filtersUsed}개|-{s.filterCost:N0}");
            if (other > 0) s.lines.Add($"각성제|-{other:N0}");
            s.lines.Add($"{Companion.Name} 지분 (25%)|-{s.companionShare:N0}");

            s.net = income - Ledger.Spent - s.companionShare;

            // 기절 사건 → 사인 분석. 이미 도감에 있는 사인이면 바로 풀린다
            var save = GameManager.Instance.Save;
            foreach (var kc in NewCases)
            {
                if (save.HasCodex(kc.causeId))
                {
                    kc.solved = true;
                    kc.autoSolved = true;
                    save.UnlockCodex(kc.causeId, kc.worldNumber);
                }
                save.cases.Add(kc);
            }
            s.newCases = NewCases.Count;

            // 세계 수첩 & 진화
            State.knownRules |= (int)Discovered;
            State.visits++;
            GameManager.Instance.Save.day++;
            s.notice = Evolution.Advance(State, Spec, GameManager.Instance.Save);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameManager.Instance.EndMission(s);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            Noise.Reset();
            RenderSettings.fog = false;
            if (lowPass != null) lowPass.cutoffFrequency = 22000f;
        }
    }
}
