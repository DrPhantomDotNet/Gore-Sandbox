using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Bullet time (B): singleplayer slow motion at <see cref="SlowScale"/> with a smooth ramp, a blue tint and vignette,
    /// and a "BULLET TIME" indicator. <see cref="GameManager"/> multiplies its time scale by <see cref="Step"/> each frame
    /// (on top of Rules.TimeScale and kill slow-mo; pause still wins). The local player is compensated through
    /// <see cref="PlayerDtScale"/> so looking and moving stay full speed. One-shot sounds already drop pitch with
    /// Time.timeScale in AudioManager.Play. Multiplayer never changes time, so it is refused there.
    /// </summary>
    public static class BulletTime
    {
        public const float SlowScale = 0.25f;
        public static bool Active { get; private set; }
        static float factor = 1f;
        static bool hooked;

        /// <summary>Current bullet-time multiplier (1 = off, ramps down to SlowScale).</summary>
        public static float Factor => factor;
        /// <summary>Multiply the local player's scaled deltaTime by this so they stay responsive.</summary>
        public static float PlayerDtScale => factor < 0.999f ? 1f / factor : 1f;

        /// <summary>
        /// Gameplay hotkeys (called by GameManager while playing with no menu open): K always toggles; B toggles
        /// too unless the held weapon already uses B (auto firearms' fire mode, Admin Gun modes).
        /// </summary>
        public static void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.K)) { Toggle(); return; }
            if (!Input.GetKeyDown(KeyCode.B)) return;
            var p = PlayerController.Instance;
            var w = p != null && p.Inventory != null ? p.Inventory.Current : null;
            bool weaponUsesB = w != null && ((w is Firearm && w.Def.Auto) || w is AdminGun);
            if (!weaponUsesB) Toggle();
        }

        /// <summary>Touch button / console entry point.</summary>
        public static void Toggle()
        {
            if (NetState.Active)
            {
                GameEvents.ShowToast("Bullet time is singleplayer only (multiplayer never changes time).");
                AudioManager.Ui("ui_error", 0.5f);
                return;
            }
            Set(!Active);
        }

        public static void Set(bool on)
        {
            if (on && NetState.Active) return;
            if (on == Active) return;
            Active = on;
            Hook();
            AudioManager.Play2D(on ? "bt_in" : "bt_out", 0.7f, 1f, AudioBus.Sfx);
            if (on) Stats.Add("bullet_time");
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            UIRoot.HudLayer += Draw;
        }

        /// <summary>Advances the ramp (unscaled time) and returns the multiplier for Time.timeScale.</summary>
        public static float Step()
        {
            var gm = GameManager.I;
            if (Active && (NetState.Active || gm == null || gm.State != GameState.Playing)) Active = false;
            float target = Active ? SlowScale : 1f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            factor = Mathf.MoveTowards(factor, target, dt * 2.5f); // ~0.3 s ramp
            if (Mathf.Abs(factor - target) < 0.001f) factor = target;
            return factor;
        }

        static void Draw()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing || factor > 0.999f) return;
            float k = Mathf.Clamp01((1f - factor) / (1f - SlowScale));
            var full = new Rect(0, 0, UI.VW, UI.VH);
            UI.Fill(full, new Color(0.15f, 0.35f, 1f, 0.07f * k));
            UI.Tex(full, TextureFactory.Vignette, new Color(0.02f, 0.06f, 0.2f, 0.75f * k));
            if (gm.HudHidden || PhotoMode.Active) return;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            var r = new Rect(UI.VW / 2f - 150, 118, 300, 44);
            UI.Rounded(r, new Color(0.05f, 0.1f, 0.25f, 0.6f * k));
            UI.Label(r, "BULLET TIME", 26, new Color(0.55f, 0.8f, 1f, k * pulse), TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }
}
