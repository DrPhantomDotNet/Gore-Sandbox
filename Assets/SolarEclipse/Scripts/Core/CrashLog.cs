using System;
using System.IO;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Local crash reporter: errors and exceptions are appended to crash_log.txt in the game's save folder
    /// (never sent anywhere). Kept under 256 KB; the oldest half is dropped when it grows past that.
    /// </summary>
    public static class CrashLog
    {
        static string path;
        static int written;

        public static string FilePath => path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            try { path = Path.Combine(Application.persistentDataPath, "crash_log.txt"); }
            catch { return; }
            Application.logMessageReceivedThreaded += OnLog;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (written > 200) return; // a runaway error loop shouldn't hammer the disk
            written++;
            try
            {
                lock (typeof(CrashLog))
                {
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 256 * 1024)
                    {
                        var all = File.ReadAllText(path);
                        File.WriteAllText(path, all.Substring(all.Length / 2));
                    }
                    File.AppendAllText(path, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] v" + GameInfo.Version + " " + type + ": " + msg + "\n" + stack + "\n");
                }
            }
            catch { }
        }
    }
}
