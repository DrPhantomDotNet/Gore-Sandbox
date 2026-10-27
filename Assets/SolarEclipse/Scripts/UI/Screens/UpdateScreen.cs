using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Shown over the main menu when a newer version exists. Windows: downloads and installs automatically, then the
    /// game restarts into the new version. Other platforms: a button to the download page.
    /// </summary>
    public sealed class UpdateScreen : UIScreen
    {
        public override bool IsModal => true;
        bool started;
        float startTime;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_notify");
            startTime = Time.unscaledTime;
        }

        public override bool OnBack() => Updater.State == Updater.Phase.Failed || Updater.State == Updater.Phase.ManualOnly ? Close() : true;

        bool Close() { UIRoot.Remove(this); return true; }

        public override void Tick()
        {
            // Start automatically after a short moment so players can read what's happening.
            if (!started && Updater.State == Updater.Phase.Available && Time.unscaledTime - startTime > 1.5f)
            {
                started = true;
                GameManager.I.StartCoroutine(Updater.DownloadAndInstall());
            }
        }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.8f * a);
            var r = UI.Centered(820, 520);
            UI.Panel(r, "Update available", a);
            var rel = Updater.Latest;
            UI.Label(new Rect(r.x + 30, r.y + 76, r.width - 60, 40), "<b>v" + GameInfo.Version + "  >  <color=#ffb040>v" + Updater.LatestVersion + "</color></b>", 30, Color.white);

            string notes = rel != null && !string.IsNullOrEmpty(rel.body) ? rel.body : (rel != null ? rel.name : "");
            if (notes != null && notes.Length > 600) notes = notes.Substring(0, 600) + "...";
            UI.Rounded(new Rect(r.x + 30, r.y + 128, r.width - 60, 190), UI.PanelLight, false);
            UI.Label(new Rect(r.x + 46, r.y + 138, r.width - 92, 170), string.IsNullOrEmpty(notes) ? "A new version of the game is ready." : notes, 16, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, false);

            var bar = new Rect(r.x + 30, r.y + 340, r.width - 60, 26);
            string status;
            float p = 0f;
            switch (Updater.State)
            {
                case Updater.Phase.Available: status = "Starting the update..."; break;
                case Updater.Phase.Downloading:
                    p = Updater.Progress;
                    status = "Downloading... " + Mathf.RoundToInt(p * 100) + "%" + (Updater.TotalBytes > 0 ? "   (" + (Updater.DownloadedBytes / 1048576f).ToString("0.0") + " / " + (Updater.TotalBytes / 1048576f).ToString("0.0") + " MB)" : "");
                    break;
                case Updater.Phase.Installing: p = 1f; status = "Installing... please wait, don't close the game."; break;
                case Updater.Phase.Restarting: p = 1f; status = "Installed! Restarting into v" + Updater.LatestVersion + "..."; break;
                case Updater.Phase.Failed: status = "<color=#ff6060>Update failed.</color> " + Updater.Error; break;
                case Updater.Phase.ManualOnly:
                    status = Application.platform == RuntimePlatform.Android && Updater.FindApk() != null
                        ? "Tap DOWNLOAD UPDATE, then open the downloaded file to install it. If Android asks, allow installing from your browser."
                        : "This version can't update itself automatically. Download the new version from the release page.";
                    break;
                default: status = ""; break;
            }
            bool busy = Updater.State == Updater.Phase.Downloading || Updater.State == Updater.Phase.Installing || Updater.State == Updater.Phase.Restarting || Updater.State == Updater.Phase.Available;
            if (busy)
            {
                UI.Rounded(bar, new Color(0, 0, 0, 0.5f), true);
                float shown = Updater.State == Updater.Phase.Installing ? Mathf.PingPong(Time.unscaledTime * 0.8f, 1f) : p;
                if (Updater.State == Updater.Phase.Installing) UI.Rounded(new Rect(bar.x + (bar.width - 160) * shown, bar.y, 160, bar.height), UI.Accent2, true);
                else UI.Rounded(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * shown), bar.height), UI.Accent, true);
            }
            UI.Label(new Rect(r.x + 30, r.y + 374, r.width - 60, 60), status, 18, UI.TextColor, TextAnchor.UpperLeft, FontStyle.Normal, true, true);

            if (Updater.State == Updater.Phase.Failed || Updater.State == Updater.Phase.ManualOnly)
            {
                var apk = Application.platform == RuntimePlatform.Android ? Updater.FindApk() : null;
                if (apk != null && UI.Button(new Rect(r.x + 30, r.yMax - 86, 300, 60), "DOWNLOAD UPDATE", ButtonKind.Primary, IconFactory.Get("arrow_down"), 20))
                    Application.OpenURL(apk.browser_download_url);
                else if (apk == null && rel != null && !string.IsNullOrEmpty(rel.html_url) && UI.Button(new Rect(r.x + 30, r.yMax - 86, 300, 60), "DOWNLOAD PAGE", ButtonKind.Primary, IconFactory.Get("arrow_down"), 20))
                    Application.OpenURL(rel.html_url);
                if (Updater.State == Updater.Phase.Failed && Updater.CanAutoInstall && UI.Button(new Rect(r.x + 350, r.yMax - 86, 200, 60), "RETRY", ButtonKind.Normal, IconFactory.Get("refresh"), 20))
                    GameManager.I.StartCoroutine(Updater.DownloadAndInstall());
                if (UI.Button(new Rect(r.xMax - 250, r.yMax - 86, 220, 60), "PLAY ANYWAY", ButtonKind.Ghost, null, 18)) Close();
            }
            else UI.Label(new Rect(r.x + 30, r.yMax - 80, r.width - 60, 50), "The game will restart by itself when the update is installed.", 15, UI.Muted, TextAnchor.MiddleLeft);
        }
    }
}
