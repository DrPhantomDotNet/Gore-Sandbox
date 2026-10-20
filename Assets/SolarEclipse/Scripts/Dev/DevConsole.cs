using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Captures Unity's log output (everything Debug.Log / warnings / errors / exceptions, from any thread) for
    /// the dev panel, and runs typed commands from the command registry.
    /// </summary>
    public static class DevConsole
    {
        public struct Entry
        {
            public LogType Type;
            public string Message, Stack;
            public float Time;
            public int Count;
        }

        public sealed class Command
        {
            public string Name, Help, Category, Usage;
            public Action<string[]> Run;
        }

        const int Max = 3000;
        static readonly List<Entry> entries = new List<Entry>(Max);
        static readonly Queue<Entry> incoming = new Queue<Entry>();
        static readonly object gate = new object();
        public static readonly SortedDictionary<string, Command> Commands = new SortedDictionary<string, Command>();
        public static readonly List<string> History = new List<string>();
        public static int Errors, Warnings, Logs;
        static bool hooked;

        static readonly HashSet<string> ClientSafe = new HashSet<string>
        {
            "help", "about", "clear", "echo", "history", "version", "sysinfo", "mem", "gc", "fps", "vsync", "quality", "hud", "toast",
            "log_test", "exception_test", "pos", "fov", "sens", "screenshot", "sounds", "play", "volume", "music", "voices", "stats", "achievements", "eggs",
            "npcs", "inspect", "quit", "menu", "net", "players",
        };

        public static IReadOnlyList<Entry> Entries { get { Drain(); return entries; } }

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            Application.logMessageReceivedThreaded += OnLog;
            DevCommands.RegisterAll();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            lock (gate) incoming.Enqueue(new Entry { Type = type, Message = message, Stack = stack, Count = 1, Time = -1f });
        }

        static void Drain()
        {
            lock (gate)
            {
                while (incoming.Count > 0)
                {
                    var e = incoming.Dequeue();
                    e.Time = Time.realtimeSinceStartup;
                    if (e.Type == LogType.Error || e.Type == LogType.Exception || e.Type == LogType.Assert) Errors++;
                    else if (e.Type == LogType.Warning) Warnings++;
                    else Logs++;
                    if (entries.Count > 0 && entries[entries.Count - 1].Message == e.Message && entries[entries.Count - 1].Type == e.Type)
                    {
                        var last = entries[entries.Count - 1];
                        last.Count++;
                        last.Time = e.Time;
                        entries[entries.Count - 1] = last;
                        continue;
                    }
                    entries.Add(e);
                    if (entries.Count > Max) entries.RemoveAt(0);
                }
            }
        }

        public static void Clear() { Drain(); entries.Clear(); Errors = Warnings = Logs = 0; }

        public static void Register(string name, string category, string help, Action<string[]> run, string usage = "")
        {
            Commands[name.ToLowerInvariant()] = new Command { Name = name.ToLowerInvariant(), Category = category, Help = help, Run = run, Usage = usage };
        }

        public static void Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            line = line.Trim();
            History.Remove(line);
            History.Add(line);
            if (History.Count > 100) History.RemoveAt(0);
            Debug.Log("> " + line);
            foreach (var part in line.Split(';'))
            {
                var args = Tokenize(part.Trim());
                if (args.Length == 0) continue;
                if (NetState.IsClient && !ClientSafe.Contains(args[0].ToLowerInvariant()))
                {
                    Debug.LogWarning("In multiplayer only the host can run world commands. You can use: " + string.Join(", ", ClientSafe));
                    continue;
                }
                if (!Commands.TryGetValue(args[0].ToLowerInvariant(), out var cmd))
                {
                    var close = Commands.Keys.Where(k => k.Contains(args[0].ToLowerInvariant())).Take(5).ToArray();
                    Debug.LogWarning("Unknown command '" + args[0] + "'." + (close.Length > 0 ? " Did you mean: " + string.Join(", ", close) : " Type 'help'."));
                    continue;
                }
                try { cmd.Run(args.Skip(1).ToArray()); }
                catch (Exception e) { Debug.LogError(cmd.Name + " failed: " + e.Message + (cmd.Usage.Length > 0 ? "  usage: " + cmd.Name + " " + cmd.Usage : "")); }
            }
        }

        static string[] Tokenize(string s)
        {
            var list = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool quoted = false;
            foreach (var ch in s)
            {
                if (ch == '"') { quoted = !quoted; continue; }
                if (char.IsWhiteSpace(ch) && !quoted) { if (cur.Length > 0) { list.Add(cur.ToString()); cur.Clear(); } continue; }
                cur.Append(ch);
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list.ToArray();
        }

        public static string Complete(string partial)
        {
            if (string.IsNullOrEmpty(partial)) return partial;
            var p = partial.ToLowerInvariant();
            var matches = Commands.Keys.Where(k => k.StartsWith(p)).ToList();
            if (matches.Count == 1) return matches[0] + " ";
            if (matches.Count > 1)
            {
                Debug.Log(string.Join("  ", matches.Take(40)) + (matches.Count > 40 ? "  ..." : ""));
                // Longest common prefix.
                string prefix = matches[0];
                foreach (var m in matches) while (!m.StartsWith(prefix)) prefix = prefix.Substring(0, prefix.Length - 1);
                return prefix;
            }
            return partial;
        }
    }
}
