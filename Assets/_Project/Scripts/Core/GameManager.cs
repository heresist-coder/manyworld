using UnityEngine;

namespace Manyworld
{
    public enum GameMode { Hub, Drive, Mission, Bureau, Settlement }

    /// <summary>
    /// 게임 흐름: 기사식당(허브) → 동네 운전 → 콜 → 정산 / 전멸 시 디멘션 뷰로 → 정산 → 기사식당.
    /// 씬 하나에서 모드를 바꿔 가며 필요한 것만 코드로 짓는다 (그레이박스 프로토타입).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public SaveData Save { get; private set; }
        public GameMode Mode { get; private set; }
        public Settlement LastSettlement { get; private set; }
        public PlayerCamera Cam { get; private set; }

        HubScreen hub;
        SettlementScreen settlementScreen;
        BureauScreen bureau;
        GameObject homeTown;
        MissionLayout homeLayout;
        MissionController mission;
        DriveSession drive;
        AudioSource homeAmbience;
        Light homeSun;
        GameObject homeFog;
        public WeatherState HomeWeather { get; private set; }
        float orbit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (FindFirstObjectByType<GameManager>() == null)
                new GameObject("GameManager").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 120;
            Save = SaveData.Load();
            Sfx.Init();
            homeAmbience = Sfx.Loop("ambience_home", transform, 0.5f, false);

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Cam = camGo.GetComponent<PlayerCamera>();
            if (Cam == null) Cam = camGo.AddComponent<PlayerCamera>();
            camGo.GetComponent<Camera>().nearClipPlane = 0.1f;
            camGo.GetComponent<Camera>().farClipPlane = 400f;

