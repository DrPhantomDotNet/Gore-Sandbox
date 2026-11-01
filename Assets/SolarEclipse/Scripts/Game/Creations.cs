using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Content Update quality-of-life tools:
    ///   • Share codes for maps and sandbox creations (gzip + base64 text, safe to paste on Discord).
    ///   • Sandbox saves: everything you spawned on a map (props, machines, Goredolls, animals, vehicles) saved to a slot.
    ///   • Prop Editor: make your own physics props (shape, size, colour, material, weight, behaviour). A custom prop's whole
    ///     definition lives in its id ("cprop:..."), so multiplayer clients and share codes rebuild it with no extra data.
    /// </summary>
    public static class ShareCode
    {
        public const string MapPrefix = "GSMAP1:", BoxPrefix = "GSBOX1:";

        public static string Encode(string prefix, string json)
            => prefix + Convert.ToBase64String(CustomMaps.Compress(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string Decode(string code, string prefix, out string error, int maxBytes = 4 * 1024 * 1024)
        {
            error = null;
            code = (code ?? "").Trim();
            if (!code.StartsWith(prefix, StringComparison.Ordinal)) { error = "That isn't a code of this kind (it should start with " + prefix + ")."; return null; }
            var sb = new StringBuilder(code.Length);
            for (int i = prefix.Length; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '-') sb.Append('+'); else if (c == '_') sb.Append('/');
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/') sb.Append(c);
            }
            while (sb.Length % 4 != 0) sb.Append('=');
            try { return CustomMaps.Decompress(Convert.FromBase64String(sb.ToString()), maxBytes); }
            catch (Exception e) { error = "The code is broken or incomplete (" + e.Message + ")."; return null; }
        }

        public static void Copy(string code, string what)
        {
            try { GUIUtility.systemCopyBuffer = code; GameEvents.ShowToast(what + " code copied (" + (code.Length / 1024f).ToString("0.0") + " KB). Paste it anywhere to share."); AudioManager.Ui("ui_success"); }
            catch (Exception e) { GameEvents.ShowToast("Couldn't copy: " + e.Message); }
        }

        // ------------------------------------------------------------------ maps

        public static string MapCode(CustomMapData d) => Encode(MapPrefix, d.ToJson());

        /// <summary>Imports a map code as a new saved map. Returns null on success or an error.</summary>
        public static string ImportMap(string code)
        {
            var json = Decode(code, MapPrefix, out var err);
            if (json == null) return err;
            var d = CustomMapData.FromJson(json, out err);
            if (d == null) return err ?? "That map code doesn't contain a map.";
            d.Id = CustomMapData.NewId();
            d.Name = CustomMaps.UniqueName(CustomMapData.CleanText(d.Name, 40).Length > 0 ? CustomMapData.CleanText(d.Name, 40) : "Shared Map");
            return CustomMaps.Save(d);
        }
    }

    // ====================================================================== sandbox saves

    [Serializable] public sealed class SandboxObj { public string id; public float px, py, pz, rx, ry, rz, rw = 1f, s = 1f; public bool frozen; }
    [Serializable] public sealed class SandboxSave { public string name = "", map = "", mapName = ""; public long time; public List<SandboxObj> objs = new List<SandboxObj>(); }

    public static class SandboxSaves
    {
        public const int MaxObjects = 400;
        public sealed class Entry { public string Path; public SandboxSave Data; }
        public static readonly List<Entry> Local = new List<Entry>();

        public static string Dir
        {
            get { var d = System.IO.Path.Combine(Application.persistentDataPath, "creations"); try { Directory.CreateDirectory(d); } catch { } return d; }
        }

        public static void Refresh()
        {
            Local.Clear();
            try
            {
                foreach (var f in Directory.GetFiles(Dir, "*.json"))
                {
                    try { var s = JsonUtility.FromJson<SandboxSave>(File.ReadAllText(f)); if (s != null && s.objs != null) Local.Add(new Entry { Path = f, Data = s }); }
                    catch { }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Creations] " + e.Message); }
            Local.Sort((a, b) => b.Data.time.CompareTo(a.Data.time));
        }

        /// <summary>Everything the player spawned on the current map.</summary>
        public static SandboxSave Capture(string name)
        {
            var map = GameManager.I != null ? GameManager.I.CurrentMap : null;
            var save = new SandboxSave { name = name, map = map != null ? map.Id : "", mapName = map != null ? map.Name : "", time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            foreach (var sp in UnityEngine.Object.FindObjectsByType<Spawned>(FindObjectsSortMode.None))
            {
                if (sp == null || !sp.ByPlayer || string.IsNullOrEmpty(sp.DefId) || sp.DefId.StartsWith("car|")) continue;
                if (save.objs.Count >= MaxObjects) break;
                Vector3 pos = sp.transform.position; Quaternion rot = sp.transform.rotation;
                var doll = sp.GetComponent<Goredoll>();
                if (doll != null)
                {
                    if (doll.Vitals != null && doll.Vitals.State == LifeState.Dead) continue;
                    pos = doll.Position;
                    pos = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : pos - Vector3.up * 0.9f;
                    rot = Quaternion.Euler(0, doll.transform.eulerAngles.y, 0);
                }
                var rb = sp.GetComponent<Rigidbody>();
                save.objs.Add(new SandboxObj
                {
                    id = sp.DefId, px = pos.x, py = pos.y, pz = pos.z, rx = rot.x, ry = rot.y, rz = rot.z, rw = rot.w,
                    s = doll == null ? sp.transform.localScale.x : 1f, frozen = doll == null && rb != null && rb.isKinematic,
                });
            }
            return save;
        }

        public static string Write(SandboxSave s, string path = null)
        {
            try
            {
                path = path ?? System.IO.Path.Combine(Dir, "save_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(s));
                Refresh();
                return null;
            }
            catch (Exception e) { return e.Message; }
        }

        public static void Delete(Entry e) { try { File.Delete(e.Path); } catch { } Refresh(); }

        /// <summary>Spawns a save into the current map. Returns how many objects were placed.</summary>
        public static int Load(SandboxSave s, bool clearFirst)
        {
            if (s == null || s.objs == null) return 0;
            if (NetState.IsClient && !NetState.LocalCreative) { GameEvents.ShowToast("Loading a creation needs Creative mode - ask the host."); return 0; }
            if (clearFirst && !NetState.IsClient) WorldRoot.Current?.ClearDynamic();
            int n = 0;
            foreach (var o in s.objs)
            {
                if (n >= MaxObjects || o == null || string.IsNullOrEmpty(o.id)) continue;
                var def = SpawnCatalog.Find(o.id);
                if (def == null) continue;
                var rot = new Quaternion(o.rx, o.ry, o.rz, o.rw);
                if (Mathf.Abs(rot.x * rot.x + rot.y * rot.y + rot.z * rot.z + rot.w * rot.w - 1f) > 0.1f) rot = Quaternion.identity;
                var pos = new Vector3(o.px, o.py, o.pz);
                if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)) continue;
                var go = SpawnCatalog.Spawn(def, pos, rot);
                n++;
                if (go == null) continue;
                float sc = Mathf.Clamp(o.s, 0.2f, 5f);
                if (Mathf.Abs(sc - 1f) > 0.01f && def.Category != "AI") go.transform.localScale = Vector3.one * sc;
                if (o.frozen) { foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true; if (NetState.IsHost) NetManager.I.SetFrozen(go.GetComponent<NetEntity>(), true); }
            }
            return n;
        }

        public static string Code(SandboxSave s) => ShareCode.Encode(ShareCode.BoxPrefix, JsonUtility.ToJson(s));

        public static SandboxSave FromCode(string code, out string error)
        {
            var json = ShareCode.Decode(code, ShareCode.BoxPrefix, out error, 2 * 1024 * 1024);
            if (json == null) return null;
            try
            {
                var s = JsonUtility.FromJson<SandboxSave>(json);
                if (s == null || s.objs == null) { error = "That code has no creation in it."; return null; }
                s.name = CustomMapData.CleanText(s.name, 40); if (s.name.Length == 0) s.name = "Shared creation";
                if (s.objs.Count > MaxObjects) s.objs.RemoveRange(MaxObjects, s.objs.Count - MaxObjects);
                s.time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return s;
            }
            catch (Exception e) { error = e.Message; return null; }
        }
    }

    // ====================================================================== custom props

    public sealed class CustomPropData
    {
        public static readonly string[] Shapes = { "Box", "Sphere", "Cylinder", "Capsule" };
        public static readonly string[] Materials = { "Wood", "Metal", "Concrete", "Glass", "Rubber", "Flesh", "Plastic" };
        public static readonly string[] Behaviours = { "Normal", "Explosive", "Super bouncy", "Floaty (no gravity)", "Slippery ice", "Spinner", "Breakable", "Always on fire" };

        public string Name = "My Prop";
        public int Shape, Material, Behaviour;
        public Vector3 Size = new Vector3(0.6f, 0.6f, 0.6f);
        public Color Color = new Color(0.8f, 0.3f, 0.2f);
        public float Mass = 20f;

        static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        static float P(string s, float def) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && !float.IsNaN(v) && !float.IsInfinity(v) ? v : def;

        public void Clamp()
        {
            Name = CustomMapData.CleanText(Name, 24).Replace("|", ""); if (Name.Length == 0) Name = "Custom Prop";
            Shape = Mathf.Clamp(Shape, 0, Shapes.Length - 1); Material = Mathf.Clamp(Material, 0, Materials.Length - 1); Behaviour = Mathf.Clamp(Behaviour, 0, Behaviours.Length - 1);
            Size = new Vector3(Mathf.Clamp(Size.x, 0.1f, 6f), Mathf.Clamp(Size.y, 0.1f, 6f), Mathf.Clamp(Size.z, 0.1f, 6f));
            Mass = Mathf.Clamp(Mass, 0.5f, 2000f);
            Color = new Color(Mathf.Clamp01(Color.r), Mathf.Clamp01(Color.g), Mathf.Clamp01(Color.b));
        }

        public string Id
        {
            get
            {
                Clamp();
                var raw = string.Join("|", Name, Shape, F(Size.x), F(Size.y), F(Size.z), ColorUtility.ToHtmlStringRGB(Color), Material, F(Mass), Behaviour);
                return CustomProps.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        public static CustomPropData FromId(string id)
        {
            if (id == null || !id.StartsWith(CustomProps.Prefix, StringComparison.Ordinal) || id.Length > 400) return null;
            try
            {
                var b = id.Substring(CustomProps.Prefix.Length).Replace('-', '+').Replace('_', '/');
                while (b.Length % 4 != 0) b += "=";
                var f = Encoding.UTF8.GetString(Convert.FromBase64String(b)).Split('|');
                if (f.Length < 9) return null;
                var d = new CustomPropData { Name = f[0] };
                int.TryParse(f[1], out d.Shape);
                d.Size = new Vector3(P(f[2], 0.6f), P(f[3], 0.6f), P(f[4], 0.6f));
                if (ColorUtility.TryParseHtmlString("#" + f[5], out var c)) d.Color = c;
                int.TryParse(f[6], out d.Material);
                d.Mass = P(f[7], 20f);
                int.TryParse(f[8], out d.Behaviour);
                d.Clamp();
                return d;
            }
            catch { return null; }
        }

        public CustomPropData Clone() => (CustomPropData)MemberwiseClone();
    }

    public static class CustomProps
    {
        public const string Prefix = "cprop:";
        const string Key = "custom_props_v1";
        public static readonly List<CustomPropData> Saved = new List<CustomPropData>();
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { try { EnsureLoaded(); } catch (Exception e) { Debug.LogWarning("[Props] " + e.Message); } }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Saved.Clear();
            foreach (var line in PlayerPrefs.GetString(Key, "").Split('\n'))
            {
                var d = CustomPropData.FromId(line.Trim());
                if (d != null && Saved.Count < 60) Saved.Add(d);
            }
            Register();
        }

        public static void Store()
        {
            var ids = new List<string>();
            foreach (var d in Saved) ids.Add(d.Id);
            PlayerPrefs.SetString(Key, string.Join("\n", ids.ToArray()));
            PlayerPrefs.Save();
            Register();
        }

        /// <summary>Puts every saved custom prop into the spawn menu's Props tab.</summary>
        static void Register()
        {
            PropFactory.Props.RemoveAll(p => p.Id != null && p.Id.StartsWith(Prefix, StringComparison.Ordinal));
            foreach (var d in Saved) PropFactory.Props.Add(DefFor(d));
        }

        static SpawnableDef DefFor(CustomPropData d)
        {
            string id = d.Id;
            return new SpawnableDef { Id = id, Name = d.Name, Category = "Props", Glyph = "wrench", Description = "Custom prop (Prop Editor): " + CustomPropData.Shapes[d.Shape] + ", " + CustomPropData.Materials[d.Material] + ", " + d.Mass.ToString("0") + " kg, " + CustomPropData.Behaviours[d.Behaviour] + ".", Spawn = (p, r) => Build(d, p, r) };
        }

        /// <summary>Spawn-request fallback for ids that aren't in the local list (another player's prop).</summary>
        public static SpawnableDef DefForId(string id) { var d = CustomPropData.FromId(id); return d != null ? DefFor(d) : null; }

        public static GameObject Build(string id, Vector3 pos, Quaternion rot) { var d = CustomPropData.FromId(id); return d != null ? Build(d, pos, rot) : null; }

        public static GameObject Build(CustomPropData d, Vector3 pos, Quaternion rot)
        {
            d.Clamp();
            var go = new GameObject(d.Name);
            go.layer = Layers.Props;
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos + Vector3.up * d.Size.y * 0.5f, rot);
            SurfaceType surf; Texture2D tex = null; float gloss = 0.15f;
            switch (d.Material)
            {
                case 0: surf = SurfaceType.Wood; tex = TextureFactory.Wood; break;
                case 1: surf = SurfaceType.Metal; tex = TextureFactory.Metal; gloss = 0.6f; break;
                case 2: surf = SurfaceType.Concrete; tex = TextureFactory.Concrete; break;
                case 3: surf = SurfaceType.Glass; gloss = 0.9f; break;
                case 5: surf = SurfaceType.Flesh; gloss = 0.4f; break;
                default: surf = SurfaceType.Carpet; gloss = 0.35f; break; // rubber, plastic
            }
            var mb = new MeshBuilder { Color = d.Color, Gloss = gloss };
            var s = d.Size;
            switch (d.Shape)
            {
                case 1: mb.Sphere(Vector3.zero, s * 0.5f, 10, 14); break;
                case 2: mb.Push(Vector3.zero, Quaternion.identity, new Vector3(s.x, s.y, s.z)); mb.Cylinder(new Vector3(0, -0.5f, 0), new Vector3(0, 0.5f, 0), 0.5f, 0.5f, 14); mb.Pop(); break;
                case 3:
                {
                    float r = Mathf.Min(s.x, s.z) * 0.5f, half = Mathf.Max(0f, s.y * 0.5f - r);
                    mb.Push(Vector3.zero, Quaternion.identity, new Vector3(s.x / (2f * r), 1f, s.z / (2f * r)));
                    mb.Capsule(new Vector3(0, -half, 0), new Vector3(0, half, 0), r, r, 12, 3);
                    mb.Pop(); break;
                }
                default: if (Mathf.Min(s.x, Mathf.Min(s.y, s.z)) > 0.08f) mb.BevelBox(Vector3.zero, s, Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.06f, 1f); else mb.Box(Vector3.zero, s, 1f); break;
            }
            var mesh = mb.ToMesh(d.Name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = tex != null ? Mats.LitTextured(tex) : Mats.Lit;
            Collider col;
            if (d.Shape == 0) col = go.AddComponent<BoxCollider>();
            else if (d.Shape == 1 && Mathf.Approximately(s.x, s.y) && Mathf.Approximately(s.y, s.z)) { var sc = go.AddComponent<SphereCollider>(); sc.radius = s.x * 0.5f; col = sc; }
            else { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; col = mc; }
            if (d.Material == 4) PhysMat.Apply(col, "Rubber", 0.6f, 0.9f);
            if (d.Behaviour == 2) PhysMat.Apply(col, "Bouncy", 0.97f, 0.3f);
            if (d.Behaviour == 4) PhysMat.Apply(col, "Ice", 0f, 0f, false);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = d.Mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            RB.SetDamping(rb, d.Behaviour == 3 ? 0.8f : 0.05f, d.Behaviour == 3 ? 0.5f : 0.08f);
            if (d.Behaviour == 3) rb.useGravity = false;
            go.AddComponent<Surface>().Type = surf;
            go.AddComponent<ImpactSounds>();
            switch (d.Behaviour)
            {
                case 1: go.AddComponent<ExplosiveBarrel>(); break;
                case 5: go.AddComponent<PropSpinner>(); break;
                case 6: { var br = go.AddComponent<BreakableProp>(); br.Health = 10f + d.Mass * 1.2f; br.Material = surf; br.Fragments = Mathf.Clamp(Mathf.RoundToInt(s.x * s.y * s.z * 12f), 3, 12); br.FragmentColor = d.Color; break; }
                case 7: go.AddComponent<PropFlames>(); break;
            }
            return go;
        }
    }

    /// <summary>Custom prop behaviour: spins about its up axis (host / singleplayer physics only).</summary>
    public sealed class PropSpinner : MonoBehaviour
    {
        Rigidbody rb;
        void Awake() { rb = GetComponent<Rigidbody>(); if (rb != null) rb.maxAngularVelocity = 40f; }
        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic || NetState.IsClient) return;
            var w = Vector3.Dot(rb.angularVelocity, transform.up);
            if (w < 18f) rb.AddTorque(transform.up * rb.mass * 6f, ForceMode.Force);
        }
    }

    /// <summary>Custom prop behaviour: always burning; sets Goredolls that touch it on fire.</summary>
    public sealed class PropFlames : MonoBehaviour
    {
        float next;
        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.06f;
            var b = GetComponent<Renderer>() != null ? GetComponent<Renderer>().bounds : new Bounds(transform.position, Vector3.one);
            FX.Fire(b.center + Vector3.up * b.extents.y * 0.8f + new Vector3(UnityEngine.Random.Range(-b.extents.x, b.extents.x), 0, UnityEngine.Random.Range(-b.extents.z, b.extents.z)) * 0.7f, Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 0.5f, 0.3f);
        }
        void OnCollisionEnter(Collision c)
        {
            if (NetState.IsClient) return;
            var doll = c.collider.GetComponentInParent<Goredoll>();
            if (doll != null) Burning.Apply(doll, 8f, this);
        }
    }

    // ====================================================================== screens

    /// <summary>Paste a share code (PASTE reads the clipboard), then IMPORT.</summary>
    public sealed class CodeImportDialog : UIScreen
    {
        readonly string title, hint;
        readonly Func<string, string> onImport; // returns null on success or an error
        string code = "", error;

        public CodeImportDialog(string title, string hint, Func<string, string> onImport) { this.title = title; this.hint = hint; this.onImport = onImport; }
        public override bool IsModal => true;
        public override void OnClose() { GUIUtility.keyboardControl = 0; }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(900, 420);
            UI.Panel(r, title, a);
            UI.Label(new Rect(r.x + 30, r.y + 76, r.width - 60, 60), hint, 19, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            var f = new Rect(r.x + 30, r.y + 146, r.width - 220, 58);
            if (code.Length < 1500) code = UI.TextField(f, code, "code_field", false, "Paste the code here");
            else { UI.Rounded(f, new Color(0.1f, 0.1f, 0.13f, 0.95f)); UI.Label(new Rect(f.x + 16, f.y, f.width - 32, f.height), "Code pasted (" + (code.Length / 1024f).ToString("0.0") + " KB)", 20, UI.TextColor); }
            if (UI.Button(new Rect(r.xMax - 180, r.y + 146, 150, 58), "PASTE", ButtonKind.Normal, IconFactory.Get("copy"), 20))
            {
                try { code = (GUIUtility.systemCopyBuffer ?? "").Trim(); } catch { code = ""; }
                error = code.Length == 0 ? "The clipboard is empty." : null;
            }
            if (error != null) UI.Label(new Rect(r.x + 30, r.y + 218, r.width - 60, 80), error, 19, new Color(1f, 0.45f, 0.4f), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 240, 60), "CANCEL")) UIRoot.Pop();
            if (UI.Button(new Rect(r.xMax - 270, r.yMax - 86, 240, 60), "IMPORT", ButtonKind.Primary, IconFactory.Get("plus"), 24, code.Trim().Length > 0))
            {
                var err = onImport(code.Trim());
                if (err == null) { AudioManager.Ui("ui_success"); UIRoot.Pop(); }
                else { error = err; AudioManager.Ui("ui_error"); }
            }
        }
    }

    /// <summary>Pause menu > CREATIONS: save / load / share what you built in the sandbox, and the Prop Editor.</summary>
    public sealed class CreationsScreen : UIScreen
    {
        int selected;
        public override bool IsModal => true;
        public override bool BlocksGameplay => true;
        public override void OnOpen() { AudioManager.Ui("ui_open"); SandboxSaves.Refresh(); CustomProps.EnsureLoaded(); }
        public override bool OnBack() { UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.6f * a);
            float W = UI.VW, H = UI.VH;
            var r = new Rect(60, 60, W - 120, H - 120);
            UI.Panel(r, "Creations", a);
            UI.Label(new Rect(r.x + 30, r.y + 70, r.width - 60, 30), "Save everything you spawned on this map, load it again later, or share it with a code.", 19, UI.Muted);

            bool inGame = GameManager.I != null && GameManager.I.State == GameState.Playing;
            float bx = r.x + 30, by = r.y + 116;
            if (UI.Button(new Rect(bx, by, 330, 62), "SAVE THIS MAP'S BUILD", ButtonKind.Primary, IconFactory.Get("plus"), 20, inGame))
            {
                var name = (GameManager.I.CurrentMap != null ? GameManager.I.CurrentMap.Name : "Sandbox") + " build";
                UIRoot.Push(new TextPromptDialog("Save creation", "Name it", name, "SAVE", n =>
                {
                    var s = SandboxSaves.Capture(n);
                    if (s.objs.Count == 0) { GameEvents.ShowToast("Nothing to save - spawn some things first."); return; }
                    var err = SandboxSaves.Write(s);
                    GameEvents.ShowToast(err == null ? "Saved " + s.objs.Count + " objects as \"" + n + "\"." : "Save failed: " + err);
                    selected = 0;
                }));
            }
            if (UI.Button(new Rect(bx + 344, by, 270, 62), "IMPORT CODE", ButtonKind.Normal, IconFactory.Get("copy"), 20))
                UIRoot.Push(new CodeImportDialog("Import creation", "Paste a creation code (starts with " + ShareCode.BoxPrefix + ").", c =>
                {
                    var s = SandboxSaves.FromCode(c, out var err);
                    if (s == null) return err;
                    err = SandboxSaves.Write(s);
                    if (err == null) { selected = 0; GameEvents.ShowToast("Imported \"" + s.name + "\" (" + s.objs.Count + " objects)."); }
                    return err;
                }));
            if (UI.Button(new Rect(r.xMax - 330, by, 300, 62), "PROP EDITOR", ButtonKind.Normal, IconFactory.Get("wrench"), 20)) UIRoot.Push(new PropEditorScreen());

            var saves = SandboxSaves.Local;
            if (selected >= saves.Count) selected = saves.Count - 1;
            var view = new Rect(r.x + 30, by + 80, r.width * 0.5f - 40, r.yMax - by - 180);
            float itemH = 84, content = Mathf.Max(1, saves.Count) * (itemH + 8);
            float sc = UI.BeginScroll("creations", view, content);
            if (saves.Count == 0) UI.Label(new Rect(10, 10, view.width - 30, 120), "No saved creations yet. Build something with the Spawn Gun, then press SAVE THIS MAP'S BUILD.", 21, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            for (int i = 0; i < saves.Count; i++)
            {
                var e = saves[i];
                var rr = new Rect(0, i * (itemH + 8) - sc, view.width - 14, itemH);
                bool sel = i == selected;
                UI.Rounded(rr, sel ? new Color(0.5f, 0.06f, 0.08f, 0.95f) : new Color(0.1f, 0.1f, 0.13f, UI.Hover(rr) ? 0.98f : 0.9f));
                if (sel) UI.Outline(rr, UI.Accent, 2);
                UI.Label(new Rect(rr.x + 18, rr.y + 8, rr.width - 36, 36), e.Data.name, 24, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(rr.x + 18, rr.y + 44, rr.width - 36, 30), e.Data.objs.Count + " objects  -  " + (e.Data.mapName.Length > 0 ? e.Data.mapName : "?") + "  -  " + DateTimeOffset.FromUnixTimeSeconds(e.Data.time).ToLocalTime().ToString("yyyy-MM-dd HH:mm"), 17, UI.Muted);
                if (UI.Hit(rr) && i != selected) { selected = i; AudioManager.Ui("ui_tab"); }
            }
            UI.EndScroll("creations", view, content);

            var d = new Rect(r.x + r.width * 0.5f + 10, by + 80, r.width * 0.5f - 40, r.yMax - by - 180);
            UI.Rounded(d, new Color(0.08f, 0.08f, 0.1f, 0.85f));
            if (selected >= 0 && selected < saves.Count)
            {
                var e = saves[selected];
                UI.Label(new Rect(d.x + 24, d.y + 20, d.width - 48, 50), e.Data.name.ToUpperInvariant(), 36, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                var counts = new Dictionary<string, int>();
                foreach (var o in e.Data.objs) { var def = SpawnCatalog.Find(o.id); var n = def != null ? def.Name : o.id; counts[n] = counts.TryGetValue(n, out var c) ? c + 1 : 1; }
                var lines = new List<string>();
                foreach (var kv in counts) { if (lines.Count >= 9) { lines.Add("..."); break; } lines.Add(kv.Value + " x " + kv.Key); }
                UI.Label(new Rect(d.x + 24, d.y + 80, d.width - 48, 300), "Made on: " + e.Data.mapName + "\n\n" + string.Join("\n", lines.ToArray()), 19, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                bool here = inGame && GameManager.I.CurrentMap != null && GameManager.I.CurrentMap.Id == e.Data.map;
                if (inGame && !here) UI.Label(new Rect(d.x + 24, d.yMax - 200, d.width - 48, 50), "This was saved on another map - it will load at the same coordinates.", 16, UI.Accent2, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                float w4 = (d.width - 48 - 30) / 2f, y = d.yMax - 148;
                if (UI.Button(new Rect(d.x + 24, y, w4, 58), "LOAD (ADD)", ButtonKind.Primary, IconFactory.Get("play"), 20, inGame)) { int n = SandboxSaves.Load(e.Data, false); GameEvents.ShowToast("Loaded " + n + " objects."); UIRoot.Pop(); return; }
                if (UI.Button(new Rect(d.x + 24 + w4 + 30, y, w4, 58), "CLEAR + LOAD", ButtonKind.Normal, IconFactory.Get("refresh"), 20, inGame && !NetState.IsClient)) { int n = SandboxSaves.Load(e.Data, true); GameEvents.ShowToast("Loaded " + n + " objects."); UIRoot.Pop(); return; }
                y += 68;
                if (UI.Button(new Rect(d.x + 24, y, w4, 54), "COPY SHARE CODE", ButtonKind.Ghost, IconFactory.Get("copy"), 18)) ShareCode.Copy(SandboxSaves.Code(e.Data), "Creation");
                if (UI.Button(new Rect(d.x + 24 + w4 + 30, y, w4, 54), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 18))
                    UIRoot.Push(new ConfirmDialog("Delete creation?", "\"" + e.Data.name + "\" will be gone for good.", () => SandboxSaves.Delete(e), "DELETE", ButtonKind.Danger));
            }
            else UI.Label(new Rect(d.x + 24, d.y + 24, d.width - 48, 80), "Pick a saved creation.", 22, UI.Muted);

            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 240, 60), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) UIRoot.Pop();
        }
    }

    /// <summary>Make physics props: shape, size, colour, material, weight and a behaviour. They appear in the spawn menu (Props).</summary>
    public sealed class PropEditorScreen : UIScreen
    {
        CustomPropData cur = new CustomPropData();
        int editing = -1;
        static readonly Color[] Swatches = { new Color(0.85f, 0.15f, 0.12f), new Color(0.95f, 0.55f, 0.1f), new Color(0.95f, 0.85f, 0.15f), new Color(0.2f, 0.7f, 0.25f), new Color(0.15f, 0.45f, 0.9f), new Color(0.55f, 0.25f, 0.8f), new Color(0.95f, 0.45f, 0.7f), new Color(0.95f, 0.95f, 0.95f), new Color(0.45f, 0.45f, 0.47f), new Color(0.08f, 0.08f, 0.09f), new Color(0.55f, 0.38f, 0.22f), new Color(0.6f, 0.08f, 0.06f) };

        public override bool IsModal => true;
        public override bool BlocksGameplay => true;
        public override void OnOpen() { CustomProps.EnsureLoaded(); AudioManager.Ui("ui_open"); }
        public override bool OnBack() { UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.65f * a);
            float W = UI.VW, H = UI.VH;
            var r = new Rect(60, 60, W - 120, H - 120);
            UI.Panel(r, "Prop editor", a);

            // left: your props
            var list = new Rect(r.x + 30, r.y + 80, 420, r.height - 190);
            if (UI.Button(new Rect(list.x, list.y, list.width, 58), "NEW PROP", ButtonKind.Primary, IconFactory.Get("plus"), 22)) { cur = new CustomPropData(); editing = -1; }
            if (UI.Button(new Rect(list.x, list.y + 66, list.width, 50), "IMPORT CODE", ButtonKind.Ghost, IconFactory.Get("copy"), 18))
                UIRoot.Push(new CodeImportDialog("Import prop", "Paste a prop code (starts with " + CustomProps.Prefix + ").", c =>
                {
                    var d = CustomPropData.FromId(c.Trim());
                    if (d == null) return "That isn't a prop code.";
                    if (CustomProps.Saved.Count >= 60) return "You have 60 props already - delete some first.";
                    CustomProps.Saved.Add(d); CustomProps.Store(); cur = d.Clone(); editing = CustomProps.Saved.Count - 1;
                    return null;
                }));
            var view = new Rect(list.x, list.y + 126, list.width, list.height - 126);
            float itemH = 60, content = Mathf.Max(1, CustomProps.Saved.Count) * (itemH + 6);
            float s = UI.BeginScroll("props_ed", view, content);
            if (CustomProps.Saved.Count == 0) UI.Label(new Rect(10, 10, view.width - 30, 90), "No props yet. Design one on the right and press SAVE.", 19, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            for (int i = 0; i < CustomProps.Saved.Count; i++)
            {
                var p = CustomProps.Saved[i];
                var rr = new Rect(0, i * (itemH + 6) - s, view.width - 14, itemH);
                UI.Rounded(rr, i == editing ? new Color(0.5f, 0.06f, 0.08f, 0.95f) : new Color(0.1f, 0.1f, 0.13f, UI.Hover(rr) ? 0.98f : 0.9f));
                UI.Rounded(new Rect(rr.x + 10, rr.y + 10, 40, 40), p.Color);
                UI.Label(new Rect(rr.x + 62, rr.y, rr.width - 70, itemH), p.Name + "  (" + CustomPropData.Shapes[p.Shape] + ")", 20, UI.TextColor);
                if (UI.Hit(rr)) { editing = i; cur = p.Clone(); AudioManager.Ui("ui_tab"); }
            }
            UI.EndScroll("props_ed", view, content);

            // right: the editor
            float x = r.x + 490, w = r.width - 520, y = r.y + 80;
            UI.Label(new Rect(x, y, 160, 54), "Name", 20, UI.Muted);
            cur.Name = UI.TextField(new Rect(x + 160, y, w - 160, 54), cur.Name, "prop_name");
            if (cur.Name.Length > 24) cur.Name = cur.Name.Substring(0, 24);
            y += 64;
            float half = (w - 20) / 2f;
            UI.Selector(new Rect(x, y, half, 52), "Shape", ref cur.Shape, CustomPropData.Shapes);
            UI.Selector(new Rect(x + half + 20, y, half, 52), "Material", ref cur.Material, CustomPropData.Materials); y += 60;
            UI.Selector(new Rect(x, y, w, 52), "Behaviour", ref cur.Behaviour, CustomPropData.Behaviours); y += 60;
            float sx = cur.Size.x, sy = cur.Size.y, sz = cur.Size.z;
            float third = (w - 40) / 3f;
            UI.Slider(new Rect(x, y, third, 52), "Width", ref sx, 0.1f, 6f, "0.00", null, " m");
            UI.Slider(new Rect(x + third + 20, y, third, 52), "Height", ref sy, 0.1f, 6f, "0.00", null, " m");
            UI.Slider(new Rect(x + (third + 20) * 2, y, third, 52), "Depth", ref sz, 0.1f, 6f, "0.00", null, " m");
            cur.Size = new Vector3(sx, sy, sz); y += 60;
            UI.Slider(new Rect(x, y, w, 52), "Weight", ref cur.Mass, 0.5f, 2000f, "0", null, " kg"); y += 60;
            float cr = cur.Color.r, cg = cur.Color.g, cb = cur.Color.b;
            UI.Slider(new Rect(x, y, third, 52), "Red", ref cr, 0f, 1f, "0.00");
            UI.Slider(new Rect(x + third + 20, y, third, 52), "Green", ref cg, 0f, 1f, "0.00");
            UI.Slider(new Rect(x + (third + 20) * 2, y, third, 52), "Blue", ref cb, 0f, 1f, "0.00");
            cur.Color = new Color(cr, cg, cb); y += 62;
            float sw = Mathf.Min(54f, (w - 11 * 8) / Swatches.Length);
            for (int i = 0; i < Swatches.Length; i++)
            {
                var sr = new Rect(x + i * (sw + 8), y, sw, sw);
                UI.Rounded(sr, Swatches[i]);
                if (UI.Hit(sr)) { cur.Color = Swatches[i]; AudioManager.Ui("ui_tab"); }
            }
            y += sw + 18;
            // preview card
            var pv = new Rect(x, y, w, 120);
            UI.Rounded(pv, new Color(0.07f, 0.07f, 0.09f, 0.9f));
            float max = Mathf.Max(cur.Size.x, cur.Size.y), scale = 90f / Mathf.Max(0.1f, max);
            var shape = new Rect(pv.x + 30, pv.center.y - cur.Size.y * scale * 0.5f, cur.Size.x * scale, cur.Size.y * scale);
            UI.Rounded(shape, cur.Color, cur.Shape == 0);
            UI.Label(new Rect(pv.x + 160, pv.y + 10, pv.width - 180, 100), cur.Name + "\n" + CustomPropData.Shapes[cur.Shape] + ", " + CustomPropData.Materials[cur.Material] + ", " + cur.Mass.ToString("0") + " kg\n" + CustomPropData.Behaviours[cur.Behaviour], 19, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);

            float bw = (w - 30) / 4f, byy = r.yMax - 86;
            if (UI.Button(new Rect(x, byy, bw, 60), "SAVE", ButtonKind.Primary, IconFactory.Get("check"), 22))
            {
                cur.Clamp();
                if (editing >= 0 && editing < CustomProps.Saved.Count) CustomProps.Saved[editing] = cur.Clone();
                else if (CustomProps.Saved.Count < 60) { CustomProps.Saved.Add(cur.Clone()); editing = CustomProps.Saved.Count - 1; }
                else { GameEvents.ShowToast("You have 60 props already - delete some first."); return; }
                CustomProps.Store();
                GameEvents.ShowToast("Saved. \"" + cur.Name + "\" is in the spawn menu under Props.");
                AudioManager.Ui("ui_success");
            }
            bool inGame = GameManager.I != null && GameManager.I.State == GameState.Playing && PlayerController.Instance != null;
            if (UI.Button(new Rect(x + bw + 10, byy, bw, 60), "SPAWN IT", ButtonKind.Normal, IconFactory.Get("plus"), 20, inGame))
            {
                var p = PlayerController.Instance;
                var cam = p.Cam != null ? p.Cam.transform : p.transform;
                var at = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * (2f + Mathf.Max(cur.Size.x, cur.Size.z));
                at = Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(at.x, p.transform.position.y, at.z);
                SpawnCatalog.Spawn(CustomProps.DefForId(cur.Id), at, Quaternion.Euler(0, cam.eulerAngles.y, 0));
                UIRoot.Pop(); return;
            }
            if (UI.Button(new Rect(x + (bw + 10) * 2, byy, bw, 60), "COPY CODE", ButtonKind.Ghost, IconFactory.Get("copy"), 20)) ShareCode.Copy(cur.Id, "Prop");
            if (editing >= 0 && UI.Button(new Rect(x + (bw + 10) * 3, byy, bw, 60), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 20))
            {
                int idx = editing;
                UIRoot.Push(new ConfirmDialog("Delete prop?", "\"" + cur.Name + "\" leaves the spawn menu.", () => { if (idx < CustomProps.Saved.Count) CustomProps.Saved.RemoveAt(idx); CustomProps.Store(); editing = -1; cur = new CustomPropData(); }, "DELETE", ButtonKind.Danger));
            }
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 200, 60), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) UIRoot.Pop();
        }
    }

    /// <summary>Photo mode 2.0 gallery: every photo you took, with delete.</summary>
    public sealed class PhotoGalleryScreen : UIScreen
    {
        readonly List<string> files = new List<string>();
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        int open = -1, loadedThisFrame;

        public override bool IsModal => true;
        public override bool BlocksGameplay => true;
        public override bool PausesTime => true;
        public static string Dir => System.IO.Path.Combine(Application.persistentDataPath, "Photos");

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            try { if (Directory.Exists(Dir)) files.AddRange(Directory.GetFiles(Dir, "*.png")); } catch { }
            files.Sort((x, y) => string.CompareOrdinal(y, x));
            if (files.Count > 120) files.RemoveRange(120, files.Count - 120);
        }

        public override void OnClose() { foreach (var t in thumbs.Values) if (t != null) UnityEngine.Object.Destroy(t); thumbs.Clear(); }
        public override bool OnBack() { if (open >= 0) { open = -1; return true; } UIRoot.Remove(this); return true; }

        Texture2D Thumb(string f)
        {
            if (thumbs.TryGetValue(f, out var t)) return t;
            if (loadedThisFrame >= 2) return null; // spread the loading over frames
            loadedThisFrame++;
            try { t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(f)); } catch { t = null; }
            thumbs[f] = t;
            return t;
        }

        public override void Draw()
        {
            if (Event.current.type == EventType.Repaint) loadedThisFrame = 0;
            float a = Intro(0, 0.2f);
            UI.Dim(0.85f * a);
            float W = UI.VW, H = UI.VH;
            var r = new Rect(60, 60, W - 120, H - 120);
            UI.Panel(r, "Photo gallery", a);
            if (open >= 0 && open < files.Count)
            {
                var t = Thumb(files[open]);
                var img = new Rect(r.x + 30, r.y + 80, r.width - 60, r.height - 190);
                if (t != null) UI.Tex(img, t, Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(r.x + 30, r.yMax - 86, r.width - 900, 60), System.IO.Path.GetFileName(files[open]), 17, UI.Muted);
                if (UI.Button(new Rect(r.xMax - 820, r.yMax - 86, 180, 60), "< PREV", ButtonKind.Normal, null, 20, open > 0)) open--;
                if (UI.Button(new Rect(r.xMax - 630, r.yMax - 86, 180, 60), "NEXT >", ButtonKind.Normal, null, 20, open < files.Count - 1)) open++;
                if (UI.Button(new Rect(r.xMax - 440, r.yMax - 86, 190, 60), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 20))
                {
                    try { File.Delete(files[open]); } catch { }
                    if (thumbs.TryGetValue(files[open], out var old) && old != null) UnityEngine.Object.Destroy(old);
                    thumbs.Remove(files[open]); files.RemoveAt(open);
                    if (open >= files.Count) open = files.Count - 1;
                }
                if (UI.Button(new Rect(r.xMax - 240, r.yMax - 86, 210, 60), "ALL PHOTOS", ButtonKind.Normal, null, 20)) open = -1;
                return;
            }
            var view = new Rect(r.x + 30, r.y + 80, r.width - 60, r.height - 190);
            int cols = 5; float gap = 16, cw = (view.width - 14 - gap * (cols - 1)) / cols, ch = cw * 9f / 16f;
            float content = Mathf.Max(1, Mathf.CeilToInt(files.Count / (float)cols)) * (ch + gap);
            float s = UI.BeginScroll("gallery", view, content);
            if (files.Count == 0) UI.Label(new Rect(10, 10, view.width, 80), "No photos yet. Press P in a map for photo mode, then TAKE PHOTO.", 22, UI.Muted);
            for (int i = 0; i < files.Count; i++)
            {
                var cell = new Rect((i % cols) * (cw + gap), (i / cols) * (ch + gap) - s, cw, ch);
                if (cell.yMax < 0 || cell.y > view.height) continue;
                UI.Rounded(cell, new Color(0.1f, 0.1f, 0.12f, 0.95f));
                var t = Thumb(files[i]);
                if (t != null) UI.Tex(cell, t, Color.white, ScaleMode.ScaleAndCrop);
                if (UI.Hover(cell)) UI.Outline(cell, UI.Accent, 2);
                if (UI.Hit(cell)) { open = i; AudioManager.Ui("ui_click"); }
            }
            UI.EndScroll("gallery", view, content);
            UI.Label(new Rect(r.x + 300, r.yMax - 86, r.width - 340, 60), files.Count + " photos  -  " + Dir, 16, UI.Muted);
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 240, 60), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) UIRoot.Remove(this);
        }
    }
}
