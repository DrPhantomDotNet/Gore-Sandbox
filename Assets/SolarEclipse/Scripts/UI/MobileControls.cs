using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Touch controls for phones/tablets (auto-detected, or forced in Settings → Controls). Floating move stick
    /// on the left, drag-to-look on the right, and buttons that feed <see cref="PlayerInput"/>. With the mouse,
    /// "Always" mode can be tested on desktop (left click acts as a finger).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class MobileControls : MonoBehaviour
    {
        enum Kind { Fire, Aim, Jump, Crouch, Sprint, Reload, Use, Kick, Spawn, Rotate, Mode, Inspect, Menu, Noclip, Closer, Further, Weapon, Chat }

        sealed class Button
        {
            public Kind Kind;
            public string Label, Icon;
            public Rect Rect;        // virtual coords
            public bool Held, Pressed;
            public bool Visible = true;
            public bool Toggle;      // sprint/crouch latch
            public bool On;
        }

        readonly List<Button> buttons = new List<Button>();
        readonly Dictionary<int, object> owners = new Dictionary<int, object>(); // finger → button / "stick" / "look"
        Vector2 stickOrigin, stickPos;
        int stickFinger = int.MinValue, lookFinger = int.MinValue;
        Vector2 lastLook;
        static MobileControls I;

        public static void Create(Transform parent)
        {
            var go = new GameObject("MobileControls");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<MobileControls>();
            UIRoot.OverlayLayer += I.Draw;
            Input.multiTouchEnabled = true;
        }

        void Add(Kind k, string label, string icon, bool toggle = false) => buttons.Add(new Button { Kind = k, Label = label, Icon = icon, Toggle = toggle });

        void Awake()
        {
            Add(Kind.Fire, "FIRE", "crosshair"); Add(Kind.Aim, "AIM", "eye"); Add(Kind.Jump, "JUMP", "arrow_up"); Add(Kind.Crouch, "CROUCH", "arrow_down", true);
            Add(Kind.Sprint, "SPRINT", "run", true); Add(Kind.Reload, "RELOAD", "refresh"); Add(Kind.Use, "USE", "hand"); Add(Kind.Kick, "KICK", "boot");
            Add(Kind.Spawn, "SPAWN", "plus"); Add(Kind.Rotate, "ROTATE", "refresh"); Add(Kind.Mode, "MODE", "list"); Add(Kind.Inspect, "INSPECT", "search");
            Add(Kind.Menu, "MENU", "menu"); Add(Kind.Noclip, "NOCLIP", "ghost"); Add(Kind.Closer, "PULL", "minus"); Add(Kind.Further, "PUSH", "plus"); Add(Kind.Weapon, "NEXT", "arrow_right"); Add(Kind.Chat, "CHAT", "list");
        }

        bool Active => Settings.TouchControlsActive && GameManager.I != null && GameManager.I.State == GameState.Playing && PlayerController.Instance != null && !UIRoot.AnyBlocking && !DevPanel.Open;

        void Layout()
        {
            float W = UI.VW, H = UI.VH, s = 96f * Settings.Data.touchButtonScale, g = 14f;
            var cur = PlayerController.Instance != null && PlayerController.Instance.Inventory != null ? PlayerController.Instance.Inventory.Current : null;
            bool spawnGun = cur is SpawnGun;
            bool holding = spawnGun && ((SpawnGun)cur).Holding;
            foreach (var b in buttons)
            {
                b.Visible = true;
                switch (b.Kind)
                {
                    case Kind.Fire: b.Rect = new Rect(W - s * 1.6f - 40, H - s * 1.6f - 60, s * 1.6f, s * 1.6f); break;
                    case Kind.Aim: b.Rect = new Rect(W - s * 1.6f - 40 - s - g, H - s - 60, s, s); break;
                    case Kind.Jump: b.Rect = new Rect(W - s - 40, H - s * 1.6f - 60 - s - g, s, s); break;
                    case Kind.Reload: b.Rect = new Rect(W - s * 2 - 40 - g, H - s * 1.6f - 60 - s - g, s, s); b.Label = spawnGun ? "UNDO" : "RELOAD"; break;
                    case Kind.Crouch: b.Rect = new Rect(W - s * 1.6f - 40 - (s + g) * 2, H - s - 60, s, s); break;
                    case Kind.Kick: b.Rect = new Rect(W - s * 3 - 40 - g * 2, H - s * 1.6f - 60 - s - g, s, s); b.Label = holding ? "FREEZE" : "KICK"; break;
                    case Kind.Use: b.Rect = new Rect(W - s * 4 - 40 - g * 3, H - s * 1.6f - 60 - s - g, s, s); break;
                    case Kind.Sprint: b.Rect = new Rect(40, H - s - 340, s, s); break;
                    case Kind.Spawn: b.Rect = new Rect(W - s - 40, H - s * 1.6f - 60 - (s + g) * 2, s, s); b.Visible = spawnGun; break;
                    case Kind.Rotate: b.Rect = new Rect(W - s * 2 - 40 - g, H - s * 1.6f - 60 - (s + g) * 2, s, s); b.Visible = holding; break;
                    case Kind.Closer: b.Rect = new Rect(W - s * 3 - 40 - g * 2, H - s * 1.6f - 60 - (s + g) * 2, s, s); b.Visible = holding; break;
                    case Kind.Further: b.Rect = new Rect(W - s * 4 - 40 - g * 3, H - s * 1.6f - 60 - (s + g) * 2, s, s); b.Visible = holding; break;
                    case Kind.Mode: b.Rect = new Rect(W - s * 0.8f - 40, 130, s * 0.8f, s * 0.6f); b.Visible = cur != null && (cur.Def.Auto || cur.Def.Kind == WeaponKind.Admin); break;
                    case Kind.Inspect: b.Rect = new Rect(W - s * 1.6f - 50 - s * 0.8f, 130, s * 0.8f, s * 0.6f); break;
                    case Kind.Weapon: b.Rect = new Rect(W - s * 2.4f - 60 - s * 0.8f, 130, s * 0.8f, s * 0.6f); break;
                    case Kind.Menu: b.Rect = new Rect(30, 120, s * 0.9f, s * 0.6f); break;
                    case Kind.Noclip: b.Rect = new Rect(40 + s * 0.9f + g, 120, s * 0.9f, s * 0.6f); break;
                    case Kind.Chat: b.Rect = new Rect(50 + (s * 0.9f + g) * 2, 120, s * 0.9f, s * 0.6f); break;
                }
            }
        }

        static Vector2 ToVirtual(Vector2 screen) => new Vector2(screen.x / UI.Scale, (Screen.height - screen.y) / UI.Scale);

        void Update()
        {
            if (!Active) { ReleaseAll(); return; }
            Layout();
            foreach (var b in buttons) b.Pressed = false;

            // Gather fingers (mouse acts as finger -1 on desktop).
            var fingers = new List<(int id, Vector2 pos, TouchPhase phase)>();
            for (int i = 0; i < Input.touchCount; i++) { var t = Input.GetTouch(i); fingers.Add((t.fingerId, t.position, t.phase)); }
            if (Input.touchCount == 0 && Input.mousePresent)
            {
                if (Input.GetMouseButtonDown(0)) fingers.Add((-1, Input.mousePosition, TouchPhase.Began));
                else if (Input.GetMouseButton(0)) fingers.Add((-1, Input.mousePosition, TouchPhase.Moved));
                else if (Input.GetMouseButtonUp(0)) fingers.Add((-1, Input.mousePosition, TouchPhase.Ended));
            }

            Vector2 look = Vector2.zero;
            foreach (var (id, screen, phase) in fingers)
            {
                var v = ToVirtual(screen);
                if (phase == TouchPhase.Began)
                {
                    Button hit = null;
                    foreach (var b in buttons) if (b.Visible && Inflate(b.Rect, 10).Contains(v)) { hit = b; break; }
                    if (hit != null)
                    {
                        owners[id] = hit;
                        hit.Pressed = true;
                        if (hit.Toggle) hit.On = !hit.On; else hit.Held = true;
                        AudioManager.Ui("ui_click", 0.25f);
                    }
                    else if (v.x < UI.VW * 0.4f && stickFinger == int.MinValue) { owners[id] = "stick"; stickFinger = id; stickOrigin = stickPos = v; }
                    else if (lookFinger == int.MinValue) { owners[id] = "look"; lookFinger = id; lastLook = v; }
                    continue;
                }
                if (!owners.TryGetValue(id, out var owner)) continue;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                {
                    if (owner is Button b) b.Held = false;
                    if (id == stickFinger) stickFinger = int.MinValue;
                    if (id == lookFinger) lookFinger = int.MinValue;
                    owners.Remove(id);
                    continue;
                }
                if (id == stickFinger) stickPos = v;
                if (id == lookFinger) { look += (v - lastLook); lastLook = v; }
                // Fire button doubles as a look pad (drag while shooting).
                if (owner is Button fb && fb.Kind == Kind.Fire && phase == TouchPhase.Moved)
                {
                    if (fb.Rect.Contains(v)) { } // stays held
                }
            }

            // Write into PlayerInput.
            var stick = stickFinger != int.MinValue ? Vector2.ClampMagnitude((stickPos - stickOrigin) / 90f, 1f) : Vector2.zero;
            PlayerInput.TouchMove = new Vector2(stick.x, -stick.y);
            PlayerInput.TouchLook = new Vector2(look.x, -look.y) * 0.12f;
            PlayerInput.TouchFire = Get(Kind.Fire).Held;
            PlayerInput.TouchAim = Get(Kind.Aim).Held;
            PlayerInput.TouchJump = Get(Kind.Jump).Held;
            PlayerInput.TouchCrouch = Get(Kind.Crouch).On;
            PlayerInput.TouchSprint = Get(Kind.Sprint).On || stick.magnitude > 0.95f && stick.y < -0.7f;
            PlayerInput.TouchRotate = Get(Kind.Rotate).Held;
            var cur = PlayerController.Instance.Inventory.Current;
            if (Get(Kind.Reload).Pressed)
            {
                if (cur is SpawnGun) DevConsole.Execute("undo"); else PlayerInput.TouchReload = true;
            }
            if (Get(Kind.Use).Pressed) PlayerInput.TouchUse = true;
            if (Get(Kind.Kick).Pressed) PlayerInput.TouchKick = true;
            if (Get(Kind.Spawn).Pressed) PlayerInput.TouchMiddle = true;
            if (Get(Kind.Mode).Pressed) PlayerInput.TouchMode = true;
            if (Get(Kind.Inspect).Pressed) PlayerInput.TouchInspect = true;
            if (Get(Kind.Closer).Held) PlayerInput.TouchScroll = -0.15f;
            if (Get(Kind.Further).Held) PlayerInput.TouchScroll = 0.15f;
            if (Get(Kind.Weapon).Pressed) PlayerInput.TouchScroll = -1f;
            if (Get(Kind.Noclip).Pressed) PlayerController.Instance.SetNoclip(!PlayerController.Instance.Noclip);
            if (Get(Kind.Menu).Pressed) GameManager.I.Back();
            if (Get(Kind.Chat).Pressed && !UIRoot.Contains<ChatScreen>()) UIRoot.Push(new ChatScreen());
        }

        Button Get(Kind k) { foreach (var b in buttons) if (b.Kind == k) return b; return null; }
        static Rect Inflate(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + d * 2, r.height + d * 2);

        void ReleaseAll()
        {
            if (owners.Count == 0 && stickFinger == int.MinValue) return;
            owners.Clear();
            stickFinger = lookFinger = int.MinValue;
            foreach (var b in buttons) { b.Held = false; b.Pressed = false; }
            PlayerInput.TouchMove = Vector2.zero;
            PlayerInput.TouchFire = PlayerInput.TouchAim = PlayerInput.TouchJump = PlayerInput.TouchRotate = false;
        }

        void Draw()
        {
            if (!Active) return;
            float alpha = Settings.Data.touchOpacity;
            foreach (var b in buttons)
            {
                if (!b.Visible) continue;
                bool lit = b.Held || b.On;
                UI.Tex(b.Rect, TextureFactory.Circle, (lit ? UI.Accent : new Color(0.1f, 0.1f, 0.12f)).WithAlpha(alpha * (lit ? 0.9f : 0.6f)));
                UI.Tex(b.Rect, TextureFactory.Ring, Color.white.WithAlpha(alpha * 0.5f));
                var icon = IconFactory.Get(b.Icon);
                float isz = Mathf.Min(b.Rect.width, b.Rect.height) * 0.38f;
                if (icon != null && b.Rect.height > 70) UI.Tex(new Rect(b.Rect.center.x - isz / 2, b.Rect.center.y - isz * 0.8f, isz, isz), icon, Color.white.WithAlpha(alpha + 0.2f));
                UI.Label(new Rect(b.Rect.x, b.Rect.height > 70 ? b.Rect.center.y + isz * 0.25f : b.Rect.y, b.Rect.width, b.Rect.height > 70 ? 24 : b.Rect.height), b.Label, b.Rect.height > 70 ? 14 : 13, Color.white.WithAlpha(alpha + 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            // Stick
            if (stickFinger != int.MinValue)
            {
                UI.Tex(new Rect(stickOrigin.x - 90, stickOrigin.y - 90, 180, 180), TextureFactory.Ring, Color.white.WithAlpha(alpha * 0.6f));
                var knob = stickOrigin + Vector2.ClampMagnitude(stickPos - stickOrigin, 90f);
                UI.Tex(new Rect(knob.x - 40, knob.y - 40, 80, 80), TextureFactory.Circle, Color.white.WithAlpha(alpha * 0.7f));
            }
            else
            {
                var hint = new Vector2(220, UI.VH - 220);
                UI.Tex(new Rect(hint.x - 90, hint.y - 90, 180, 180), TextureFactory.Ring, Color.white.WithAlpha(alpha * 0.25f));
                UI.Label(new Rect(hint.x - 90, hint.y - 15, 180, 30), "MOVE", 14, Color.white.WithAlpha(alpha * 0.5f), TextAnchor.MiddleCenter);
            }
        }
    }
}
