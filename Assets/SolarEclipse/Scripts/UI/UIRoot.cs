using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Owns the screen stack and draws every UI layer in a fixed order each OnGUI.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public static UIRoot I { get; private set; }
        static readonly List<UIScreen> stack = new List<UIScreen>();

        /// <summary>Drawn under screens (in-game HUD).</summary>
        public static System.Action HudLayer;
        /// <summary>Drawn above everything except the dev panel (touch controls).</summary>
        public static System.Action OverlayLayer;
        /// <summary>Topmost (dev panel).</summary>
        public static System.Action TopLayer;

        public static UIScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public static int Count => stack.Count;
        public static bool AnyBlocking { get { foreach (var s in stack) if (s.BlocksGameplay) return true; return false; } }
        public static bool AnyPausesTime { get { foreach (var s in stack) if (s.PausesTime) return true; return false; } }
        public static bool CursorWanted { get { foreach (var s in stack) if (s.ShowCursor) return true; return false; } }
        public static bool DevPanelCapturing;

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("UIRoot");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<UIRoot>();
            go.AddComponent<Toasts>();
        }

        public static T Push<T>(T screen) where T : UIScreen
        {
            stack.Add(screen);
            screen.MarkOpened();
            screen.OnOpen();
            return screen;
        }

        public static void Pop()
        {
            if (stack.Count == 0) return;
            var s = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            s.OnClose();
        }

        public static void Remove(UIScreen s)
        {
            if (stack.Remove(s)) s.OnClose();
        }

        /// <summary>Clears the stack and shows a single screen.</summary>
        public static T Replace<T>(T screen) where T : UIScreen
        {
            while (stack.Count > 0) Pop();
            return Push(screen);
        }

        public static bool Contains<T>() where T : UIScreen
        {
            foreach (var s in stack) if (s is T) return true;
            return false;
        }

        public static T Find<T>() where T : UIScreen
        {
            foreach (var s in stack) if (s is T t) return t;
            return null;
        }

        void Update()
        {
            for (int i = stack.Count - 1; i >= 0; i--)
                if (i < stack.Count) stack[i].Tick();
        }

        /// <summary>Called by input code when Esc/back is pressed and a screen is open.</summary>
        public static bool HandleBack()
        {
            var top = Top;
            return top != null && top.OnBack();
        }

        void OnGUI()
        {
            using (NetState.Local()) DrawAll(); // clicks in menus are the local player's own actions
        }

        void DrawAll()
        {
            UI.BeginFrame();
            GUI.depth = 0;
            HudLayer?.Invoke();

            // Only draw from the last non-modal screen upward.
            int start = 0;
            for (int i = stack.Count - 1; i >= 0; i--)
                if (!stack[i].IsModal) { start = i; break; }
            for (int i = start; i < stack.Count; i++)
            {
                bool top = i == stack.Count - 1;
                GUI.enabled = top && !DevPanelCapturing;
                stack[i].Draw();
            }
            GUI.enabled = true;
            OverlayLayer?.Invoke();
            Toasts.Draw();
            TopLayer?.Invoke();
            UI.DrawTooltip();
        }
    }
}
