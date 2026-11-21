using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 gore expansion: wound shapes (long grazes, slit stabs, deep cuts showing muscle, organs showing
    /// through very deep cuts that the Spawn Gun can pull out), swelling, skull fractures, a jaw / ears / nose /
    /// fingers / toes that come off, partial head destruction, face peeling, exposed ribs and spine, degloving,
    /// crush flattening, torn clothes, blood-soaked hair, burnt clothes falling away, jagged torn stumps with skin
    /// flaps, tendons and stretching muscle fibres, and piles of bodies that settle properly.
    /// Called from BodyPart.TakeDamage (OnWound) and Dismemberment. Visual pieces are local debris; anything that
    /// changes the body's structure goes through Dismemberment so multiplayer clients get it too.
    /// </summary>
    public static class GoreDetail
    {
        static readonly Color SkinRaw = new Color(0.62f, 0.1f, 0.1f), Bone = new Color(0.93f, 0.9f, 0.82f), Tendon = new Color(0.95f, 0.93f, 0.85f);

        /// <summary>Per-part one-time effects (each body part only loses its jaw once, and so on).</summary>
        sealed class Flags : MonoBehaviour { public readonly HashSet<string> Done = new HashSet<string>(); public int Fingers; }
        static bool Once(BodyPart p, string what)
        {
            var f = p.GetComponent<Flags>() ?? p.gameObject.AddComponent<Flags>();
            return f.Done.Add(what);
        }

        // ------------------------------------------------------------------ entry point from BodyPart

        public static void OnWound(BodyPart part, Wound w, DamageInfo info, float dmg, Vector3 pos, GameObject decal)
        {
            if (part == null || part.Doll == null || !Settings.GoreEnabled) return;
            bool super = Settings.SuperUltra, unreal = Settings.Unreal, realistic = Settings.Data.realisticGore;
            var t = part.transform;
            var local = t.InverseTransformPoint(pos);
            var type = part.Type;

            // Wound shapes.
            if (decal != null)
            {
                if (w.Kind == WoundKind.Graze) { AlignDecal(decal, info.Normal, info.Direction); decal.transform.localScale = Vector3.Scale(decal.transform.localScale, new Vector3(2.8f, 0.55f, 1f)); }
                else if (w.Kind == WoundKind.Stab) { AlignDecal(decal, info.Normal, info.Direction); decal.transform.localScale = Vector3.Scale(decal.transform.localScale, new Vector3(1.1f, 0.32f, 1f)); }
            }
            if (w.Kind == WoundKind.Laceration && dmg > 20f) MuscleStrip(part, pos, info.Normal, info.Direction, Mathf.Clamp(dmg / 160f, 0.06f, 0.16f));
            if (w.Kind == WoundKind.Laceration && dmg > (super ? 30f : 45f) && (type == PartType.Chest || type == PartType.Abdomen) && Settings.Organs && Once(part, "organ")) OrganGlimpse(part, pos, info.Normal);
            if ((w.Kind == WoundKind.GunshotEntry || w.Kind == WoundKind.Laceration || w.Kind == WoundKind.Stab) && CoveredByShirt(part)) TornCloth(part, pos, info.Normal);

            // Blunt force: swelling, skull fractures.
            if (w.Kind == WoundKind.Blunt && dmg > 10f) Swell(part, pos, 0.07f, Mathf.Clamp(dmg / 900f, 0.004f, 0.02f));
            if (part.IsHead && (info.Type == DamageType.Blunt || info.Type == DamageType.Fall) && dmg > 35f && Once(part, "skull" + (local.x > 0 ? "R" : "L"))) SkullFracture(part, pos, info.Normal);

            // Pieces of the face.
            if (part.IsHead && Settings.Dismemberment)
            {
                float cy = HumanModel.HeadCenterY;
                bool front = local.z > 0.04f;
                if (front && local.y < cy - 0.03f && dmg > (super ? 30f : unreal ? 40f : 55f) && (info.IsBallistic || info.Type == DamageType.Blunt || info.Type == DamageType.Explosion) && Once(part, "jaw")) LoseJaw(part, info.Direction);
                else if (Mathf.Abs(local.x) > 0.06f && local.y > cy - 0.04f && local.y < cy + 0.04f && (info.IsSharp ? dmg > 18f : dmg > 40f) && Once(part, "ear" + (local.x > 0 ? "R" : "L"))) Piece(part, pos, info.Direction, "Ear", new Vector3(0.012f, 0.03f, 0.02f), part.Doll.Look != null ? part.Doll.Look.Skin : SkinRaw);
                else if (front && Mathf.Abs(local.x) < 0.025f && Mathf.Abs(local.y - cy) < 0.03f && (info.IsSharp ? dmg > 20f : dmg > 45f) && Once(part, "nose")) Piece(part, pos, info.Direction, "Nose", new Vector3(0.022f, 0.03f, 0.025f), part.Doll.Look != null ? part.Doll.Look.Skin : SkinRaw);
                if (super && (info.Type == DamageType.Explosion || info.Type == DamageType.Blunt) && dmg > 60f && Once(part, "peel")) FacePeel(part);
            }

            // Fingers and toes.
            if ((type == PartType.HandL || type == PartType.HandR) && (info.IsBallistic || info.IsSharp) && dmg > 14f && Random.value < 0.45f) Finger(part, pos, info.Direction, false);
            if ((type == PartType.FootL || type == PartType.FootR) && info.IsSharp && dmg > 14f && Random.value < 0.45f) Finger(part, pos, info.Direction, true);

            // Torso destruction.
            if (type == PartType.Chest && (info.Type == DamageType.Explosion && dmg > 45f || part.Health < -part.MaxHealth * (super ? 0.2f : 0.5f)) && Settings.Organs && Once(part, "ribs")) Ribcage(part, info.Normal);
            if ((type == PartType.Chest || type == PartType.Abdomen) && local.z < -0.03f && (info.IsSharp || info.IsBallistic) && dmg > (super ? 25f : 40f) && Settings.Organs && Once(part, "spine")) SpineExposed(part, pos);

            // Extreme: degloving, crush flattening.
            if (super && !realistic && (type == PartType.HandL || type == PartType.HandR || type == PartType.ForearmL || type == PartType.ForearmR) && (info.Type == DamageType.Crush || info.IsSharp) && dmg > 45f && Once(part, "deglove"))
                Tint(part, v => true, new Color(0.6f, 0.06f, 0.07f), 0.85f);
            if ((info.Type == DamageType.Crush || info.Type == DamageType.Fall) && dmg > 40f && Once(part, "flat")) Flatten(part, info.Direction.sqrMagnitude > 0.01f ? info.Direction : Vector3.down, 0.72f);
        }

        static bool CoveredByShirt(BodyPart p)
        {
            switch (p.Type)
            {
                case PartType.Chest: case PartType.Abdomen: case PartType.UpperArmL: case PartType.UpperArmR: return true;
                case PartType.ForearmL: case PartType.ForearmR: return p.Doll.Look != null && p.Doll.Look.LongSleeves;
                case PartType.Pelvis: case PartType.ThighL: case PartType.ThighR: case PartType.ShinL: case PartType.ShinR: return true; // trousers
                default: return false;
            }
        }

        /// <summary>Rotate a wound decal about its normal so its long side follows the blade or bullet.</summary>
        static void AlignDecal(GameObject d, Vector3 normal, Vector3 dir)
        {
            var along = Vector3.ProjectOnPlane(dir, normal);
            if (along.sqrMagnitude < 1e-4f) return;
            d.transform.rotation = Quaternion.LookRotation(-normal, Vector3.Cross(normal, along.normalized));
        }

        // ------------------------------------------------------------------ mesh edits (vertex colours / positions)

        public static void Tint(BodyPart p, System.Func<Vector3, bool> where, Color col, float amount)
        {
            var m = GoreKit.OwnMesh(p);
            if (m == null) return;
            var v = m.vertices; var c = m.colors;
            if (c == null || c.Length != v.Length) return;
            for (int i = 0; i < v.Length; i++) if (where(v[i])) { var k = Color.Lerp(c[i], col, amount); k.a = c[i].a; c[i] = k; }
            m.colors = c;
        }

        /// <summary>Raised swelling: vertices near the hit push out a little along their normals.</summary>
        public static void Swell(BodyPart p, Vector3 worldPoint, float radius, float amount)
        {
            var m = GoreKit.OwnMesh(p);
            if (m == null) return;
            var lp = p.transform.InverseTransformPoint(worldPoint);
            var v = m.vertices; var n = m.normals;
            if (n == null || n.Length != v.Length) return;
            bool any = false;
            for (int i = 0; i < v.Length; i++)
            {
                float d = (v[i] - lp).magnitude;
                if (d > radius) continue;
                float k = 1f - d / radius;
                v[i] += n[i] * amount * k * k; any = true;
            }
            if (!any) return;
            m.vertices = v; m.RecalculateBounds();
        }

        /// <summary>Crush: squash the part along the crushing direction.</summary>
        public static void Flatten(BodyPart p, Vector3 worldDir, float factor)
        {
            var m = GoreKit.OwnMesh(p);
            if (m == null) return;
            var ld = p.transform.InverseTransformDirection(worldDir).normalized;
            var center = m.bounds.center;
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                var rel = v[i] - center;
                float along = Vector3.Dot(rel, ld);
                v[i] = center + rel - ld * along * (1f - factor) + Random.insideUnitSphere * 0.002f;
            }
            m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds();
            Tint(p, x => true, new Color(0.42f, 0.12f, 0.16f), 0.35f); // bruised, crushed tissue
        }

        // ------------------------------------------------------------------ small attached visuals

        static GameObject Visual(Transform parent, string name, Mesh mesh, Vector3 localPos, Quaternion localRot, Material mat = null)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localRotation = localRot;
            var lossy = parent.lossyScale.x;
            if (lossy > 0.001f) go.transform.localScale = Vector3.one / lossy;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : Mats.Lit;
            return go;
        }

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Mesh Cached(string key, System.Action<MeshBuilder> build)
        {
            if (meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.6f };
            build(mb);
            return meshes[key] = mb.ToMesh(key);
        }

        static void MuscleStrip(BodyPart p, Vector3 pos, Vector3 normal, Vector3 dir, float length)
        {
            var t = p.transform;
            var along = Vector3.ProjectOnPlane(dir, normal); if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(normal, Vector3.up);
            var rot = Quaternion.LookRotation(along.normalized, normal);
            var mesh = Cached("muscle_strip", mb =>
            {
                mb.SetColor(new Color(0.55f, 0.07f, 0.08f), 0.9f); mb.Box(Vector3.zero, new Vector3(0.012f, 0.004f, 1f));
                mb.SetColor(new Color(0.75f, 0.16f, 0.16f), 0.9f); for (int i = -2; i <= 2; i++) mb.Box(new Vector3(i * 0.002f, 0.0022f, 0), new Vector3(0.0008f, 0.0008f, 0.95f)); // fibres
            });
            var go = Visual(t, "Exposed Muscle", mesh, t.InverseTransformPoint(pos + normal * 0.001f), Quaternion.Inverse(t.rotation) * rot);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(1f, 1f, length));
        }

        /// <summary>A loop of gut or a lobe of liver showing through a very deep wound. It's held on by a weak joint: the Spawn Gun can pull it out.</summary>
        static void OrganGlimpse(BodyPart p, Vector3 pos, Vector3 normal)
        {
            if (p.Body == null) return;
            bool gut = p.Type == PartType.Abdomen;
            var go = OrganModels.Chunk(gut ? "Intestine" : "Liver", gut ? OrganModels.GutSegment() : OrganModels.Liver(), pos + normal * 0.02f, gut ? 0.25f : 0.8f, gut ? 0.03f : 0.05f, RB.Vel(p.Body), 120f);
            var j = go.AddComponent<FixedJoint>(); j.connectedBody = p.Body; j.breakForce = 90f; j.breakTorque = 90f;
            foreach (var bp in p.Doll.Parts.Values) if (bp != null && bp.Col != null) { var c = go.GetComponent<Collider>(); if (c != null) Physics.IgnoreCollision(c, bp.Col, true); }
        }

        static void TornCloth(BodyPart p, Vector3 pos, Vector3 normal)
        {
            var skin = p.Doll.Look != null ? p.Doll.Look.Skin : new Color(0.9f, 0.7f, 0.58f);
            Decals.Paint(pos - normal * 0.0005f, normal, Random.Range(0.05f, 0.08f), new Color(skin.r * 0.95f, skin.g * 0.85f, skin.b * 0.8f, 1f), p.Col); // the cloth around the hole is gone
        }

        static void SkullFracture(BodyPart head, Vector3 pos, Vector3 normal)
        {
            var col = head.GetComponent<Collider>();
            for (int i = 0; i < 3; i++)
            {
                var d = Decals.Wound(pos + Random.insideUnitSphere * 0.015f, normal, 12, Random.Range(0.03f, 0.05f), col, Random.Range(0f, 360f));
                if (d != null) d.transform.localScale = Vector3.Scale(d.transform.localScale, new Vector3(1.8f, 0.25f, 1f)); // thin crack lines
            }
            Swell(head, pos, 0.05f, -0.012f); // a dent
            AudioManager.Play("bone_crack", pos, 0.8f, 0.8f);
        }

        static void LoseJaw(BodyPart head, Vector3 dir)
        {
            var t = head.transform;
            var at = t.TransformPoint(new Vector3(0, HumanModel.HeadCenterY - 0.07f, 0.05f));
            OrganModels.Chunk("Jaw", OrganModels.Jaw(), at, 0.3f, 0.03f, dir * 3f + Vector3.down, 90f);
            BloodFX.Burst(at, dir, 2f, Settings.BloodColor);
            Decals.Wound(at, t.forward, 14, 0.09f, head.GetComponent<Collider>());
            if (head.Doll.Face != null) head.Doll.Face.LoseJaw();
            if (head.Doll.Voice != null) head.Doll.Voice.Mute = true; // no jaw, no words
            for (int i = 0; i < 4; i++) OrganModels.Chunk("Tooth", OrganModels.Tooth(Random.Range(0, 4)), at, 0.01f, 0.006f, dir * 2f + Random.insideUnitSphere * 2f, 40f);
            AudioManager.Play("bone_crack", at, 1f, 0.7f);
        }

        static void Piece(BodyPart p, Vector3 pos, Vector3 dir, string name, Vector3 size, Color col)
        {
            var mesh = Cached("piece_" + name + col.GetHashCode(), mb => { mb.SetColor(col, 0.4f); mb.Sphere(Vector3.zero, size, 6, 8); mb.SetColor(SkinRaw, 0.9f); mb.Box(new Vector3(0, 0, -size.z * 0.6f), size * 0.8f); });
            OrganModels.Chunk(name, mesh, pos, 0.02f, Mathf.Max(size.x, size.y), dir * 2.5f + Random.insideUnitSphere, 60f);
            Decals.Wound(pos, (pos - p.transform.position).normalized, 4, 0.04f, p.GetComponent<Collider>());
            BloodFX.Burst(pos, dir, 0.8f, Settings.BloodColor);
        }

        static void Finger(BodyPart p, Vector3 pos, Vector3 dir, bool toe)
        {
            var f = p.GetComponent<Flags>() ?? p.gameObject.AddComponent<Flags>();
            if (f.Fingers >= (toe ? 3 : 4)) return;
            f.Fingers++;
            var skin = p.Doll.Look != null ? p.Doll.Look.Skin : new Color(0.9f, 0.7f, 0.58f);
            var mesh = Cached((toe ? "toe" : "finger") + skin.GetHashCode(), mb =>
            {
                mb.SetColor(skin, 0.4f); mb.Cylinder(Vector3.zero, Vector3.up * (toe ? 0.022f : 0.045f), 0.008f, 0.007f, 6);
                mb.SetColor(SkinRaw, 0.9f); mb.Cylinder(Vector3.down * 0.002f, Vector3.zero, 0.0075f, 0.008f, 6);
                mb.SetColor(Bone, 0.5f); mb.Cylinder(Vector3.down * 0.004f, Vector3.zero, 0.003f, 0.003f, 5);
            });
            OrganModels.Chunk(toe ? "Toe" : "Finger", mesh, pos, 0.01f, 0.01f, dir * 2f + Random.insideUnitSphere * 1.5f, 60f);
            BloodFX.Spurt(pos, -dir, 0.8f);
            Decals.Wound(pos, -dir, 4, 0.025f, p.GetComponent<Collider>());
        }

        /// <summary>Half the skull is gone: skull pieces, brain, one eye lost and a huge open wound on that side.</summary>
        public static void PartialHead(BodyPart head, Vector3 dir)
        {
            var t = head.transform;
            bool right = Vector3.Dot(dir, t.right) < 0f; // the side facing the shot
            var side = t.TransformPoint(new Vector3(right ? 0.05f : -0.05f, HumanModel.HeadCenterY + 0.02f, 0f));
            for (int i = 0; i < 4; i++) OrganModels.Chunk("Skull piece", OrganModels.SkullPiece(Random.Range(0, 6)), side, 0.05f, 0.025f, dir * 4f + Random.insideUnitSphere * 2f, 60f);
            if (Settings.Organs) OrganModels.Chunk("Brain matter", OrganModels.Brain(), side, 0.3f, 0.04f, dir * 3f + Random.insideUnitSphere, 90f);
            Tint(head, v => right ? v.x > 0.01f : v.x < -0.01f, new Color(0.5f, 0.05f, 0.06f), 0.9f);
            Swell(head, side, 0.09f, -0.03f);
            if (head.Doll.Face != null) head.Doll.Face.DestroyEye(!right);
            Decals.Wound(side, (side - t.TransformPoint(new Vector3(0, HumanModel.HeadCenterY, 0))).normalized, 14, 0.14f, head.GetComponent<Collider>());
            BloodFX.Burst(side, dir, 3f, Settings.BloodColor);
            GoreKit.WallSpray(side, dir, 120f);
            head.Doll.Vitals.BrainDestroyed = true;
            head.Doll.Vitals.Die("Catastrophic head wound");
            AudioManager.Play("headshot", side, 1f, 0.85f);
        }

        static void FacePeel(BodyPart head)
        {
            Tint(head, v => v.z > 0.02f && v.y < HumanModel.HeadCenterY + 0.06f, new Color(0.58f, 0.08f, 0.09f), 0.85f);
            Decals.Wound(head.transform.TransformPoint(new Vector3(0, HumanModel.HeadCenterY, 0.09f)), head.transform.forward, 14, 0.12f, head.GetComponent<Collider>());
        }

        static void Ribcage(BodyPart chest, Vector3 normal)
        {
            var t = chest.transform;
            var front = t.forward;
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f; float y = 0.08f + (i / 2) * 0.05f;
                Visual(t, "Exposed Rib", OrganModels.Rib(i % 4), new Vector3(side * 0.05f, y, 0.1f), Quaternion.Euler(0, side > 0 ? 20f : -20f + 180f, 0));
            }
            Decals.Wound(t.TransformPoint(new Vector3(0, 0.14f, 0.11f)), front, 14, 0.2f, chest.GetComponent<Collider>());
            Tint(chest, v => v.z > 0.05f && v.y > 0.05f && v.y < 0.25f, new Color(0.5f, 0.05f, 0.06f), 0.8f);
        }

        static void SpineExposed(BodyPart p, Vector3 pos)
        {
            var t = p.transform;
            for (int i = 0; i < 3; i++) Visual(t, "Exposed Vertebra", OrganModels.Vertebra(), new Vector3(0, 0.04f + i * 0.045f, -0.09f), Quaternion.identity);
            Decals.Wound(pos, -t.forward, 13, 0.12f, p.GetComponent<Collider>());
        }

        // ------------------------------------------------------------------ stumps (from Dismemberment.Sever)

        /// <summary>Torn (not cut) stumps: jagged edges, hanging skin flaps and, at the wrist or ankle, tendons sticking out.</summary>
        public static void StumpExtras(BodyPart on, Vector3 worldPos, Vector3 outward, float radius, bool jagged, bool tendons)
        {
            if (on == null || on.Gibbed) return;
            var t = on.transform;
            var skin = on.Doll != null && on.Doll.Look != null ? on.Doll.Look.Skin : new Color(0.9f, 0.7f, 0.58f);
            var rot = Quaternion.Inverse(t.rotation) * Quaternion.FromToRotation(Vector3.up, outward);
            var lp = t.InverseTransformPoint(worldPos);
            if (jagged)
            {
                var spikes = Cached("stump_jag", mb => { for (int k = 0; k < 9; k++) { float a = k / 9f * Mathf.PI * 2f; mb.SetColor(k % 2 == 0 ? SkinRaw : new Color(0.75f, 0.15f, 0.14f), 0.9f); mb.Box(new Vector3(Mathf.Cos(a) * 0.045f, 0.012f + (k % 3) * 0.006f, Mathf.Sin(a) * 0.045f), new Vector3(0.012f, 0.02f + (k % 3) * 0.012f, 0.012f)); } });
                var g = Visual(t, "Torn Edge", spikes, lp, rot); g.transform.localScale *= radius / 0.05f;
                for (int k = 0; k < 3; k++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    var flap = Cached("flap" + skin.GetHashCode(), mb => { mb.SetColor(skin, 0.35f); mb.Box(new Vector3(0, -0.02f, 0), new Vector3(0.025f, 0.04f, 0.003f)); mb.SetColor(SkinRaw, 0.9f); mb.Box(new Vector3(0, -0.02f, -0.0025f), new Vector3(0.024f, 0.039f, 0.002f)); });
                    var f = Visual(t, "Skin Flap", flap, lp + (Quaternion.Inverse(t.rotation) * (Quaternion.FromToRotation(Vector3.up, outward) * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)))) * radius, rot * Quaternion.Euler(Random.Range(150f, 200f), a * Mathf.Rad2Deg, 0));
                    f.transform.localScale *= radius / 0.05f;
                }
            }
            if (tendons)
            {
                var tend = Cached("tendons", mb => { mb.SetColor(Tendon, 0.8f); for (int k = 0; k < 4; k++) { float a = k / 4f * Mathf.PI * 2f + 0.4f; mb.Cylinder(new Vector3(Mathf.Cos(a) * 0.025f, 0, Mathf.Sin(a) * 0.025f), new Vector3(Mathf.Cos(a) * 0.03f, 0.03f + k * 0.008f, Mathf.Sin(a) * 0.03f), 0.003f, 0.0015f, 5); } });
                var g = Visual(t, "Tendons", tend, lp, rot); g.transform.localScale *= radius / 0.05f;
            }
        }

        /// <summary>When a limb is torn off, strands of muscle stretch between it and the stump, then snap.</summary>
        public static void Fibres(BodyPart stump, BodyPart piece, Vector3 at)
        {
            if (stump == null || piece == null) return;
            var go = new GameObject("Muscle Fibres");
            go.transform.SetParent(stump.transform, true);
            var f = go.AddComponent<FibreStrands>();
            f.Init(stump.transform, piece.transform, at);
        }

        sealed class FibreStrands : MonoBehaviour
        {
            Transform a, b; Vector3 la, lb; readonly List<LineRenderer> lines = new List<LineRenderer>(); float born;
            public void Init(Transform ta, Transform tb, Vector3 at)
            {
                a = ta; b = tb; born = Time.time; la = ta.InverseTransformPoint(at); lb = tb.InverseTransformPoint(at);
                for (int i = 0; i < 4; i++)
                {
                    var lr = new GameObject("Fibre").AddComponent<LineRenderer>(); lr.transform.SetParent(transform, false);
                    lr.positionCount = 2; lr.widthMultiplier = Random.Range(0.004f, 0.008f); lr.sharedMaterial = Mats.Lit;
                    lr.startColor = lr.endColor = new Color(0.6f, 0.08f, 0.09f); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lines.Add(lr);
                }
            }
            void Update()
            {
                if (a == null || b == null) { Destroy(gameObject); return; }
                var pa = a.TransformPoint(la); var pb = b.TransformPoint(lb);
                float d = Vector3.Distance(pa, pb);
                for (int i = 0; i < lines.Count; i++)
                {
                    var off = new Vector3(Mathf.Sin(i * 1.7f), Mathf.Cos(i * 2.3f), Mathf.Sin(i * 0.9f)) * 0.015f;
                    lines[i].SetPosition(0, pa + off); lines[i].SetPosition(1, pb + off * 0.6f);
                    lines[i].widthMultiplier = Mathf.Max(0.001f, 0.007f * (1f - d / 0.45f));
                }
                if (d > 0.45f || Time.time - born > 1.5f) { if (d > 0.2f) BloodFX.Drip((pa + pb) * 0.5f, Vector3.zero); Destroy(gameObject); } // snap
            }
        }

        // ------------------------------------------------------------------ other systems

        /// <summary>Burning bodies: blackened scraps of clothing come away and fall.</summary>
        public static void ClothScrap(BodyPart p)
        {
            if (p == null || !CoveredByShirt(p)) return;
            var col = p.Doll.Look != null ? (p.Type == PartType.Pelvis || p.Type.ToString().StartsWith("Thigh") || p.Type.ToString().StartsWith("Shin") ? p.Doll.Look.Pants : p.Doll.Look.Shirt) : Color.gray;
            var charred = Color.Lerp(col, new Color(0.06f, 0.05f, 0.04f), 0.65f);
            var mesh = Cached("scrap" + charred.GetHashCode(), mb => { mb.SetColor(charred, 0.2f); mb.Box(Vector3.zero, new Vector3(0.05f, 0.002f, 0.04f)); });
            var go = OrganModels.Chunk("Burnt cloth", mesh, p.transform.position + Random.insideUnitSphere * 0.06f, 0.005f, 0.02f, Vector3.up * 0.5f + Random.insideUnitSphere * 0.4f, 20f);
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) RB.SetDamping(rb, 4f, 2f); // flutters down
            Tint(p, v => Random.value < 0.08f, new Color(0.08f, 0.06f, 0.05f), 0.8f);
        }

        /// <summary>Blood soaks into the hair from a bleeding head.</summary>
        public static void BloodyHair(BodyPart head, float amount)
        {
            Tint(head, v => v.y > HumanModel.HeadCenterY + 0.04f || v.z < -0.06f, new Color(0.3f, 0.02f, 0.03f), Mathf.Clamp01(amount));
        }

        /// <summary>Piles of bodies: dead ragdolls get more solver iterations so stacked bodies settle instead of jittering.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            Goredoll.Died += (d, a) =>
            {
                if (d == null) return;
                foreach (var bp in d.Parts.Values) if (bp != null && bp.Body != null) { bp.Body.solverIterations = 14; bp.Body.solverVelocityIterations = 4; bp.Body.sleepThreshold = 0.08f; }
            };
        }

        /// <summary>Gore censor (Settings > Gore): gibs and body pieces render as black silhouettes.</summary>
        public static void Censor(GameObject chunk)
        {
            if (!Settings.Data.goreCensor || chunk == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in chunk.GetComponentsInChildren<Renderer>()) { r.GetPropertyBlock(mpb); mpb.SetColor("_Color", new Color(0.02f, 0.02f, 0.02f)); r.SetPropertyBlock(mpb); }
        }

        // ------------------------------------------------------------------ vertical split (head to toe)

        /// <summary>A huge downward blade blow splits the whole body down the middle: every part is cut along one plane.</summary>
        public static void VerticalSplitVisual(Goredoll doll, Vector3 worldPoint, Vector3 worldNormal)
        {
            foreach (var p in new List<BodyPart>(doll.Parts.Values))
            {
                if (p == null || p.Gibbed || p.Filter == null || p.Filter.sharedMesh == null || !p.Filter.sharedMesh.isReadable) continue;
                var t = p.transform;
                var plane = new Plane(t.InverseTransformDirection(worldNormal).normalized, t.InverseTransformPoint(worldPoint));
                if (!MeshSlicer.Slice(p.Filter.sharedMesh, plane, out var keep, out var drop)) continue;
                p.Filter.sharedMesh = keep;
                p.Renderer.sharedMaterials = new[] { Mats.Lit, Mats.Flesh };
                var half = new GameObject("Half " + p.Spec.Name);
                half.layer = Layers.Debris;
                half.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, true);
                half.transform.SetPositionAndRotation(t.position, t.rotation);
                half.transform.localScale = t.lossyScale;
                half.AddComponent<MeshFilter>().sharedMesh = drop;
                half.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mats.Lit, Mats.Flesh };
                var bc = half.AddComponent<BoxCollider>(); bc.center = drop.bounds.center; bc.size = drop.bounds.size;
                var rb = half.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(0.3f, p.Body != null ? p.Body.mass * 0.5f : 1f);
                if (p.Body != null) { p.Body.mass = Mathf.Max(0.3f, p.Body.mass * 0.5f); RB.SetVel(rb, RB.Vel(p.Body) - worldNormal * 1.5f); }
                foreach (var other in doll.Parts.Values) if (other != null && other.Col != null) Physics.IgnoreCollision(bc, other.Col, true);
                half.AddComponent<Gib>(); half.AddComponent<Spawned>().DisplayName = "Half a " + p.Spec.Medical;
                Debris.Register(half, 120f, Debris.GroupGib);
                Censor(half);
            }
            BloodFX.Burst(worldPoint, Vector3.up, 4f, Settings.BloodColor);
            BloodFX.Burst(worldPoint + Vector3.down * 0.5f, -worldNormal, 3f, Settings.BloodColor);
            GoreKit.WallSpray(worldPoint, worldNormal, 150f);
            AudioManager.Play("blade_chop", worldPoint, 1f, 0.7f);
            AudioManager.Play("dismember", worldPoint, 1f);
            doll.Vitals.BrainDestroyed = true;
            doll.Vitals.Die("Split in two");
            doll.Ragdoll.Fall(10f);
        }
    }
}
