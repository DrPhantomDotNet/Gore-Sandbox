using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Builds a complete Goredoll: meshes, colliders, rigidbodies, joints, face, organs, voice, brain.</summary>
    public static class GoredollFactory
    {
        static readonly PartType[] Order =
        {
            PartType.Pelvis, PartType.Abdomen, PartType.Chest, PartType.Neck, PartType.Head,
            PartType.UpperArmL, PartType.ForearmL, PartType.HandL, PartType.UpperArmR, PartType.ForearmR, PartType.HandR,
            PartType.ThighL, PartType.ShinL, PartType.FootL, PartType.ThighR, PartType.ShinR, PartType.FootR,
        };

        public static readonly string[] Presets = { "Normal", "Coward", "Brave", "Aggressive", "Passive" };

        public static Goredoll Spawn(Vector3 feet, float yaw, string preset = "Normal", int variant = -1, Transform parent = null, int seed = 0)
        {
            // The look is seeded so a multiplayer host and its clients build the exact same person.
            if (seed == 0) seed = Random.Range(1, int.MaxValue);
            var netPreset = preset; // full preset incl. "~a3~c<code>" tokens (armour / custom look), see NpcMods
            var mods = NpcMods.Parse(ref preset);
            var randomState = Random.state;
            Random.InitState(seed);
            var look = mods.Custom != null ? mods.Custom.ToAppearance() : Appearance.Random(variant);
            if (mods.Custom == null) ArmedDolls.Dress(look, preset);
            var zombieKind = Zombies.KindOf(preset);
            if (zombieKind.HasValue) Zombies.Zombify(look, zombieKind.Value);
            Random.state = randomState;
            var root = new GameObject("Goredoll " + look.Name);
            root.layer = Layers.Ragdoll;
            root.transform.SetParent(parent != null ? parent : WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            root.transform.SetPositionAndRotation(feet + Vector3.up * 0.02f, Quaternion.Euler(0, yaw, 0));
            root.transform.localScale = Vector3.one * look.Height;
            var doll = root.AddComponent<Goredoll>();
            doll.Look = look;
            doll.DisplayName = look.Name;
            doll.SpawnTime = Time.time;
            doll.Seed = seed; doll.Variant = variant; doll.Preset = netPreset;

            foreach (var type in Order) BuildPart(doll, root.transform, type, look);
            IgnoreSelfCollisions(doll);

            doll.Bleeding = root.AddComponent<Bleeding>();
            doll.Bleeding.Init(doll);
            doll.Dismember = root.AddComponent<Dismemberment>();
            doll.Dismember.Init(doll);
            doll.Face = FaceRig.Build(doll.Parts[PartType.Head].transform, look);
            doll.Voice = root.AddComponent<GoredollVoice>();
            doll.Voice.Init(doll, doll.Parts[PartType.Head].transform, doll.Parts[PartType.Chest].transform);
            doll.Ragdoll = root.AddComponent<ActiveRagdoll>();
            doll.Ragdoll.Init(doll);
            doll.Brain = root.AddComponent<GoredollBrain>();
            doll.Brain.Init(doll);
            doll.Brain.ApplyPreset(preset);
            if (Rules.BigHeads) doll.Parts[PartType.Head].transform.localScale = Vector3.one * 1.8f;
            root.AddComponent<Spawned>().DisplayName = "Goredoll " + look.Name;
            if (zombieKind.HasValue) Zombies.Attach(doll, zombieKind.Value, seed);
            NpcMods.Attach(doll, preset, mods);

            Stats.Add(Stats.Spawned);
            if (!NetState.IsClient) EnforceLimit();
            if (NetState.IsHost) NetManager.I.OnDollSpawned(doll, seed, netPreset, variant);
            return doll;
        }

        static void BuildPart(Goredoll doll, Transform root, PartType type, Appearance look)
        {
            var spec = Anatomy.Parts[type];
            var go = new GameObject(spec.Name);
            go.layer = Layers.Ragdoll;
            go.transform.SetParent(root, false);
            go.transform.localPosition = spec.Pivot;
            go.transform.localRotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = HumanModel.Build(type, look);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;

            Collider col;
            switch (spec.Shape)
            {
                case ColliderShape.Box:
                    var bc = go.AddComponent<BoxCollider>(); bc.center = spec.ColliderCenter; bc.size = spec.ColliderSize; col = bc; break;
                case ColliderShape.Sphere:
                    var sc = go.AddComponent<SphereCollider>(); sc.center = spec.ColliderCenter; sc.radius = spec.ColliderSize.x; col = sc; break;
                default:
                    var cc = go.AddComponent<CapsuleCollider>(); cc.center = spec.ColliderCenter; cc.radius = spec.ColliderSize.x; cc.height = spec.ColliderSize.y; cc.direction = 1; col = cc; break;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = spec.Mass * look.Build;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 16;
            rb.solverVelocityIterations = 6;
            rb.maxAngularVelocity = 25f;
            RB.SetDamping(rb, 0.05f, 0.6f);

            var part = go.AddComponent<BodyPart>();
            part.Type = type; part.Spec = spec; part.Doll = doll; part.Body = rb; part.Col = col; part.Filter = mf; part.Renderer = mr;
            part.MaxHealth = part.Health = spec.Health;
            doll.Parts[type] = part;

            if (spec.Parent.HasValue)
            {
                var parent = doll.Parts[spec.Parent.Value];
                part.Parent = parent;
                parent.Children.Add(part);
                var j = go.AddComponent<ConfigurableJoint>();
                j.connectedBody = parent.Body;
                j.anchor = Vector3.zero;
                j.axis = Vector3.right;
                j.secondaryAxis = Vector3.up;
                j.autoConfigureConnectedAnchor = true;
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = ConfigurableJointMotion.Limited;
                j.angularYMotion = spec.LimitY > 0 ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
                j.angularZMotion = spec.LimitZ > 0 ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
                JointCalibration.SetXLimits(j, spec.LimitX.x, spec.LimitX.y);
                j.angularYLimit = new SoftJointLimit { limit = spec.LimitY };
                j.angularZLimit = new SoftJointLimit { limit = spec.LimitZ };
                j.angularXLimitSpring = new SoftJointLimitSpring { spring = 0, damper = 0 };
                j.enablePreprocessing = false;
                j.rotationDriveMode = RotationDriveMode.Slerp;
                j.slerpDrive = new JointDrive { positionSpring = spec.Muscle, positionDamper = spec.Muscle * 0.06f, maximumForce = float.MaxValue };
                j.targetRotation = Quaternion.identity;
                j.massScale = 1f;
                j.connectedMassScale = 1f;
                part.Joint = j;
            }

            var organs = OrganModels.BuildInside(part);
            if (organs != null) part.OrgansVisual = organs;
        }

        static bool IsTorso(PartType t) => t == PartType.Pelvis || t == PartType.Abdomen || t == PartType.Chest || t == PartType.Neck || t == PartType.Head;

        static void IgnoreSelfCollisions(Goredoll doll)
        {
            var list = new List<BodyPart>(doll.Parts.Values);
            for (int i = 0; i < list.Count; i++)
                for (int k = i + 1; k < list.Count; k++)
                {
                    var a = list[i]; var b = list[k];
                    bool ignore = a.Parent == b || b.Parent == a
                        || (IsTorso(a.Type) && IsTorso(b.Type))
                        || (a.Parent != null && a.Parent.Parent == b) || (b.Parent != null && b.Parent.Parent == a)
                        || (a.Spec.IsArm && a.Parent != null && a.Parent.Type == PartType.Chest && (b.Type == PartType.Neck || b.Type == PartType.Abdomen || b.Type == PartType.Head))
                        || (b.Spec.IsArm && b.Parent != null && b.Parent.Type == PartType.Chest && (a.Type == PartType.Neck || a.Type == PartType.Abdomen || a.Type == PartType.Head))
                        || (a.Spec.IsLeg && a.Parent != null && a.Parent.Type == PartType.Pelvis && b.Type == PartType.Abdomen)
                        || (b.Spec.IsLeg && b.Parent != null && b.Parent.Type == PartType.Pelvis && a.Type == PartType.Abdomen);
                    if (ignore) Physics.IgnoreCollision(a.Col, b.Col, true);
                }
        }

        static void EnforceLimit()
        {
            int max = Mathf.Max(1, Settings.Data.maxRagdolls);
            while (Goredoll.All.Count > max)
            {
                // Remove the oldest dead one first, otherwise the oldest.
                Goredoll victim = null;
                foreach (var g in Goredoll.All) if (g.IsDead && (victim == null || g.SpawnTime < victim.SpawnTime)) victim = g;
                if (victim == null) foreach (var g in Goredoll.All) if (victim == null || g.SpawnTime < victim.SpawnTime) victim = g;
                victim.Despawn();
            }
        }

        /// <summary>Renders a portrait icon of a Goredoll for the spawn menu.</summary>
        public static Texture2D Portrait(string name, int variant)
        {
            var look = Appearance.Random(variant);
            var root = new GameObject("Portrait");
            foreach (var type in Order)
            {
                var spec = Anatomy.Parts[type];
                var go = new GameObject(spec.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = spec.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = HumanModel.Build(type, look);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                if (type == PartType.Head) FaceRig.Build(go.transform, look);
            }
            return IconFactory.Render(name, root, new Vector3(-0.35f, -0.15f, -1f), 192, 1.05f, "person");
        }
    }
}
