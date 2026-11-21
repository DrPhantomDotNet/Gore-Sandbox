using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// GS2 weapon skins: a colour finish per gun (Customize Weapons > SKIN), saved on this device and tinted onto the
    /// gun model through a MaterialPropertyBlock when the gun is attached.
    /// </summary>
    public static class WeaponSkins
    {
        public static readonly string[] Names = { "FACTORY", "GOLD", "CAMO", "ARCTIC", "BLOOD", "NIGHT", "CHROME" };
        static readonly Color[] Tints = { Color.white, new Color(1.6f, 1.25f, 0.45f), new Color(0.75f, 0.95f, 0.55f), new Color(1.7f, 1.75f, 1.8f), new Color(1.4f, 0.35f, 0.3f), new Color(0.35f, 0.35f, 0.4f), new Color(1.5f, 1.55f, 1.65f) };
        static MaterialPropertyBlock mpb;

        public static int Get(string weaponId) => Mathf.Clamp(PlayerPrefs.GetInt("skin_" + weaponId, 0), 0, Names.Length - 1);
        public static void Set(string weaponId, int skin) { PlayerPrefs.SetInt("skin_" + weaponId, Mathf.Clamp(skin, 0, Names.Length - 1)); PlayerPrefs.Save(); }

        public static void Apply(WeaponModel model, string weaponId)
        {
            if (model == null) return;
            int s = Get(weaponId);
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || model.GlowRenderers.Contains(r) || r is ParticleSystemRenderer || r is LineRenderer) continue;
                r.GetPropertyBlock(mpb);
                if (s == 0) mpb.Clear(); else mpb.SetColor("_Color", Tints[s]);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