            // 씬에 남아 있는 기본 조명은 쓰지 않는다
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);

            hub = gameObject.AddComponent<HubScreen>();
            settlementScreen = gameObject.AddComponent<SettlementScreen>();
            bureau = gameObject.AddComponent<BureauScreen>();
            // 자연 월드(숲 현장)로 시작한다. 예전 흐름(기사식당·멀티버스 콜)은 F12
            StartField();
        }

        public bool InField => mission != null && mission.Field;

        /// <summary>숲 현장의 회화풍 후처리 (F9).</summary>
        public static bool PaintFilter = true;

        public void ApplyPaint() => Shader.SetGlobalFloat("_ManyworldPaint", PaintFilter && InField ? 1f : 0f);

        /// <summary>숲 현장: 차 옆에서 시작해 현장 → 차 → 콜 대기. note는 시작 무전 한 줄.</summary>
        public void StartField(string note = null)
        {
            if (drive != null) Destroy(drive.gameObject);
            drive = null;
            DropMission();
            if (homeTown != null) homeTown.SetActive(false);
            Time.timeScale = 1f;
            Mode = GameMode.Mission;
            SetScreens();
            ForestLighting.Load(); // 구운 간접광
            mission = MissionController.BeginField(Cam);
            ApplyPaint();
            if (note != null) mission.Toast(note);
        }

        /// <summary>진행 중인 현장을 치운다. 비활성화를 먼저 해서 새 월드의 네비메시에 섞이지 않게 한다.</summary>
        void DropMission()
        {
            if (mission == null) return;
            if (mission.Field) ForestLighting.Unload();
            Shader.SetGlobalFloat("_ManyworldPaint", 0f);
            Shader.SetGlobalVector("_ColorMapParams", Vector4.zero); // 풀 컬러맵 끄기
            mission.gameObject.SetActive(false);
            Destroy(mission.gameObject);
            mission = null;
            Cam.target = null;
        }

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard?.mKey.wasPressedThisFrame == true) Sfx.Muted = !Sfx.Muted;
            if (keyboard?.f9Key.wasPressedThisFrame == true)
            {
                PaintFilter = !PaintFilter; // 회화풍 후처리 켜고 끄기
                ApplyPaint();
            }
            if (keyboard?.f10Key.wasPressedThisFrame == true) ScreenSpaceGI.Toggle(); // 화면 공간 간접광 켜고 끄기
            if (keyboard?.f12Key.wasPressedThisFrame == true)
            {
                if (InField) EnterHub();          // 개발용: 예전 흐름
                else if (Mode == GameMode.Hub) StartField();
            }

            if (Mode == GameMode.Hub || Mode == GameMode.Settlement || Mode == GameMode.Bureau)
            {
                // 허브 배경: 우리 동네를 천천히 내려다본다
                orbit += Time.deltaTime * 3f;
                var t = Cam.transform;
                var pos = Quaternion.Euler(0, orbit, 0) * new Vector3(0, 38, -70);
                t.position = pos;
                t.LookAt(new Vector3(0, 0, 0));
            }
        }

        public void EnterHub()
        {
            DropMission();
            Time.timeScale = 1f;
            Mode = GameMode.Hub;
            SetScreens();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            BuildHomeTown();
            Sfx.Play("radio_call", 0.5f); // 띠리릭─ 다세계 콜입니다
        }

        void BuildHomeTown()
        {
            if (homeTown != null) { homeTown.SetActive(true); ApplyHomeLighting(); return; }
            var layout = TownBuilder.Build(null, true);
            homeLayout = layout;
            homeTown = layout.root.gameObject;
            var sunGo = new GameObject("HomeSun");
            sunGo.transform.SetParent(homeTown.transform, false);
            homeSun = sunGo.AddComponent<Light>();
            homeSun.type = LightType.Directional;
            homeSun.shadows = LightShadows.Soft;
            homeSun.transform.rotation = Quaternion.Euler(22f, -60f, 0);
            homeSun.color = new Color(1f, 0.82f, 0.62f); // 노을빛
            homeSun.intensity = 1.3f;
            ApplyHomeLighting();
        }

        void ApplyHomeLighting()
        {
            RenderSettings.fog = false;
            var cam = Cam.GetComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.farClipPlane = 600f;
            Cam.target = null;
            // 우리 동네: 따뜻하고 바랜 톤. 날씨와 시간대는 날짜마다 다르다
            HomeWeather = WeatherState.ForHome(Save.day, Save.Date.Month);
            WorldLook.Apply(HomeWeather.Modify(WorldLook.Home), homeSun, homeTown.transform, cam);
            if (homeFog != null) Destroy(homeFog);
            homeFog = VolumetricAtmosphere.Create(homeTown.transform, Vector3.zero, 150f, HomeWeather.Modify(VolumetricAtmosphere.Home), homeSun);
            HomeWeather.AttachFx(homeTown.transform);
        }

        /// <summary>콜 수락 → 기사식당 앞에서 티코를 몰고 게이트까지.</summary>
        public void BeginDrive(CallContract contract)
        {
            BuildHomeTown();
            Mode = GameMode.Drive;
            SetScreens();
            drive = DriveSession.Begin(contract, homeLayout, Cam.transform);
        }

        /// <summary>동네 일 나가기: 배달하다 무전 콜을 받는다.</summary>
        public void BeginDaily()
        {
            BuildHomeTown();
            Mode = GameMode.Drive;
            SetScreens();
            drive = DriveSession.BeginDaily(homeLayout, Cam.transform);
        }

        public void CancelDrive()
        {
            if (drive != null) Destroy(drive.gameObject);
            drive = null;
            EnterHub();
        }

        public void StartCall(CallContract contract, bool companionFocused = false)
        {
            if (drive != null) Destroy(drive.gameObject);
            drive = null;
            var ws = Save.GetWorld(contract.worldNumber);
            if (ws == null)
            {
                ws = WorldGenerator.CreateInitialState(contract.spec);
                ws.lastVisitDay = Save.day;
                Save.worlds.Add(ws);
            }
            if (homeTown != null) homeTown.SetActive(false);
            Mode = GameMode.Mission;
            SetScreens();
            mission = MissionController.Begin(contract, ws, Cam, companionFocused);
        }

        public void EndMission(Settlement s)
        {
            LastSettlement = s;
            if (mission != null) Destroy(mission.gameObject);
            mission = null;
            Cam.target = null;

            if (s.outcome == CallOutcome.Wiped)
            {
                Save.bureauTicket += 1 + Random.Range(0, 4);
                Mode = GameMode.Bureau;
                bureau.Begin(s);
            }
            else
            {
                ApplySettlement(s);
                Mode = GameMode.Settlement;
            }
            SetScreens();
            if (homeTown != null) { homeTown.SetActive(true); ApplyHomeLighting(); }
        }

        /// <summary>뷰로 창구에서 수수료를 낸 뒤 호출된다.</summary>
        public void FinishBureau(int recoveryFee, string[] feeLines)
        {
            var s = LastSettlement;
            s.recoveryFee = recoveryFee;
            foreach (var l in feeLines) s.lines.Add(l);
            s.net -= recoveryFee;
            ApplySettlement(s);
            Mode = GameMode.Settlement;
            SetScreens();
        }

        void ApplySettlement(Settlement s)
        {
            Save.money += s.net;
            // 은주와의 관계: 같이 살아 돌아오면 가까워지고, 전멸하면 멀어진다. 서로 깨워 주면 더
            int delta = s.outcome switch
            {
                CallOutcome.Success => 5,
                CallOutcome.CompanionSolo => 4,
                CallOutcome.Withdrawn => 1,
                _ => -3,
            } + s.playerRevives * 3 + s.companionRevives * 2;
            s.bondDelta = delta;
            s.bondNote = Bond.Add(Save, delta);
            int accidents = s.playerKnockouts + s.companionKnockouts;
            Save.knockouts += accidents; // D9: 기절도 감점
            if (s.outcome == CallOutcome.Wiped)
            {
                Save.wipes++;
                Save.cleanStreak = 0;
            }
            else if (accidents > 0) Save.cleanStreak = 0;
            else if (s.outcome == CallOutcome.Success) Save.cleanStreak++;
            Save.Save();
        }

        public void ResetSave()
        {
            SaveData.Delete();
            Save = new SaveData();
            hub.Invalidate();
        }

        void SetScreens()
        {
            if (homeAmbience != null) homeAmbience.mute = Mode == GameMode.Mission || Mode == GameMode.Bureau;
            hub.enabled = Mode == GameMode.Hub;
            settlementScreen.enabled = Mode == GameMode.Settlement;
            bureau.enabled = Mode == GameMode.Bureau;
            if (Mode == GameMode.Hub) hub.Invalidate();
        }
    }
}
