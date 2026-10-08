using UnityEngine;

namespace Manyworld
{
    public static class VolumeUI
    {
        public static void Draw(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, 120, 26), Sfx.Muted ? "소리: 꺼짐" : $"소리: {Mathf.RoundToInt(Sfx.Master * 100)}%", UIStyle.Small);
            float v = GUI.HorizontalSlider(new Rect(r.x + 120, r.y + 8, r.width - 200, 20), Sfx.Master, 0f, 1f);
            if (Mathf.Abs(v - Sfx.Master) > 0.001f) Sfx.Master = v;
            if (GUI.Button(new Rect(r.xMax - 70, r.y, 70, 28), Sfx.Muted ? "켜기" : "끄기", UIStyle.Button)) Sfx.Muted = !Sfx.Muted;
        }
    }

    /// <summary>콜 진행 중 HUD. 오른쪽 위 택시 미터기가 핵심 (brainstorm-01 4장).</summary>
    public class MissionHUD : MonoBehaviour
    {
        MissionController mc;
        float shownSpent;
        float flash;

        void Start()
        {
            mc = GetComponent<MissionController>();
            mc.Ledger.Charged += _ =>
            {
                flash = 1f;
                Sfx.Play("meter_tick", 0.45f, 0.03f, 0.04f); // 딸깍
            };
        }

        void Update()
        {
            // 미터기 숫자는 실제 지출을 따라 "딸깍딸깍" 올라간다
            shownSpent = Mathf.MoveTowards(shownSpent, mc.Ledger.Spent, Mathf.Max(800f, (mc.Ledger.Spent - shownSpent) * 6f) * Time.unscaledDeltaTime);
            flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 4f);
        }

        void OnGUI()
        {
            if (mc == null || mc.Player == null) return;
            UIStyle.BeginScaled();
            float W = UIStyle.ScaledWidth;
            const float H = 1080f;

            DrawMeter(new Rect(W - 400, 24, 376, 210));
            DrawWorldInfo(new Rect(24, 24, 620, 140));
            DrawObjective(new Rect(W / 2 - 360, 24, 720, 70));
            DrawStatus(new Rect(24, H - 210, 520, 186));
            DrawToasts(new Rect(W / 2 - 420, H - 300, 840, 200));

            if (!mc.Player.IsDown)
            {
                // 조준점
                var c = new Vector2(W / 2, H / 2);
                float g = mc.Player.cam.aiming ? 4 : 10;
                UIStyle.Fill(new Rect(c.x - 1, c.y - g - 8, 2, 8), Color.white);
                UIStyle.Fill(new Rect(c.x - 1, c.y + g, 2, 8), Color.white);
                UIStyle.Fill(new Rect(c.x - g - 8, c.y - 1, 8, 2), Color.white);
                UIStyle.Fill(new Rect(c.x + g, c.y - 1, 8, 2), Color.white);
            }

            if (mc.Prompt != null)
            {
                var r = new Rect(W / 2 - 300, H / 2 + 70, 600, 40);
                GUI.Label(r, mc.Prompt, UIStyle.With(UIStyle.Label, Color.white, 20, TextAnchor.MiddleCenter));
                if (mc.HoldProgress > 0f)
                {
                    UIStyle.Fill(new Rect(W / 2 - 150, H / 2 + 115, 300, 8), new Color(0, 0, 0, 0.6f));
                    UIStyle.Fill(new Rect(W / 2 - 150, H / 2 + 115, 300 * mc.HoldProgress, 8), UIStyle.Amber);
                }
            }

            DrawMinimap(new Rect(W - 262, H - 262, 238, 238));
            DrawCodexWarnings(W, H);
            if (mc.Player.IsDown) DrawDowned(W, H);
            if (mc.WipeFade > 0f)
            {
                UIStyle.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, mc.WipeFade));
                GUI.Label(new Rect(0, H / 2 - 30, W, 60), "타닥… 타닥… 쾅. (타자기 소리, 도장 찍는 소리)",
                    UIStyle.With(UIStyle.Label, new Color(1, 1, 1, mc.WipeFade), 22, TextAnchor.MiddleCenter));
            }
            if (mc.Paused) DrawPause(W, H);
        }

        void DrawMeter(Rect r)
        {
            UIStyle.Fill(r, new Color(0.05f, 0.05f, 0.04f, 0.88f));
            UIStyle.Fill(new Rect(r.x, r.y, r.width, 4), UIStyle.Red);
            GUI.Label(new Rect(r.x + 14, r.y + 10, 200, 24), "미터기 · 지출", UIStyle.Small);
            var digits = new Rect(r.x + 14, r.y + 34, r.width - 28, 52);
            UIStyle.Fill(digits, new Color(0.12f, 0.02f, 0.02f));
            var col = Color.Lerp(UIStyle.Amber, Color.white, flash);
            GUI.Label(new Rect(digits.x, digits.y, digits.width - 10, digits.height), $"₩ {Mathf.RoundToInt(shownSpent):N0}",
                UIStyle.With(UIStyle.Mono, col, 36));

            var c = mc.Contract;
            var l = mc.Ledger;
            float y = r.y + 96;
            Row(r, ref y, "계약금", c.deposit, true);
            Row(r, ref y, "중도금", c.midPayment, l.midReached);
            Row(r, ref y, "잔금 (귀환 시)", c.balance, l.objectiveDone);
            int secured = c.deposit + (l.midReached ? c.midPayment : 0);
            GUI.Label(new Rect(r.x + 14, y + 4, r.width - 28, 24),
                $"확정 {UIStyle.Won(secured)}  ·  카빈 {l.playerShots}발 / 은주 {l.companionShots}발",
                UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.7f), 14));
        }

        void Row(Rect r, ref float y, string label, int amount, bool done)
        {
            var color = done ? UIStyle.Green : new Color(1, 1, 1, 0.45f);
            GUI.Label(new Rect(r.x + 14, y, 200, 22), (done ? "■ " : "□ ") + label, UIStyle.With(UIStyle.Small, color, 15));
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), UIStyle.Won(amount), UIStyle.With(UIStyle.Small, color, 15, TextAnchor.UpperRight));
            y += 24;
        }

        void DrawWorldInfo(Rect r)
        {
            var s = mc.Spec;
            GUI.Label(new Rect(r.x, r.y, r.width, 40), $"{s.Title} <size=18>· {s.nickname}</size>", UIStyle.Title);
            string rules = s.RuleText(true);
            if (mc.Discovered != WorldRule.None)
            {
                var known = new WorldSpec { rules = mc.Discovered };
                rules = $"확인: {known.RuleText(false)}  ·  공사 추정: {rules}";
            }
            else rules = $"공사 추정: {rules}";
            GUI.Label(new Rect(r.x, r.y + 42, r.width, 24), rules, UIStyle.Small);
            int t = Mathf.FloorToInt(mc.Elapsed);
            int lim = Mathf.FloorToInt(mc.Contract.timeLimit);
            var tc = t > lim ? UIStyle.Red : new Color(1, 1, 1, 0.8f);
            GUI.Label(new Rect(r.x, r.y + 66, r.width, 24), $"경과 {t / 60:00}:{t % 60:00} / 제한 {lim / 60:00}:{lim % 60:00}" + (t > lim ? "  (체류 초과)" : ""),
                UIStyle.With(UIStyle.Small, tc));
            if (s.rules.HasFlag(WorldRule.ToxicGas))
                GUI.Label(new Rect(r.x, r.y + 90, r.width, 24), $"방독면 필터 {Mathf.CeilToInt(mc.FilterLeft)}초 남음 (자동 교체 ₩4,000)", UIStyle.With(UIStyle.Small, UIStyle.Amber));
        }

        void DrawObjective(Rect r)
        {
            string text;
            Vector3 dest;
            if (!mc.MidReached && !mc.ObjectiveDone) { text = "중도금 지점 (장터거리 깃발)"; dest = mc.Layout.midpointPos; }
            else if (!mc.ObjectiveDone) { text = $"{mc.Contract.task} (북동쪽 측량 현장)"; dest = mc.Layout.objectivePos; }
            else { text = "게이트로 귀환"; dest = mc.Layout.gatePos; }
            float d = Vector3.Distance(mc.Player.transform.position, dest);
            UIStyle.Fill(r, new Color(0, 0, 0, 0.35f));
            GUI.Label(new Rect(r.x, r.y + 6, r.width, 30), $"목표: {text}", UIStyle.With(UIStyle.Label, Color.white, 20, TextAnchor.MiddleCenter));
            var to = dest - mc.Player.transform.position;
            string arrow = Arrow(to, mc.Player.cam.yaw);
            GUI.Label(new Rect(r.x, r.y + 36, r.width, 26), $"{arrow}  {d:0}m" + (mc.PlayerInCar ? $"   ·   티코 {mc.Car.SpeedKmh:0} km/h" : ""), UIStyle.With(UIStyle.Small, UIStyle.Amber, 16, TextAnchor.MiddleCenter));
        }

        /// <summary>카메라 기준 방향 화살표 (큰 월드라 길 잃기 쉽다).</summary>
        static string Arrow(Vector3 to, float yaw)
        {
            float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            int i = Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 45f) % 8;
            return arrows[i];
        }

        void DrawStatus(Rect r)
        {
            UIStyle.Fill(r, new Color(0, 0, 0, 0.45f));
            var p = mc.Player;
            Bar(new Rect(r.x + 14, r.y + 14, 300, 18), "기사", p.health.current / p.health.max, p.IsDown ? "기절" : null);
            var comp = mc.Companion;
            Bar(new Rect(r.x + 14, r.y + 44, 300, 18), Companion.Name, comp.health.current / comp.health.max, comp.health.IsDown ? "기절" : null);

            string order = comp.order switch
            {
                CompanionOrder.Follow => "따라와",
                CompanionOrder.Hold => "대기",
                CompanionOrder.Cover => "엄호",
                CompanionOrder.HoldFire => "쏘지 마",
                CompanionOrder.ReviveMe => "깨우러 가는 중",
                _ => "먼저 가",
            };
            GUI.Label(new Rect(r.x + 14, r.y + 72, 500, 24), $"명령: <b>{order}</b>", UIStyle.Label);
            GUI.Label(new Rect(r.x + 14, r.y + 98, 500, 22), "[1] 따라와  [2] 대기  [3] 엄호  [4] 쏘지 마", UIStyle.Small);
            string ammo = p.weapon.Reloading ? "재장전 중…" : $"카빈 {p.weapon.mag}/{p.weapon.magSize}";
            GUI.Label(new Rect(r.x + 14, r.y + 126, 500, 24), $"{ammo}   <size=14>R 재장전 · V 쇠파이프 · 우클릭 조준</size>", UIStyle.Label);
            if (comp.AimTarget != null)
                GUI.Label(new Rect(r.x + 14, r.y + 152, 500, 22), $"은주 조준 중… {Mathf.RoundToInt(comp.AimProgress * 100)}%", UIStyle.With(UIStyle.Small, new Color(0.8f, 0.95f, 1f)));
            if (comp.ReviveProgress > 0f)
                GUI.Label(new Rect(r.x + 14, r.y + 152, 500, 22), $"은주가 기사를 깨우는 중… {Mathf.RoundToInt(comp.ReviveProgress * 100)}%", UIStyle.With(UIStyle.Small, UIStyle.Green));
        }

        void Bar(Rect r, string label, float t, string overlay)
        {
            GUI.Label(new Rect(r.x, r.y - 2, 80, r.height + 4), label, UIStyle.With(UIStyle.Small, Color.white, 15));
            var bar = new Rect(r.x + 80, r.y, r.width - 80, r.height);
            UIStyle.Fill(bar, new Color(0.2f, 0.05f, 0.05f, 0.9f));
            UIStyle.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(t), bar.height), t > 0.35f ? UIStyle.Green : UIStyle.Red);
            if (overlay != null) GUI.Label(bar, overlay, UIStyle.With(UIStyle.Small, Color.white, 14, TextAnchor.MiddleCenter));
        }

        void DrawToasts(Rect r)
        {
            float y = r.yMax;
            for (int i = mc.Toasts.Count - 1; i >= 0; i--)
            {
                var (text, time) = mc.Toasts[i];
                float age = Time.time - time;
                if (age > 6f) continue;
                float a = Mathf.Clamp01(6f - age);
                y -= 30;
                GUI.Label(new Rect(r.x, y, r.width, 28), text, UIStyle.With(UIStyle.Label, new Color(1, 1, 1, a), 19, TextAnchor.MiddleCenter));
            }
        }

        void DrawMinimap(Rect r)
        {
            var lay = mc.Layout;
            if (lay.minimap == null || lay.worldSize <= 0f) return;
            UIStyle.Fill(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(0, 0, 0, 0.6f));
            GUI.DrawTexture(r, lay.minimap);
            Vector2 ToMap(Vector3 w) => new Vector2(r.x + w.x / lay.worldSize * r.width, r.yMax - w.z / lay.worldSize * r.height);
            void Mark(Vector3 w, string label, Color c, int size = 13)
            {
                var p = ToMap(w);
                UIStyle.Fill(new Rect(p.x - 3, p.y - 3, 6, 6), c);
                GUI.Label(new Rect(p.x + 4, p.y - 10, 80, 20), label, UIStyle.With(UIStyle.Small, c, size));
            }
            foreach (var kv in lay.landmarks) Mark(kv.Value, kv.Key, new Color(0.15f, 0.12f, 0.1f, 0.8f), 11);
            Mark(lay.gatePos, "게이트", new Color(0.6f, 0.3f, 0.9f));
            Mark(lay.midpointPos, "중도금", mc.MidReached ? UIStyle.Green : UIStyle.Amber);
            Mark(lay.objectivePos, "목표", mc.ObjectiveDone ? UIStyle.Green : UIStyle.Red);
            if (!mc.PlayerInCar) Mark(mc.Car.transform.position, "티코", new Color(0.2f, 0.5f, 1f));
            if (!mc.Companion.InVehicle) Mark(mc.Companion.transform.position, "", Color.white, 1);

            // 기사: 카메라 방향 화살표
            var pp = ToMap(mc.PlayerInCar ? mc.Car.transform.position : mc.Player.transform.position);
            var old = GUI.matrix;
            float scale = Screen.height / 1080f;
            GUIUtility.RotateAroundPivot(mc.Player.cam.yaw, pp * scale);
            GUI.Label(new Rect(pp.x - 10, pp.y - 12, 20, 24), "▲", UIStyle.With(UIStyle.Label, Color.white, 18, TextAnchor.MiddleCenter));
            GUI.matrix = old;
            GUI.Label(new Rect(r.x, r.y + 2, r.width, 18), "N", UIStyle.With(UIStyle.Small, Color.white, 13, TextAnchor.UpperCenter));
        }

        void DrawCodexWarnings(float W, float H)
        {
            if (mc.CodexWarning != null)
            {
                var r = new Rect(W / 2 - 330, 104, 660, 36);
                UIStyle.Fill(r, new Color(0.5f, 0.05f, 0.05f, 0.75f));
                GUI.Label(r, mc.CodexWarning, UIStyle.With(UIStyle.Label, Color.white, 19, TextAnchor.MiddleCenter));
            }
            if (mc.ThreatBehind)
            {
                float a = 0.6f + Mathf.Sin(Time.time * 12f) * 0.3f;
                GUI.Label(new Rect(W / 2 - 200, H - 150, 400, 50), "▼ 뒤! ▼  <size=15>[사인 #01]</size>", UIStyle.With(UIStyle.Title, new Color(1f, 0.25f, 0.2f, a), 32, TextAnchor.MiddleCenter));
            }
        }

        void DrawDowned(float W, float H)
        {
            UIStyle.Fill(new Rect(0, 0, W, H), new Color(0.15f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(0, H / 2 - 120, W, 40), "의식이 흐려진다… 무전 잡음, 멀리서 발소리",
                UIStyle.With(UIStyle.Label, new Color(1, 1, 1, 0.85f), 24, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(0, H / 2 - 70, W, 30), "무전 명령:  [1] 깨워 줘   [2] 먼저 가 (혼자 목표 → 티코)",
                UIStyle.With(UIStyle.Label, UIStyle.Amber, 20, TextAnchor.MiddleCenter));
        }

        static void VolumeControl(Rect r) => VolumeUI.Draw(r);

        void DrawPause(float W, float H)
        {
            UIStyle.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            GUI.Label(new Rect(0, H / 2 - 140, W, 50), "일시 정지", UIStyle.With(UIStyle.Title, Color.white, 34, TextAnchor.MiddleCenter));
            if (GUI.Button(new Rect(W / 2 - 160, H / 2 - 60, 320, 50), "계속하기", UIStyle.Button)) mc.SetPaused(false);
            VolumeControl(new Rect(W / 2 - 160, H / 2 + 140, 320, 60));
            GUI.Label(new Rect(0, H / 2 + 10, W, 120),
                "WASD 이동 · Shift 달리기(발소리 큼) · 마우스 시점 · 좌클릭 사격(₩330/발) · 우클릭 조준\nR 재장전 · V 쇠파이프(공짜) · E 상호작용 · F 손전등 · 1~4 은주 명령 · Esc 일시 정지",
                UIStyle.With(UIStyle.Small, Color.white, 16, TextAnchor.UpperCenter));
        }
    }
}
