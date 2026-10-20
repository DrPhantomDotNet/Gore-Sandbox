using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Randomised look of one Goredoll.</summary>
    public sealed class Appearance
    {
        public Color Skin, Shirt, Pants, Shoes, Hair, Eyes, Lips;
        public int HairStyle;     // 0 short, 1 buzz, 2 bald, 3 long-ish
        public bool LongSleeves, Beard;
        public float Height = 1f, Build = 1f;
        public string Name;

        static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.80f, 0.69f), new Color(0.91f, 0.72f, 0.58f), new Color(0.82f, 0.62f, 0.47f),
            new Color(0.68f, 0.48f, 0.34f), new Color(0.52f, 0.36f, 0.25f), new Color(0.38f, 0.26f, 0.18f),
        };
        static readonly Color[] Shirts =
        {
            new Color(0.25f, 0.55f, 0.25f), new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.3f, 0.6f), new Color(0.9f, 0.9f, 0.88f),
            new Color(0.15f, 0.15f, 0.17f), new Color(0.95f, 0.6f, 0.15f), new Color(0.5f, 0.3f, 0.6f), new Color(0.6f, 0.6f, 0.62f),
        };
        static readonly Color[] PantsColors = { new Color(0.2f, 0.3f, 0.5f), new Color(0.15f, 0.2f, 0.35f), new Color(0.55f, 0.48f, 0.35f), new Color(0.12f, 0.12f, 0.13f), new Color(0.35f, 0.33f, 0.28f) };
        static readonly Color[] HairColors = { new Color(0.12f, 0.08f, 0.05f), new Color(0.3f, 0.18f, 0.08f), new Color(0.55f, 0.4f, 0.2f), new Color(0.85f, 0.7f, 0.4f), new Color(0.06f, 0.06f, 0.06f), new Color(0.6f, 0.25f, 0.1f) };
        static readonly Color[] EyeColors = { new Color(0.35f, 0.2f, 0.1f), new Color(0.2f, 0.45f, 0.7f), new Color(0.3f, 0.5f, 0.3f), new Color(0.15f, 0.1f, 0.06f) };
        static readonly string[] Names =
        {
            "Kevin", "Gary", "Dave", "Steve", "Mike", "Brian", "Carl", "Doug", "Frank", "Greg", "Hank", "Ivan", "Jeff", "Larry", "Marcus",
            "Ned", "Oscar", "Pete", "Ron", "Stan", "Todd", "Vince", "Walt", "Bob", "Chad", "Dwayne", "Earl", "Phil", "Rick", "Tony",
            "Alex", "Sam", "Jordan", "Riley", "Morgan", "Casey", "Taylor", "Jamie", "Quinn", "Drew",
        };

        public static Appearance Random(int variant = -1)
        {
            var a = new Appearance
            {
                Skin = Pick(SkinTones), Shirt = Pick(Shirts), Pants = Pick(PantsColors), Hair = Pick(HairColors), Eyes = Pick(EyeColors),
                Shoes = UnityEngine.Random.value < 0.5f ? new Color(0.12f, 0.1f, 0.09f) : new Color(0.85f, 0.85f, 0.85f),
                HairStyle = UnityEngine.Random.Range(0, 4), LongSleeves = UnityEngine.Random.value < 0.3f, Beard = UnityEngine.Random.value < 0.2f,
                Height = UnityEngine.Random.Range(0.95f, 1.05f), Build = UnityEngine.Random.Range(0.92f, 1.1f), Name = Pick(Names),
            };
            a.Lips = Color.Lerp(a.Skin, new Color(0.75f, 0.35f, 0.35f), 0.45f);
            // Variant 0 is the classic look (green shirt, jeans) from the original game's menu.
            if (variant == 0) { a.Shirt = Shirts[0]; a.Pants = PantsColors[0]; a.LongSleeves = false; }
            return a;
        }

        static Color Pick(Color[] arr) => arr[UnityEngine.Random.Range(0, arr.Length)];
        static string Pick(string[] arr) => arr[UnityEngine.Random.Range(0, arr.Length)];
    }
}
