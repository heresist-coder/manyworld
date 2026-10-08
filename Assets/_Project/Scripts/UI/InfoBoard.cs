using System.Collections.Generic;
using UnityEngine;

namespace Manyworld
{
    /// <summary>기사식당 "정보 교환" 탭. 오늘 나와 있는 기사들에게 소문을 사고, 내가 확인한 정보를 판다.</summary>
    public class InfoBoard
    {
        Vector2 scroll;
        string feedback;

        public void Draw(Rect r, SaveData save, IList<int> callWorlds)
        {
            UIStyle.Fill(r, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            var present = InfoExchange.Present(save);
            var offers = InfoExchange.TodayOffers(save, callWorlds);
            var sellable = InfoExchange.Sellable(save);

            float contentH = 120 + present.Count * 170 + 60 + sellable.Count * 34 + 60 + save.rumors.Count * 30;
            var inner = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20);
            scroll = GUI.BeginScrollView(inner, scroll, new Rect(0, 0, inner.width - 20, contentH));
            float w = inner.width - 30, y = 0;

            GUI.Label(new Rect(0, y, w, 30), "오늘 기사식당에 나와 있는 기사들", UIStyle.With(UIStyle.Title, Color.white, 20));
            y += 32;
            GUI.Label(new Rect(0, y, w, 40), "소문은 공짜가 아니다. 헛소문도, 낡은 정보도, 일부러 틀린 정보도 있다. 직접 가서 확인하면 누가 믿을 만한지 드러난다.",
                UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.65f), 14));
            y += 46;
            if (feedback != null)
            {
                GUI.Label(new Rect(0, y, w, 24), feedback, UIStyle.With(UIStyle.Small, UIStyle.Amber, 15));
                y += 28;
            }

            foreach (var d in present)
            {
                UIStyle.Fill(new Rect(0, y, w, 160), new Color(1, 1, 1, 0.05f));
                GUI.Label(new Rect(12, y + 8, w - 24, 26), $"<b>{d.name}</b>  <size=14>{InfoExchange.TrustText(d)}</size>", UIStyle.With(UIStyle.Label, Color.white, 19));
                GUI.Label(new Rect(12, y + 34, w - 24, 22), d.look, UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.6f), 14));
                float yy = y + 60;
                int shown = 0;
                foreach (var o in offers)
                {
                    if (o.source != d.name) continue;
                    GUI.Label(new Rect(12, yy, w - 200, 44), $"\"{o.text}\"", UIStyle.With(UIStyle.Small, Color.white, 15));
                    bool canBuy = save.money >= o.price;
                    GUI.enabled = canBuy;
                    if (GUI.Button(new Rect(w - 180, yy, 168, 34), $"사기 {UIStyle.Won(o.price)}", UIStyle.Button))
                    {
                        save.money -= o.price;
                        o.owned = true;
                        save.rumors.Add(o);
                        save.Save();
                        feedback = $"{d.name}에게 소문을 샀다. 콜 보드에 표시된다.";
                        Sfx.Play("meter_tick", 0.5f, 0f);
                    }
                    GUI.enabled = true;
                    yy += 46;
                    shown++;
                }
                if (shown == 0) GUI.Label(new Rect(12, yy, w - 24, 24), "\"오늘은 들은 게 없어.\"", UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.5f), 15));
                y += 170;
            }

            // 팔기
            y += 10;
            GUI.Label(new Rect(0, y, w, 28), "내가 확인한 정보 팔기", UIStyle.With(UIStyle.Title, Color.white, 20));
            y += 34;
            if (sellable.Count == 0)
            {
                GUI.Label(new Rect(0, y, w, 24), "아직 팔 만한 게 없다. 세계 규칙을 직접 확인하거나 사인 도감을 채우자.", UIStyle.With(UIStyle.Small, new Color(1, 1, 1, 0.6f), 15));
                y += 30;
            }
            foreach (var (key, text, price) in sellable)
            {
                NpcDriver buyer = null;
                foreach (var d in present) if (!d.bought.Contains(key)) { buyer = d; break; }
                GUI.Label(new Rect(0, y + 4, w - 240, 26), text, UIStyle.With(UIStyle.Small, Color.white, 15));
                GUI.enabled = buyer != null;
                if (GUI.Button(new Rect(w - 230, y, 230, 30), buyer != null ? $"{buyer.name}에게 {UIStyle.Won(price)}" : "다 팔았다", UIStyle.Button))
                {
                    buyer.bought.Add(key);
                    save.money += price;
                    save.Save();
                    feedback = $"{buyer.name}: \"좋아, 이건 쓸 만하네.\" (+{UIStyle.Won(price)})";
                    Sfx.Play("meter_tick", 0.5f, 0f);
                }
                GUI.enabled = true;
                y += 34;
            }

            // 내가 들은 소문
            y += 16;
            GUI.Label(new Rect(0, y, w, 28), "들은 소문", UIStyle.With(UIStyle.Title, Color.white, 20));
            y += 32;
            for (int i = save.rumors.Count - 1; i >= 0; i--)
            {
                var rm = save.rumors[i];
                string mark = rm.verified == 1 ? "<color=#88dd88>맞음</color>" : rm.verified == -1 ? "<color=#ff8866>틀림</color>" : "미확인";
                GUI.Label(new Rect(0, y, w, 28), $"[{mark}] {rm.source}: \"{rm.text}\"", UIStyle.With(UIStyle.Small, Color.white, 14));
                y += 30;
            }
            GUI.EndScrollView();
        }
    }
}
