using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace SolarEclipse
{
    /// <summary>
    /// Auto-updater. On startup it asks GitHub for the latest release of the game's repository. If that release is a
    /// newer version, the update screen downloads it, installs it and restarts the game.
    ///
    /// Windows: fully automatic (download zip → extract → a small helper script waits for the game to close, copies
    /// the new files over this install, and starts the new version). Other platforms: opens the download page.
    ///
    /// The repository comes from Resources/update_repo.txt, which the GitHub cloud build writes automatically
    /// (owner/repo), or from <see cref="GameInfo.UpdateRepo"/>. Releases are created by .github/workflows/build.yml.
    /// </summary>
    public static class Updater
    {
        [Serializable] public class Asset { public string name; public string browser_download_url; public long size; }
        [Serializable] public class Release { public string tag_name; public string name; public string body; public string html_url; public bool draft; public bool prerelease; public Asset[] assets; }

        public enum Phase { Idle, Checking, UpToDate, Available, Downloading, Installing, Restarting, Failed, ManualOnly }

        public static Phase State { get; private set; } = Phase.Idle;
        public static Release Latest { get; private set; }
        public static string LatestVersion { get; private set; } = "";
        public static string Error { get; private set; } = "";
        public static float Progress { get; private set; }
        public static long DownloadedBytes { get; private set; }
        public static long TotalBytes { get; private set; }

        public static string Repo
        {
            get
            {
                try
                {
                    var t = Resources.Load<TextAsset>("update_repo");
                    if (t != null && !string.IsNullOrWhiteSpace(t.text)) return t.text.Trim();
                }
                catch { }
                return (GameInfo.UpdateRepo ?? "").Trim();
            }
        }

        /// <summary>Auto-install works on Windows builds; the editor never updates itself.</summary>
        public static bool CanAutoInstall => Application.platform == RuntimePlatform.WindowsPlayer;
        public static bool Enabled => !Application.isEditor && Settings.Data.autoUpdate && Repo.Contains("/");

        // ------------------------------------------------------------------ check

        /// <summary>Checks GitHub in the background; calls <paramref name="onNewer"/> on the main thread if an update exists.</summary>
        public static IEnumerator Check(Action onNewer)
        {
            if (!Enabled || State == Phase.Checking) yield break;
            State = Phase.Checking;
            using (var req = UnityWebRequest.Get("https://api.github.com/repos/" + Repo + "/releases/latest"))
            {
                req.SetRequestHeader("User-Agent", "GoreSandbox-Updater");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (!Ok(req)) { State = Phase.UpToDate; Debug.Log("[Updater] check failed: " + req.error); yield break; }
                Release rel = null;
                try { rel = JsonUtility.FromJson<Release>(req.downloadHandler.text); } catch (Exception e) { Debug.LogWarning("[Updater] bad release JSON: " + e.Message); }
                if (rel == null || string.IsNullOrEmpty(rel.tag_name) || rel.draft) { State = Phase.UpToDate; yield break; }
                Latest = rel;
                LatestVersion = rel.tag_name.TrimStart('v', 'V');
                if (Compare(LatestVersion, GameInfo.Version) <= 0) { State = Phase.UpToDate; Debug.Log("[Updater] up to date (" + GameInfo.Version + ")"); yield break; }
                State = CanAutoInstall && FindAsset() != null ? Phase.Available : Phase.ManualOnly;
                Debug.Log("[Updater] new version " + LatestVersion + " (have " + GameInfo.Version + ")");
                onNewer?.Invoke();
            }
        }

        static bool Ok(UnityWebRequest r)
        {
#if UNITY_2020_2_OR_NEWER
            return r.result == UnityWebRequest.Result.Success;
#else
            return !r.isNetworkError && !r.isHttpError;
#endif
        }

        /// <summary>"0.10.1" vs "0.9" → 1. Non-numeric parts are ignored.</summary>
        public static int Compare(string a, string b)
        {
            var pa = (a ?? "").Split('.', '-', '+');
            var pb = (b ?? "").Split('.', '-', '+');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out var vx) ? vx : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out var vy) ? vy : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        public static Asset FindAsset()
        {
            if (Latest?.assets == null) return null;
            foreach (var a in Latest.assets)
                if (a != null && a.name != null && a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && a.name.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0) return a;
            return null;
        }

        /// <summary>The Android APK of the latest release (phones download it and tap to install).</summary>
        public static Asset FindApk()
        {
            if (Latest?.assets == null) return null;
            foreach (var a in Latest.assets)
                if (a != null && a.name != null && a.name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        // ------------------------------------------------------------------ install (Windows)

        static string InstallDir => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string WorkDir => Path.Combine(Application.temporaryCachePath, "update");

        /// <summary>Can we write next to the game? (Program Files needs admin; we never ask for it.)</summary>
        public static bool InstallDirWritable()
        {
            try
            {
                var probe = Path.Combine(InstallDir, ".update_probe");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static IEnumerator DownloadAndInstall()
        {
            var asset = FindAsset();
            if (asset == null) { Fail("This update has no Windows download."); yield break; }
            if (!InstallDirWritable())
            {
                Fail("The game is installed in a folder it can't write to (like Program Files), and updating there would need an admin password. Move the game folder to your Desktop or Documents, or download the update manually.");
                yield break;
            }
            State = Phase.Downloading;
            Progress = 0f;
            string zip = Path.Combine(Application.temporaryCachePath, "GoreSandbox-update.zip");
            try { if (Directory.Exists(WorkDir)) Directory.Delete(WorkDir, true); if (File.Exists(zip)) File.Delete(zip); } catch { }
            TotalBytes = asset.size;
            using (var req = new UnityWebRequest(asset.browser_download_url, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
                req.SetRequestHeader("User-Agent", "GoreSandbox-Updater");
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    Progress = req.downloadProgress >= 0 ? req.downloadProgress : 0f;
                    DownloadedBytes = (long)req.downloadedBytes;
                    yield return null;
                }
                if (!Ok(req)) { Fail("Download failed: " + req.error); yield break; }
            }
            Progress = 1f;

            State = Phase.Installing;
            yield return null;
            string error = null, newRoot = null;
            var extract = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Unzip(zip, WorkDir);
                    newRoot = FindGameRoot(WorkDir);
                    if (newRoot == null) error = "The download doesn't contain the game.";
                }
                catch (Exception e) { error = "Couldn't unpack the update: " + e.Message; }
            });
            while (!extract.IsCompleted) yield return null;
            if (error != null) { Fail(error); yield break; }

            try { LaunchInstaller(newRoot, zip); }
            catch (Exception e) { Fail("Couldn't start the installer: " + e.Message); yield break; }
            State = Phase.Restarting;
            yield return new WaitForSecondsRealtime(1.2f);
            Application.Quit();
        }

        /// <summary>Extracts a zip with ZipArchive (always available in Unity, unlike the ZipFile helper).</summary>
        static void Unzip(string zipPath, string dest)
        {
            string root = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);
            using (var fs = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // ignore paths escaping the folder
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var src = entry.Open())
                    using (var dst = File.Create(target)) src.CopyTo(dst);
                }
            }
        }

        /// <summary>The folder holding the new GoreSandbox.exe (the zip may or may not have a top folder).</summary>
        static string FindGameRoot(string dir)
        {
            foreach (var exe in Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(exe);
                var folder = Path.GetDirectoryName(exe);
                if (name.IndexOf("crash", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (Directory.Exists(Path.Combine(folder, name + "_Data"))) return folder;
            }
            return null;
        }

        /// <summary>
        /// A batch file that waits for this process to exit, copies the new files over the install (robocopy keeps
        /// retrying while Windows releases file locks), cleans up and starts the new version. Runs hidden, as the
        /// current user (no admin).
        /// </summary>
        static void LaunchInstaller(string newRoot, string zip)
        {
            string exeName = Directory.GetFiles(newRoot, "*.exe")[0];
            foreach (var e in Directory.GetFiles(newRoot, "*.exe"))
                if (Directory.Exists(Path.Combine(newRoot, Path.GetFileNameWithoutExtension(e) + "_Data"))) exeName = e;
            exeName = Path.GetFileName(exeName);
            int pid = Process.GetCurrentProcess().Id;
            string log = Path.Combine(Application.temporaryCachePath, "update.log");
            var bat = new StringBuilder();
            bat.AppendLine("@echo off");
            bat.AppendLine("setlocal");
            bat.AppendLine(":wait");
            bat.AppendLine($"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL");
            bat.AppendLine("if not errorlevel 1 ( timeout /t 1 /nobreak >NUL & goto wait )");
            bat.AppendLine($"robocopy \"{newRoot}\" \"{InstallDir}\" /E /IS /IT /R:10 /W:1 /NFL /NDL /NP > \"{log}\"");
            bat.AppendLine("if errorlevel 8 goto failed");
            bat.AppendLine($"rmdir /S /Q \"{WorkDir}\"");
            bat.AppendLine($"del /Q \"{zip}\"");
            bat.AppendLine($"start \"\" \"{Path.Combine(InstallDir, exeName)}\"");
            bat.AppendLine("goto end");
            bat.AppendLine(":failed");
            bat.AppendLine($"start \"\" \"{Path.Combine(InstallDir, exeName)}\" -updateFailed");
            bat.AppendLine(":end");
            bat.AppendLine("(goto) 2>nul & del \"%~f0\"");
            string batPath = Path.Combine(Application.temporaryCachePath, "GoreSandbox-update.bat");
            File.WriteAllText(batPath, bat.ToString(), Encoding.ASCII);
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + batPath + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Application.temporaryCachePath,
            });
            Debug.Log("[Updater] installer started; quitting so it can replace the files");
        }

        static void Fail(string why)
        {
            Error = why;
            State = Phase.Failed;
            Debug.LogWarning("[Updater] " + why);
        }

        /// <summary>True when the previous update's file copy failed (the installer passes -updateFailed).</summary>
        public static bool LastUpdateFailed => Environment.GetCommandLineArgs() is string[] a && Array.IndexOf(a, "-updateFailed") >= 0;
    }
}
