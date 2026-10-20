using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Special held item for the boss realm: a flashlight. The actual spotlight lives on the camera
    /// (<see cref="BossRealm"/>) so it keeps lighting the way even while another item is held. LMB toggles it.
    /// Not in the WeaponCatalog, so it can't be spawned or given anywhere else.
    /// </summary>
    public sealed class Flashlight : Weapon
    {
        public static readonly WeaponDef FlashlightDef = new WeaponDef
        {
            Id = "flashlight", Name = "Flashlight", Kind = WeaponKind.Fists, Glyph = "bulb",
            Description = "It's the only thing you have.", HipPos = new Vector3(0.15f, -0.17f, 0.36f),
        };

        float idle;
        Renderer lens;

        public static Flashlight Create() => new Flashlight { Def = FlashlightDef };

        public override string AmmoText => BossRealm.FlashlightOn ? "ON" : "OFF";

        protected override void OnAttach()
        {
            var body = new MeshBuilder { Color = new Color(0.08f, 0.08f, 0.09f), Gloss = 0.35f };
            body.Cylinder(new Vector3(0, 0, -0.13f), new Vector3(0, 0, 0.03f), 0.017f, 0.017f, 14);
            body.Cylinder(new Vector3(0, 0, 0.03f), new Vector3(0, 0, 0.08f), 0.018f, 0.027f, 16);
            body.SetColor(new Color(0.25f, 0.25f, 0.27f), 0.6f);
            body.Cylinder(new Vector3(0, 0, 0.076f), new Vector3(0, 0, 0.084f), 0.028f, 0.028f, 16);
            for (int i = 0; i < 5; i++) body.Cylinder(new Vector3(0, 0, -0.11f + i * 0.022f), new Vector3(0, 0, -0.104f + i * 0.022f), 0.0185f, 0.0185f, 12, false);
            body.SetColor(new Color(0.5f, 0.05f, 0.05f), 0.3f);
            body.Box(new Vector3(0, 0.018f, -0.02f), new Vector3(0.008f, 0.005f, 0.014f));
            Model.Piece("Body", body, Mats.LitMetal, Vector3.zero);
            var l = new MeshBuilder { Color = Color.white };
            l.Cylinder(new Vector3(0, 0, 0.084f), new Vector3(0, 0, 0.086f), 0.024f, 0.024f, 16);
            var lt = Model.Piece("Lens", l, Mats.Glow(new Color(1f, 0.95f, 0.8f), 3f), Vector3.zero);
            lens = lt.GetComponent<Renderer>();
            Model.GripR = Model.Anchor("GripR", new Vector3(0.02f, 0f, -0.06f), Quaternion.Euler(0, 0, -90f));
            IconFactory.SetLayer(Model.gameObject, Layers.Viewmodel);
        }

        protected override void OnEquip()
        {
            Vm.LeftOnAnchor = false;
            Vm.HideLeft = true;
            Vm.Right.SetPose(HandPose.Knife);
        }

        protected override void OnHolster()
        {
            Vm.HideLeft = false;
            Vm.Right.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            idle += dt;
            Vm.TargetPos = Def.HipPos + new Vector3(0, Mathf.Sin(idle * 1.7f) * 0.004f, 0);
            Vm.TargetRot = Quaternion.Euler(Mathf.Sin(idle * 1.3f) * 0.8f, Mathf.Sin(idle * 0.9f) * 0.8f, 0);
            if (f.Fire)
            {
                BossRealm.FlashlightOn = !BossRealm.FlashlightOn;
                AudioManager.Play2D("button", 0.6f, BossRealm.FlashlightOn ? 1.2f : 0.9f, AudioBus.Sfx);
                Vm.AddRecoil(new Vector3(0, 0, -0.01f), new Vector3(-3f, 0, 0));
            }
            if (lens != null) lens.enabled = BossRealm.FlashlightOn;
        }
    }
}
