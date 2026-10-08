using UnityEngine;
using UnityEngine.InputSystem;

namespace Manyworld
{
    /// <summary>
    /// 티코. 바퀴 4개를 레이캐스트로 받치는 아케이드 서스펜션 차량 (휠 콜라이더 없음).
    /// 앞바퀴 굴림, 가볍고 잘 돈다. 뒤집힐 수 있다 — 대신 둘이 밀면 세워진다 (brainstorm-02 1장).
    /// 달리다 몬스터를 치면 피해를 준다. 엔진 소리는 소음이다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TicoCar : MonoBehaviour
    {
        public bool controllable = true;
        public float maxSpeed = 22f;          // m/s ≈ 80km/h
        public float engineForce = 5200f;
        public float brakeForce = 7000f;
        public float maxSteer = 32f;
        public float suspensionRest = 0.45f;
        public float wheelRadius = 0.3f;
        public float spring = 26000f;
        public float damper = 2300f;
        public float grip = 0.85f;

        /// <summary>테스트·자동 주행용 입력. null이 아니면 키보드 대신 쓴다 (x 조향, y 가속).</summary>
        public Vector2? externalInput;

        Rigidbody rb;
        AudioSource engine;
        float throttleSmoothed, steerSmoothed, flipTimer;
        readonly Vector3[] mounts =
        {
            new Vector3(-0.68f, 0.55f, 1.05f), new Vector3(0.68f, 0.55f, 1.05f),
            new Vector3(-0.68f, 0.55f, -1.05f), new Vector3(0.68f, 0.55f, -1.05f),
        };
        readonly Transform[] wheelVisuals = new Transform[4];
        readonly Quaternion[] wheelBase = new Quaternion[4];
        readonly float[] wheelSpin = new float[4];
        readonly bool[] grounded = new bool[4];

        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public float ForwardSpeed => rb == null ? 0f : Vector3.Dot(rb.linearVelocity, transform.forward);
        public bool IsFlipped { get; private set; }
        public Health health;
        public bool IsBroken { get; private set; }
        public bool HeadlightsOn { get; private set; }
        readonly Light[] headlights = new Light[2];
        public bool AnyWheelGrounded => grounded[0] || grounded[1] || grounded[2] || grounded[3];

        public static TicoCar Create(Transform parent, Vector3 pos, Quaternion rot, bool withCompanion)
        {
            var car = TownBuilder.BuildTico(parent, pos + Vector3.up * 0.3f, rot);
            Graybox.SetLayerRecursive(car, 2);
            var art = ArtCatalog.Instance;
            if (withCompanion && (art == null || art.ticoBody == null)) // ithappy 차체가 있으면 지붕을 뚫고 나와서 뺀다
            {
                // 조수석의 흰 크롭티
                Graybox.Prim(PrimitiveType.Capsule, car.transform, "Eunju", new Vector3(0.35f, 1.15f, -0.1f), new Vector3(0.35f, 0.3f, 0.35f), new Color(0.97f, 0.97f, 0.95f), false);
                Graybox.Prim(PrimitiveType.Capsule, car.transform, "Driver", new Vector3(-0.35f, 1.15f, -0.1f), new Vector3(0.35f, 0.3f, 0.35f), new Color(0.25f, 0.3f, 0.45f), false);
            }
            var rb = car.AddComponent<Rigidbody>();
            rb.mass = 750f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0, 0.3f, 0.1f);
            car.AddComponent<Health>().Init(260f); // 깡통차. 몬스터가 때리면 찌그러진다
            return car.AddComponent<TicoCar>();
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            engine = Sfx.Loop("engine_loop", transform, 0.55f, false);
            BuildWheels();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Headlight_" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(i == 0 ? -0.55f : 0.55f, 0.75f, 1.75f);
                go.transform.localRotation = Quaternion.Euler(6f, 0, 0);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 40f;
                l.spotAngle = 68f;
                l.intensity = 28f;
                l.color = new Color(1f, 0.92f, 0.75f);
                l.enabled = false;
                headlights[i] = l;
            }
        }

        /// <summary>퍼졌다. 더는 못 몬다 (brainstorm-01 10장: 망가지면 걸어서 탈출).</summary>
        public void Break()
        {
            if (IsBroken) return;
            IsBroken = true;
            controllable = false;
            externalInput = null;
            SetHeadlights(false);
            if (engine != null) engine.Stop();
            Sfx.PlayAt("car_bump", transform.position, 1f, 50f, 0.05f);

            // 보닛에서 연기
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(transform, false);
            smoke.transform.localPosition = new Vector3(0, 1f, 1.3f);
            var ps = smoke.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2.5f;
            main.startSpeed = 1.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startColor = new Color(0.25f, 0.25f, 0.25f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.rotation = new Vector3(-90, 0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh != null)
            {
                var mat = new Material(sh);
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            }
        }

        public void SetHeadlights(bool on)
        {
            HeadlightsOn = on;
            foreach (var l in headlights) if (l != null) l.enabled = on;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (controllable && !externalInput.HasValue && kb != null && kb.lKey.wasPressedThisFrame)
            {
                SetHeadlights(!HeadlightsOn);
                Sfx.Play("meter_tick", 0.5f, 0f);
            }
        }

        void BuildWheels()
        {
            var art = ArtCatalog.Instance;
            for (int i = 0; i < 4; i++)
            {
                GameObject w = null;
                if (art != null && art.ticoWheels != null && art.ticoWheels.Length == 4 && art.ticoWheels[i] != null)
                {
                    w = Instantiate(art.ticoWheels[i], transform);
                    foreach (var c in w.GetComponentsInChildren<Collider>()) c.enabled = false;
                    // 바퀴 메시의 가장 얇은 축을 차축(X)으로 맞추고 지름을 맞춘다
                    var mf = w.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var s = mf.sharedMesh.bounds.size;
                        Quaternion toX = s.x <= s.y && s.x <= s.z ? Quaternion.identity
                            : s.y <= s.z ? Quaternion.Euler(0, 0, 90) : Quaternion.Euler(0, 90, 0);
                        wheelBase[i] = toX;
                        float diameter = Mathf.Max(s.x, s.y, s.z);
                        w.transform.localScale = Vector3.one * (wheelRadius * 2f / Mathf.Max(0.01f, diameter));
                    }
                    else wheelBase[i] = Quaternion.identity;
                }
                else
                {
                    w = Graybox.Prim(PrimitiveType.Cylinder, transform, "Wheel", Vector3.zero, new Vector3(wheelRadius * 2f, 0.1f, wheelRadius * 2f), new Color(0.08f, 0.08f, 0.08f), false);
                    wheelBase[i] = Quaternion.Euler(0, 0, 90);
                }
                w.name = "Wheel_" + i;
                w.layer = 2;
                wheelVisuals[i] = w.transform;
            }
            // 그레이박스 바퀴는 숨긴다 (BuildTico가 만든 장식)
            foreach (Transform t in transform)
                if (t.name == "Wheel") t.gameObject.SetActive(false);
        }

        Vector2 ReadInput()
        {
            if (externalInput.HasValue) return externalInput.Value;
            var kb = Keyboard.current;
            if (!controllable || kb == null) return Vector2.zero;
            float throttle = 0f, steer = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
            return new Vector2(steer, throttle);
        }

        void FixedUpdate()
        {
            var input = ReadInput();
            bool handbrake = controllable && !externalInput.HasValue && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            float dt = Time.fixedDeltaTime;
            throttleSmoothed = Mathf.MoveTowards(throttleSmoothed, input.y, dt * 4f);
            steerSmoothed = Mathf.MoveTowards(steerSmoothed, input.x, dt * 5f);

            float fwdSpeed = ForwardSpeed;
            float steerAngle = steerSmoothed * maxSteer * Mathf.Lerp(1f, 0.45f, Mathf.Abs(fwdSpeed) / maxSpeed);
            float wheelLoad = rb.mass * -Physics.gravity.y / 4f;

            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var mount = transform.TransformPoint(mounts[i]);
                var down = -transform.up;
                float maxLen = suspensionRest + wheelRadius;
                grounded[i] = Physics.Raycast(mount, down, out var hit, maxLen, ~(1 << 2), QueryTriggerInteraction.Ignore);
                float dist = grounded[i] ? hit.distance : maxLen;

                if (grounded[i])
                {
                    // 서스펜션: 스프링 + 댐퍼
                    float compression = maxLen - dist;
                    var pointVel = rb.GetPointVelocity(mount);
                    float upVel = Vector3.Dot(pointVel, transform.up);
                    float f = compression * spring - upVel * damper;
                    rb.AddForceAtPosition(transform.up * Mathf.Max(0f, f), mount);

                    // 타이어: 바퀴가 보는 방향 기준으로 굴림·제동·옆미끄럼
                    var wheelRot = front ? Quaternion.AngleAxis(steerAngle, transform.up) : Quaternion.identity;
                    var wFwd = Vector3.ProjectOnPlane(wheelRot * transform.forward, hit.normal).normalized;
                    var wRight = Vector3.Cross(hit.normal, wFwd);
                    float vF = Vector3.Dot(pointVel, wFwd);
                    float vS = Vector3.Dot(pointVel, wRight);

                    float longF = 0f;
                    if (front) // 앞바퀴 굴림
                    {
                        bool braking = throttleSmoothed * vF < -0.5f;
                        if (braking) longF = -Mathf.Sign(vF) * brakeForce * 0.5f * Mathf.Abs(throttleSmoothed);
                        else if (Mathf.Abs(fwdSpeed) < maxSpeed || Mathf.Sign(throttleSmoothed) != Mathf.Sign(fwdSpeed))
                            longF = throttleSmoothed * engineForce * 0.5f * (throttleSmoothed < 0 ? 0.55f : 1f);
                    }
                    if (Mathf.Abs(throttleSmoothed) < 0.05f) longF -= vF * 60f; // 엔진 브레이크·구름 저항
                    if (handbrake && !front) longF -= Mathf.Sign(vF) * brakeForce * 0.35f;

                    float sideGrip = grip * (handbrake && !front ? 0.25f : 1f);
                    float sideF = -vS * wheelLoad * sideGrip / Mathf.Max(0.5f, 1f) * 2.2f;
                    // 마찰 한계
                    var tire = wFwd * longF + wRight * sideF;
                    float limit = wheelLoad * 1.3f + Mathf.Max(0f, f) * 0.3f;
                    if (tire.magnitude > limit) tire = tire.normalized * limit;
                    rb.AddForceAtPosition(tire, hit.point + transform.up * 0.2f);
                }

                // 바퀴 그림
                var wv = wheelVisuals[i];
                if (wv != null)
                {
                    wv.position = mount + down * (dist - wheelRadius);
                    wheelSpin[i] += fwdSpeed / wheelRadius * Mathf.Rad2Deg * dt;
                    var steerRot = front ? Quaternion.Euler(0, steerAngle, 0) : Quaternion.identity;
                    wv.localRotation = steerRot * Quaternion.Euler(wheelSpin[i], 0, 0) * wheelBase[i];
                }
            }

            // 공중에 뜨면 약하게 자세를 잡는다 (프로토타입 편의)
            if (!AnyWheelGrounded)
                rb.AddTorque(Vector3.Cross(transform.up, Vector3.up) * rb.mass * 2f);
            else
                rb.AddForce(-transform.up * rb.linearVelocity.magnitude * 25f); // 다운포스

            UpdateFlip(dt);

            if (engine != null)
            {
                engine.pitch = 0.8f + Mathf.Abs(fwdSpeed) / maxSpeed * 0.9f + Mathf.Abs(throttleSmoothed) * 0.15f;
                engine.volume = 0.4f + Mathf.Abs(throttleSmoothed) * 0.25f;
            }
        }

        void UpdateFlip(float dt)
        {
            bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.35f && rb.linearVelocity.magnitude < 1.5f;
            flipTimer = upsideDown ? flipTimer + dt : 0f;
            IsFlipped = flipTimer > 1f;
        }

        /// <summary>둘이 밀어서 세운다.</summary>
        public void Unflip()
        {
            var yaw = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position += Vector3.up * 0.8f;
            rb.rotation = yaw;
            transform.SetPositionAndRotation(rb.position, yaw);
            flipTimer = 0f;
            IsFlipped = false;
        }

        void OnCollisionEnter(Collision c)
        {
            float impact = c.relativeVelocity.magnitude;
            if (impact > 3f) Sfx.PlayAt("car_bump", c.GetContact(0).point, Mathf.Clamp01(impact / 12f), 40f, 0.1f, 0.25f);

            // 몬스터를 친다
            var zone = c.collider.GetComponent<HitZone>();
            // 세게 박으면 차도 상한다
            if (zone == null && impact > 9f && health != null) health.TakeDamage(new DamageInfo { amount = impact * 1.2f, source = DamageSource.Environment, point = c.GetContact(0).point });
            if (zone != null && zone.owner != null && impact > 5f && zone.owner.GetComponent<Monster>() != null)
                zone.owner.TakeDamage(new DamageInfo { amount = (impact - 4f) * 9f, source = DamageSource.Vehicle, point = c.GetContact(0).point });
        }

        public void Stop()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
