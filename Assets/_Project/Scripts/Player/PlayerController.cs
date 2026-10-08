using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// 플레이어 기사. WASD 이동, Shift 달리기(발소리), 1~4 동료 명령, F 손전등.
    /// 체력 0 → 기절. 기절 중엔 무전 명령만 가능 (brainstorm-02 2장).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public PlayerCamera cam;
        public Health health;
        public PlayerWeapon weapon;
        public Light flashlight;
        public float moveMultiplier = 1f;
        public CharacterRig rig;

        CharacterController cc;
        Transform visual;
        float verticalVel;
        float footstepTimer;

        public bool FlashlightOn => flashlight != null && flashlight.enabled;
        public bool IsDown => health.IsDown;
        public bool IsSprinting { get; private set; }
        public bool InVehicle { get; private set; }
        readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();

        /// <summary>켜져 있던 렌더러만 숨기고, 나중에 그것만 되살린다.</summary>
        public static void HideRenderers(Transform root, bool hide, System.Collections.Generic.List<Renderer> memory)
        {
            if (hide)
            {
                memory.Clear();
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { memory.Add(r); r.enabled = false; }
            }
            else
            {
                foreach (var r in memory) if (r != null) r.enabled = true;
                memory.Clear();
            }
        }

        /// <summary>티코에 타고 내린다. 타면 몸을 숨기고 카메라가 차를 따라간다.</summary>
        public void SetInVehicle(bool inside, Transform car, Vector3 exitPos)
        {
            InVehicle = inside;
            health.untargetable = inside;
            HideRenderers(visual, inside, hiddenRenderers);
            cc.enabled = !inside;
            if (inside)
            {
                // 운전석에 붙어서 차와 같이 움직인다 (동료와 몬스터는 이 위치를 본다)
                transform.SetParent(car, true);
                transform.localPosition = new Vector3(-0.35f, 0.3f, 0f);
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.SetParent(car.parent, true);
                transform.SetPositionAndRotation(exitPos, Quaternion.Euler(0, car.eulerAngles.y, 0));
                verticalVel = 0f;
            }
            cam.target = inside ? car : transform;
            cam.vehicleMode = inside;
            if (flashlight != null && inside) flashlight.enabled = false;
        }

        public static PlayerController Create(Vector3 pos, PlayerCamera camera)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            go.layer = 2;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);

            var p = go.AddComponent<PlayerController>();
            p.cam = camera;
            p.health = go.AddComponent<Health>();
            p.health.Init(100f);

            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            p.visual = vis;
            Graybox.Prim(PrimitiveType.Capsule, vis, "Body", new Vector3(0, 0.9f, 0), new Vector3(0.7f, 0.9f, 0.7f), new Color(0.25f, 0.3f, 0.45f), false);
            Graybox.Prim(PrimitiveType.Sphere, vis, "Head", new Vector3(0, 1.62f, 0), Vector3.one * 0.38f, new Color(0.85f, 0.7f, 0.55f), false);
            Graybox.Box(vis, "Gun", new Vector3(0.28f, 1.25f, 0.45f), new Vector3(0.08f, 0.1f, 0.8f), new Color(0.2f, 0.15f, 0.1f), false);
            Graybox.SetLayerRecursive(go, 2);
            var art = ArtCatalog.Instance;
            p.rig = CharacterRig.Attach(vis, art != null ? art.driverModel : null, 1.78f, art != null ? art.rifle : null);
            if (p.rig != null) Graybox.SetLayerRecursive(go, 2);

            p.weapon = go.AddComponent<PlayerWeapon>();
            p.weapon.owner = p;

            var lightGo = new GameObject("Flashlight");
            lightGo.transform.SetParent(camera.transform, false);
            lightGo.transform.localPosition = new Vector3(0.3f, -0.2f, 0.3f);
            p.flashlight = lightGo.AddComponent<Light>();
            p.flashlight.type = LightType.Spot;
            p.flashlight.spotAngle = 48f;
            p.flashlight.range = 32f;
            p.flashlight.intensity = 40f;
            p.flashlight.color = new Color(1f, 0.93f, 0.78f);
            p.flashlight.enabled = false;

            camera.target = go.transform;
            camera.yaw = 45f;
            return p;
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            health.Downed += _ =>
            {
                if (rig != null) rig.Down = true;
                else visual.localRotation = Quaternion.Euler(-80, 0, 0);
            };
            health.Revived += () =>
            {
                if (rig != null) rig.Down = false;
                else visual.localRotation = Quaternion.identity;
            };
        }

        void Update()
        {
            var mc = MissionController.Instance;
            if (mc == null || !mc.InputEnabled) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            HandleCommands(kb, mc);
            if (health.IsDown || InVehicle) return;

            if (kb.fKey.wasPressedThisFrame && flashlight != null)
                flashlight.enabled = !flashlight.enabled;

            var mouse = Mouse.current;
            cam.aiming = mouse != null && mouse.rightButton.isPressed;
            if (rig != null) rig.Aim = cam.aiming || weapon.RecentlyFired;

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed) input.x -= 1;
            input = Vector2.ClampMagnitude(input, 1f);

            IsSprinting = kb.leftShiftKey.isPressed && input.y > 0.1f && !cam.aiming;
            float speed = cam.aiming ? 2.4f : IsSprinting ? 6.5f : 4f;
            speed *= moveMultiplier;

            var yawRot = Quaternion.Euler(0, cam.yaw, 0);
            var move = yawRot * new Vector3(input.x, 0, input.y) * speed;

            // 물에 들어가면 느려진다 (침수 세계)
            if (mc.Layout != null && transform.position.y < mc.Layout.waterLevel - 0.3f) speed *= 0.6f;
            if (cc.isGrounded && verticalVel < 0) verticalVel = -2f;
            verticalVel += Physics.gravity.y * Time.deltaTime;
            cc.Move((move + Vector3.up * verticalVel) * Time.deltaTime);

            // 조준·사격 중엔 카메라 방향, 아니면 이동 방향을 본다
            Quaternion want = transform.rotation;
            if (cam.aiming || weapon.RecentlyFired) want = yawRot;
            else if (move.sqrMagnitude > 0.01f) want = Quaternion.LookRotation(move.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * 14f);

            // 발소리
            if (move.sqrMagnitude > 0.1f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    footstepTimer = IsSprinting ? 0.3f : 0.5f;
                    Noise.Emit(transform.position, IsSprinting ? 9f : cam.aiming ? 1.5f : 3.5f);
                }
            }
        }

        void HandleCommands(Keyboard kb, MissionController mc)
        {
            var comp = mc.Companion;
            if (comp == null) return;
            if (health.IsDown)
            {
                // 기절 전용 무전 명령. 의식이 오락가락해서 가끔 끊긴다
                if (kb.digit1Key.wasPressedThisFrame) TryRadio(comp, CompanionOrder.ReviveMe);
                if (kb.digit2Key.wasPressedThisFrame) TryRadio(comp, CompanionOrder.GoAhead);
                return;
            }
            if (kb.digit1Key.wasPressedThisFrame) comp.SetOrder(CompanionOrder.Follow);
            if (kb.digit2Key.wasPressedThisFrame) comp.SetOrder(CompanionOrder.Hold);
            if (kb.digit3Key.wasPressedThisFrame) comp.SetOrder(CompanionOrder.Cover);
            if (kb.digit4Key.wasPressedThisFrame) comp.SetOrder(CompanionOrder.HoldFire);
        }

        void TryRadio(Companion comp, CompanionOrder order)
        {
            Sfx.Play("radio_call", 0.4f, 0.05f);
            if (Random.value < 0.25f)
            {
                MissionController.Instance.Toast("…치직… (무전이 끊겼다)");
                return;
            }
            comp.SetOrder(order);
        }
    }
}
