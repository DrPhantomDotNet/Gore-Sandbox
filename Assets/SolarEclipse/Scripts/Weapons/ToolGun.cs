using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// GS2 Tool Gun (sandbox building): WELD two physics objects together, PAINT props in a palette of colours and
    /// place LIGHTs. Host / singleplayer only for welds and lights (plain physics / scene objects).
    /// </summary>
    public sealed class ToolGun : Weapon
    {
        static readonly string[] Modes = { "WELD", "PAINT", "LIGHT" };
        static readonly Color[] Palette = { new Color(0.85f, 0.1f, 0.1f), new Color(0.95f, 0.55f, 0.1f), new Color(0.95f, 0.85f, 0.15f), new Color(0.2f, 0.75f, 0.25f), new Color(0.15f, 0.45f, 0.95f), new Color(0.6f, 0.25f, 0.85f), new Color(0.95f, 0.95f, 0.95f), new Color(0.12f, 0.12f, 0.13f), new Color(0.95f, 0.45f, 0.7f) };
        static readonly string[] PaletteNames = { "red", "orange", "yellow", "green", "blue", "purple", "white", "black", "pink" };
        static readonly List<Light> lights = new List<Light>();
        int mode, colour; Rigidbody first; float pulse;
        MaterialPropertyBlock mpb;

        public override string AmmoText => mode == 1 ? PaletteNames[colour].ToUpperInvariant() : mode == 2 ? lights.Count + " / 24 lights" : (first != null ? "PICK THE SECOND OBJECT" : "PICK AN OBJECT");
        public override string ModeText => Modes[mode];

        protected override void OnEquip() { Vm.Right.SetPose(HandPose.PistolGrip); Vm.Left.SetPose(HandPose.Support); }
        protected override void OnHolster() { first = null; }

        bool Host() { if (!NetState.IsClient) return true; GameEvents.ShowToast("Tool Gun: only the host can do that in multiplayer."); AudioManager.Ui("ui_error", 0.5f); return false; }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            pulse += dt;
            if (f.Reload) { mode = (mode + 1) % Modes.Length; first = null; AudioManager.Play("sg_tick", Eye, 0.4f, 1f + mode * 0.15f, 0f); GameEvents.ShowToast("Tool Gun: " + Modes[mode]); }
            bool aimed = Physics.Raycast(Eye, Aim, out var hit, 60f, Layers.GrabMask | Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (f.Fire) Primary(aimed, hit);
            if (f.AimPressed) Secondary(aimed, hit);
            var pos = Def.HipPos + new Vector3(0, Mathf.Sin(pulse * 2f) * 0.002f, 0);
            var rot = Quaternion.identity;
            TickInspect(f, dt, ref pos, ref rot);
            Vm.TargetPos = pos; Vm.TargetRot = rot;
        }

        void Primary(bool aimed, RaycastHit hit)
        {
            if (!aimed) { AudioManager.Play("sg_tick", Eye, 0.3f, 0.7f, 0f); return; }
            Vm.AddRecoil(new Vector3(0, 0.01f, -0.05f), new Vector3(-2f, 0, 0));
            switch (mode)
            {
                case 0: // weld
                    if (!Host()) return;
                    var rb = hit.rigidbody;
                    if (first == null) { if (rb == null) { GameEvents.ShowToast("Weld: pick a physics object first."); return; } first = rb; AudioManager.Play("rope_pick", hit.point, 0.6f); return; }
                    if (rb == first) { GameEvents.ShowToast("Pick a different object."); return; }
                    var j = first.gameObject.AddComponent<FixedJoint>();
                    j.connectedBody = rb; // null = welded to the world
                    j.breakForce = 25000f; j.breakTorque = 25000f;
                    var doll = first.GetComponentInParent<Goredoll>(); if (doll != null && doll.Ragdoll != null) doll.Ragdoll.Fall(3f);
                    FX.Sparks(hit.point, hit.normal, 16);
                    AudioManager.Play("rope_attach", hit.point, 0.7f, 0.8f);
                    GameEvents.ShowToast(rb != null ? "Welded together" : "Welded to the world");
                    Stats.Add("welds");
                    first = null;
                    break;
                case 1: // paint
                    var rends = hit.collider.GetComponentsInChildren<Renderer>();
                    if (rends.Length == 0 && hit.collider.attachedRigidbody != null) rends = hit.collider.attachedRigidbody.GetComponentsInChildren<Renderer>();
                    if (mpb == null) mpb = new MaterialPropertyBlock();
                    foreach (var r in rends) { if (r == null || r is ParticleSystemRenderer || r is LineRenderer) continue; r.GetPropertyBlock(mpb); mpb.SetColor("_Color", Palette[colour] * 1.4f); r.SetPropertyBlock(mpb); }
                    AudioManager.Play("hiss", hit.point, 0.4f, 1.4f);
                    Decals.Paint(hit.point, hit.normal, 0.12f, Palette[colour], hit.collider);
                    break;
                case 2: // light
                    if (!Host()) return;
                    if (lights.Count >= 24) { var old = lights[0]; lights.RemoveAt(0); if (old != null) Object.Destroy(old.gameObject); }
                    var lgo = new GameObject("Tool Light");
                    lgo.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
                    lgo.transform.position = hit.point + hit.normal * 0.35f;
                    var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 1.8f; l.color = Palette[colour] * 0.5f + Color.white * 0.5f; l.shadows = LightShadows.None;
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(bulb.GetComponent<Collider>());
                    bulb.transform.SetParent(lgo.transform, false); bulb.transform.localScale = Vector3.one * 0.12f;
                    bulb.GetComponent<Renderer>().sharedMaterial = Mats.Glow(l.color, 3f);
                    if (hit.rigidbody != null) lgo.transform.SetParent(hit.rigidbody.transform, true);
                    lights.Add(l);
                    AudioManager.Play("sg_tick", hit.point, 0.5f, 1.4f);
                    break;
            }
        }

        void Secondary(bool aimed, RaycastHit hit)
        {
            switch (mode)
            {
                case 0:
                    if (first != null) { first = null; GameEvents.ShowToast("Weld cancelled"); return; }
                    if (!aimed || hit.rigidbody == null || !Host()) return;
                    int n = 0;
                    foreach (var j in hit.rigidbody.GetComponents<FixedJoint>()) { Object.Destroy(j); n++; }
                    foreach (var j in Object.FindObjectsOfType<FixedJoint>()) if (j.connectedBody == hit.rigidbody) { Object.Destroy(j); n++; }
                    GameEvents.ShowToast(n > 0 ? "Removed " + n + " weld" + (n == 1 ? "" : "s") : "Nothing welded there");
                    break;
                case 1:
                    colour = (colour + 1) % Palette.Length;
                    GameEvents.ShowToast("Paint: " + PaletteNames[colour]);
                    AudioManager.Play("sg_tick", Eye, 0.4f, 1.2f, 0f);
                    break;
                case 2:
                    if (!aimed || !Host()) return;
                    Light best = null; float bd = 2.5f;
                    foreach (var l in lights) if (l != null) { float d = Vector3.Distance(l.transform.position, hit.point); if (d < bd) { bd = d; best = l; } }
                    if (best != null) { lights.Remove(best); Object.Destroy(best.gameObject); GameEvents.ShowToast("Light removed"); }
                    break;
            }
        }
    }
}
