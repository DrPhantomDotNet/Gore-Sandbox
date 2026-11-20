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
            "The carotid arteries in the neck carry blood to the brain. Cut one and it's minutes, not hours.",
            "Below about 80% oxygen in the blood, people start losing consciousness.",
            "Bruises change colour as they heal: red, purple, blue, then yellow.",
            "Blood pools darken as they dry. Step in one and you leave footprints.",
            "Pale skin and blue lips mean blood loss or lack of oxygen.",
            "A tourniquet stops limb bleeding. Goredolls don't carry one.",
            "Holding RMB in a headlock chokes harder: watch the oxygen counter fall.",
            "Exit wounds are bigger than entry wounds.",
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
            // Heart monitor trace running along the bar up to the progress point.
            float px = bar.width * Mathf.Clamp01(Progress), cy = bar.y - 26f, step = 6f, prevY = cy;
            for (float x = 0; x < px; x += step)
            {
                float ph = Mathf.Repeat(x / 120f, 1f);
                float y = cy - (ph > 0.3f && ph < 0.33f ? 22f : ph >= 0.33f && ph < 0.36f ? -10f : ph > 0.5f && ph < 0.6f ? 5f : 0f);
                UI.Fill(new Rect(bar.x + x, Mathf.Min(prevY, y), 2.5f, Mathf.Abs(prevY - y) + 2.5f), new Color(0.3f, 1f, 0.45f, 0.85f));
                UI.Fill(new Rect(bar.x + x, y, step, 2.5f), new Color(0.3f, 1f, 0.45f, 0.85f));
                prevY = y;
            }
            UI.Label(new Rect(84, H - 120, W - 168, 40), "TIP: " + tip, 20, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
            var spin = new Vector2((W - 110) * UI.Scale, 90 * UI.Scale);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Time.unscaledTime * 180f, spin);
            UI.Tex(new Rect(W - 150, 50, 80, 80), IconFactory.Get("eclipse"), UI.Accent2);
            GUI.matrix = m;
        }
    }
}
