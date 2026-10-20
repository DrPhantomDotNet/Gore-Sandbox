using System;
using UnityEngine;

namespace SolarEclipse
{
    public sealed class ConfirmDialog : UIScreen
    {
        readonly string title, body, confirmLabel;
        readonly Action onConfirm;
        readonly ButtonKind kind;

        public ConfirmDialog(string title, string body, Action onConfirm, string confirmLabel = "OK", ButtonKind kind = ButtonKind.Primary)
        { this.title = title; this.body = body; this.onConfirm = onConfirm; this.confirmLabel = confirmLabel; this.kind = kind; }

        public override bool IsModal => true;

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.55f * a);
            var r = UI.Centered(620, 280);
            r.y += (1 - a) * 30;
            UI.Panel(r, title, a);
            UI.Label(new Rect(r.x + 30, r.y + 84, r.width - 60, 100), body, 24, UI.TextColor.WithAlpha(a), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 260, 60), "CANCEL", ButtonKind.Normal)) UIRoot.Pop();
            if (UI.Button(new Rect(r.xMax - 290, r.yMax - 86, 260, 60), confirmLabel, kind)) { UIRoot.Pop(); onConfirm?.Invoke(); }
        }
    }
}
