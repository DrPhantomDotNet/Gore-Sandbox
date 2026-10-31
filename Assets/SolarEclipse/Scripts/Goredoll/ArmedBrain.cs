using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Armed Goredolls: "Gunman" (Glock) and "Soldier" (AR-15, helmet + vest). Like zombies they are ordinary Goredolls
    /// built from a preset, so they replicate through the normal SpawnDoll message. The gun model is attached to the
    /// right hand on every machine; the <see cref="ArmedBrain"/> only thinks on the host / in singleplayer.
    /// </summary>
    public static class ArmedDolls
    {
        public const string Gunman = "Gunman", Soldier = "Soldier";
        static bool registered;

        public static bool IsArmed(string preset) => preset == Gunman || preset == Soldier;

        /// <summary>Outfit, inside the seeded spawn (deterministic everywhere).</summary>
        public static void Dress(Appearance a, string preset)
        {
            if (preset == Soldier)
            {
                a.Shirt = new Color(0.33f, 0.36f, 0.24f);
                a.Pants = new Color(0.36f, 0.34f, 0.25f);
                a.Shoes = new Color(0.12f, 0.1f, 0.08f);
                a.LongSleeves = true;
                a.HairStyle = 1;
            }
            else if (preset == Gunman)
            {
                a.Shirt = Random.value < 0.5f ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.32f, 0.22f, 0.15f);
                a.Pants = new Color(0.14f, 0.15f, 0.2f);
                a.LongSleeves = true;
            }
        }

        public static void RegisterSpawnables(List<SpawnableDef> ai)
        {
            if (registered) return;
            registered = true;
            ai.Add(new SpawnableDef
            {
                Id = "armed_gunman", Name = "Gunman", Category = "AI", Glyph = "person",
                Description = "Carries a Glock. Keeps his distance and shoots at you and at zombies.",
                Spawn = (pos, rot) => GoredollFactory.Spawn(pos, rot.eulerAngles.y, Armor.WithSpawnArmor(Gunman), SpawnCatalog.GoredollVariant).gameObject,
            });
            ai.Add(new SpawnableDef
            {
                Id = "armed_soldier", Name = "Soldier", Category = "AI", Glyph = "person",
                Description = "AR-15, helmet and vest. Fires in bursts, strafes and takes cover.",
                Spawn = (pos, rot) => GoredollFactory.Spawn(pos, rot.eulerAngles.y, Soldier + "~a3", -1).gameObject,
            });
        }

        public static string GunFor(string preset) => preset == Soldier ? "ar15" : "glock";

        /// <summary>Adds the gun and the armed mind. Called by GoredollFactory (through NpcMods) on host and clients.</summary>
        public static void Attach(Goredoll doll, string preset)
        {
            var brain = doll.gameObject.AddComponent<ArmedBrain>();
            brain.Init(doll, preset == Soldier);
            doll.DisplayName = (preset == Soldier ? "Soldier " : "Gunman ") + doll.DisplayName;
            var sp = doll.GetComponent<Spawned>();
            if (sp != null) sp.DisplayName = doll.DisplayName;
        }

        /// <summary>Puts a weapon model in a (right) hand. The barrel follows the hand's -Y axis (the arm direction).</summary>
        public static WeaponModel MountGun(Transform hand, string gunId, int layer)
        {
            WeaponModel m;
            try { m = WeaponModels.Build(gunId, layer); }
            catch (System.Exception e) { Debug.LogWarning("Armed doll gun model failed: " + e.Message); return null; }
            if (m == null) return null;
            var rot = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var grip = m.GripR != null ? new Vector3(0f, m.GripR.localPosition.y, m.GripR.localPosition.z + 0.02f) : Vector3.zero;
            var palm = new Vector3(0f, -0.085f, 0.012f);
            m.transform.SetParent(hand, false);
            m.transform.localRotation = rot;
            m.transform.localPosition = palm - rot * grip;
            foreach (var t in m.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return m;
        }

        // ------------------------------------------------------------------ spawn-menu portraits

        public static bool RenderPendingIcon()
        {
            foreach (var p in new[] { Gunman, Soldier })
            {
                string key = "prop_armed_" + p.ToLowerInvariant();
                if (IconFactory.Has(key)) continue;
                try
                {
                    var state = Random.state;
                    Random.InitState(p == Soldier ? 4242 : 777);
                    var look = Appearance.Random(-1);
                    Dress(look, p);
                    Random.state = state;
                    var root = CustomDolls.BuildMannequin(look, p == Soldier ? Armor.Full : Armor.None, false, GunFor(p));
                    IconFactory.Render(key, root, new Vector3(-0.35f, -0.15f, -1f), 192, 1.05f, "person");
                }
                catch (System.Exception e) { Debug.LogWarning("Armed portrait failed: " + e.Message); }
                if (!IconFactory.Has(key)) IconFactory.Register(key, IconFactory.Get("person"));
                return true;
            }
            return false;
        }
    }

    /// <summary>Mind of an armed Goredoll: picks targets, keeps its distance, strafes, aims (with reaction time and
    /// spread), shoots through the real ballistics, reloads, takes cover and flees when badly hurt.</summary>
    public sealed class ArmedBrain : MonoBehaviour
    {
        enum Mode { Idle, Wander, Engage, Search, Cover, Flee }

        Goredoll doll;
        ActiveRagdoll body;
        GoredollBrain mind;
        bool soldier, dropped;
        WeaponDef def, shotDef;
        int weaponIndex = -1;
        WeaponModel gun;
        GameObject flash;
        float flashUntil;

        int ammo, burstLeft;
        bool reloading;
        float reloadUntil, nextShot, acquiredAt, reaction, lastSeen = -99f, lastHurt = -99f, think, modeTime, retargetAt, strafeUntil, strafeDir = 1f, idleFor;
        Vector3 lastSeenPos;
        Mode mode = Mode.Idle;
        IPlayerTarget targetPlayer;
        Goredoll targetDoll, grudge;
        float grudgeTime = -99f;
        IPlayerTarget hurtByPlayer;
        float hurtByPlayerTime = -99f;
        bool visible;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;

        const float PlayerDamageScale = 0.6f;
        /// <summary>Optional: may this armed doll go after this player? (Open World police only chase wanted players.)</summary>
        public static System.Func<Goredoll, IPlayerTarget, bool> PlayerFilter;

        public bool HasGun => !dropped && gun != null;
        bool HasTarget => targetPlayer != null ? !targetPlayer.IsDead : targetDoll != null && targetDoll.Vitals.Alive;

        public void Init(Goredoll d, bool isSoldier)
        {
            doll = d;
            body = d.Ragdoll;
            mind = d.Brain;
            soldier = isSoldier;
            def = WeaponCatalog.Get(ArmedDolls.GunFor(isSoldier ? ArmedDolls.Soldier : ArmedDolls.Gunman)) ?? WeaponCatalog.All.Find(w => w.Kind == WeaponKind.Firearm);
            weaponIndex = WeaponCatalog.All.IndexOf(def);
            shotDef = new WeaponDef
            {
                Id = def.Id, Name = def.Name, Kind = def.Kind, Damage = def.Damage, Penetration = def.Penetration, Force = def.Force,
                Caliber = def.Caliber, Range = def.Range, Pellets = 1, FireSound = def.FireSound,
            };
            ammo = def.Mag;
            if (mind != null)
            {
                mind.enabled = false;
                mind.Bravery = isSoldier ? 0.85f : Random.Range(0.4f, 0.7f);
                mind.Aggression = 0.9f;
            }
            if (body != null) body.AimRifle = isSoldier;
            if (d.Parts.TryGetValue(PartType.HandR, out var hand) && hand != null)
            {
                gun = ArmedDolls.MountGun(hand.transform, ArmedDolls.GunFor(isSoldier ? ArmedDolls.Soldier : ArmedDolls.Gunman), d.gameObject.layer);
                if (gun != null)
                {
                    var mb = new MeshBuilder { Color = Color.white };
                    mb.Sphere(Vector3.zero, new Vector3(0.035f, 0.035f, 0.09f), 4, 6);
                    var mz = gun.Muzzle != null ? gun.Muzzle : gun.transform;
                    flash = new GameObject("MuzzleFlash");
                    flash.layer = d.gameObject.layer;
                    flash.transform.SetParent(mz, false);
                    flash.transform.localPosition = new Vector3(0, 0, 0.06f);
                    flash.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("MuzzleFlash");
                    var mr = flash.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = FlashMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    flash.SetActive(false);
                }
            }
        }

        static Material flashMat;
        static Material FlashMat => flashMat != null ? flashMat : (flashMat = Mats.Glow(new Color(1f, 0.75f, 0.35f), 4f));

        /// <summary>From Armor.Process (every hit on this doll, before wounds).</summary>
        public void OnHurt(DamageInfo info)
        {
            lastHurt = Time.time;
            if (info.Source is Goredoll g && g != doll) { grudge = g; grudgeTime = Time.time; }
            else
            {
                var p = info.Source as IPlayerTarget ?? (info.Source is Weapon ? PlayerController.Instance : null);
                if (p != null) { hurtByPlayer = p; hurtByPlayerTime = Time.time; }
            }
            retargetAt = 0f;
        }

        Vector3 Pelvis => doll.Parts[PartType.Pelvis].Body.position;
        Vector3 Eye => doll.Parts.TryGetValue(PartType.Head, out var h) && h != null && !h.Severed ? h.transform.position + Vector3.up * 0.12f : Pelvis + Vector3.up * 0.7f;

        void Update()
        {
            if (doll == null) return;
            if (flash != null && flash.activeSelf && Time.time > flashUntil) flash.SetActive(false);
            if (!dropped && ShouldDrop()) DropGun();
            if (dropped)
            {
                // Disarmed: the ordinary (frightened) Goredoll mind takes over while alive.
                if (body != null) body.AimWeight = 0f;
                if (!doll.NetProxy && NetState.Authority && doll.Vitals.Alive && mind != null)
                {
                    mind.Fear = Mathf.Max(mind.Fear, 0.7f);
                    mind.enabled = true;
                    enabled = false;
                }
                return;
            }
            if (doll.NetProxy || !NetState.Authority || mind == null) return;

            var v = doll.Vitals;
            if (!v.Conscious || v.Paralysis == Paralysis.Total || Rules.FreezeAi)
            {
                body.AimWeight = 0f;
                body.DesiredVelocity = Vector3.zero;
                mind.Thought = !v.Alive ? "dead" : v.Paralysis == Paralysis.Total ? "paralysed" : Rules.FreezeAi ? "frozen" : "unconscious";
                UpdateFace();
                return;
            }
            float dt = Time.deltaTime;
            mind.Fear = Mathf.MoveTowards(mind.Fear, Mathf.Clamp01(v.Pain / 200f), dt * 0.02f);
            think -= dt;
            if (think <= 0f)
            {
                think = Random.Range(0.18f, 0.26f);
                Perceive();
                Decide();
            }
            Act(dt);
            UpdateFace();
        }

        bool ShouldDrop()
        {
            if (gun == null) return true;
            if (doll.IsDead) return true;
            foreach (var t in new[] { PartType.UpperArmR, PartType.ForearmR, PartType.HandR })
                if (!doll.Parts.TryGetValue(t, out var p) || p == null || p.Severed || p.Gibbed) return true;
            return false;
        }

        void DropGun()
        {
            dropped = true;
            if (body != null) body.AimWeight = 0f;
            if (flash != null) Destroy(flash);
            if (gun == null) return;
            var go = gun.gameObject;
            gun = null;
            Vector3 vel = Vector3.zero;
            var handRb = go.GetComponentInParent<Rigidbody>();
            if (handRb != null) vel = RB.Vel(handRb);
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, true);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Debris;
            foreach (var rr in go.GetComponentsInChildren<Renderer>(true)) rr.enabled = true; // a gibbed hand hides its children
            var bc = go.AddComponent<BoxCollider>();
            if (soldier) { bc.center = new Vector3(0, 0.02f, 0.2f); bc.size = new Vector3(0.06f, 0.2f, 0.9f); }
            else { bc.center = new Vector3(0, -0.01f, 0.07f); bc.size = new Vector3(0.035f, 0.14f, 0.21f); }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.5f, def != null ? def.WeightKg : 1f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            RB.SetVel(rb, vel);
            rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            Debris.Register(go, 90f);
            if (doll != null && doll.Vitals.Alive && mind != null) doll.Voice.Say("v_no", 0.9f);
        }

        // ------------------------------------------------------------------ perception

        Vector3 AimPoint(bool head)
        {
            if (targetPlayer != null) return targetPlayer.EyePosition + Vector3.down * (head ? 0.08f : 0.45f);
            if (targetDoll != null)
            {
                if (head && targetDoll.Parts.TryGetValue(PartType.Head, out var h) && h != null && !h.Severed) return h.transform.position + Vector3.up * 0.1f;
                if (targetDoll.Parts.TryGetValue(PartType.Chest, out var c) && c != null) return c.transform.position + Vector3.up * 0.12f;
                return targetDoll.Position;
            }
            return Pelvis;
        }

        bool CanSee(Vector3 point, float dist)
        {
            if (dist > 70f) return false;
            if (dist > 14f)
            {
                var fwd = doll.Parts[PartType.Chest].transform.forward;
                if (Vector3.Angle(MathX.Flat(fwd), MathX.Flat(point - Pelvis)) > 80f && Time.time - lastHurt > 3f) return false;
            }
            return !Physics.Linecast(Eye, point, Layers.SightMask, QueryTriggerInteraction.Ignore);
        }

        void Perceive()
        {
            var eye = Eye;
            if (Time.time >= retargetAt || !HasTarget)
            {
                retargetAt = Time.time + 0.6f;
                IPlayerTarget bestP = null; Goredoll bestD = null;
                float best = float.MaxValue;
                if (!Rules.AiIgnorePlayer)
                    foreach (var p in PlayerTargets.All)
                    {
                        if (p == null || (p is Object o && o == null) || p.IsDead) continue;
                        if (PlayerFilter != null && p != hurtByPlayer && !PlayerFilter(doll, p)) continue;
                        float d = Vector3.Distance(eye, p.EyePosition);
                        if (!CanSee(p.EyePosition + Vector3.down * 0.3f, d)) continue;
                        if (p == hurtByPlayer && Time.time - hurtByPlayerTime < 15f) d *= 0.5f;
                        if (d < best) { best = d; bestP = p; bestD = null; }
                    }
                foreach (var g in Goredoll.All)
                {
                    if (g == null || g == doll || !g.Vitals.Alive) continue;
                    bool enemy = g.Zombie != null || (g == grudge && Time.time - grudgeTime < 20f)
                        || (g.Brain != null && g.Brain.Hostile && g.GetComponent<ArmedBrain>() == null);
                    if (!enemy) continue;
                    var gp = g.Parts.TryGetValue(PartType.Chest, out var ch) && ch != null ? ch.transform.position : g.Position;
                    float d = Vector3.Distance(eye, gp);
                    if (!CanSee(gp, d)) continue;
                    if (g.Zombie != null && d < 6f) d *= 0.5f; // close zombies first
                    if (d < best) { best = d; bestD = g; bestP = null; }
                }
                bool changed = bestP != null ? bestP != targetPlayer : bestD != null && bestD != targetDoll;
                if (bestP != null || bestD != null)
                {
                    if (changed)
                    {
                        acquiredAt = Time.time;
                        reaction = soldier ? Random.Range(0.35f, 0.7f) : Random.Range(0.5f, 1.1f);
                        if (Random.value < 0.4f) doll.Voice.Say("v_hey", 0.9f, false);
                    }
                    targetPlayer = bestP; targetDoll = bestD;
                }
                else if (HasTarget && Time.time - lastSeen > 12f) { targetPlayer = null; targetDoll = null; }
            }
            if (targetPlayer != null && (Rules.AiIgnorePlayer || targetPlayer.IsDead)) targetPlayer = null;
            visible = false;
            if (HasTarget)
            {
                var ap = AimPoint(false);
                visible = CanSee(ap, Vector3.Distance(eye, ap));
                if (visible) { lastSeen = Time.time; lastSeenPos = ap; }
            }
        }

        // ------------------------------------------------------------------ decisions

        void SetMode(Mode m)
        {
            if (m == mode) return;
            mode = m;
            modeTime = Time.time;
            path.Clear(); pathIndex = 0;
            var nav = NavGrid.Current;
            switch (m)
            {
                case Mode.Flee:
                    if (nav != null && nav.FleePoint(lastSeenPos, Pelvis, Random.Range(18f, 30f), out var f)) PathTo(f);
                    doll.Voice.Say(Random.value < 0.5f ? "v_scream" : "v_no");
                    break;
                case Mode.Cover:
                    if (nav != null && nav.FindCover(lastSeenPos + Vector3.up * 1.5f, Pelvis, 12f, out var c)) PathTo(c);
                    break;
                case Mode.Search:
                    PathTo(lastSeenPos);
                    break;
                case Mode.Wander:
                    if (nav != null && nav.RandomPoint(Pelvis, 12f, out var w)) PathTo(w);
                    break;
                case Mode.Engage:
                    if (Random.value < 0.5f) doll.Voice.Say("v_argh", 0.8f, false);
                    break;
            }
        }

        void PathTo(Vector3 target)
        {
            if (NavGrid.Current == null || !NavGrid.Current.FindPath(Pelvis, target, path)) { path.Clear(); path.Add(target); }
            pathIndex = path.Count > 1 ? 1 : 0;
        }

        void Decide()
        {
            var v = doll.Vitals;
            if (body.IsDown) return;
            float fear = mind.Fear;
            float fleeAt = 0.55f + mind.Bravery * 0.35f;
            bool hurting = v.Pain > 40f || v.BloodLossFraction > 0.18f || Time.time - lastHurt < 2.5f;
            if (mode == Mode.Flee)
            {
                if (fear < fleeAt - 0.25f && Time.time - modeTime > 6f) SetMode(HasTarget ? Mode.Engage : Mode.Idle);
                else if (path.Count == 0 || pathIndex >= path.Count) { modeTime = Time.time - 1f; SetMode(Mode.Cover); }
                return;
            }
            if (fear > fleeAt && hurting) { SetMode(Mode.Flee); return; }
            if (mode == Mode.Cover && Time.time - modeTime < (reloading ? 6f : 3.5f) && pathIndex < path.Count) return;
            if (HasTarget && visible)
            {
                if (reloading && mode != Mode.Cover && Random.value < 0.5f) { SetMode(Mode.Cover); return; }
                if (mode == Mode.Engage && Time.time - lastHurt < 0.3f && Random.value < (soldier ? 0.25f : 0.1f)) { SetMode(Mode.Cover); return; }
                SetMode(Mode.Engage);
            }
            else if (HasTarget && Time.time - lastSeen < 10f) { if (mode != Mode.Search) SetMode(Mode.Search); }
            else if (mode == Mode.Engage || mode == Mode.Search || mode == Mode.Cover) SetMode(Mode.Idle);
            else if (mode == Mode.Idle && Time.time - modeTime > idleFor) { idleFor = Random.Range(4f, 9f); SetMode(Mode.Wander); }
            else if (mode == Mode.Wander && (pathIndex >= path.Count || Time.time - modeTime > 15f)) SetMode(Mode.Idle);
        }

        // ------------------------------------------------------------------ acting

        float MaxSpeed(bool run)
        {
            float legs = Mathf.Min(Fn(PartType.ThighL), Fn(PartType.ShinL), Fn(PartType.ThighR), Fn(PartType.ShinR));
            float blood = Mathf.Clamp01(1f - doll.Vitals.BloodLossFraction * 1.8f);
            return (run ? 4.1f : 1.35f) * Mathf.Lerp(0.35f, 1f, legs) * Mathf.Lerp(0.45f, 1f, blood);
        }

        float Fn(PartType t) => doll.Parts.TryGetValue(t, out var p) && p != null ? p.Function : 0f;

        void Act(float dt)
        {
            Vector3 move = Vector3.zero;
            bool run = false, aim = false;
            var aimPoint = HasTarget ? AimPoint(false) : lastSeenPos;
            body.Pose = PoseMode.Idle;
            switch (mode)
            {
                case Mode.Idle:
                    mind.Thought = "on guard";
                    if (Time.time - modeTime > 3f) body.Pose = PoseMode.LookAround;
                    break;
                case Mode.Wander:
                    mind.Thought = "patrolling";
                    move = FollowPath();
                    break;
                case Mode.Search:
                    mind.Thought = "where did they go?";
                    aim = true;
                    move = FollowPath();
                    if (move == Vector3.zero && Time.time - modeTime > 3f) { targetPlayer = null; targetDoll = null; SetMode(Mode.Idle); }
                    break;
                case Mode.Cover:
                    mind.Thought = reloading ? "reloading!" : "taking cover";
                    move = FollowPath();
                    run = move != Vector3.zero;
                    aim = !run && HasTarget;
                    break;
                case Mode.Flee:
                    mind.Thought = "RETREAT!";
                    move = FollowPath();
                    run = true;
                    break;
                case Mode.Engage:
                {
                    aim = true;
                    mind.Thought = reloading ? "reloading!" : "engaging";
                    if (!HasTarget) break;
                    var to = MathX.Flat(aimPoint - Pelvis);
                    float dist = to.magnitude;
                    float pref = soldier ? 14f : 9f;
                    if (targetDoll != null && targetDoll.Zombie != null) pref = soldier ? 10f : 7f;
                    var dir = dist > 0.01f ? to / dist : Vector3.forward;
                    if (dist > pref + 6f) { move = dir; run = dist > 25f; }
                    else if (dist < pref - 3f) move = -dir;
                    else
                    {
                        if (Time.time > strafeUntil)
                        {
                            strafeUntil = Time.time + Random.Range(1.2f, 2.8f);
                            strafeDir = Random.value < 0.25f ? 0f : (Random.value < 0.5f ? -1f : 1f);
                        }
                        move = new Vector3(dir.z, 0, -dir.x) * strafeDir;
                    }
                    break;
                }
            }

            if (run) aim = false;
            body.AimWeight = Mathf.MoveTowards(body.AimWeight, aim ? 1f : 0f, dt * 4f);
            if (HasTarget && (aim || mode == Mode.Engage))
            {
                var shoulder = doll.Parts.TryGetValue(PartType.Chest, out var ch) && ch != null ? ch.transform.position + Vector3.up * 0.25f : Pelvis + Vector3.up * 0.6f;
                var d = aimPoint - shoulder;
                body.AimPitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                body.LookTarget = aimPoint;
            }
            else body.LookTarget = null;

            if (move.sqrMagnitude > 0.01f)
            {
                move = Avoid(move.normalized);
                body.DesiredVelocity = move * MaxSpeed(run) * (aim ? 0.85f : 1f);
                if (body.Pose == PoseMode.Idle || body.Pose == PoseMode.LookAround) body.Pose = run ? PoseMode.Run : PoseMode.Walk;
            }
            else body.DesiredVelocity = Vector3.zero;

            if (aim && HasTarget) FaceTowards(aimPoint);
            else if (move.sqrMagnitude > 0.01f) FaceTowards(Pelvis + move);

            if (aim) TryShoot(aimPoint, move.sqrMagnitude > 0.01f);
            if (reloading && Time.time >= reloadUntil)
            {
                reloading = false;
                ammo = def.Mag;
                if (!string.IsNullOrEmpty(def.MagIn)) AudioManager.Play(def.MagIn, Pelvis + Vector3.up * 0.4f, 0.7f, 1f, 1f, 1f, 25f);
            }
        }

        Vector3 FollowPath()
        {
            if (path.Count == 0 || pathIndex >= path.Count) return Vector3.zero;
            var to = MathX.Flat(path[pathIndex] - Pelvis);
            if (to.magnitude < 0.7f)
            {
                pathIndex++;
                if (pathIndex >= path.Count) return Vector3.zero;
                to = MathX.Flat(path[pathIndex] - Pelvis);
            }
            return to.normalized;
        }

        Vector3 Avoid(Vector3 dir)
        {
            var origin = Pelvis + Vector3.up * -0.3f;
            int mask = Layers.Mask(Layers.Default, Layers.Props);
            if (!Physics.SphereCast(origin, 0.25f, dir, out _, 1.1f, mask, QueryTriggerInteraction.Ignore)) return dir;
            foreach (float a in new[] { 40f, -40f, 75f, -75f, 110f, -110f })
            {
                var d = Quaternion.Euler(0, a, 0) * dir;
                if (!Physics.SphereCast(origin, 0.25f, d, out _, 1.1f, mask, QueryTriggerInteraction.Ignore)) return d;
            }
            return -dir;
        }

        void FaceTowards(Vector3 point)
        {
            var to = MathX.Flat(point - Pelvis);
            if (to.sqrMagnitude > 0.01f) body.DesiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ shooting

        void StartReload()
        {
            if (reloading) return;
            reloading = true;
            reloadUntil = Time.time + def.ReloadTime * 1.4f;
            if (!string.IsNullOrEmpty(def.MagOut)) AudioManager.Play(def.MagOut, Pelvis + Vector3.up * 0.4f, 0.7f, 1f, 1f, 1f, 25f);
        }

        void TryShoot(Vector3 aimPoint, bool moving)
        {
            if (gun == null || !HasTarget || !visible || reloading) return;
            if (Time.time < acquiredAt + reaction || Time.time < nextShot) return;
            if (!body.IsStanding || body.AimWeight < 0.8f) return;
            if (Rules.InfiniteAmmo == false && ammo <= 0) { StartReload(); return; }
            if (ammo <= 0) ammo = def.Mag;

            bool head = Random.value < (soldier ? 0.2f : 0.1f);
            aimPoint = AimPoint(head);
            var mz = gun.Muzzle != null ? gun.Muzzle : gun.transform;
            var origin = mz.position;
            var dir = aimPoint - origin;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            if (Vector3.Angle(mz.forward, dir) > 55f) { nextShot = Time.time + 0.1f; return; } // gun not up yet

            float armFn = Mathf.Min(Fn(PartType.UpperArmR), Fn(PartType.ForearmR), Fn(PartType.HandR));
            float focus = Mathf.Clamp01((Time.time - acquiredAt) / 3f);
            float spread = (soldier ? 1.5f : 2.4f) * (1f + mind.Fear * 1.5f) * (moving ? 1.6f : 1f) * (1f + (1f - armFn) * 2f) * Mathf.Lerp(1.3f, 0.7f, focus);
            var r = Random.insideUnitCircle * spread;
            dir = Quaternion.LookRotation(dir) * Quaternion.Euler(r.y, r.x, 0f) * Vector3.forward;

            // Line of fire: never through ourselves or another armed Goredoll that isn't the target.
            if (Physics.Raycast(origin, dir, out var first, def.Range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var bp = first.collider.GetComponent<BodyPart>();
                if (bp != null && bp.Doll != null && bp.Doll != targetDoll && (bp.Doll == doll || bp.Doll.GetComponent<ArmedBrain>() != null))
                {
                    nextShot = Time.time + 0.4f;
                    if (mode == Mode.Engage) strafeUntil = 0f;
                    return;
                }
            }

            Fire(origin, dir);
            ammo--;
            if (soldier)
            {
                if (burstLeft <= 0) burstLeft = Random.Range(2, 5);
                burstLeft--;
                nextShot = Time.time + (burstLeft > 0 ? 60f / Mathf.Max(60f, def.Rpm) + 0.03f : Random.Range(0.5f, 1.1f));
            }
            else nextShot = Time.time + Random.Range(0.3f, 0.7f);
            if (ammo <= 0) StartReload();
        }

        void Fire(Vector3 origin, Vector3 dir)
        {
            AudioManager.Play(def.FireSound ?? "gun_glock", origin, 1f, Random.Range(0.95f, 1.05f), 1f, 3f, 150f);
            FX.Flash(origin, new Color(1f, 0.75f, 0.4f), 5f, 2.5f, 0.05f);
            FX.MuzzleSmoke(origin, dir, 0.5f);
            if (flash != null) { flash.SetActive(true); flash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f)); flashUntil = Time.time + 0.05f; }
            if (gun != null && gun.Eject != null) Casings.Eject(gun.Eject.position, gun.Eject.right, gun.Eject.up, Vector3.zero, def.CasingType);
            GameEvents.EmitNoise(origin, def.NoiseRadius, 1f, doll);

            // The local player has no collider bullets can hit (the host's own LocalHitbox is only for remote shots),
            // so test the shot against their body capsule here.
            float range = def.Range;
            IDamageable playerHit = null;
            var lp = PlayerController.Instance;
            if (lp != null && !lp.IsDead && !Rules.AiIgnorePlayer
                && RayCapsule(origin, dir, lp.Position + Vector3.up * 0.3f, lp.EyePosition - Vector3.up * 0.05f, 0.32f, out float t)
                && !Physics.Raycast(origin, dir, t, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                range = t;
                playerHit = lp.Damageable;
            }
            shotDef.Range = range;
            Ballistics.Fire(origin, dir, shotDef, doll, origin);
            if (playerHit != null)
                playerHit.TakeDamage(new DamageInfo
                {
                    Amount = def.Damage * PlayerDamageScale, Force = dir * def.Force * 0.3f, Point = origin + dir * range, Normal = -dir, Direction = dir,
                    Type = DamageType.Bullet, Source = doll, Weapon = doll.DisplayName + "'s " + def.Name, Caliber = def.Caliber,
                });
            // Clients: tracer, impacts and the gunshot (no damage; the host already did it).
            if (NetState.IsHost && NetManager.I != null && weaponIndex >= 0 && weaponIndex < 256)
                NetManager.I.BroadcastFire(255, weaponIndex, origin, new List<Vector3> { dir });
        }

        /// <summary>Ray vs capsule (segment a-b, radius r): distance along the ray to the closest approach if it passes within r.</summary>
        static bool RayCapsule(Vector3 o, Vector3 d, Vector3 a, Vector3 b, float r, out float t)
        {
            t = 0f;
            // Closest points between the ray and the segment.
            var u = d; var v = b - a; var w = o - a;
            float A = Vector3.Dot(u, u), B = Vector3.Dot(u, v), C = Vector3.Dot(v, v), D = Vector3.Dot(u, w), E = Vector3.Dot(v, w);
            float den = A * C - B * B;
            float sc, tc;
            if (den < 1e-6f) { sc = 0f; tc = C > 0 ? E / C : 0f; }
            else { sc = (B * E - C * D) / den; tc = (A * E - B * D) / den; }
            tc = Mathf.Clamp01(tc);
            sc = Mathf.Max(0f, (Vector3.Dot(a + v * tc - o, u)) / Mathf.Max(A, 1e-6f));
            var pr = o + u * sc; var ps = a + v * tc;
            if ((pr - ps).sqrMagnitude > r * r) return false;
            t = Mathf.Max(0f, sc - r * 0.5f);
            return true;
        }

        void UpdateFace()
        {
            var face = doll.Face;
            if (face == null) return;
            var v = doll.Vitals;
            if (!v.Alive) { face.Expression = Expression.Dead; face.ClearLook(); return; }
            if (v.State == LifeState.Unconscious) { face.Expression = Expression.Unconscious; return; }
            if (v.AirwayBlood > 0.4f) face.Expression = Expression.Choking;
            else if (v.Pain > 45f) face.Expression = Expression.Pain;
            else if (mode == Mode.Flee || mind.Fear > 0.5f) face.Expression = Expression.Afraid;
            else if (mode == Mode.Engage) face.Expression = Expression.Angry;
            else face.Expression = Expression.Neutral;
            if (HasTarget && visible) face.LookAt(AimPoint(true)); else face.ClearLook();
        }
    }
}
