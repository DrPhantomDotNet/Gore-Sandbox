using System.Collections.Generic;
using SolarEclipse.Mods;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The Modding Hub (main menu > Mods). BROWSE = the online Mod board, INSTALLED = mods you play with, MY MODS = mods
    /// you made (edit / upload / share code / delete). ADVANCED MODDING shows the same tabs for the custom-code kinds
    /// (SandScript weapons and NPCs) with a CREATE button per kind. Touch friendly: big rows and buttons only.
    /// </summary>
    public sealed class ModsScreen : UIScreen
    {
        static readonly string[] TabNames = { "BROWSE", "INSTALLED", "MY MODS" };
        static readonly string[] Sorts = { "Newest", "Name" };
        readonly bool advanced;
        int tab;
        int sort;
        string search = "";
        // Popups
        string infoTitle, infoText;
        readonly List<string> infoNotes = new List<string>();
        bool importOpen;
        string importText = "";

        public ModsScreen(bool advanced = false, int tab = 0) { this.advanced = advanced; this.tab = tab; }

        public override bool IsModal => true;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            ModStore.Boot();
            ModBoard.Connect();
            AdvancedModding.SaveHook = SaveFromAdvancedEditor;
        }

        public override bool OnBack()
        {
            if (infoTitle != null) { infoTitle = null; return true; }
            if (importOpen) { importOpen = false; return true; }
            AudioManager.Ui("ui_close");
            UIRoot.Pop();
            return true;
        }

        /// <summary>Advanced editors (Mods/ModSupport.cs) save through here: into MY MODS, and installed.</summary>
        static void SaveFromAdvancedEditor(ModPackage p)
        {
            if (string.IsNullOrEmpty(p.authorGuid)) p.authorGuid = ModText.Id(ModStore.LocalGuid);
            if (string.IsNullOrEmpty(p.author)) p.author = ModStore.LocalName;
            var err = ModStore.SaveCreated(p);
            if (err == null && ModStore.FindInstalled(p.id) == null) err = ModStore.Install(p);
            Toasts.Show("MODS", err ?? "Saved to My Mods and installed.", err == null ? UI.Good : UI.Bad);
        }

        void Info(string title, string text, bool withNotes = false)
        {
            infoTitle = title;
            infoText = text;
            infoNotes.Clear();
            if (withNotes) infoNotes.AddRange(ModWeapons.LastNotes);
        }

        bool InScope(ModPackage p)
        {
            if (p == null) return false;
            var h = ModHub.Handler(p.Kind);
            bool adv = h != null ? h.Advanced : p.Advanced;
            if (adv != advanced) return false;
            if (search.Length > 0)
            {
                var q = search.ToLowerInvariant();
                if (!(p.name ?? "").ToLowerInvariant().Contains(q) && !(p.author ?? "").ToLowerInvariant().Contains(q) && !(p.description ?? "").ToLowerInvariant().Contains(q)) return false;
            }
            return true;
        }

        static List<ModPackage> examples;
        static List<ModPackage> Examples
        {
            get
            {
                if (examples != null) return examples;
                examples = new List<ModPackage>();
                try
                {
                    foreach (var x in AdvancedExamples.All)
                    {
                        if (string.IsNullOrEmpty(x.authorGuid)) x.authorGuid = "goresandboxexamples";
                        if (ModStore.Validate(x) == null) examples.Add(x);
                        else Debug.LogWarning("[Mods] example '" + x.name + "' failed validation: " + ModStore.Validate(x));
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("[Mods] examples: " + e.Message); }
                return examples;
            }
        }

        List<ModPackage> Items()
        {
            var list = new List<ModPackage>();
            if (tab == 0)
            {
                foreach (var e in ModBoard.Entries.Values) { if (InScope(e.Pkg)) list.Add(e.Pkg); }
                // Built-in examples (Mods/AdvancedExamples.cs) so Advanced Modding is never empty.
                if (advanced) foreach (var x in Examples) if (InScope(x) && !list.Exists(p => p.id == x.id)) list.Add(x);
            }
            else foreach (var p in tab == 1 ? ModStore.Installed : ModStore.Created) if (InScope(p)) list.Add(p);
            if (sort == 0) list.Sort((a, b) => b.updated.CompareTo(a.updated));
            else list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        // ------------------------------------------------------------------ draw

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.7f * a);
            var r = UI.Centered(Mathf.Min(1700, UI.VW - 24), Mathf.Min(1050, UI.VH - 16));
            UI.Panel(r, advanced ? "Advanced Modding" : "Modding Hub", a);
            bool popup = infoTitle != null || importOpen;
            var old = GUI.enabled;
            GUI.enabled = !popup;

            // Header row: tabs + advanced switch
            float x = r.x + 24, y = r.y + 76;
            tab = UI.Tabs(new Rect(x, y, Mathf.Min(720, r.width * 0.5f), 60), tab, TabNames, 22);
            var advRect = new Rect(r.xMax - 24 - 380, y, 380, 60);
            if (UI.Button(advRect, advanced ? "WEAPON MODS" : "ADVANCED MODDING", advanced ? ButtonKind.Normal : ButtonKind.Primary, IconFactory.Get(advanced ? "gun" : "wrench"), 22))
                UIRoot.Replace(new ModsScreen(!advanced, tab));
            y += 74;

            // Toolbar: search, sort, import, create
            search = UI.TextField(new Rect(x, y, 380, 60), search, "mods_search", false, "Search mods...");
            UI.Selector(new Rect(x + 392, y, 300, 60), "Sort", ref sort, Sorts);
            if (UI.Button(new Rect(x + 704, y, 230, 60), "IMPORT CODE", ButtonKind.Normal, IconFactory.Get("arrow_down"), 20)) { importOpen = true; importText = ""; }
            float cx = r.xMax - 24;
            foreach (var h in Handlers())
            {
                string label = "NEW " + h.Title.ToUpperInvariant();
                float bw = Mathf.Max(220, UI.TextWidth(label, 20, FontStyle.Bold) + 90);
                cx -= bw;
                if (cx < x + 946) break;
                if (UI.Button(new Rect(cx, y, bw, 60), label, ButtonKind.Primary, IconFactory.Get("plus"), 20)) OpenEditor(h, null);
                cx -= 12;
            }
            y += 72;

            // Status line
            string status = tab == 0
                ? "Mod board: " + ModBoard.Status + "  -  " + ModBoard.Entries.Count + " mods online" + (ModBoard.Rejected > 0 ? "  -  " + ModBoard.Rejected + " blocked by the virus/cheat check" : "")
                : tab == 1 ? ModStore.Installed.Count + " installed. Installed weapons show up in the spawn menu with a MODDED tag."
                : "Mods you made. Upload them to the Mod board or copy a share code for Discord.";
            UI.Label(new Rect(x, y, r.width - 48, 30), status, 17, UI.Accent2);
            UI.Label(new Rect(x, y + 28, r.width - 48, 30), "Mods are stored on free public servers and may disappear - keep your share code. Mods are data only: they can't run programs on your device.", 15, UI.Muted);
            y += 66;

            // List
            var view = new Rect(x, y, r.width - 48, r.yMax - y - 90);
            DrawList(view);

            if (UI.Button(new Rect(r.x + 24, r.yMax - 76, 220, 60), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            if (advanced && Handlers().Count == 0)
                UI.Label(new Rect(r.x + 260, r.yMax - 76, r.width - 300, 60), "No advanced mod kinds are available in this build.", 17, UI.Muted);
            GUI.enabled = old;

            if (infoTitle != null) DrawInfo();
            else if (importOpen) DrawImport();
        }

        List<ModKindHandler> Handlers()
        {
            var list = new List<ModKindHandler>();
            foreach (var h in ModHub.Kinds.Values) if (h.Advanced == advanced && h.OpenEditor != null) list.Add(h);
            list.Sort((a, b) => ((int)b.Kind).CompareTo((int)a.Kind));
            return list;
        }

        void OpenEditor(ModKindHandler h, ModPackage p)
        {
            try { h.OpenEditor(p == null ? null : ModStore.Copy(p)); }
            catch (System.Exception e) { Info("EDITOR", "The editor couldn't open: " + e.Message); }
        }

        void DrawList(Rect view)
        {
            var items = Items();
            const float rowH = 150f;
            float content = Mathf.Max(view.height, items.Count * (rowH + 12f) + 10f);
            float s = UI.BeginScroll("mods_list_" + tab + advanced, view, content);
            if (items.Count == 0)
            {
                string empty = tab == 0 ? (ModBoard.Online ? "No mods found online yet. Be the first: make one in MY MODS and upload it!" : "Connecting to the Mod board...")
                    : tab == 1 ? "Nothing installed yet. Install mods from BROWSE or import a share code."
                    : "You haven't made any mods yet. Tap " + (advanced ? "a NEW button" : "NEW WEAPON") + " above.";
                UI.Label(new Rect(0, 40 - s, view.width, 60), empty, 22, UI.Muted, TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < items.Count; i++)
            {
                var rr = new Rect(0, i * (rowH + 12f) - s, view.width - 14, rowH);
                if (rr.yMax < -10 || rr.y > view.height + 10) continue;
                DrawCard(rr, items[i]);
            }
            UI.EndScroll("mods_list_" + tab + advanced, view, content);
        }

        void DrawCard(Rect rr, ModPackage p)
        {
            var h = ModHub.Handler(p.Kind);
            UI.Rounded(rr, UI.PanelLight, false);
            UI.Rounded(new Rect(rr.x + 14, rr.y + 14, 120, 120), new Color(0, 0, 0, 0.3f), false);
            Texture icon = null;
            if (p.Kind == ModKind.Weapon) { var def = ModWeapons.DefFor(p.id); if (def != null) icon = Hud.WeaponIcon(def); }
            UI.Tex(new Rect(rr.x + 24, rr.y + 24, 100, 100), icon != null ? icon : IconFactory.Get(h != null ? h.Icon : "box"), Color.white, ScaleMode.ScaleToFit);

            float tx = rr.x + 150;
            float buttonsW = 560f;
            float textW = rr.width - 150 - buttonsW - 20;
            string name = p.name ?? "";
            UI.Label(new Rect(tx, rr.y + 10, textW, 40), name.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            float tagX = tx + Mathf.Min(textW - 110, UI.TextWidth(name.ToUpperInvariant(), 26, FontStyle.Normal, UI.HeavyFont) + 12);
            UI.Rounded(new Rect(tagX, rr.y + 16, 100, 28), UI.Accent2, true);
            UI.Label(new Rect(tagX, rr.y + 16, 100, 28), "MODDED", 15, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            string kind = h != null ? h.Title : "Unknown kind";
            UI.Label(new Rect(tx, rr.y + 50, textW, 30), "by " + p.author + "  -  v" + p.version + "  -  " + kind, 17, UI.Accent2);
            UI.Label(new Rect(tx, rr.y + 80, textW, 62), string.IsNullOrEmpty(p.description) ? "No description." : p.description, 16, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);

            // Buttons (2 rows of 2, 270 x 60)
            var bx = rr.xMax - buttonsW - 10;
            Rect B(int col, int row) => new Rect(bx + col * 282, rr.y + 10 + row * 68, 272, 60);
            var installed = ModStore.FindInstalled(p.id);
            bool mine = ModStore.IsMine(p);
            ModBoard.Entries.TryGetValue(p.id, out var online);
            switch (tab)
            {
                case 0:
                {
                    bool newer = installed != null && installed.version < p.version;
                    if (installed == null) { if (UI.Button(B(1, 0), "INSTALL", ButtonKind.Primary, IconFactory.Get("arrow_down"), 22)) DoInstall(p); }
                    else if (newer) { if (UI.Button(B(1, 0), "UPDATE", ButtonKind.Primary, IconFactory.Get("refresh"), 22)) DoInstall(p); }
                    else if (UI.Button(B(1, 0), "UNINSTALL", ButtonKind.Danger, IconFactory.Get("trash"), 20)) DoUninstall(p);
                    if (UI.Button(B(0, 0), "SHARE CODE", ButtonKind.Ghost, IconFactory.Get("copy"), 18)) CopyCode(p);
                    if (mine || DevPanel.LoggedIn)
                        if (UI.Button(B(1, 1), DevPanel.LoggedIn && !mine ? "REMOVE (DEV)" : "REMOVE ONLINE", ButtonKind.Danger, IconFactory.Get("close"), 18))
                            UIRoot.Push(new ConfirmDialog("REMOVE MOD", "Remove '" + p.name + "' from the Mod board for everyone? Installed copies keep working.", () =>
                            {
                                var err = ModBoard.Remove(p.id);
                                Info("MOD BOARD", err ?? "Removed from the Mod board.");
                            }, "REMOVE", ButtonKind.Danger));
                    if (installed != null && !newer) UI.Label(B(0, 1), "Installed", 18, UI.Good, TextAnchor.MiddleCenter);
                    break;
                }
                case 1:
                    if (UI.Button(B(1, 0), "UNINSTALL", ButtonKind.Danger, IconFactory.Get("trash"), 20)) DoUninstall(p);
                    if (UI.Button(B(0, 0), "SHARE CODE", ButtonKind.Ghost, IconFactory.Get("copy"), 18)) CopyCode(p);
                    if (online != null && online.Pkg.version > p.version && online.Pkg.authorGuid == p.authorGuid)
                        { if (UI.Button(B(1, 1), "UPDATE v" + online.Pkg.version, ButtonKind.Primary, IconFactory.Get("refresh"), 20)) DoInstall(online.Pkg); }
                    if (mine && h != null && h.OpenEditor != null && UI.Button(B(0, 1), "EDIT", ButtonKind.Normal, IconFactory.Get("wrench"), 20))
                        OpenEditor(h, ModStore.FindCreated(p.id) ?? p);
                    break;
                default:
                    if (h != null && h.OpenEditor != null && UI.Button(B(0, 0), "EDIT", ButtonKind.Normal, IconFactory.Get("wrench"), 20)) OpenEditor(h, p);
                    bool up = online != null;
                    if (UI.Button(B(1, 0), up ? "UPDATE ONLINE" : "UPLOAD", ButtonKind.Primary, IconFactory.Get("arrow_up"), 20)) DoUpload(p);
                    if (UI.Button(B(0, 1), "SHARE CODE", ButtonKind.Ghost, IconFactory.Get("copy"), 18)) CopyCode(p);
                    if (installed == null) { if (UI.Button(new Rect(B(1, 1).x, B(1, 1).y, 130, 60), "PLAY", ButtonKind.Normal, IconFactory.Get("arrow_down"), 18)) DoInstall(p); }
                    else if (UI.Button(new Rect(B(1, 1).x, B(1, 1).y, 130, 60), "UNINST.", ButtonKind.Ghost, null, 16)) DoUninstall(p);
                    if (UI.Button(new Rect(B(1, 1).x + 142, B(1, 1).y, 130, 60), "DELETE", ButtonKind.Danger, null, 16))
                        UIRoot.Push(new ConfirmDialog("DELETE MOD", "Delete '" + p.name + "' from My Mods? Copy its share code first if you want to keep it.", () => ModStore.DeleteCreated(p.id), "DELETE", ButtonKind.Danger));
                    break;
            }
        }

        // ------------------------------------------------------------------ actions

        void DoInstall(ModPackage p)
        {
            var err = ModStore.Install(p);
            if (err != null) { AudioManager.Ui("ui_error"); Info("NOT INSTALLED", "Blocked by the safety check: " + err); return; }
            AudioManager.Ui("ui_success");
            if (ModWeapons.LastNotes.Count > 0 && p.Kind == ModKind.Weapon) Info("INSTALLED", "'" + p.name + "' is installed. Some values were over the fair-play limits and were clamped:", true);
            else Toasts.Show("INSTALLED", p.name + (p.Kind == ModKind.Weapon ? " is in the spawn menu (GUNS) with a MODDED tag." : " is installed."), UI.Good);
        }

        void DoUninstall(ModPackage p)
        {
            ModStore.Uninstall(p.id);
            Toasts.Show("UNINSTALLED", p.name, UI.Warn);
        }

        void DoUpload(ModPackage src)
        {
            var p = ModStore.Copy(src);
            ModBoard.Entries.TryGetValue(p.id, out var online);
            if (online != null && online.Pkg.version >= p.version) p.version = online.Pkg.version + 1;
            var err = ModStore.Validate(p);
            if (err == null)
            {
                err = ModBoard.Upload(p);
                if (err == null || err.StartsWith("Queued")) { src.version = p.version; ModStore.SaveCreated(src); }
            }
            if (err != null && !err.StartsWith("Queued")) { AudioManager.Ui("ui_error"); Info("NOT UPLOADED", err); return; }
            AudioManager.Ui("ui_success");
            Info("UPLOADED", (err ?? "'" + p.name + "' v" + p.version + " is on the Mod board.") + " Free public servers can drop mods at any time, so keep your share code too.", ModWeapons.LastNotes.Count > 0 && p.Kind == ModKind.Weapon);
        }

        void CopyCode(ModPackage p)
        {
            try
            {
                var code = ModCodec.ToCode(p);
                GUIUtility.systemCopyBuffer = code;
                Info("SHARE CODE COPIED", "The share code for '" + p.name + "' (" + code.Length / 1024f + " KB of text) is on your clipboard. Paste it on Discord; others use IMPORT CODE to get the mod.");
            }
            catch (System.Exception e) { Info("SHARE CODE", "Couldn't make the code: " + e.Message); }
        }

        // ------------------------------------------------------------------ popups

        void DrawInfo()
        {
            UI.Dim(0.5f);
            float h = 320 + Mathf.Min(infoNotes.Count, 10) * 30;
            var r = UI.Centered(1000, h);
            UI.Panel(r, infoTitle);
            UI.Label(new Rect(r.x + 30, r.y + 80, r.width - 60, 120), infoText, 21, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            float y = r.y + 190;
            for (int i = 0; i < infoNotes.Count && i < 10; i++, y += 30)
                UI.Label(new Rect(r.x + 40, y, r.width - 80, 30), "- " + infoNotes[i], 17, UI.Warn);
            if (infoNotes.Count > 10) UI.Label(new Rect(r.x + 40, y, r.width - 80, 30), "...and " + (infoNotes.Count - 10) + " more", 17, UI.Warn);
            if (UI.Button(new Rect(r.xMax - 250, r.yMax - 84, 220, 64), "OK", ButtonKind.Primary, IconFactory.Get("check"), 24)) infoTitle = null;
        }

        void DrawImport()
        {
            UI.Dim(0.5f);
            var r = UI.Centered(1100, 420);
            UI.Panel(r, "Import share code");
            UI.Label(new Rect(r.x + 30, r.y + 76, r.width - 60, 60), "Paste a mod code (starts with " + ModCodec.Prefix + "). It's checked for cheats and anything that isn't plain mod data before it's installed.", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            importText = UI.TextField(new Rect(r.x + 30, r.y + 150, r.width - 60, 64), importText, "mods_import", false, "GSMOD1:...");
            if (importText != null && importText.Length > 200000) importText = importText.Substring(0, 200000);
            if (UI.Button(new Rect(r.x + 30, r.y + 236, 300, 64), "PASTE", ButtonKind.Normal, IconFactory.Get("copy"), 22))
            {
                try { importText = GUIUtility.systemCopyBuffer ?? ""; } catch { importText = ""; }
            }
            if (UI.Button(new Rect(r.x + 30, r.yMax - 84, 220, 64), "CANCEL", ButtonKind.Ghost, null, 22)) importOpen = false;
            if (UI.Button(new Rect(r.xMax - 330, r.yMax - 84, 300, 64), "IMPORT", ButtonKind.Primary, IconFactory.Get("arrow_down"), 24, !string.IsNullOrWhiteSpace(importText)))
            {
                var err = ModStore.Import(importText, out var p);
                importOpen = false;
                if (err != null) { AudioManager.Ui("ui_error"); Info("NOT IMPORTED", err); }
                else
                {
                    AudioManager.Ui("ui_success");
                    bool mineMod = ModStore.IsMine(p);
                    Info("IMPORTED", "'" + p.name + "' by " + p.author + (mineMod ? " was added to MY MODS (it's yours)." : " is installed."), p.Kind == ModKind.Weapon && ModWeapons.LastNotes.Count > 0);
                    tab = mineMod ? 2 : 1;
                }
            }
        }
    }
}
