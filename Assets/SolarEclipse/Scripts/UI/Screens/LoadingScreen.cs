using UnityEngine;

namespace SolarEclipse
{
    public sealed class LoadingScreen : UIScreen
    {
        static readonly string[] Tips =
        {
            "Arterial wounds pulse with the heartbeat. Watch the spray rhythm.",
            "A Goredoll that loses 40% of its blood will not get up again.",
            "Shotgun pellets at close range can take limbs clean off.",
            "Goredolls with broken legs crawl. Persistent little guys.",
            "Aiming at a Goredoll makes it nervous. Some will put their hands up.",
            "The Spawn Gun can grab a single limb and drag the whole body around.",
            "Gore can be reduced or turned off in Settings > Gore.",
            "Every sound you hear was synthesised when the game started.",
        };

        public string MapName = "";
        public string Status = "Loading...";
        public float Progress;
        readonly string tip = Tips[Random.Range(0, Tips.Length)];
        public Texture2D Thumb;
        string[] tags;

        public override bool OnBack() => true;

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            UI.Fill(new Rect(0, 0, W, H), Color.black);
            if (Thumb != null) UI.Tex(new Rect(0, 0, W, H), Thumb, new Color(0.35f, 0.35f, 0.35f), ScaleMode.ScaleAndCrop);
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, Color.black);
            UI.Label(new Rect(80, H - 300, W - 160, 100), MapName.ToUpperInvariant(), 80, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            if (tags == null) tags = MapCatalog.All.Find(d => d.Name == MapName)?.Tags ?? new string[0];
            MapTagChips.Draw(84, H - 330, tags, 18);
            UI.Label(new Rect(84, H - 200, W - 160, 40), Status, 24, UI.Accent2);
            var bar = new Rect(84, H - 150, W - 168, 10);
            UI.Rounded(bar, new Color(1, 1, 1, 0.1f), true);
            UI.Rounded(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(Progress), bar.height), UI.Accent, true);
            UI.Label(new Rect(84, H - 120, W - 168, 40), "TIP: " + tip, 20, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
            var spin = new Vector2((W - 110) * UI.Scale, 90 * UI.Scale);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Time.unscaledTime * 180f, spin);
            UI.Tex(new Rect(W - 150, 50, 80, 80), IconFactory.Get("eclipse"), UI.Accent2);
            GUI.matrix = m;
        }
    }
}
