using System;
using UnityEngine;

namespace SolarEclipse
{
    public enum TimeOfDay { Day, Sunset, Night, BloodMoon }
    public enum WeatherKind { Clear, Rain, Thunderstorm, Fog, Snow }

    /// <summary>Options picked on the map select screen.</summary>
    public sealed class MapOptions
    {
        public TimeOfDay Time = TimeOfDay.Day;
        public int StartingNpcs = 3;
        public int Loadout; // 0 = fists + spawn gun, 1 = full arsenal
        public bool Nextbots;
        public bool AiIgnore;
        public WeatherKind Weather = WeatherKind.Clear;
        public static readonly string[] Weathers = { "Clear", "Rain", "Thunderstorm", "Fog", "Snow" };
        public static readonly string[] Loadouts = { "Fists + Spawn Gun", "Full Arsenal" };
        public static readonly string[] Times = { "Day", "Sunset", "Night", "Blood Moon" };
    }

    /// <summary>A playable map: metadata for the menu plus the code that builds it.</summary>
    public sealed class MapDef
    {
        public string Id, Name, Description, Size, Glyph = "map", Ambience = "amb_wind";
        public string[] Features = Array.Empty<string>();
        /// <summary>Small badges shown on the map card / loading screen, e.g. "Procedurally Generated". Plain ASCII.</summary>
        public string[] Tags = Array.Empty<string>();
        public Color ThemeTop = new Color(0.3f, 0.5f, 0.8f), ThemeBottom = new Color(0.8f, 0.85f, 0.9f), Accent = Color.white;
        public TimeOfDay DefaultTime = TimeOfDay.Day;
        public bool Secret, Indoor;
        public Func<bool> IsUnlocked = () => true;
        public string UnlockHint = "";
        /// <summary>Builds the level geometry, interactables and spawn points.</summary>
        public Action<MapBuilder> Build;
        public Texture2D Thumbnail;
    }
}
