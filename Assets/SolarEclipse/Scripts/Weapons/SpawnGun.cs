using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Everything the Spawn Gun can create, by spawn-menu tab. AI holds only the Goredoll; Items is empty for
    /// now but fully wired (call <see cref="RegisterItem"/>); Props come from <see cref="PropFactory"/>;
    /// Entities holds the nextbots.
    /// </summary>
    public static class SpawnCatalog
    {
        public static readonly List<SpawnableDef> AI = new List<SpawnableDef>();
        public static readonly List<SpawnableDef> Items = new List<SpawnableDef>();
        public static readonly List<SpawnableDef> Entities = new List<SpawnableDef>();
        public static List<SpawnableDef> Props => PropFactory.Props;

        public static SpawnableDef Selected;
        public static string GoredollPreset = "Normal";
        public static int GoredollVariant = -1;   // -1 random look, 0 classic
        public static bool SpawnFrozen;

        static SpawnCatalog()
        {
            AI.Add(new SpawnableDef
            {
                Id = "goredoll", Name = "Goredoll", Category = "AI", Glyph = "person",
                Description = "Active-ragdoll human with a full anatomy, physiology and an AI brain.",
                Spawn = (pos, rot) => GoredollFactory.Spawn(pos, rot.eulerAngles.y, GoredollPreset, GoredollVariant).gameObject,
            });
            Zombies.RegisterSpawnables(AI); // 5 zombie types (Goredoll/Zombies.cs)
            foreach (var nb in Nextbots.Defs)
            {
                var id = nb.Id;
                Entities.Add(new SpawnableDef
                {
                    Id = "nextbot_" + id, Name = nb.Name, Category = "Entities", Glyph = "skull", Description = nb.Description,
                    Spawn = (pos, rot) => Nextbots.Spawn(id, pos).gameObject,
                });
            }
            Selected = AI[0];
        }

        /// <summary>Hook for future items (medkits, armour, ...). They show up in the Items tab automatically.</summary>
        public static void RegisterItem(SpawnableDef def)
        {
            def.Category = "Items";
            Items.RemoveAll(d => d.Id == def.Id);
            Items.Add(def);
        }

        public static SpawnableDef Find(string id)
        {
            foreach (var l in new[] { AI, Items, Props, Entities })
                foreach (var d in l) if (d.Id == id) return d;
            return null;
        }

        /// <summary>Spawns a definition and tags it for undo/delete. Returns the root object.</summary>
        public static GameObject Spawn(SpawnableDef def, Vector3 pos, Quaternion rot)
        {
            if (def == null) return null;
            if (NetState.ClientIntercepts)
            {
                // The host spawns it for everyone (Creative mode required).
                if (!NetState.LocalCreative) { GameEvents.ShowToast("Spawning needs Creative mode — ask the host."); return null; }
                NetManager.I.RequestSpawn(def, pos, rot);
                return null;
            }
            GameObject go;
            if (def.Category == "Props") { var rb = PropFactory.Spawn(def.Id, pos, rot); go = rb != null ? rb.gameObject : null; }
            else go = def.Spawn(pos, rot);
            if (go == null) return null;
            var s = go.GetComponent<Spawned>() ?? go.AddComponent<Spawned>();
            s.DefId = def.Id;
            s.DisplayName = def.Name;
            SpawnGun.UndoStack.Add(go);
            Stats.Add("spawned");
            if (SpawnFrozen && def.Category != "AI")
            {
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                if (NetState.IsHost) NetManager.I.SetFrozen(go.GetComponent<NetEntity>(), true);
            }
            return go;
        }
    }

    /// <summary>
    /// The Spawn Gun ("physics gun"). MMB spawns the selection where you aim. Hold LMB to grab and carry
    /// anything physical; scroll to push/pull it; hold R to rotate it with the mouse (camera locks); F freezes it
    /// in place; RMB throws (or unfreezes what you aim at); X/Delete deletes; Z undoes the last spawn; +/- scales.
    /// </summary>
    public sealed class SpawnGun : Weapon
    {
        public static readonly List<GameObject> UndoStack = new List<GameObject>();

        Rigidbody held;
        Goredoll heldDoll;
        Vector3 localGrab;
        float distance;
        Quaternion relRot;          // held rotation relative to the camera yaw
        bool rotating, wasKinematic;
        LineRenderer beam;
        AudioSource hum;
        float pulse, spawnFlash;
        public string TargetName { get; private set; } = "";

        public override bool ConsumesKick => held != null;
        public override bool ConsumesScroll => held != null;
        public override string AmmoText => SpawnCatalog.Selected != null ? SpawnCatalog.Selected.Name : "Nothing selected";
        public override string ModeText => held != null ? (rotating ? "ROTATE" : "HOLD " + distance.ToString("0.0") + " m") : "MMB SPAWN";
        public bool Holding => held != null;

        protected override void OnAttach()
        {
            var go = new GameObject("SpawnGunBeam");
            go.layer = Layers.Effects;
            go.transform.SetParent(Player.transform, false);
            beam = go.AddComponent<LineRenderer>();
            beam.positionCount = 16;
            beam.sharedMaterial = Mats.AdditiveWith(TextureFactory.Spark);
            beam.widthMultiplier = 0.035f;
            beam.textureMode = LineTextureMode.Stretch;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.enabled = false;
        }

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.PistolGrip);
            Vm.Left.SetPose(HandPose.Support);
        }

        protected override void OnHolster()
        {
            Release(Vector3.zero);
            beam.enabled = false;
            if (hum != null) hum.Stop();
        }

        public override void Destroy()
        {
            base.Destroy();
            if (beam != null) Object.Destroy(beam.gameObject);
        }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            pulse += dt;
            spawnFlash = Mathf.MoveTowards(spawnFlash, 0f, dt * 4f);
            if (held == null || !held) { held = null; heldDoll = null; }

            // Aim target for the HUD.
            TargetName = "";
            RaycastHit aimHit;
            bool aimed = Physics.Raycast(Eye, Aim, out aimHit, 80f, Layers.GrabMask | Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (aimed && held == null) TargetName = NameOf(aimHit.collider);

            if (f.Middle) SpawnSelected(aimed, aimHit);
            if (f.Undo) Undo();

            if (held == null)
            {
                Player.LockLook = false;
                rotating = false;
                if (f.Fire && aimed) TryGrab(aimHit);
                if (f.AimPressed && aimed) Unfreeze(aimHit.collider);
                if (f.Delete && aimed) Delete(aimHit.collider);
                if ((f.ScaleUp || f.ScaleDown) && aimed) Scale(aimHit.collider, f.ScaleUp ? 1f : -1f, dt);
            }
            else HoldTick(f, dt);

            // Pose: hold it a little higher and further out while carrying.
            float carry = held != null ? 1f : 0f;
            var pos = Def.HipPos + new Vector3(0, 0.01f, 0.02f) * carry + new Vector3(0, Mathf.Sin(pulse * 2f) * 0.002f, 0);
            var rot = Quaternion.Euler(-2f * carry, 0, 0);
            if (held == null) TickInspect(f, dt, ref pos, ref rot);
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            if (Model.Glow != null) Model.Glow.localScale = Vector3.one * (1f + (held != null ? 0.12f + Mathf.Sin(pulse * 30f) * 0.04f : Mathf.Sin(pulse * 3f) * 0.02f) + spawnFlash * 0.3f);
            UpdateBeam();
        }

        // ------------------------------------------------------------------ Spawning

        void SpawnSelected(bool aimed, RaycastHit hit)
        {
            var def = SpawnCatalog.Selected;
            if (def == null) { GameEvents.ShowToast("Nothing selected — press Esc to open the spawn menu."); AudioManager.Ui("ui_error"); return; }
            Vector3 point = aimed ? hit.point : Eye + Aim * 8f;
            Vector3 normal = aimed ? hit.normal : Vector3.up;
            Vector3 pos;
            if (def.Category == "AI" || def.Category == "Entities")
            {
                // Feet on the ground: step back off walls, then drop to the floor below.
                pos = point + normal * 0.45f;
                if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var g, 30f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) pos = g.point;
            }
            else pos = point + normal * 0.5f + Vector3.up * 0.2f;
            var yaw = Quaternion.LookRotation(MathX.Flat(Eye - pos).sqrMagnitude > 0.01f ? MathX.Flat(Eye - pos) : Vector3.forward);
            var go = SpawnCatalog.Spawn(def, pos, def.Category == "AI" || def.Category == "Entities" ? yaw : Quaternion.Euler(0, Player.transform.eulerAngles.y, 0));
            if (go == null) return;
            spawnFlash = 1f;
            AudioManager.Play("sg_spawn", pos, 0.8f, Random.Range(0.95f, 1.05f));
            FX.Flash(pos + Vector3.up * 0.5f, new Color(0.3f, 1f, 0.6f), 5f, 3f, 0.25f);
            FX.Dust(pos, 0.5f, new Color(0.4f, 1f, 0.7f, 0.6f), 16);
            Vm.AddRecoil(new Vector3(0, 0.05f, -0.2f) * 0.3f, new Vector3(-6f, 0, 0));
            Tracers.Spawn(Model.Muzzle.position, pos + Vector3.up * 0.4f, new Color(0.3f, 1f, 0.6f));
        }

        void Undo()
        {
            for (int i = UndoStack.Count - 1; i >= 0; i--)
            {
                var go = UndoStack[i];
                UndoStack.RemoveAt(i);
                if (go == null) continue;
                if (held != null && held.transform.IsChildOf(go.transform)) Release(Vector3.zero);
                DeleteObject(go);
                AudioManager.Ui("ui_back");
                GameEvents.ShowToast("Undone");
                return;
            }
            GameEvents.ShowToast("Nothing to undo");
        }

        // ------------------------------------------------------------------ Grabbing

        void TryGrab(RaycastHit hit)
        {
            var rb = hit.rigidbody;
            if (rb == null) { AudioManager.Play("sg_tick", Eye, 0.4f, 0.8f, 0f); return; }
            held = rb;
            heldDoll = rb.GetComponentInParent<Goredoll>();
            wasKinematic = rb.isKinematic;
            localGrab = rb.transform.InverseTransformPoint(hit.point);
            distance = Mathf.Clamp(hit.distance, 1.2f, 60f);
            relRot = Quaternion.Inverse(YawRot) * rb.rotation;
            // Multiplayer client grabbing something the host owns: the host moves it, we steer.
            remoteHeld = NetState.IsClient && NetManager.Handle(rb, out _, out _);
            if (remoteHeld) NetManager.I.RequestGrab(rb, localGrab);
            else if (heldDoll != null && heldDoll.Ragdoll != null) heldDoll.Ragdoll.Fall(3f);
            if (heldDoll != null && heldDoll.Vitals.Conscious) heldDoll.Voice.Say("v_whoa", 1f, true);
            if (hum == null) hum = AudioManager.CreateLoop("sg_hum", Player.transform, 0.35f, AudioBus.Sfx, 20f, false);
            if (hum != null) hum.Play();
            AudioManager.Play("sg_grab", hit.point, 0.6f);
            Stats.Add("grabs");
        }

        Quaternion YawRot => Quaternion.Euler(0, CamT.eulerAngles.y, 0);
        bool remoteHeld;

        void HoldTick(InputFrame f, float dt)
        {
            if (Mathf.Abs(f.Scroll) > 0.01f) { distance = Mathf.Clamp(distance + f.Scroll * Mathf.Max(0.4f, distance * 0.12f), 1f, 60f); AudioManager.Play("sg_tick", Eye, 0.25f, 1f + f.Scroll * 0.1f, 0f); }

            rotating = f.ReloadHeld;
            Player.LockLook = rotating;
            if (rotating)
            {
                var look = f.Look;
                bool roll = Input.GetKey(KeyCode.LeftShift);
                var delta = roll ? Quaternion.AngleAxis(-look.x * 2f, CamT.forward)
                                 : Quaternion.AngleAxis(-look.x * 2f, Vector3.up) * Quaternion.AngleAxis(look.y * 2f, CamT.right);
                var world = YawRot * relRot;
                world = delta * world;
                relRot = Quaternion.Inverse(YawRot) * world;
            }

            var targetPoint = Eye + Aim * distance;
            var grabWorld = held.transform.TransformPoint(localGrab);
            var targetRot = YawRot * relRot;
            if (remoteHeld)
            {
                var nm = NetManager.I;
                nm.ClientGrabTarget = targetPoint; nm.ClientGrabRot = targetRot; nm.ClientGrabRotating = rotating;
            }
            else if (held.isKinematic)
            {
                // Frozen things move rigidly (no physics).
                var newRot = heldDoll != null && !rotating ? held.rotation : targetRot;
                var offset = newRot * Vector3.Scale(localGrab, held.transform.lossyScale);
                held.MovePosition(targetPoint - offset);
                held.MoveRotation(newRot);
            }
            else
            {
                var err = targetPoint - grabWorld;
                var desired = Vector3.ClampMagnitude(err * 14f, 40f);
                var v = Vector3.Lerp(RB.Vel(held), desired, 1f - Mathf.Exp(-18f * dt));
                RB.SetVel(held, v);
                held.WakeUp();
                if (heldDoll == null || rotating)
                {
                    var q = targetRot * Quaternion.Inverse(held.rotation);
                    q.ToAngleAxis(out float ang, out var axis);
                    if (ang > 180f) ang -= 360f;
                    if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.001f)
                        held.angularVelocity = Vector3.ClampMagnitude(axis * ang * Mathf.Deg2Rad * 12f, 30f);
                }
            }

            if (f.Kick) { Freeze(); return; }
            if (f.AimPressed) { var throwV = Aim * Mathf.Lerp(28f, 12f, Mathf.Clamp01(held.mass / 60f)); Release(throwV); AudioManager.Play("sg_throw", Eye, 0.7f, 1f, 0f); Stats.Add("throws"); return; }
            if (f.Delete) { var go = held.gameObject; Release(Vector3.zero); Delete(go.GetComponent<Collider>() ?? go.GetComponentInChildren<Collider>()); return; }
            if (f.ScaleUp || f.ScaleDown) Scale(held.GetComponent<Collider>() ?? held.GetComponentInChildren<Collider>(), f.ScaleUp ? 1f : -1f, dt);
            if (f.FireUp || !f.FireHeld) Release(Vector3.zero);
        }

        void Release(Vector3 throwVelocity)
        {
            if (held == null) return;
            if (remoteHeld) { NetManager.I.RequestGrabEnd(throwVelocity); remoteHeld = false; }
            else if (!held.isKinematic)
            {
                if (throwVelocity != Vector3.zero) RB.SetVel(held, throwVelocity);
                else RB.SetVel(held, Vector3.ClampMagnitude(RB.Vel(held), 20f));
            }
            AudioManager.Play("sg_release", held.position, 0.4f);
            held = null;
            heldDoll = null;
            rotating = false;
            Player.LockLook = false;
            if (hum != null) hum.Stop();
        }

        void Freeze()
        {
            var rb = held;
            bool remote = remoteHeld;
            if (remote) { NetManager.I.RequestFreeze(rb, true); remoteHeld = false; NetManager.I.ClientGrabActive = false; }
            Release(Vector3.zero);
            if (!remote) rb.isKinematic = true;
            AudioManager.Play("sg_freeze", rb.position, 0.7f);
            FX.Flash(rb.position, new Color(0.4f, 0.8f, 1f), 3f, 2f, 0.2f);
            GameEvents.ShowToast("Frozen (RMB on it to unfreeze)");
        }

        void Unfreeze(Collider c)
        {
            var rb = c.attachedRigidbody;
            if (rb == null || !rb.isKinematic || c.gameObject.layer == Layers.Boundaries) return;
            if (rb.GetComponent<Spawned>() == null && rb.GetComponentInParent<Goredoll>() == null && c.gameObject.layer != Layers.Props) return;
            if (NetState.IsClient && NetManager.Handle(rb, out _, out _)) { NetManager.I.RequestFreeze(rb, false); AudioManager.Play("sg_unfreeze", rb.position, 0.7f); return; }
            rb.isKinematic = false;
            rb.WakeUp();
            AudioManager.Play("sg_unfreeze", rb.position, 0.7f);
        }

        // ------------------------------------------------------------------ Delete / scale

        /// <summary>The object a delete would remove (spawned root, Goredoll, debris), or null for map geometry.</summary>
        public static GameObject DeletableRoot(Collider c)
        {
            if (c == null) return null;
            var doll = c.GetComponentInParent<Goredoll>();
            if (doll != null) return doll.gameObject;
            var sp = c.GetComponentInParent<Spawned>();
            if (sp != null) return sp.gameObject;
            var nb = c.GetComponentInParent<Nextbot>();
            if (nb != null) return nb.gameObject;
            if (c.gameObject.layer == Layers.Debris || c.gameObject.layer == Layers.Props || c.GetComponentInParent<Gib>() != null)
                return c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
            return null;
        }

        public static void DeleteObject(GameObject go)
        {
            if (go == null) return;
            if (NetState.ClientIntercepts && go.GetComponentInParent<NetEntity>() != null) { NetManager.I.RequestDelete(go); return; }
            var doll = go.GetComponent<Goredoll>();
            if (doll != null) doll.Despawn();
            else Object.Destroy(go);
        }

        void Delete(Collider c)
        {
            var go = DeletableRoot(c);
            if (go == null) { AudioManager.Ui("ui_error", 0.4f); return; }
            var p = c.bounds.center;
            DeleteObject(go);
            AudioManager.Play("sg_delete", p, 0.7f);
            FX.Dust(p, 0.4f, new Color(1f, 0.3f, 0.3f, 0.6f), 18);
            Stats.Add("deleted");
        }

        void Scale(Collider c, float sign, float dt)
        {
            if (c == null) return;
            var sp = c.GetComponentInParent<Spawned>();
            if (sp == null || sp.GetComponent<Goredoll>() != null || sp.GetComponent<Nextbot>() != null) return;
            var t = sp.transform;
            float s = t.localScale.x;
            float ns = Mathf.Clamp(s * (1f + sign * dt * 1.2f), 0.15f, 6f);
            if (Mathf.Approximately(ns, s)) return;
            var ne = sp.GetComponent<NetEntity>();
            if (NetState.IsClient && ne != null)
            {
                t.localScale = Vector3.one * ns; // instant feedback; the host confirms
                if (Time.frameCount % 3 == 0) NetManager.I.RequestScale(ne, ns);
                return;
            }
            t.localScale = Vector3.one * ns;
            if (NetState.IsHost && ne != null && Time.frameCount % 3 == 0) NetManager.I.OnScaled(ne);
            var rb = sp.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = Mathf.Max(0.05f, rb.mass * Mathf.Pow(ns / s, 3f));
            if (Time.frameCount % 6 == 0) AudioManager.Play("sg_scale", t.position, 0.3f, 0.8f + ns * 0.15f);
        }

        // ------------------------------------------------------------------ Visuals

        void UpdateBeam()
        {
            beam.enabled = held != null;
            if (held == null) return;
            var a = Model.Muzzle.position;
            var b = held.transform.TransformPoint(localGrab);
            var mid = Eye + Aim * Vector3.Distance(Eye, b) * 0.5f; // bends like a physgun beam
            var col = rotating ? new Color(1f, 0.8f, 0.3f) : new Color(0.3f, 1f, 0.6f);
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                var p = Vector3.Lerp(Vector3.Lerp(a, mid, t), Vector3.Lerp(mid, b, t), t);
                p += Random.insideUnitSphere * 0.006f * Mathf.Sin(t * Mathf.PI);
                beam.SetPosition(i, p);
            }
            beam.startColor = col.WithAlpha(0.9f);
            beam.endColor = col.WithAlpha(0.6f);
            beam.widthMultiplier = 0.03f + Mathf.Sin(pulse * 40f) * 0.006f;
        }

        static string NameOf(Collider c)
        {
            var doll = c.GetComponentInParent<Goredoll>();
            if (doll != null) return doll.DisplayName + " · " + doll.StatusLine;
            var sp = c.GetComponentInParent<Spawned>();
            if (sp != null) return sp.DisplayName + (c.attachedRigidbody != null && c.attachedRigidbody.isKinematic ? " (frozen)" : "");
            var nb = c.GetComponentInParent<Nextbot>();
            if (nb != null) return nb.DisplayName;
            return c.attachedRigidbody != null ? c.attachedRigidbody.name : "";
        }
    }
}
