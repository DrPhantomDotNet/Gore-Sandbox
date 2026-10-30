namespace SolarEclipse
{
    /// <summary>Product identity shown in menus. Rename the game here.</summary>
    public static class GameInfo
    {
        public const string Title = "GORE SANDBOX";
        public const string Subtitle = "a SolarEclipse Games production";
        public const string Studio = "SolarEclipse Games";
        public const string Version = "0.9.0";
        public const string Access = "Player";
        /// <summary>GitHub "owner/repo" whose releases the auto-updater checks. Leave empty: the GitHub cloud build fills it in
        /// automatically (Resources/update_repo.txt). Only set it if you build the game yourself.</summary>
        /// <summary>Early Access badge on the main menu (set false for the full release).</summary>
        public const bool EarlyAccess = true;
        /// <summary>Community Discord (main menu DISCORD button).</summary>
        public const string DiscordUrl = "https://discord.gg/mjRZdWJBRN";
        /// <summary>The itch.io page (main menu ITCH.IO button; hidden while empty), e.g. "https://name.itch.io/gore-sandbox".</summary>
        public const string ItchUrl = "";
        public const string UpdateRepo = "DrPhantomDotNet/Gore-Sandbox";
    }
}
