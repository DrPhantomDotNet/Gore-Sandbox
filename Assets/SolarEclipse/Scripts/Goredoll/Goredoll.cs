using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Root component of one Goredoll: ties body parts, physiology, ragdoll, brain, voice and face together.</summary>
    public sealed class Goredoll : MonoBehaviour
    {
        public static readonly List<Goredoll> All = new List<Goredoll>();

        public Appearance Look;
        public string DisplayName = "Goredoll";
        public readonly Physiology Vitals = new Physiology();
        public readonly Dictionary<PartType, BodyPart> Parts = new Dictionary<PartType, BodyPart>();
        public readonly List<Wound> Wounds = new List<Wound>();
        public ActiveRagdoll Ragdoll;
        public GoredollBrain Brain;
        public GoredollVoice Voice;
        public FaceRig Face;
        public Dismemberment Dismember;
        public Bleeding Bleeding;
        public float SpawnTime;
        public bool KilledByPlayer;
        /// <summary>Spawn parameters (kept so an infected corpse can rise as a zombie with the same look).</summary>
        public int Seed, Variant = -1;
        public string Preset = "Normal";
        /// <summary>Set when this Goredoll is a zombie (see ZombieDoll).</summary>
        public ZombieDoll Zombie;
        /// <summary>Bitten or eaten by a zombie: rises as one ~3 s after death.</summary>
        public bool Bitten;
        LifeState lastState = LifeState.Alive;
        object lastAttacker;
        bool despawned;
        /// <summary>Client copy of a host-simulated Goredoll (kinematic body, no brain, vitals from snapshots).</summary>
        public bool NetProxy { get; private set; }

        public void MakeProxy()
        {
            NetProxy = true;
            foreach (var p in Parts.Values)
            {
                if (p == null || p.Body == null) continue;
                p.Body.isKinematic = true;
                p.Body.interpolation = RigidbodyInterpolation.None;
            }
            if (Brain != null) Brain.enabled = false;
            if (Ragdoll != null) Ragdoll.enabled = false;
            if (Voice != null) Voice.Proxy = true;
            var px = gameObject.AddComponent<DollProxy>();
            px.Doll = this;
        }

        /// <summary>Client: true when this call should be handled by the host instead (caller must return).</summary>
        bool Forward => NetProxy && NetState.ClientIntercepts;

        public Vector3 Position => Parts[PartType.Pelvis].Body.position;
        public bool IsDead => Vitals.State == LifeState.Dead;

        public float TotalBleed
        {
            get
            {
                float b = 0;
                foreach (var p in Parts.Values)
                    if (p != null && !p.Severed) b += p.BleedRate;
                return b * Rules.BleedMultiplier;
            }
        }

        public bool TorsoBleeding
        {
            get
            {
                foreach (var t in new[] { PartType.Chest, PartType.Abdomen, PartType.Pelvis })
                    if (Parts.TryGetValue(t, out var p) && p.BleedRate > 3f) return true;
                return false;
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            float dt = Time.deltaTime;
            if (NetProxy)
            {
                // Vitals arrive from the host; just react to state changes locally.
                if (Vitals.State != lastState)
                {
                    if (Vitals.State != LifeState.Alive) Voice.Silence();
                    if (Vitals.State == LifeState.Dead) Ragdoll.State = RagdollState.Limp;
                    lastState = Vitals.State;
                }
                return;
            }
            Vitals.Tick(dt, TotalBleed);
            if (Vitals.State != lastState)
            {
                if (Vitals.State != LifeState.Alive) Voice.Silence();
                if (Vitals.State == LifeState.Dead) OnDeath();
                if (lastState == LifeState.Dead && Vitals.State != LifeState.Dead) { } // revived
                lastState = Vitals.State;
            }
            if (Vitals.Alive) Stats.Add(Stats.BloodMl, 0); // keep stat cache warm
            // Kill volume
            if (Position.y < (WorldRoot.Current != null ? WorldRoot.Current.KillY : -60f)) Despawn();
        }

        /// <summary>Raised on every damaging hit (Advanced Modding: ScriptedBrain's "on hurt").</summary>
        public event System.Action<DamageInfo, float> Damaged;

        public void OnPartDamaged(BodyPart part, DamageInfo info, Wound wound, float damage)
        {
            if (despawned) return;
            if (part.Severed) return;
            lastAttacker = info.Source;
            Damaged?.Invoke(info, damage);
            if (info.Source is PlayerController || info.Source is Weapon) { GameEvents.Hit(info.Point, damage); KilledByPlayer = true; }
            else if (info.Source is RemotePlayer rp && NetState.IsHost) NetManager.I.SendHitMarker(rp, info.Point, damage);
            Brain.OnDamaged(info, damage);
            Voice.OnHurt(damage, info.IsSharp);
            Ragdoll.OnHit(damage, part.Type);
            if (info.Type == DamageType.Explosion || damage > 60f) Ragdoll.Fall(3f);
        }

        void OnDeath()
        {
            var cause = string.IsNullOrEmpty(Vitals.CauseOfDeath) ? "Unknown" : Vitals.CauseOfDeath;
            if (KilledByPlayer) Stats.Add(Stats.Kills);
            string killer = lastAttacker is IPlayerTarget t ? t.PlayerName : lastAttacker is Weapon ? PlayerController.Instance?.PlayerName : null;
            GameEvents.Kill((NetState.Active && killer != null ? killer + "  >  " : "") + DisplayName + "  -  " + cause, new Color(1f, 0.45f, 0.4f));
            if (NetState.IsHost) NetManager.I.BroadcastDeath(this, cause, NetState.Active ? killer : null);
            if (Rules.SlowMoOnKill) GameManager.I.SlowMo(0.25f, 1.2f);
            Ragdoll.State = RagdollState.Limp;
            Zombies.OnDollDied(this);
            Died?.Invoke(this, lastAttacker); // Open World: wanted stars, bounties (OpenWorld/OWDirector.cs)
        }

        /// <summary>Any Goredoll died (doll, last attacker: a player, weapon, Goredoll or null).</summary>
        public static event System.Action<Goredoll, object> Died;

        public void Launch(Vector3 velocity)
        {
            if (Forward) { if (NetState.LocalAction) NetManager.I.RequestLaunch(this, velocity); return; }
            if (StoryHooks.NoLaunch(this)) return; // story NPCs that can't be pushed around yet
            Ragdoll.Launch(velocity);
        }

        public void Kill(string cause)
        {
            if (Forward) { if (NetState.LocalAction) NetManager.I.RequestDollCommand(this, 1); return; }
            Vitals.UndeadMayDie = true; // the Admin Gun / Kill command can put zombies down for good
            Vitals.Die(cause);
        }

        public void Say(string clip) => Voice.Say(clip);

        /// <summary>Admin Gun revive: heals everything that is still attached and stands the Goredoll back up.</summary>
        public bool Revive()
        {
            if (Forward) { if (NetState.LocalAction) NetManager.I.RequestDollCommand(this, 2); return true; }
            if (Vitals.Decapitated || (Parts.TryGetValue(PartType.Head, out var head) && (head.Gibbed || head.Severed))) return false;
            Vitals.ReviveFully();
            foreach (var p in Parts.Values)
            {
                if (p == null || p.Severed || p.Gibbed) continue;
                p.Health = p.MaxHealth; p.Destroyed = false; p.Fractured = false;
                foreach (var w in p.Wounds) { w.BleedRate = 0; w.Arterial = false; }
            }
            lastState = LifeState.Alive;
            Ragdoll.ForceStandNow();
            Stats.Add(Stats.Revives);
            if (NetState.IsHost) NetManager.I.BroadcastRevive(this);
            return true;
        }

        public void Despawn()
        {
            if (despawned) return;
            if (Forward) { NetManager.I.RequestDelete(gameObject); return; }
            despawned = true;
            All.Remove(this);
            Destroy(gameObject);
        }

        public string StatusLine
        {
            get
            {
                if (IsDead) return "Dead — " + Vitals.CauseOfDeath;
                if (Vitals.State == LifeState.Unconscious) return "Unconscious";
                if (Vitals.Paralysis == Paralysis.Total) return "Paralysed (conscious)";
                if (Ragdoll.State == RagdollState.Crawling) return "Crawling";
                if (Ragdoll.IsDown) return "Down";
                return Brain != null ? Brain.Thought : "";
            }
        }
    }
}
