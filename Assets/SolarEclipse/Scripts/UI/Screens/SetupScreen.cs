using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// First-launch setup: gore level, voice chat + microphone, age (chat maturity) and profanity. Shown once, over
    /// the main menu, until it's finished. Everything can be changed later in Settings.
    /// </summary>
    public sealed class SetupScreen : UIScreen
    {
        enum Page { Welcome, Gore, Voice, Age, Chat, Done }
        Page page;
        float pageTime;
        readonly Action onFinished;
        Vector2 micScroll;

        public SetupScreen(Action onFinished = null) { this.onFinished = onFinished; }
        public override bool IsModal => true;
        public override bool OnBack() { if (page > Page.Welcome) Go(page - 1); return true; } // can't skip with Esc

        public override void OnOpen() { AudioManager.Ui("ui_open"); pageTime = Time.unscaledTime; }

        void Go(Page p)
        {
            // Under 13: the chat page has no choice to make (profanity is always hidden).
            if (p == Page.Chat && ChatFilter.ForcedOn) Settings.Data.chatFilter = true;
            page = p;
            pageTime = Time.unscaledTime;
            AudioManager.Ui("ui_tab");
        }

        void Finish()
        {
            Settings.Data.setupDone = true;
            Settings.Save();
            Settings.Apply();
            AudioManager.Ui("ui_success");
            UIRoot.Remove(this);
            onFinished?.Invoke();
        }

        public override void Draw()
        {
            float a = Intro(0, 0.3f);
            UI.Dim(0.82f * a);
            var r = UI.Centered(Mathf.Min(1100, UI.VW - 60), Mathf.Min(720, UI.VH - 40));
            UI.Panel(r, "Welcome", a);
            var d = Settings.Data;

            // Step dots
            int steps = (int)Page.Done + 1;
            for (int i = 0; i < steps; i++)
                UI.Rounded(new Rect(r.xMax - 40 - (steps - i) * 26, r.y + 30, 16, 16), i == (int)page ? UI.Accent2 : i < (int)page ? UI.Accent : new Color(1, 1, 1, 0.15f), true);

            float t = Mathf.Clamp01((Time.unscaledTime - pageTime) / 0.25f);
            var body = new Rect(r.x + 50, r.y + 90 + (1 - t) * 20, r.width - 100, r.height - 200);
            var c = UI.TextColor.WithAlpha(t);
            bool canNext = true;

            switch (page)
            {
                case Page.Welcome:
                    UI.Label(new Rect(body.x, body.y, body.width, 60), "<b>Hi! Welcome to " + GameInfo.Title + ".</b>", 40, c);
                    UI.Label(new Rect(body.x, body.y + 80, body.width, 160), "Before you start, let's get a few settings right for you. It only takes a moment, and you can change all of it later in Settings.", 24, UI.Muted.WithAlpha(t), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                    break;

                case Page.Gore:
                {
                    UI.Label(new Rect(body.x, body.y, body.width, 50), "<b>How much gore do you want?</b>", 32, c);
                    int n = Settings.GoreLevels.Length;
                    float bw = (body.width - (n - 1) * 12) / n;
                    for (int i = 0; i < n; i++)
                    {
                        var b = new Rect(body.x + i * (bw + 12), body.y + 80, bw, 80);
                        if (UI.Button(b, Settings.GoreLevels[i], d.goreLevel == i ? ButtonKind.Primary : ButtonKind.Normal, null, 20)) d.goreLevel = i;
                    }
                    UI.Label(new Rect(body.x, body.y + 190, body.width, 90), Settings.GoreDescriptions[Mathf.Clamp(d.goreLevel, 0, 6)], 22, UI.Accent2.WithAlpha(t), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                    break;
                }

                case Page.Voice:
                {
                    UI.Label(new Rect(body.x, body.y, body.width, 50), "<b>Do you want to use voice chat in multiplayer?</b>", 30, c);
                    bool on = d.voiceMode != 2;
                    if (UI.Button(new Rect(body.x, body.y + 70, 220, 64), "YES", on ? ButtonKind.Primary : ButtonKind.Normal, null, 22)) { if (!on) d.voiceMode = 0; }
                    if (UI.Button(new Rect(body.x + 240, body.y + 70, 220, 64), "NO", !on ? ButtonKind.Primary : ButtonKind.Normal, null, 22)) d.voiceMode = 2;
                    UI.Label(new Rect(body.x + 490, body.y + 70, body.width - 490, 64), "You can always change this in Settings > Voice.", 17, UI.Muted.WithAlpha(t));
                    if (d.voiceMode != 2)
                    {
                        UI.Label(new Rect(body.x, body.y + 160, body.width, 40), "Select your microphone:", 22, c);
                        string[] mics;
                        try { mics = Microphone.devices; } catch { mics = new string[0]; }
                        var list = new Rect(body.x, body.y + 206, body.width, body.height - 210);
                        if (mics.Length == 0) UI.Label(list, "No microphone found. You can pick one later in Settings > Voice.", 18, UI.Muted.WithAlpha(t), TextAnchor.UpperLeft);
                        else
                        {
                            float rowH = 54f;
                            var all = new string[mics.Length + 1];
                            all[0] = "";
                            Array.Copy(mics, 0, all, 1, mics.Length);
                            float content = all.Length * (rowH + 8);
                            float s = UI.BeginScroll("setup_mics", list, content);
                            for (int i = 0; i < all.Length; i++)
                            {
                                var rr = new Rect(0, i * (rowH + 8) - s, list.width - 16, rowH);
                                if (rr.yMax < 0 || rr.y > list.height) continue;
                                bool sel = (d.micDevice ?? "") == all[i];
                                if (UI.Button(rr, i == 0 ? "System default microphone" : all[i], sel ? ButtonKind.Primary : ButtonKind.Normal, IconFactory.Get("speaker"), 18)) d.micDevice = all[i];
                            }
                            UI.EndScroll("setup_mics", list, content);
                        }
                    }
                    break;
                }

                case Page.Age:
                {
                    UI.Label(new Rect(body.x, body.y, body.width, 50), "<b>How old are you?</b>", 32, c);
                    UI.Label(new Rect(body.x, body.y + 56, body.width, 80), "This never leaves your device: it isn't published or sent to anyone. It's only used to set how mature the chat is. If you're under 13, profanity is always hidden.", 18, UI.Muted.WithAlpha(t), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                    float bw = (body.width - 3 * 14) / 4f;
                    for (int i = 1; i < ChatFilter.AgeGroups.Length; i++)
                        if (UI.Button(new Rect(body.x + (i - 1) * (bw + 14), body.y + 160, bw, 76), ChatFilter.AgeGroups[i], d.ageGroup == i ? ButtonKind.Primary : ButtonKind.Normal, null, 22)) d.ageGroup = i;
                    if (UI.Button(new Rect(body.x + 3 * (bw + 14), body.y + 160, bw, 76), "Rather not say", d.ageGroup == 0 ? ButtonKind.Primary : ButtonKind.Normal, null, 20)) d.ageGroup = 0;
                    break;
                }

                case Page.Chat:
                    UI.Label(new Rect(body.x, body.y, body.width, 50), "<b>Chat</b>", 32, c);
                    if (ChatFilter.ForcedOn)
                    {
                        d.chatFilter = true;
                        UI.Label(new Rect(body.x, body.y + 70, body.width, 120), "Because you're under 13, swear words in chat will always be hidden with ****.", 24, UI.Muted.WithAlpha(t), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                    }
                    else
                    {
                        UI.Label(new Rect(body.x, body.y + 60, body.width, 50), "Do you want to see profanity (swear words) in the chat?", 24, c);
                        if (UI.Button(new Rect(body.x, body.y + 130, 260, 70), "NO, HIDE IT", d.chatFilter ? ButtonKind.Primary : ButtonKind.Normal, null, 20)) d.chatFilter = true;
                        if (UI.Button(new Rect(body.x + 280, body.y + 130, 260, 70), "YES, SHOW IT", !d.chatFilter ? ButtonKind.Primary : ButtonKind.Normal, null, 20)) d.chatFilter = false;
                        UI.Label(new Rect(body.x, body.y + 220, body.width, 40), "Change it any time in Settings > Interface > Profanity filter.", 17, UI.Muted.WithAlpha(t));
                    }
                    break;

                case Page.Done:
                    UI.Label(new Rect(body.x, body.y + 40, body.width, 70), "<b>Thanks, that's everything!</b>", 40, c, TextAnchor.MiddleCenter);
                    UI.Label(new Rect(body.x, body.y + 130, body.width, 60), "Have fun playing.", 26, UI.Accent2.WithAlpha(t), TextAnchor.MiddleCenter);
                    break;
            }

            // Navigation
            if (page > Page.Welcome && page < Page.Done && UI.Button(new Rect(r.x + 40, r.yMax - 90, 200, 62), "BACK", ButtonKind.Ghost, IconFactory.Get("arrow_left"), 20)) Go(page - 1);
            if (page == Page.Done)
            {
                if (UI.Button(new Rect(r.xMax - 300, r.yMax - 90, 260, 62), "LET'S GO", ButtonKind.Primary, IconFactory.Get("play"), 22)) Finish();
            }
            else if (UI.Button(new Rect(r.xMax - 260, r.yMax - 90, 220, 62), page == Page.Welcome ? "START" : "NEXT", ButtonKind.Primary, IconFactory.Get("arrow_right"), 22, canNext)) Go(page + 1);
        }
    }
}
