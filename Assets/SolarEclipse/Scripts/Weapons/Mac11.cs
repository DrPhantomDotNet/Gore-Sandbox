using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gun pack 3 factory: MAC-11 machine pistol and the dual katanas.
    /// Models: WeaponModels4.cs. Sounds: Audio/RecipesGuns3.cs.
    /// </summary>
    public static class Guns3
    {
        public static Weapon Create(WeaponDef def)
        {
            if (def == null) return null;
            switch (def.Id)
            {
                case "mac11": return new Mac11 { Def = def };
                case "katana_dual": return new DualKatanas { Def = def };
                default: return null;
            }
        }
    }

    /// <summary>
    /// MAC-11: open-bolt .380 machine pistol (~1150 rpm). Everything else comes from <see cref="Firearm"/>;
    /// this adds the violent muzzle climb that builds up over a burst.
    /// </summary>
    public sealed class Mac11 : Firearm
    {
        float climb, lastShot;

        public override bool OpenBolt => true;
        public override string ModeText => base.ModeText + " 1150";

        protected override void OnShot()
        {
            // Each shot in a burst climbs a bit more (resets after a short pause).
            if (Time.time - lastShot > 0.25f) climb = 0f;
            lastShot = Time.time;
            climb = Mathf.Min(climb + 0.12f, 1f);
            float a = Mathf.Lerp(1f, 0.6f, AimWeight) * Mods.RecoilMul;
            CameraFX.Punch((0.35f + 0.55f * climb) * a, Random.Range(-0.32f, 0.32f) * (0.5f + climb) * a);
        }
    }
}
