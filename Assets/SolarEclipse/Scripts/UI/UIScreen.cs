using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A full-screen or modal UI page managed by <see cref="UIRoot"/>.</summary>
    public abstract class UIScreen
    {
        public float OpenedAt { get; private set; }
        public float Age => Time.unscaledTime - OpenedAt;
        /// <summary>Pauses gameplay input (and time if PausesTime) while open.</summary>
        public virtual bool BlocksGameplay => true;
        public virtual bool PausesTime => false;
        public virtual bool ShowCursor => true;
        /// <summary>When true the screens below stay visible (drawn disabled).</summary>
        public virtual bool IsModal => false;

        public void MarkOpened() => OpenedAt = Time.unscaledTime;
        public virtual void OnOpen() { }
        public virtual void OnClose() { }
        public virtual void Tick() { }
        public abstract void Draw();

        /// <summary>Esc/back pressed. Return true if handled.</summary>
        public virtual bool OnBack() { UIRoot.Pop(); return true; }

        /// <summary>Ease-out 0..1 for entrance animations.</summary>
        protected float Intro(float delay = 0f, float duration = 0.35f) => MathX.SmoothStep01((Age - delay) / duration);
    }
}
