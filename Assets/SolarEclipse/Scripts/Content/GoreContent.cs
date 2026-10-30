using System;
using System.Collections.Generic;
using System.Reflection;
using SolarEclipse.Mods;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Put this on any <see cref="UIScreen"/> class (with a public constructor that takes no arguments) and it gets a
    /// button in the main menu's EXTRAS list. See Assets/SolarEclipse/DEVELOPING.md.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GoreMenuButtonAttribute : Attribute
    {
        public readonly string Label, Icon, Tooltip;
        public GoreMenuButtonAttribute(string label, string icon = "star", string tooltip = null) { Label = label; Icon = icon; Tooltip = tooltip; }
    }

    /// <summary>Loads the editor-made content (Resources/GoreContent) and finds the [GoreMenuButton] screens.</summary>
    public static class GoreContent
    {
        public sealed class ScreenEntry { public string Label, Icon, Tooltip; public Type Type; }

        static List<GoreStoryAsset> stories;
        static List<ScreenEntry> screens;
        static bool weaponsLoaded;

        public static List<GoreStoryAsset> Stories
        {
            get
            {
                if (stories != null) return stories;
                stories = new List<GoreStoryAsset>();
                try
                {
                    foreach (var s in Resources.LoadAll<GoreStoryAsset>("GoreContent")) if (s != null && s.includeInGame) stories.Add(s);
                    stories.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.name, b.name));
                }
                catch (Exception e) { Debug.LogWarning("[Content] stories: " + e.Message); }
                return stories;
            }
        }

        public static List<ScreenEntry> Screens
        {
            get
            {
                if (screens != null) return screens;
                screens = new List<ScreenEntry>();
                try
                {
                    foreach (var t in typeof(GoreContent).Assembly.GetTypes())
                    {
                        if (!typeof(UIScreen).IsAssignableFrom(t) || t.IsAbstract) continue;
                        var at = t.GetCustomAttribute<GoreMenuButtonAttribute>();
                        if (at == null || t.GetConstructor(Type.EmptyTypes) == null) continue;
                        screens.Add(new ScreenEntry { Label = at.Label, Icon = at.Icon, Tooltip = at.Tooltip, Type = t });
                    }
                    screens.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
                }
                catch (Exception e) { Debug.LogWarning("[Content] screens: " + e.Message); }
                return screens;
            }
        }

        public static bool HasExtras => Stories.Count > 0 || Screens.Count > 0;

        public static void Open(ScreenEntry s)
        {
            try { UIRoot.Push((UIScreen)Activator.CreateInstance(s.Type)); }
            catch (Exception e) { Debug.LogException(e); GameEvents.ShowToast("Couldn't open " + s.Label + ": " + e.Message); }
        }

        /// <summary>Called by ModStore.Boot after the mod kinds registered: installs every editor-made weapon.</summary>
        public static void LoadWeapons()
        {
            if (weaponsLoaded) return;
            weaponsLoaded = true;
            GoreWeaponAsset[] all;
            try { all = Resources.LoadAll<GoreWeaponAsset>("GoreContent"); }
            catch (Exception e) { Debug.LogWarning("[Content] weapons: " + e.Message); return; }
            Array.Sort(all, (a, b) => string.CompareOrdinal(a.ContentId, b.ContentId)); // same catalog order everywhere
            int n = 0;
            foreach (var w in all)
            {
                if (w == null || !w.includeInGame) continue;
                var p = w.ToPackage();
                var err = ModStore.Validate(p);
                if (err != null) { Debug.LogWarning("[Content] weapon '" + w.name + "' not loaded: " + err); continue; }
                ModWeapons.Official.Add(p.id);
                ModWeapons.Install(p);
                n++;
            }
            if (n > 0) Debug.Log("[Content] " + n + " editor-made weapon(s) loaded");
        }
    }
}
