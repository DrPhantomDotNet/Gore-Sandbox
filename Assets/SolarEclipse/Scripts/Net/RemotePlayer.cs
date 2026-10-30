using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Another player in the world: a Goredoll-style body built from the same procedural meshes, holding their
    /// current weapon, walking/crouching/aiming/dying from their network state, with a hitbox that routes damage
    /// to the right machine. On the host it is also an AI target, so Goredolls react to every player.
    /// </summary>
    public sealed class RemotePlayer : MonoBehaviour, IPlayerTarget, IDamageable
    {
        public NetPlayer Player;
        public AudioSource VoiceSource;
        readonly Dictionary<PartType, Transform> parts = new Dictionary<PartType, Transform>();
        Transform model, socket;
        WeaponModel weapon;
        int weaponIndex = -1;
        float phase, flash, recoil, deadBlend, crouchBlend, height = 1f;
        Vector3 lastPos;
        Light muzzleLight;
        CapsuleCollider capsule;
        FaceRig face;

        public static RemotePlayer Create(NetPlayer p, Transform parent)
        {
            var go = new GameObject("Player " + p.Name);
            go.layer = Layers.RemotePlayer;
            go.transform.SetParent(parent, false);
            go.transform.position = p.Pos;
            var rp = go.AddComponent<RemotePlayer>();
            rp.Player = p;
            rp.Build();
            return rp;
        }

        public void SetName(string n) { if (Player != null) Player.Name = n; gameObject.name = "Player " + n; }

        // Profile (icon/hat/look) last applied to the model: rebuilt when the player's profile changes.
        ProfileData appliedProfile;
        int appliedVersion = -1;

        void BuildModel()
        {
            // The player's profile look (Core/PlayerProfile.cs), else seeded from the name so everyone sees the same person.
            var prof = NetManager.I != null ? NetManager.I.ProfileOf(Player) : null;
            appliedProfile = prof; appliedVersion = prof != null ? prof.Version : -1;
            var custom = prof != null ? prof.BodyLook : null;
            Appearance look;
            if (custom != null) look = custom.ToAppearance();
            else
            {
                var st = Random.state;
                Random.InitState((Player.Name ?? "p").GetHashCode() ^ 0x5EED);
                look = Appearance.Random(-1);
                Random.state = st;
            }
            height = look.Height;

            parts.Clear();
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * look.Height;
            foreach (PartType t in System.Enum.GetValues(typeof(PartType)))
            {
                var spec = Anatomy.Parts[t];
                var go = new GameObject(spec.Name);
                go.layer = Layers.RemotePlayer;
                var parent = spec.Parent.HasValue ? parts[spec.Parent.Value] : model;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = spec.Parent.HasValue ? spec.Pivot - Anatomy.Parts[spec.Parent.Value].Pivot : spec.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = HumanModel.Build(t, look);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                parts[t] = go.transform;
            }
            face = FaceRig.Build(parts[PartType.Head], look);
            if (custom != null && custom.Glasses) CustomDolls.AddGlasses(parts[PartType.Head], Layers.RemotePlayer);
            if (prof != null) Hats.Attach(parts[PartType.Head], prof.Hat, prof.HatColor, Layers.RemotePlayer);
            IconFactory.SetLayer(model.gameObject, Layers.RemotePlayer);
        }

        /// <summary>Rebuilds the body when the player's profile arrives or changes (keeps the voice source).</summary>
        void RefreshProfile()
        {
            var prof = NetManager.I != null ? NetManager.I.ProfileOf(Player) : null;
            if (prof == appliedProfile && (prof == null || prof.Version == appliedVersion)) return;
            if (VoiceSource != null) VoiceSource.transform.SetParent(transform, true);
            if (model != null) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
            BuildModel();
            if (VoiceSource != null) VoiceSource.transform.SetParent(parts[PartType.Head], false);
        }

        void Build()
        {
            BuildModel();

            socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(transform, false);

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(socket, false);
            muzzleLight = lightGo.AddComponent<Light>();
            muzzleLight.type = LightType.Point; muzzleLight.range = 7f; muzzleLight.color = new Color(1f, 0.75f, 0.45f); muzzleLight.enabled = false;

            capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up * 0.9f; capsule.height = 1.8f; capsule.radius = 0.3f;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Positional voice
            var vgo = new GameObject("Voice");
            vgo.transform.SetParent(parts[PartType.Head], false);
            VoiceSource = vgo.AddComponent<AudioSource>();
            VoiceSource.spatialBlend = Settings.Data.proximityVoice ? 1f : 0f;
            VoiceSource.minDistance = 2f; VoiceSource.maxDistance = 40f;
            VoiceSource.rolloffMode = AudioRolloffMode.Linear;
            VoiceSource.dopplerLevel = 0f;
            lastPos = transform.position;
        }

        void OnEnable() { if (!PlayerTargets.All.Contains(this)) PlayerTargets.All.Add(this); }
        void OnDisable() => PlayerTargets.All.Remove(this);

        void Update()
        {
            if (Player == null) return;
            var nm = NetManager.I;
            // Never show your own body (e.g. a stale avatar after an id change).
            if (nm != null && Player.Id == nm.LocalId) { if (Player.Avatar == this) Player.Avatar = null; Destroy(gameObject); return; }
            if (Time.frameCount % 15 == (Player.Id % 15)) RefreshProfile();
            float dt = Time.deltaTime;
            // Hide the avatar while our camera is inside it (walking into another player).
            var cam = PlayerController.Instance != null ? PlayerController.Instance.Cam : null;
            if (cam != null && model != null)
            {
                var local = transform.InverseTransformPoint(cam.transform.position);
                bool inside = new Vector2(local.x, local.z).magnitude < 0.45f && local.y > -0.2f && local.y < height * 1.95f;
                if (model.gameObject.activeSelf == inside) model.gameObject.SetActive(!inside);
            }
            // Follow the network position smoothly.
            var target = Player.Pos;
            if ((transform.position - target).sqrMagnitude > 25f) transform.position = target;
            else transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-15f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, Player.Yaw, 0), 1f - Mathf.Exp(-15f * dt));

            bool dead = (Player.Flags & NetPlayer.FDead) != 0;
            bool crouch = (Player.Flags & NetPlayer.FCrouch) != 0;
            bool noclip = (Player.Flags & NetPlayer.FNoclip) != 0;
            deadBlend = Mathf.MoveTowards(deadBlend, dead ? 1f : 0f, dt * 2.5f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouch ? 1f : 0f, dt * 6f);
            capsule.enabled = !dead && !noclip;
            capsule.height = Mathf.Lerp(1.8f, 1.2f, crouchBlend);
            capsule.center = Vector3.up * capsule.height * 0.5f;

            var vel = (transform.position - lastPos) / Mathf.Max(dt, 0.0001f);
            lastPos = transform.position;
            float speed = MathX.Flat(vel).magnitude;
            phase += speed * dt * 4.5f;
            float swing = Mathf.Clamp01(speed / 4f) * (noclip ? 0f : 1f);
            float s = Mathf.Sin(phase);

            // Legs
            float crouchThigh = -70f * crouchBlend, crouchShin = 110f * crouchBlend;
            parts[PartType.ThighL].localRotation = Quaternion.Euler(-28f * s * swing + crouchThigh, 0, 0);
            parts[PartType.ThighR].localRotation = Quaternion.Euler(28f * s * swing + crouchThigh, 0, 0);
            parts[PartType.ShinL].localRotation = Quaternion.Euler(Mathf.Max(0, -s) * 45f * swing + crouchShin, 0, 0);
            parts[PartType.ShinR].localRotation = Quaternion.Euler(Mathf.Max(0, s) * 45f * swing + crouchShin, 0, 0);
            parts[PartType.FootL].localRotation = Quaternion.Euler(-crouchThigh - crouchShin, 0, 0);
            parts[PartType.FootR].localRotation = Quaternion.Euler(-crouchThigh - crouchShin, 0, 0);
            parts[PartType.Abdomen].localRotation = Quaternion.Euler(12f * crouchBlend, 0, 0);

            // Aim: torso/head follow pitch; arms hold the weapon forward.
            float pitch = Mathf.Clamp(Player.Pitch, -70f, 70f);
            parts[PartType.Chest].localRotation = Quaternion.Euler(pitch * 0.35f, 0, 0);
            parts[PartType.Neck].localRotation = Quaternion.Euler(pitch * 0.25f, 0, 0);
            parts[PartType.Head].localRotation = Quaternion.Euler(pitch * 0.35f, 0, 0);
            bool armed = weapon != null && weapon.gameObject.activeSelf && weaponIndex > 0;
            float armX = armed ? -78f + pitch * 0.4f - recoil * 12f : -6f + 18f * s * swing;
            parts[PartType.UpperArmR].localRotation = Quaternion.Euler(armX, 0, armed ? -8f : 6f);
            parts[PartType.ForearmR].localRotation = Quaternion.Euler(armed ? -20f : -14f, 0, 0);
            parts[PartType.UpperArmL].localRotation = Quaternion.Euler(armed ? armX + 8f : -6f - 18f * s * swing, 0, armed ? 22f : -6f);
            parts[PartType.ForearmL].localRotation = Quaternion.Euler(armed ? -38f : -14f, 0, 0);

            // Crouch lowers the whole model so the feet stay planted; death lays it down.
            model.localPosition = Vector3.up * (-0.42f * crouchBlend * height);
            model.localRotation = Quaternion.Euler(-88f * Mathf.SmoothStep(0, 1, deadBlend), 0, 0);
            if (face != null) face.Expression = dead ? Expression.Dead : (Player.Flags & NetPlayer.FFiring) != 0 ? Expression.Angry : Expression.Neutral;

            // Weapon in hand
            if (Player.Weapon != weaponIndex) SetWeapon(Player.Weapon);
            if (weapon != null)
            {
                weapon.gameObject.SetActive(!dead);
                socket.localPosition = new Vector3(0.12f, (1.36f - 0.42f * crouchBlend) * height, 0.36f);
                socket.localRotation = Quaternion.Euler(pitch, 0, 0) * Quaternion.Euler(-recoil * 6f, 0, 0);
            }
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);
            flash -= dt;
            muzzleLight.enabled = flash > 0f;
            if (VoiceSource != null) VoiceSource.spatialBlend = Settings.Data.proximityVoice ? 1f : 0f;
        }

        void SetWeapon(int index)
        {
            weaponIndex = index;
            if (weapon != null) Destroy(weapon.gameObject);
            weapon = null;
            if (index <= 0 || index >= WeaponCatalog.All.Count) return; // fists
            weapon = WeaponModels.Build(WeaponCatalog.All[index].Id, Layers.RemotePlayer);
            weapon.transform.SetParent(socket, false);
            IconFactory.SetLayer(weapon.gameObject, Layers.RemotePlayer);
            foreach (var c in weapon.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (weapon.Muzzle != null) muzzleLight.transform.SetParent(weapon.Muzzle, false);
        }

        public Vector3 MuzzlePosition => weapon != null && weapon.Muzzle != null ? weapon.Muzzle.position : EyePosition + AimDirection * 0.6f;

        public void OnFire(WeaponDef def)
        {
            flash = 0.05f;
            recoil = Mathf.Min(1f, recoil + def.Recoil * 0.25f);
            if (weapon != null && weapon.Muzzle != null) FX.MuzzleSmoke(weapon.Muzzle.position, AimDirection, def.Pellets > 1 ? 1.5f : 0.8f);
        }

        // ------------------------------------------------------------------ IPlayerTarget

        public Vector3 Position => transform.position;
        public Vector3 EyePosition => transform.position + Vector3.up * Mathf.Lerp(1.62f, 1.1f, crouchBlend) * height;
        public Vector3 AimDirection => Quaternion.Euler(Player != null ? Player.Pitch : 0f, Player != null ? Player.Yaw : 0f, 0f) * Vector3.forward;
        public bool HoldingGun => weaponIndex > 0 && weaponIndex < WeaponCatalog.All.Count && WeaponCatalog.All[weaponIndex].Kind == WeaponKind.Firearm;
        public bool IsDead => Player == null || (Player.Flags & NetPlayer.FDead) != 0;
        public bool IsAlive => !IsDead;
        public Transform Root => transform;
        public IDamageable Damageable => this;
        public string PlayerName => Player != null ? Player.Name : "Player";

        // ------------------------------------------------------------------ damage routing

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return;
            if (Settings.GoreEnabled) BloodFX.Entry(info.Point, info.Normal, info.Direction, Mathf.Clamp(info.Amount / 35f, 0.3f, 1.5f) * Settings.BloodScale);
            AudioManager.Play("flesh_hit", info.Point, 0.8f);
            if (NetState.IsHost) NetManager.I.HurtRemotePlayer(Player, info, info.Source);
            else if (NetState.IsClient && NetState.LocalAction && !NetState.ApplyingRemote) NetManager.I.RequestHurtPlayer(this, info);
        }
    }

    /// <summary>Host-side ban list (by player id and IP), saved between sessions.</summary>
    public static class Bans
    {
        public sealed class Entry { public string Guid, Name, Ip, Date; }
        public static readonly List<Entry> List = new List<Entry>();
        const string Key = "mp_bans";

        public static void Load()
        {
            List.Clear();
            foreach (var line in PlayerPrefs.GetString(Key, "").Split('\n'))
            {
                var f = line.Split('\t');
                if (f.Length >= 4 && f[0].Length > 0) List.Add(new Entry { Guid = f[0], Name = f[1], Ip = f[2], Date = f[3] });
            }
        }

        static void Save()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in List) sb.Append(e.Guid).Append('\t').Append(e.Name.Replace('\t', ' ')).Append('\t').Append(e.Ip).Append('\t').Append(e.Date).Append('\n');
            PlayerPrefs.SetString(Key, sb.ToString());
            PlayerPrefs.Save();
        }

        public static bool IsBanned(string guid, string ip)
        {
            foreach (var e in List) if (e.Guid == guid || (!string.IsNullOrEmpty(ip) && e.Ip == ip && ip != "127.0.0.1")) return true;
            return false;
        }

        public static void Add(string guid, string name, string ip)
        {
            if (IsBanned(guid, null)) return;
            List.Add(new Entry { Guid = guid, Name = name, Ip = ip, Date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") });
            Save();
        }

        public static void Remove(Entry e) { List.Remove(e); Save(); }
    }

    /// <summary>Simple message box with an OK button.</summary>
    public sealed class MessageDialog : UIScreen
    {
        readonly string title, body;
        public MessageDialog(string title, string body) { this.title = title; this.body = body; }
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_error");

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(680, 320);
            UI.Panel(r, title, a);
            UI.Label(new Rect(r.x + 30, r.y + 84, r.width - 60, 140), body, 22, UI.TextColor.WithAlpha(a), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(r.xMax - 250, r.yMax - 86, 220, 60), "OK", ButtonKind.Primary)) UIRoot.Pop();
        }
    }
}
