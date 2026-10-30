using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Severs limbs at joints (bullets, explosions), slices them procedurally where the blade passed (knife, fan),
    /// and gibs body parts into meat, bone and organs. Stumps get flesh caps with bone, both sides bleed.
    /// </summary>
    public sealed class Dismemberment : MonoBehaviour
    {
        Goredoll doll;
        static Mesh stumpMesh, boneMesh;

        public void Init(Goredoll d) => doll = d;

        static float ArterialRate(PartType t)
        {
            switch (t)
            {
                case PartType.Head: case PartType.Neck: return 80f;
                case PartType.ThighL: case PartType.ThighR: case PartType.UpperArmL: case PartType.UpperArmR: return 55f;
                case PartType.ShinL: case PartType.ShinR: case PartType.ForearmL: case PartType.ForearmR: return 30f;
                default: return 12f;
            }
        }

        float Radius(BodyPart p) => p.Spec.Shape == ColliderShape.Box ? Mathf.Min(p.Spec.ColliderSize.x, p.Spec.ColliderSize.z) * 0.5f : p.Spec.ColliderSize.x;

        static void MarkSevered(BodyPart p)
        {
            p.Severed = true;
            foreach (var c in p.Children) MarkSevered(c);
        }

        Wound AmputationWound(BodyPart on, Vector3 localPoint, Vector3 localNormal, string weapon, PartType lost)
        {
            var w = new Wound
            {
                Kind = WoundKind.Amputation, Part = on.Type, Time = Time.time, LocalPoint = localPoint, LocalNormal = localNormal,
                Location = "the " + Anatomy.Parts[lost].Medical + " (traumatic amputation at the level of the " + Anatomy.Parts[on.Type].Medical + ")",
                Weapon = weapon, BleedRate = Settings.GoreEnabled && !Rules.NoBleeding ? ArterialRate(lost) : 0, Arterial = true, Pain = 85f, Damage = 100f,
            };
            on.Wounds.Add(w);
            doll.Wounds.Add(w);
            return w;
        }

        // ------------------------------------------------------------------ sever at joint

        bool Forward(byte kind, BodyPart part, Vector3 point, Vector3 dir, bool sharp)
        {
            if (doll.NetProxy && NetState.ClientIntercepts)
            {
                if (NetState.LocalAction) NetManager.I.RequestDismember(doll, kind, part, point, dir, sharp);
                return true;
            }
            if (NetState.IsHost) NetManager.I.BroadcastDismember(doll, kind, part, point, dir, sharp);
            return false;
        }

        public void Sever(BodyPart part, Vector3 point, Vector3 normal, bool sharp)
        {
            if (part == null || !part.CanSever || !Settings.Dismemberment || StoryHooks.NoDismember(part)) return;
            if (sharp && part.IsLimb && !part.Sliced) { Slice(part, point, normal); return; }
            if (Forward(0, part, point, normal, sharp)) return;
            if (sharp && part.IsLimb && !part.Sliced) { Slice(part, point, normal); return; }
            var parent = part.Parent;
            var jointPos = part.transform.position;
            var dir = (part.Col.bounds.center - jointPos).normalized;
            Destroy(part.Joint);
            part.Joint = null;
            MarkSevered(part);
            Stump(parent, jointPos, dir, Radius(part) * 1.05f, parent.IsLimb || parent.Type == PartType.Chest || parent.Type == PartType.Pelvis);
            Stump(part, jointPos, -dir, Radius(part), true);
            var w = AmputationWound(parent, parent.transform.InverseTransformPoint(jointPos), parent.transform.InverseTransformDirection(dir), sharp ? "Blade" : "Ballistic trauma", part.Type);
            doll.Bleeding.AddStump(parent, parent.transform.InverseTransformPoint(jointPos), parent.transform.InverseTransformDirection(dir), w);
            var pieceWound = new Wound { Kind = WoundKind.Amputation, Part = part.Type, BleedRate = 15f, Arterial = false, LocalPoint = Vector3.zero, LocalNormal = Vector3.up };
            part.Wounds.Add(pieceWound);
            doll.Bleeding.AddEmitter(part, pieceWound);
            RB.SetVel(part.Body, RB.Vel(part.Body) + dir * 1.5f + Random.insideUnitSphere);
            Consequences(part, jointPos, dir);
        }

        void Consequences(BodyPart lost, Vector3 pos, Vector3 dir)
        {
            float more = GoreKit.ChunkMul;
            BloodFX.Burst(pos, dir, 1.4f * more, Settings.BloodColor);
            BloodFX.Burst(pos, -dir, 0.8f * more, Settings.BloodColor);
            GoreKit.ScreenSplat(pos, 1.5f * more);
            GoreKit.WallSpray(pos, dir, 60f * more);
            AudioManager.Play("dismember", pos, 1f);
            var v = doll.Vitals;
            v.AddPain(85f);
            if (lost.Type == PartType.Head || lost.Type == PartType.Neck)
            {
                v.Decapitated = true;
                v.Die("Decapitation");
                Stats.Add(Stats.Decaps);
                GameEvents.Kill(doll.DisplayName + " lost their head", new Color(1f, 0.3f, 0.3f));
            }
            else
            {
                Stats.Add(Stats.Limbs);
                if (v.Conscious && doll.Voice != null) doll.Voice.Say("v_scream");
            }
            doll.Ragdoll.Fall(4f);
            doll.Brain?.OnDamaged(DamageInfo.Simple(80f, Vector3.zero, pos, DamageType.Slash), 80f);
        }

        // ------------------------------------------------------------------ procedural slice

        /// <summary>Cuts a limb mesh along the blade's plane. The far half becomes a free piece with the rest of the limb attached.</summary>
        public void Slice(BodyPart part, Vector3 worldPoint, Vector3 worldPlaneNormal)
        {
            if (part == null || part.Severed || part.Gibbed || part.Sliced || !Settings.Dismemberment || StoryHooks.NoDismember(part)) return;
            if (Forward(1, part, worldPoint, worldPlaneNormal, true)) return;
            var t = part.transform;
            // Keep cuts mostly across the limb so the result reads as an amputation.
            var axis = Vector3.down;
            var n = t.InverseTransformDirection(worldPlaneNormal).normalized;
            if (Vector3.Angle(n, axis) > 90f) n = -n;
            if (Vector3.Angle(n, axis) > 45f) n = Vector3.Slerp(axis, n, 45f / Vector3.Angle(n, axis)).normalized;
            var localPoint = t.InverseTransformPoint(worldPoint);
            float len = part.Spec.Length;
            localPoint.y = Mathf.Clamp(localPoint.y, -len * 0.85f, -len * 0.15f);
            localPoint.x = 0; localPoint.z = 0;
            var plane = new Plane(n, localPoint);
            if (!MeshSlicer.Slice(part.Filter.sharedMesh, plane, out var positive, out var negative)) { Sever(part, worldPoint, worldPlaneNormal, false); return; }
            bool originPositive = plane.GetSide(Vector3.zero);
            var attachedMesh = originPositive ? positive : negative;
            var freeMesh = originPositive ? negative : positive;
            float cut = -localPoint.y;

            // Attached half stays on the body.
            part.Filter.sharedMesh = attachedMesh;
            part.Renderer.sharedMaterials = new[] { Mats.Lit, Mats.Flesh };
            if (part.Col is CapsuleCollider cap)
            {
                cap.height = Mathf.Max(cap.radius * 2.05f, cut + cap.radius);
                cap.center = new Vector3(0, -cut * 0.5f, 0);
            }
            else if (part.Col is BoxCollider box)
            {
                box.size = new Vector3(box.size.x, cut, box.size.z);
                box.center = new Vector3(box.center.x, -cut * 0.5f, box.center.z);
            }
            part.Sliced = true;
            part.Health = Mathf.Min(part.Health, part.MaxHealth * 0.3f);

            // Free half: new rigidbody that carries the rest of the limb.
            var piece = new GameObject("Severed " + part.Spec.Name);
            piece.layer = Layers.Ragdoll;
            piece.transform.SetParent(transform, false);
            piece.transform.SetPositionAndRotation(t.position, t.rotation);
            piece.transform.localScale = Vector3.one;
            piece.AddComponent<MeshFilter>().sharedMesh = freeMesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mats.Lit, Mats.Flesh };
            var r = Radius(part);
            var pc = piece.AddComponent<CapsuleCollider>();
            pc.direction = 1;
            pc.radius = r;
            pc.height = Mathf.Max(r * 2.05f, len - cut + r);
            pc.center = new Vector3(0, -(cut + (len - cut) * 0.5f), 0);
            foreach (var other in doll.Parts.Values) if (other != null && other.Col != null) Physics.IgnoreCollision(pc, other.Col, other == part);
            var rb = piece.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.3f, part.Body.mass * (1f - cut / len));
            part.Body.mass = Mathf.Max(0.3f, part.Body.mass * cut / len);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            RB.SetDamping(rb, 0.05f, 0.5f);
            RB.SetVel(rb, RB.Vel(part.Body) + t.TransformDirection(n) * 1.2f);
            rb.angularVelocity = part.Body.angularVelocity;
            var piecePart = piece.AddComponent<BodyPart>();
            piecePart.Type = part.Type; piecePart.Spec = part.Spec; piecePart.Doll = doll; piecePart.Body = rb; piecePart.Col = pc;
            piecePart.Filter = piece.GetComponent<MeshFilter>(); piecePart.Renderer = piece.GetComponent<MeshRenderer>();
            piecePart.MaxHealth = piecePart.Health = part.MaxHealth * 0.5f;
            piecePart.Severed = true; piecePart.Sliced = true;
            piece.AddComponent<Spawned>().DisplayName = "Severed " + part.Spec.Medical;

            // Re-attach the downstream limb (hand / foot) to the free piece.
            foreach (var child in part.Children)
            {
                if (child.Joint == null) continue;
                var worldAnchor = child.transform.TransformPoint(child.Joint.anchor);
                child.Joint.autoConfigureConnectedAnchor = false;
                child.Joint.connectedBody = rb;
                child.Joint.connectedAnchor = piece.transform.InverseTransformPoint(worldAnchor);
                child.Parent = piecePart;
                piecePart.Children.Add(child);
                MarkSevered(child);
                foreach (var dc in DescendantsAndSelf(child)) if (dc.Col != null) Physics.IgnoreCollision(dc.Col, pc, true);
            }
            part.Children.Clear();

            // Bone stubs for both sides at the cut.
            var cutWorld = t.TransformPoint(localPoint);
            var outward = t.TransformDirection(n);
            Bone(part.transform, localPoint, n, r);
            Bone(piece.transform, localPoint, -n, r);

            var w = AmputationWound(part, localPoint, n, "Blade", part.Type);
            doll.Bleeding.AddStump(part, localPoint, n, w);
            var pw = new Wound { Kind = WoundKind.Amputation, Part = part.Type, BleedRate = 12f, LocalPoint = localPoint, LocalNormal = -n };
            piecePart.Wounds.Add(pw);
            doll.Bleeding.AddEmitter(piecePart, pw);
            AudioManager.Play("slice", cutWorld, 1f);
            Stats.Add(Stats.Slices);
            Consequences(part, cutWorld, outward);
        }

        static IEnumerable<BodyPart> DescendantsAndSelf(BodyPart p)
        {
            yield return p;
            foreach (var c in p.Children) foreach (var d in DescendantsAndSelf(c)) yield return d;
        }

        // ------------------------------------------------------------------ gibbing

        public void Gib(BodyPart part, Vector3 dir)
        {
            if (part == null || part.Gibbed || !part.Gibbable || !Settings.Dismemberment || StoryHooks.NoDismember(part)) return;
            if (Forward(2, part, part.transform.position, dir, false)) return;
            var pos = part.Col != null ? part.Col.bounds.center : part.transform.position;
            var vel = part.Body != null ? RB.Vel(part.Body) : Vector3.zero;
            // Free the children with stumps.
            foreach (var child in part.Children)
            {
                if (child.Gibbed || child.Joint == null) continue;
                var jp = child.transform.position;
                Destroy(child.Joint);
                child.Joint = null;
                MarkSevered(child);
                Stump(child, jp, (jp - pos).normalized * -1f, Radius(child), true);
                RB.SetVel(child.Body, RB.Vel(child.Body) + (child.transform.position - pos).normalized * 3f);
            }
            if (part.Parent != null && part.Joint != null)
            {
                var jp = part.transform.position;
                Destroy(part.Joint);
                part.Joint = null;
                var parent = part.Parent;
                Stump(parent, jp, (pos - jp).normalized, Radius(part), true);
                var w = AmputationWound(parent, parent.transform.InverseTransformPoint(jp), parent.transform.InverseTransformDirection((pos - jp).normalized), "Blast", part.Type);
                doll.Bleeding.AddStump(parent, parent.transform.InverseTransformPoint(jp), parent.transform.InverseTransformDirection((pos - jp).normalized), w);
            }
            part.Gibbed = true;
            part.Severed = true;
            foreach (var r in part.GetComponentsInChildren<Renderer>()) r.enabled = false;
            if (part.Col != null) part.Col.enabled = false;
            if (part.Body != null) { part.Body.isKinematic = true; part.Body.detectCollisions = false; }
            OrganModels.Spill(part, dir, vel);
            float more = GoreKit.ChunkMul;
            BloodFX.Burst(pos, dir, 2.2f * more, Settings.BloodColor);
            if (Settings.Unreal) { BloodFX.Burst(pos, -dir, 1.4f * more, Settings.BloodColor); BloodFX.Mist(pos, dir, 1.5f * more); }
            GoreKit.ScreenSplat(pos, 2.5f * more);
            GoreKit.PoolAt(pos, 1.1f * more);
            for (int s = 0; s < (Settings.SuperUltra ? 4 : Settings.Unreal ? 2 : 1); s++) GoreKit.WallSpray(pos, (dir + Random.insideUnitSphere * 0.6f).normalized, 80f);
            if (part.Type == PartType.Head) Stats.Add(GoreKit.StatHeads);
            for (int i = 0; i < Mathf.RoundToInt(6 * more); i++)
                if (Physics.Raycast(pos, (Random.onUnitSphere + Vector3.down).normalized, out var hit, 3f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                    Decals.Blood(hit.point, hit.normal, Random.Range(0.4f, 1f), Random.value < 0.5f ? 0 : 3, hit.collider);
            AudioManager.Play("gib", pos, 1f);
            CameraFX.ShakeAt(pos, 0.25f, 8f);
            Stats.Add(Stats.Gibs);
            var v = doll.Vitals;
            switch (part.Type)
            {
                case PartType.Head: v.BrainDestroyed = true; v.Die("Head destroyed"); Stats.Add(Stats.Decaps); break;
                case PartType.Neck: v.Decapitated = true; v.Die("Decapitation"); break;
                case PartType.Chest: v.HeartDestroyed = true; v.Die("Massive thoracic trauma"); break;
                case PartType.Abdomen: v.Die("Massive abdominal trauma"); break;
                default: v.AddPain(90f); doll.Ragdoll.Fall(4f); if (v.Conscious) doll.Voice.Say("v_scream"); break;
            }
        }

        // ------------------------------------------------------------------ stump visuals

        void Stump(BodyPart on, Vector3 worldPos, Vector3 worldOutward, float radius, bool bone)
        {
            if (on == null || on.Gibbed) return;
            var t = on.transform;
            var go = new GameObject("Stump");
            go.layer = Layers.Ragdoll;
            go.transform.SetParent(t, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, worldOutward);
            var lossy = t.lossyScale.x;
            go.transform.localScale = Vector3.one * (radius / 0.05f) / Mathf.Max(0.01f, lossy);
            go.AddComponent<MeshFilter>().sharedMesh = StumpMesh();
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mats.Lit, Mats.Flesh };
            if (bone) Bone(t, t.InverseTransformPoint(worldPos), t.InverseTransformDirection(worldOutward), radius);
        }

        void Bone(Transform on, Vector3 localPos, Vector3 localDir, float radius)
        {
            var go = new GameObject("Bone");
            go.layer = Layers.Ragdoll;
            go.transform.SetParent(on, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDir);
            go.transform.localScale = Vector3.one * Mathf.Clamp(radius / 0.05f, 0.4f, 1.6f);
            go.AddComponent<MeshFilter>().sharedMesh = BoneMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }

        /// <summary>Unit stump (radius 0.05): a ragged ring of skin around a flesh cap (submesh 1).</summary>
        static Mesh StumpMesh()
        {
            if (stumpMesh != null) return stumpMesh;
            var mb = new MeshBuilder { Color = new Color(0.55f, 0.08f, 0.08f), Gloss = 0.85f };
            var rng = new System.Random(3);
            mb.Loft(3, 14, i => (Vector3.up * (i * 0.006f - 0.004f), 0.05f * (1f - i * 0.04f), 0.05f * (1f - i * 0.04f)), Vector3.up, false, false,
                (i, j) => 1f + (i == 2 ? ((float)rng.NextDouble() - 0.5f) * 0.25f : 0f));
            var walls = mb.ToMesh("StumpWalls");
            // Cap disc with radial UVs so the cross-section texture fits.
            var cap = new MeshBuilder { Color = Color.white, Gloss = 0.9f };
            int c = cap.Vert(Vector3.up * 0.009f, new Vector2(0.5f, 0.5f));
            int seg = 18;
            var ring = new int[seg];
            for (int k = 0; k < seg; k++)
            {
                float a = k / (float)seg * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.048f + Vector3.up * (0.008f + ((float)rng.NextDouble() - 0.5f) * 0.004f);
                ring[k] = cap.Vert(p, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int k = 0; k < seg; k++) cap.Tri(c, ring[k], ring[(k + 1) % seg], Vector3.up);
            var m = new Mesh { name = "Stump" };
            var verts = new List<Vector3>(walls.vertices); verts.AddRange(cap.V);
            var cols = new List<Color>(walls.colors); cols.AddRange(cap.C);
            var uvs = new List<Vector2>(walls.uv); uvs.AddRange(cap.UV);
            m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, uvs);
            m.subMeshCount = 2;
            m.SetTriangles(walls.triangles, 0);
            var capTris = new List<int>();
            foreach (var i in cap.T) capTris.Add(i + walls.vertexCount);
            m.SetTriangles(capTris, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return stumpMesh = m;
        }

        static Mesh BoneMesh()
        {
            if (boneMesh != null) return boneMesh;
            var mb = new MeshBuilder { Color = new Color(0.93f, 0.9f, 0.82f), Gloss = 0.45f };
            mb.Cylinder(Vector3.down * 0.01f, Vector3.up * 0.028f, 0.014f, 0.011f, 8);
            mb.SetColor(new Color(0.5f, 0.08f, 0.1f), 0.7f);
            mb.Cylinder(Vector3.up * 0.028f, Vector3.up * 0.029f, 0.007f, 0.007f, 8);
            return boneMesh = mb.ToMesh("BoneStub");
        }
    }
}
