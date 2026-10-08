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
            EnterHub();
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.mKey.wasPressedThisFrame == true) Sfx.Muted = !Sfx.Muted;

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
            VolumetricAtmosphere.Create(homeTown.transform, Vector3.zero, 150f, VolumetricAtmosphere.Home, homeSun);
            ApplyHomeLighting();
        }

        void ApplyHomeLighting()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.47f, 0.4f);
            var cam = Cam.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.62f, 0.45f);
            cam.fieldOfView = 55f;
            Cam.target = null;
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
