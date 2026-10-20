using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Links an editor preview object back to its index in <see cref="CustomMapData.Objects"/>.</summary>
    public sealed class MapEditorTag : MonoBehaviour { public int Index; }

    /// <summary>
    /// In-game map editor. Runs while GameManager is in the Menu state (no player, no AI, no physics props):
    /// a free-flying camera, a data model (<see cref="CustomMapData"/>) and one preview GameObject per entry,
    /// rebuilt from data whenever it changes. UI lives in <see cref="MapEditorScreen"/>.
    /// </summary>
    public sealed class MapEditor : MonoBehaviour
    {
        public enum Tool { Select, Place }

        public static MapEditor Current { get; private set; }
        public static bool MouseLook => Current != null && Current.looking;

        public CustomMapData Data;
        public Tool Mode = Tool.Select;
        public CustomMapObj Brush;
        public string BrushName = "";
        public int Selected = -1;
        public bool Snap = true;
        public static readonly float[] GridSteps = { 0.25f, 0.5f, 1f, 2f, 4f };
        public static readonly string[] GridNames = { "0.25 m", "0.5 m", "1 m", "2 m", "4 m" };
        public int GridIndex = 2;
        public float GridStep => GridSteps[Mathf.Clamp(GridIndex, 0, GridSteps.Length - 1)];
        public bool Dirty;
        public bool ShowHelp;
        public string Status = "";
        public float StatusAt = -10f;
        public Camera Cam;
        public bool Grabbing => grabbing;

        /// <summary>Virtual-canvas rects covered by editor UI this frame (clicks there don't reach the 3D view).</summary>
        public readonly List<Rect> UiRects = new List<Rect>();

        Transform objRoot;
        GameObject ground;
        readonly List<GameObject> views = new List<GameObject>();
        GameObject ghost;
        string ghostKey;
        Vector3 ghostBase;
        bool ghostValid;
        bool looking;
        float yaw, pitch = 20f;
        // drag / grab
        bool mouseDownOnSelection, grabbing;
        Vector2 downPos;
        Vector3 grabStartPos, grabTarget, grabViewBase;
        bool grabHasTarget;
        // undo
        readonly List<CustomMapData> undo = new List<CustomMapData>();
        readonly List<CustomMapData> redo = new List<CustomMapData>();
        string lastUndoKey;
        float lastUndoTime;

        // session kept across test play
        static CustomMapData pendingData;
        static bool pendingDirty;
        static Vector3 pendingCamPos = new Vector3(0, 7, -18);
        static float pendingYaw, pendingPitch = 22f;
        static MapDef testDef;

        /// <summary>Unsaved editor state left behind (test play ended through the pause menu, etc.).</summary>
        public static CustomMapData PendingSession => pendingData;
        public static bool PendingDirty => pendingDirty;

        // ================================================================== lifecycle

        public static void Open(CustomMapData data, bool resumeCamera = false)
        {
            if (Current != null) Destroy(Current.gameObject);
            MenuBackdrop.DestroyBackdrop();
            Decals.ClearAll(); // the menu backdrop's blood decals would float around the editor
            BloodFX.Clear();
            FX.Clear();
            AudioManager.StopMusic();
            var go = new GameObject("MapEditor");
            var ed = go.AddComponent<MapEditor>();
            Current = ed;
            ed.Data = data ?? CustomMaps.NewMap("New Map");
            ed.Data.Validate();
            if (!resumeCamera) { pendingCamPos = new Vector3(0, 7, -18); pendingYaw = 0; pendingPitch = 22f; }
            ed.Init(pendingCamPos, pendingYaw, pendingPitch);
            pendingData = ed.Data;
            UIRoot.Replace(new MapEditorScreen());
            AudioManager.PlayAmbience(ed.Data.Indoor ? "amb_hum" : "amb_wind", 0.4f);
        }

        void Init(Vector3 camPos, float camYaw, float camPitch)
        {
            objRoot = new GameObject("Objects").transform;
            objRoot.SetParent(transform, false);
            var camGo = new GameObject("EditorCamera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = 70f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = Mathf.Max(300f, Settings.Data.renderDistance);
            Cam.cullingMask = ~((1 << Layers.Viewmodel) | (1 << 31));
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = camPos;
            yaw = camYaw; pitch = camPitch;
            camGo.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            ApplyLighting();
            RebuildAll();
            SetBrush(CustomMapKit.Shapes[0]);
            Mode = Tool.Select;
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                pendingData = Data; pendingDirty = Dirty;
                if (Cam != null) { pendingCamPos = Cam.transform.position; pendingYaw = yaw; pendingPitch = pitch; }
                Current = null;
            }
        }

        public void ApplyLighting()
        {
            MapLighting.Apply((TimeOfDay)Data.Time, Data.Indoor, transform);
            CustomMapKit.ApplyFog(Data);
        }

        public void Exit()
        {
            pendingData = null; pendingDirty = false;
            Current = null;
            Destroy(gameObject);
            GameManager.I.ReturnToMenu();
            UIRoot.Push(new MapEditorHubScreen());
        }

        public void TestPlay()
        {
            if (GameManager.I == null) return;
            pendingData = Data; pendingDirty = Dirty;
            pendingCamPos = Cam.transform.position; pendingYaw = yaw; pendingPitch = pitch;
            var def = CustomMaps.CreateTemp(Data);
            testDef = def;
            var opt = new MapOptions { Time = (TimeOfDay)Data.Time, StartingNpcs = Data.Npcs >= 0 ? Data.Npcs : 3 };
            Current = null;
            Destroy(gameObject);
            GameManager.I.StartMap(def, opt);
            GameEvents.ShowToast("Testing your map - press Esc to return to the editor.");
        }

        /// <summary>Esc while test-playing an editor map: back to the editor. Called by GameManager.</summary>
        public static bool TryReturnFromTest()
        {
            if (testDef == null || GameManager.I == null || GameManager.I.CurrentMap != testDef) return false;
            testDef = null;
            var data = pendingData;
            bool dirty = pendingDirty;
            GameManager.I.ReturnToMenu();
            Open(data, true);
            if (Current != null) { Current.Dirty = dirty; Current.Say("Back in the editor."); }
            return true;
        }

        public string Save()
        {
            var err = CustomMaps.Save(Data);
            if (err == null) { Dirty = false; Say("Saved \"" + Data.Name + "\"."); AudioManager.Ui("ui_success"); }
            else { Say("Save failed: " + err); AudioManager.Ui("ui_error"); }
            return err;
        }

        public void Say(string s) { Status = s; StatusAt = Time.unscaledTime; }

        // ================================================================== views

        public void RebuildAll()
        {
            for (int i = objRoot.childCount - 1; i >= 0; i--) DestroyImmediate(objRoot.GetChild(i).gameObject);
            views.Clear();
            BuildGround();
            for (int i = 0; i < Data.Objects.Count; i++) views.Add(MakeView(i));
            if (Selected >= Data.Objects.Count) Selected = -1;
        }

        void BuildGround()
        {
            if (ground != null) DestroyImmediate(ground);
            ground = null;
            if (!Data.Ground) return;
            ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            float g = Data.GroundSize;
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.1f };
            mb.Box(new Vector3(0, -0.5f, 0), new Vector3(g, 1f, g), 0.25f);
            ground.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Ground");
            ground.AddComponent<MeshRenderer>().sharedMaterial = MapBuilder.Tex(CustomMapKit.TexOf(Data.GroundMat));
            var bc = ground.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, -0.5f, 0); bc.size = new Vector3(g, 1f, g);
        }

        public void RebuildGround() { BuildGround(); Dirty = true; }

        GameObject MakeView(int i)
        {
            GameObject go = null;
            try { go = CustomMapKit.Build(Data.Objects[i], objRoot, true); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (go == null) { go = new GameObject("Empty"); go.transform.SetParent(objRoot, false); }
            go.AddComponent<MapEditorTag>().Index = i;
            return go;
        }

        public void Rebuild(int i)
        {
            if (i < 0 || i >= views.Count) return;
            if (views[i] != null) DestroyImmediate(views[i]);
            views[i] = MakeView(i);
        }

        // ================================================================== editing ops

        public void PushUndo(string coalesceKey = null)
        {
            float now = Time.unscaledTime;
            if (coalesceKey != null && coalesceKey == lastUndoKey && now - lastUndoTime < 1.2f) { lastUndoTime = now; return; }
            lastUndoKey = coalesceKey; lastUndoTime = now;
            undo.Add(Data.Clone());
            if (undo.Count > 60) undo.RemoveAt(0);
            redo.Clear();
            Dirty = true;
        }

        /// <summary>Records a snapshot taken before a multi-step change (map settings dialog).</summary>
        public void AddUndoSnapshot(CustomMapData before)
        {
            undo.Add(before);
            if (undo.Count > 60) undo.RemoveAt(0);
            redo.Clear();
            lastUndoKey = null;
            Dirty = true;
        }

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public void Undo()
        {
            if (undo.Count == 0) { Say("Nothing to undo."); return; }
            redo.Add(Data);
            Data = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            AfterHistory("Undo");
        }

        public void Redo()
        {
            if (redo.Count == 0) { Say("Nothing to redo."); return; }
            undo.Add(Data);
            Data = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            AfterHistory("Redo");
        }

        void AfterHistory(string what)
        {
            CancelGrab();
            lastUndoKey = null;
            Dirty = true;
            pendingData = Data;
            if (Selected >= Data.Objects.Count) Selected = -1;
            ApplyLighting();
            RebuildAll();
            Say(what);
            AudioManager.Ui("ui_click", 0.6f);
        }

        public CustomMapObj SelectedObj => Selected >= 0 && Selected < Data.Objects.Count ? Data.Objects[Selected] : null;

        public void SetBrush(CustomMapKit.Entry e)
        {
            var old = Brush;
            Brush = CustomMapKit.FromEntry(e);
            BrushName = e.Name;
            if (old != null) Brush.Ry = old.Ry;
            Mode = Tool.Place;
            Selected = -1;
            CancelGrab();
        }

        public bool Place(Vector3 pos)
        {
            if (Data.Objects.Count >= CustomMapData.MaxObjects) { Say("Object limit reached (" + CustomMapData.MaxObjects + ")."); AudioManager.Ui("ui_error"); return false; }
            PushUndo();
            var o = Brush.Clone();
            o.Px = R3(pos.x); o.Py = R3(pos.y); o.Pz = R3(pos.z);
            Data.Objects.Add(o);
            views.Add(MakeView(Data.Objects.Count - 1));
            AudioManager.Ui("ui_click", 0.7f);
            return true;
        }

        public void DeleteSelected()
        {
            if (SelectedObj == null) return;
            PushUndo();
            int i = Selected;
            Data.Objects.RemoveAt(i);
            if (views[i] != null) DestroyImmediate(views[i]);
            views.RemoveAt(i);
            for (int k = i; k < views.Count; k++) if (views[k] != null) views[k].GetComponent<MapEditorTag>().Index = k;
            Selected = -1;
            Say("Deleted.");
            AudioManager.Ui("ui_back", 0.7f);
        }

        public void DuplicateSelected()
        {
            var o = SelectedObj;
            if (o == null) return;
            if (Data.Objects.Count >= CustomMapData.MaxObjects) { Say("Object limit reached."); return; }
            PushUndo();
            var c = o.Clone();
            var right = Quaternion.Euler(0, o.Ry, 0) * Vector3.right;
            float w = CustomMapKit.Shapes.Exists(e => e.Kind == o.K) ? Mathf.Max(o.Sx, GridStep) : 1f;
            c.Px = R3(o.Px + right.x * w); c.Pz = R3(o.Pz + right.z * w);
            Data.Objects.Add(c);
            views.Add(MakeView(Data.Objects.Count - 1));
            Selected = Data.Objects.Count - 1;
            Say("Duplicated (Ctrl+D). Drag it or use the arrow keys to move it.");
        }

        public void ModifySelected(System.Action<CustomMapObj> change, string coalesceKey = null)
        {
            var o = SelectedObj;
            if (o == null) return;
            PushUndo(coalesceKey);
            change(o);
            Rebuild(Selected);
        }

        /// <summary>Applies a change to the selection, or to the placement brush when placing.</summary>
        public void Modify(System.Action<CustomMapObj> change, string coalesceKey = null)
        {
            if (Mode == Tool.Place && Brush != null) change(Brush);
            else ModifySelected(change, coalesceKey != null ? coalesceKey + Selected : null);
        }

        public void Rotate(float deg, int axis = 1)
        {
            Modify(o =>
            {
                if (axis == 0) o.Rx = R3(Mathf.Repeat(o.Rx + deg + 180f, 360f) - 180f);
                else if (axis == 2) o.Rz = R3(Mathf.Repeat(o.Rz + deg + 180f, 360f) - 180f);
                else o.Ry = R3(Mathf.Repeat(o.Ry + deg, 360f));
            }, "rot" + axis);
        }

        public void ScaleBy(float f)
        {
            Modify(o =>
            {
                if (IsShape(o.K) || (o.K == "item" && o.Id == "spikes"))
                {
                    o.Sx = R3(Mathf.Clamp(o.Sx * f, 0.05f, 200)); o.Sy = R3(Mathf.Clamp(o.Sy * f, 0.05f, 200)); o.Sz = R3(Mathf.Clamp(o.Sz * f, 0.05f, 200));
                }
                else if (o.K == "prop") { o.Sx = o.Sy = o.Sz = R3(Mathf.Clamp(o.Sx * f, 0.25f, 4f)); }
                else if (o.K == "light") o.Range = R3(Mathf.Clamp(o.Range * f, 1, 60));
            }, "scale");
        }

        public void Nudge(Vector3 delta)
        {
            ModifySelected(o => { o.Px = R3(o.Px + delta.x); o.Py = R3(o.Py + delta.y); o.Pz = R3(o.Pz + delta.z); }, "nudge" + Selected);
        }

        public void Focus()
        {
            var o = SelectedObj;
            if (o == null) return;
            var target = new Vector3(o.Px, o.Py + o.Sy * 0.5f, o.Pz);
            float dist = Mathf.Clamp(Mathf.Max(o.Sx, o.Sy, o.Sz) * 1.8f, 3f, 60f);
            Cam.transform.position = target - Cam.transform.forward * dist;
        }

        public static bool IsShape(string k) => k == "block" || k == "floor" || k == "wall" || k == "platform" || k == "ramp" || k == "stairs" || k == "pillar";
        static float R3(float v) => Mathf.Round(v * 1000f) / 1000f;

        // ================================================================== per frame

        bool UiHasFocus => false; // the editor screen has no text fields; dialogs with fields block editor input anyway
        static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        Vector2 MouseV => new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / Mathf.Max(0.01f, UI.Scale);

        public bool MouseOverUi
        {
            get
            {
                var m = MouseV;
                foreach (var r in UiRects) if (r.Contains(m)) return true;
                return false;
            }
        }

        bool Active => UIRoot.Top is MapEditorScreen && !DevPanel.Open;

        void Update()
        {
            if (Cam == null) return;
            bool active = Active;
            if (!active) { looking = false; UpdateGhost(false); return; }

            // ----- camera
            if (Input.GetMouseButtonDown(1) && !MouseOverUi) looking = true;
            if (!Input.GetMouseButton(1)) looking = false;
            if (looking)
            {
                yaw += Input.GetAxisRaw("Mouse X") * 2.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2.2f, -89f, 89f);
                Cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (!UiHasFocus && !Ctrl)
            {
                var mv = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) mv += Vector3.forward;
                if (Input.GetKey(KeyCode.S)) mv += Vector3.back;
                if (Input.GetKey(KeyCode.A)) mv += Vector3.left;
                if (Input.GetKey(KeyCode.D)) mv += Vector3.right;
                var world = Cam.transform.rotation * mv;
                if (Input.GetKey(KeyCode.Space) || (looking && Input.GetKey(KeyCode.E))) world += Vector3.up;
                if (Input.GetKey(KeyCode.C) || (looking && Input.GetKey(KeyCode.Q))) world += Vector3.down;
                float speed = Shift ? 32f : 11f;
                Cam.transform.position += world * speed * Time.unscaledDeltaTime;
            }

            // ----- hotkeys
            if (!UiHasFocus) Hotkeys();

            // ----- mouse in the 3D view
            bool overUi = MouseOverUi;
            if (Mode == Tool.Place) PlaceUpdate(overUi);
            else SelectUpdate(overUi);
            UpdateGhost(Mode == Tool.Place && !overUi);
        }

        void Hotkeys()
        {
            if (Ctrl)
            {
                if (Input.GetKeyDown(KeyCode.Z)) { if (Shift) Redo(); else Undo(); }
                if (Input.GetKeyDown(KeyCode.Y)) Redo();
                if (Input.GetKeyDown(KeyCode.S)) Save();
                if (Input.GetKeyDown(KeyCode.D)) DuplicateSelected();
                if (Input.GetKeyDown(KeyCode.P)) TestPlay();
                return;
            }
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { Mode = Tool.Select; CancelGrab(); }
            if (Input.GetKeyDown(KeyCode.Alpha2) && Brush != null) { Mode = Tool.Place; Selected = -1; CancelGrab(); }
            if (Input.GetKeyDown(KeyCode.Tab)) Snap = !Snap;
            if (Input.GetKeyDown(KeyCode.F)) Focus();
            if (!looking)
            {
                float step = Shift ? 5f : 15f;
                if (Input.GetKeyDown(KeyCode.Q)) Rotate(-step);
                if (Input.GetKeyDown(KeyCode.E)) Rotate(step);
                if (Input.GetKeyDown(KeyCode.R)) Rotate(90f);
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f && !MouseOverUi) Rotate(Mathf.Sign(wheel) * step);
            }
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) ScaleBy(1.25f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) ScaleBy(0.8f);

            if (Mode == Tool.Select && SelectedObj != null && !grabbing)
            {
                if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) { DeleteSelected(); return; }
                if (Input.GetKeyDown(KeyCode.G)) BeginGrab();
                float s = Snap ? GridStep : 0.1f;
                if (Shift) s *= 4f;
                var fwd = Cam.transform.forward; fwd.y = 0;
                fwd = Mathf.Abs(fwd.x) > Mathf.Abs(fwd.z) ? new Vector3(Mathf.Sign(fwd.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(fwd.z));
                var right = new Vector3(fwd.z, 0, -fwd.x);
                if (Input.GetKeyDown(KeyCode.UpArrow)) Nudge(fwd * s);
                if (Input.GetKeyDown(KeyCode.DownArrow)) Nudge(-fwd * s);
                if (Input.GetKeyDown(KeyCode.RightArrow)) Nudge(right * s);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) Nudge(-right * s);
                if (Input.GetKeyDown(KeyCode.PageUp)) Nudge(Vector3.up * s);
                if (Input.GetKeyDown(KeyCode.PageDown)) Nudge(Vector3.down * s);
            }
        }

        /// <summary>Esc handling. Returns true if the editor consumed it (otherwise the screen opens the editor menu).</summary>
        public bool HandleEscape()
        {
            if (ShowHelp) { ShowHelp = false; return true; }
            if (grabbing) { CancelGrab(); Say("Move cancelled."); return true; }
            if (Mode == Tool.Place) { Mode = Tool.Select; return true; }
            if (Selected >= 0) { Selected = -1; return true; }
            return false;
        }

        // ----- placing

        Ray MouseRay => looking ? Cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)) : Cam.ScreenPointToRay(Input.mousePosition);

        int RayMask => Physics.DefaultRaycastLayers & ~(1 << Layers.Boundaries);

        Vector3 Extents(CustomMapObj o)
        {
            if (!IsShape(o.K)) return new Vector3(0.4f, 0.5f, 0.4f);
            float a = o.Ry * Mathf.Deg2Rad, c = Mathf.Abs(Mathf.Cos(a)), s = Mathf.Abs(Mathf.Sin(a));
            return new Vector3(c * o.Sx * 0.5f + s * o.Sz * 0.5f, o.Sy * 0.5f, s * o.Sx * 0.5f + c * o.Sz * 0.5f);
        }

        bool TargetPoint(CustomMapObj forObj, out Vector3 p)
        {
            var ray = MouseRay;
            Vector3 n;
            if (Physics.Raycast(ray, out var hit, 800f, RayMask, QueryTriggerInteraction.Ignore)) { p = hit.point; n = hit.normal; }
            else if (ray.direction.y < -0.01f) { p = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); n = Vector3.up; }
            else { p = ray.origin + ray.direction * 12f; n = Vector3.up; }
            if ((p - ray.origin).sqrMagnitude > 600f * 600f) { p = ray.origin + ray.direction * 12f; n = Vector3.up; }

            var ext = Extents(forObj);
            if (Mathf.Abs(n.y) < 0.7f) { p.x += n.x * ext.x; p.z += n.z * ext.z; } // against a wall: push out by half the footprint
            else if (n.y < -0.7f) p.y -= forObj.K == "light" ? 0.35f : ext.y * 2f;
            else if (forObj.K == "light") p.y += 2.5f;

            if (Snap)
            {
                float g = GridStep;
                p.x = Mathf.Round(p.x / g) * g;
                p.z = Mathf.Round(p.z / g) * g;
            }
            p.y = Mathf.Round(p.y * 100f) / 100f;
            return true;
        }

        void PlaceUpdate(bool overUi)
        {
            ghostValid = false;
            if (!overUi && TargetPoint(Brush, out var p)) { ghostValid = true; grabTarget = p; }
            if (Input.GetMouseButtonDown(0) && !overUi && !looking && ghostValid) Place(grabTarget);
        }

        // ----- selecting / moving

        void SelectUpdate(bool overUi)
        {
            if (grabbing)
            {
                var o = SelectedObj;
                if (o == null) { CancelGrab(); return; }
                if (TargetPoint(o, out var p))
                {
                    grabTarget = p; grabHasTarget = true;
                    var v = views[Selected];
                    if (v != null) v.transform.position = grabViewBase + (p - grabStartPos);
                }
                bool release = mouseDownOnSelection ? Input.GetMouseButtonUp(0) : Input.GetMouseButtonDown(0);
                if (release) CommitGrab();
                return;
            }

            if (Input.GetMouseButtonDown(0) && !overUi && !looking)
            {
                mouseDownOnSelection = false;
                if (Physics.Raycast(MouseRay, out var hit, 800f, RayMask, QueryTriggerInteraction.Collide))
                {
                    var tag = hit.collider.GetComponentInParent<MapEditorTag>();
                    if (tag != null && tag.Index < Data.Objects.Count)
                    {
                        if (Selected != tag.Index) AudioManager.Ui("ui_tab", 0.6f);
                        Selected = tag.Index;
                        mouseDownOnSelection = true;
                        downPos = Input.mousePosition;
                        return;
                    }
                }
                Selected = -1;
            }
            if (mouseDownOnSelection && Input.GetMouseButton(0) && SelectedObj != null && ((Vector2)Input.mousePosition - downPos).magnitude > 8f)
            {
                BeginGrab();
                return;
            }
            if (Input.GetMouseButtonUp(0)) mouseDownOnSelection = false;
        }

        public void BeginGrabPublic() { if (Mode == Tool.Select) { mouseDownOnSelection = false; BeginGrab(); } }

        void BeginGrab()
        {
            var o = SelectedObj;
            if (o == null || Selected >= views.Count || views[Selected] == null) return;
            grabbing = true;
            grabHasTarget = false;
            grabStartPos = new Vector3(o.Px, o.Py, o.Pz);
            grabViewBase = views[Selected].transform.position;
            foreach (var c in views[Selected].GetComponentsInChildren<Collider>()) c.enabled = false;
            if (!mouseDownOnSelection) Say("Moving - click to drop, Esc to cancel.");
        }

        void CommitGrab()
        {
            grabbing = false;
            mouseDownOnSelection = false;
            var o = SelectedObj;
            if (o == null) return;
            if (grabHasTarget && (grabTarget - grabStartPos).sqrMagnitude > 0.0001f)
            {
                PushUndo();
                o.Px = R3(grabTarget.x); o.Py = R3(grabTarget.y); o.Pz = R3(grabTarget.z);
            }
            Rebuild(Selected);
        }

        public void CancelGrab()
        {
            if (!grabbing) return;
            grabbing = false;
            mouseDownOnSelection = false;
            Rebuild(Selected);
        }

        // ----- ghost preview

        void UpdateGhost(bool show)
        {
            if (!show || Brush == null)
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }
            var b = Brush;
            string key = b.K + "|" + b.Id + "|" + b.Sx + "|" + b.Sy + "|" + b.Sz + "|" + b.Mat + "|" + b.Cr + "|" + b.Cg + "|" + b.Cb + "|" + b.Rx + "|" + b.Ry + "|" + b.Rz + "|" + b.Range + "|" + b.Power;
            if (ghost == null || key != ghostKey)
            {
                if (ghost != null) DestroyImmediate(ghost);
                var tmp = b.Clone();
                tmp.Px = tmp.Py = tmp.Pz = 0;
                ghost = null;
                try { ghost = CustomMapKit.Build(tmp, transform, true); } catch (System.Exception e) { Debug.LogException(e); }
                if (ghost == null) { ghost = new GameObject("Ghost"); ghost.transform.SetParent(transform, false); }
                ghost.name = "Ghost";
                foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
                ghostBase = ghost.transform.position;
                ghostKey = key;
            }
            ghost.SetActive(ghostValid);
            if (ghostValid) ghost.transform.position = ghostBase + grabTarget;
        }

        // ================================================================== GL overlay (grid, selection boxes)

        void OnRenderObject()
        {
            if (Camera.current != Cam) return;
            var mat = Mats.Lines;
            if (mat == null) return;
            mat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            // grid around the camera
            float g = Mathf.Max(1f, GridStep);
            var cp = Cam.transform.position;
            float cx = Mathf.Round(cp.x / g) * g, cz = Mathf.Round(cp.z / g) * g;
            int n = Mathf.Clamp(Mathf.RoundToInt(40f / g), 10, 40);
            float y = 0.02f;
            GL.Color(new Color(1f, 1f, 1f, Snap ? 0.14f : 0.06f));
            for (int i = -n; i <= n; i++)
            {
                GL.Vertex3(cx + i * g, y, cz - n * g); GL.Vertex3(cx + i * g, y, cz + n * g);
                GL.Vertex3(cx - n * g, y, cz + i * g); GL.Vertex3(cx + n * g, y, cz + i * g);
            }
            // world axes
            GL.Color(new Color(1f, 0.3f, 0.3f, 0.6f)); GL.Vertex3(-5, y, 0); GL.Vertex3(5, y, 0);
            GL.Color(new Color(0.3f, 0.5f, 1f, 0.6f)); GL.Vertex3(0, y, -5); GL.Vertex3(0, y, 5);

            if (Selected >= 0 && Selected < views.Count && views[Selected] != null)
                WireBox(BoundsOf(views[Selected]), new Color(1f, 0.85f, 0.2f, 0.95f));
            if (ghost != null && ghost.activeSelf)
                WireBox(BoundsOf(ghost), new Color(0.3f, 1f, 0.5f, 0.9f));
            GL.End();
            GL.PopMatrix();
        }

        static Bounds BoundsOf(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.one * 0.3f);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            b.Expand(0.04f);
            return b;
        }

        static void WireBox(Bounds b, Color c)
        {
            GL.Color(c);
            var mn = b.min; var mx = b.max;
            Vector3 P(int i) => new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
            int[] e = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 1, 3, 4, 6, 5, 7, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < e.Length; i += 2) { GL.Vertex(P(e[i])); GL.Vertex(P(e[i + 1])); }
        }

        public int CountKind(string k)
        {
            int n = 0;
            foreach (var o in Data.Objects) if (o.K == k) n++;
            return n;
        }
    }
}
