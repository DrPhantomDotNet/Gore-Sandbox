using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Esc in a map: pause menu on the left, "Game Settings" (cheats/modifiers) on the right.
    /// </summary>
    public sealed class PauseMenuScreen : UIScreen
    {
        public override bool PausesTime => !NetState.Active; // multiplayer keeps running
        public override bool IsModal => true;
        int tab;
        static readonly string[] Tabs = { "PLAYER", "GOREDOLLS", "WORLD", "VISUAL" };
        static readonly string[] HostTabs = { "PLAYERS", "PLAYER", "GOREDOLLS", "WORLD", "VISUAL" };

        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.55f * a);
            float W = UI.VW, H = UI.VH;

            // ---- left column
            var left = new Rect(60, 80, 420, H - 160);
            UI.Panel(left, NetState.Active ? "Menu" : "Paused", a);
            string sub = GameManager.I.CurrentMap != null ? GameManager.I.CurrentMap.Name : "";
            if (NetState.Active) sub = (NetState.IsHost ? "Hosting · " : "Playing on ") + NetManager.I.ServerName + " · " + sub;
            UI.Label(new Rect(left.x + 28, left.y + 70, left.width - 56, 26), sub, 18, UI.Muted);
            float y = left.y + 110;
            Rect B() { var r = new Rect(left.x + 28, y, left.width - 56, 58); y += 68; return r; }
            if (UI.Button(B(), "RESUME", ButtonKind.Primary, IconFactory.Get("play"))) OnBack();
            if (UI.Button(B(), "SETTINGS", ButtonKind.Normal, IconFactory.Get("gear"))) UIRoot.Push(new SettingsScreen(true));
            if (UI.Button(B(), "CONTROLS", ButtonKind.Normal, IconFactory.Get("keyboard"))) UIRoot.Push(new HelpScreen());
            if (UI.Button(B(), "SESSION STATS", ButtonKind.Normal, IconFactory.Get("chart"))) UIRoot.Push(new ScoreboardScreen());
            if (UI.Button(B(), "CUSTOMIZE WEAPONS", ButtonKind.Normal, IconFactory.Get("wrench"))) UIRoot.Push(new WeaponCustomizeScreen());
            if (NetState.IsClient) { y += 0; }
            else if (UI.Button(B(), "CLEAR EVERYTHING SPAWNED", ButtonKind.Ghost, IconFactory.Get("trash"), 20))
                UIRoot.Push(new ConfirmDialog("Clear the map?", "Removes every Goredoll, prop, gib and blood decal.", () => { WorldRoot.Current?.ClearDynamic(); GameEvents.ShowToast("Map cleared"); }, "CLEAR", ButtonKind.Danger));
            if (!NetState.Active && UI.Button(B(), "RESTART MAP", ButtonKind.Ghost, IconFactory.Get("refresh"))) UIRoot.Push(new ConfirmDialog("Restart the map?", "Everything goes back to how it started.", () => GameManager.I.RestartMap(), "RESTART", ButtonKind.Danger));
            string leave = NetState.IsHost ? "STOP HOSTING" : NetState.IsClient ? "LEAVE SERVER" : "MAIN MENU";
            string leaveBody = NetState.IsHost ? "Everyone on your server will be disconnected." : NetState.IsClient ? "You'll go back to the main menu." : "Back to the main menu.";
            if (UI.Button(B(), leave, ButtonKind.Danger, IconFactory.Get("home"))) UIRoot.Push(new ConfirmDialog(leave + "?", leaveBody, () => GameManager.I.ReturnToMenu(), "LEAVE", ButtonKind.Danger));
            if (UI.Button(B(), "QUIT GAME", ButtonKind.Danger, IconFactory.Get("power"))) UIRoot.Push(new ConfirmDialog("Quit the game?", "The Goredolls will be relieved.", () => GameManager.I.Quit(), "QUIT", ButtonKind.Danger));
            UI.Label(new Rect(left.x + 28, left.yMax - 50, left.width - 56, 30), $"Version: {GameInfo.Version}   Access: {GameInfo.Access}", 15, UI.Muted);

            // ---- right: game settings
            var right = new Rect(left.xMax + 30, 80, Mathf.Min(980, W - left.xMax - 90), H - 160);
            if (NetState.IsClient)
            {
                // Clients: the host owns the game rules. Show the player list (mute, see who has Creative).
                UI.Panel(right, "Players", a);
                PlayersPanel.Draw(new Rect(right.x + 24, right.y + 80, right.width - 48, right.height - 110));
                return;
            }
            UI.Panel(right, NetState.IsHost ? "Server & Game Settings" : "Game Settings", a);
            var names = NetState.IsHost ? HostTabs : Tabs;
            tab = Mathf.Clamp(UI.Tabs(new Rect(right.x + 24, right.y + 76, right.width - 48, 48), tab, names, 18), 0, names.Length - 1);
            var view = new Rect(right.x + 24, right.y + 140, right.width - 48, right.height - 230);
            if (NetState.IsHost && tab == 0) { PlayersPanel.Draw(new Rect(view.x, view.y, view.width, view.height + 80)); return; }
            GameRulesPanel.Draw(view, NetState.IsHost ? tab - 1 : tab);
            if (UI.Button(new Rect(right.x + 24, right.yMax - 76, 280, 54), "RESET ALL RULES", ButtonKind.Ghost, IconFactory.Get("refresh"), 18))
            {
                Rules.ResetAll();
                GameEvents.ShowToast("All game rules reset");
            }
        }
    }

    /// <summary>The rule toggles/sliders, shared by the pause menu and the dev panel.</summary>
    public static class GameRulesPanel
    {
        static int row;
        static float scrollY, width;

        static Rect Next() { var r = new Rect(0, row * 62 + scrollY, width, 56); row++; return r; }

        public static int Rows(int tab) => tab == 0 ? 11 : tab == 1 ? 13 : tab == 2 ? 7 : 6;

        public static void Draw(Rect view, int tab)
        {
            float content = Rows(tab) * 62f + 10f;
            float s = UI.BeginScroll("rules" + tab, view, content);
            row = 0; scrollY = -s; width = view.width - 16;
            var p = PlayerController.Instance;
            switch (tab)
            {
                case 0:
                    UI.Toggle(Next(), "No fall damage", ref Rules.NoFallDamage, "Land from any height without getting hurt.");
                    UI.Toggle(Next(), "Invincibility", ref Rules.Invincible, "You can't be hurt by anything.");
                    UI.Toggle(Next(), "Infinite ammo", ref Rules.InfiniteAmmo, "Magazines never run dry.");
                    UI.Toggle(Next(), "Instant reload", ref Rules.NoReload, "Reloads finish almost instantly.");
                    UI.Toggle(Next(), "Noclip allowed (V)", ref Rules.NoclipAllowed, "Fly through everything except map Boundaries.");
                    if (p != null)
                    {
                        bool nc = p.Noclip;
                        if (UI.Toggle(Next(), "Noclip on", ref nc)) p.SetNoclip(nc);
                    }
                    else Next();
                    UI.Toggle(Next(), "Super kick", ref Rules.SuperKick, "Kicks and haymakers send things flying.");
                    UI.Toggle(Next(), "Explosive bullets", ref Rules.ExplosiveBullets, "Every bullet impact explodes.");
                    UI.Slider(Next(), "Movement speed", ref Rules.PlayerSpeedMultiplier, 0.25f, 4f, "0.00", null, "×");
                    UI.Slider(Next(), "Jump height", ref Rules.JumpMultiplier, 0.25f, 4f, "0.00", null, "×");
                    if (p != null && UI.Button(Next(), "Heal to full", ButtonKind.Ghost, IconFactory.Get("heart"))) { p.Health.Revive(); AudioManager.Ui("ui_success"); }
                    break;
                case 1:
                    UI.Toggle(Next(), "AI ignore player", ref Rules.AiIgnorePlayer, "Goredolls and nextbots act as if you're not there.");
                    UI.Toggle(Next(), "Freeze AI", ref Rules.FreezeAi, "Brains stop thinking (bodies still react to physics).");
                    UI.Toggle(Next(), "Goredoll infighting", ref Rules.NpcInfighting, "Aggressive Goredolls attack each other.");
                    UI.Slider(Next(), "AI aggression", ref Rules.AiAggression, -1f, 1f, "0.00", "Shifts every personality toward cowardice (-) or rage (+).");
                    UI.Slider(Next(), "Goredoll speed", ref Rules.NpcSpeedMultiplier, 0.25f, 3f, "0.00", null, "×");
                    UI.Slider(Next(), "Goredoll toughness", ref Rules.NpcHealthMultiplier, 0.1f, 10f, "0.00", null, "×");
                    UI.Toggle(Next(), "One-hit kill", ref Rules.OneHitKill, "Any damage kills.");
                    UI.Toggle(Next(), "Headshots always kill", ref Rules.InstantDeathOnHeadshot);
                    UI.Toggle(Next(), "Always dismember", ref Rules.AlwaysDismember, "Every severing hit takes the limb off.");
                    UI.Toggle(Next(), "No bleeding", ref Rules.NoBleeding);
                    UI.Toggle(Next(), "No pain reactions", ref Rules.NoPain);
                    UI.Slider(Next(), "Bleeding rate", ref Rules.BleedMultiplier, 0f, 5f, "0.00", null, "×");
                    UI.Toggle(Next(), "Big heads (new spawns)", ref Rules.BigHeads, "Goredolls spawned from now on have huge heads.");
                    break;
                case 2:
                {
                    float ts = Rules.TimeScale;
                    if (UI.Slider(Next(), "Time scale", ref ts, 0.05f, 2f, "0.00", "Slow motion below 1.", "×")) Rules.TimeScale = ts;
                    UI.Toggle(Next(), "Slow-mo on kill", ref Rules.SlowMoOnKill);
                    if (UI.Slider(Next(), "Gravity", ref Rules.GravityScale, 0f, 3f, "0.00", null, "×")) Rules.ApplyGravity();
                    UI.Slider(Next(), "Damage", ref Rules.DamageMultiplier, 0.1f, 10f, "0.00", null, "×");
                    UI.Slider(Next(), "Bullet force", ref Rules.BulletForceMultiplier, 0f, 10f, "0.00", null, "×");
                    UI.Toggle(Next(), "Bullet tracers", ref Rules.BulletTracers);
                    if (UI.Button(Next(), "Clear blood & decals", ButtonKind.Ghost, IconFactory.Get("drop"))) { Decals.ClearAll(); BloodFX.Clear(); }
                    break;
                }
                case 3:
                    UI.Toggle(Next(), "Show AI thoughts", ref Rules.ShowAiThoughts, "Floating text with what each brain is thinking.");
                    UI.Toggle(Next(), "Show colliders", ref Rules.ShowColliders, "Wireframe physics shapes.");
                    UI.Toggle(Next(), "X-ray organs", ref Rules.Xray, "See the organs inside every Goredoll.");
                    UI.Toggle(Next(), "Wireframe world", ref CameraOverlays.Wireframe);
                    bool hud = GameManager.I != null && GameManager.I.HudHidden;
                    if (UI.Toggle(Next(), "Hide HUD (F3)", ref hud) && GameManager.I != null) GameManager.I.HudHidden = hud;
                    if (UI.Button(Next(), "Take screenshot (F2)", ButtonKind.Ghost, IconFactory.Get("camera"))) GameManager.I?.Screenshot();
                    break;
            }
            UI.EndScroll("rules" + tab, view, content);
        }
    }
}
