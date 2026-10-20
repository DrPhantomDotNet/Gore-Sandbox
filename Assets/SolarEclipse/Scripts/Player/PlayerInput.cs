using UnityEngine;

namespace SolarEclipse
{
    /// <summary>One frame of player intent from keyboard/mouse and the on-screen touch controls.</summary>
    public struct InputFrame
    {
        public Vector2 Move, Look;
        public bool Jump, JumpHeld, Sprint, Crouch;
        public bool Fire, FireHeld, FireUp, Aim, AimPressed, Middle;
        public bool Reload, ReloadHeld, Inspect, Use, Kick, Mode, Noclip, Delete, Undo, ScaleUp, ScaleDown, Menu;
        public int Slot;          // -1 none
        public float Scroll;      // + up / - down
    }

    /// <summary>Collects input every frame. Gameplay reads <see cref="Frame"/>; touch controls write into the Touch* fields.</summary>
    public static class PlayerInput
    {
        public static InputFrame Frame;
        public static bool Enabled = true;

        // Written by MobileControls each frame.
        public static Vector2 TouchMove, TouchLook;
        public static bool TouchFire, TouchAim, TouchJump, TouchCrouch, TouchSprint, TouchReload, TouchUse, TouchKick, TouchMiddle, TouchRotate, TouchMode, TouchInspect;
        public static float TouchScroll;
        public static int TouchSlot = -1;
        static bool prevTouchFire, prevTouchJump, prevTouchAim, crouchToggled, sprintToggled, aimToggled;

        public static void Update()
        {
            var f = new InputFrame { Slot = -1 };
            if (!Enabled || UIRoot.AnyBlocking || UIRoot.DevPanelCapturing)
            {
                Frame = f;
                ResetTouchEdges();
                return;
            }
            var d = Settings.Data;
            // Movement
            float x = 0, y = 0;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            f.Move = Vector2.ClampMagnitude(new Vector2(x, y) + TouchMove, 1f);

            float mx = Input.GetAxisRaw("Mouse X"), my = Input.GetAxisRaw("Mouse Y");
            f.Look = new Vector2(mx, my * (d.invertY ? -1f : 1f)) * d.mouseSensitivity + TouchLook * d.touchSensitivity;

            f.Jump = Input.GetKeyDown(KeyCode.Space) || (TouchJump && !prevTouchJump);
            f.JumpHeld = Input.GetKey(KeyCode.Space) || TouchJump;
            bool sprintKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (d.sprintToggle && Input.GetKeyDown(KeyCode.LeftShift)) sprintToggled = !sprintToggled;
            f.Sprint = (d.sprintToggle ? sprintToggled : sprintKey) || TouchSprint;
            bool crouchKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            if (d.crouchToggle && (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))) crouchToggled = !crouchToggled;
            f.Crouch = (d.crouchToggle ? crouchToggled : crouchKey) || TouchCrouch;

            f.Fire = Input.GetMouseButtonDown(0) || (TouchFire && !prevTouchFire);
            f.FireHeld = Input.GetMouseButton(0) || TouchFire;
            f.FireUp = Input.GetMouseButtonUp(0) || (!TouchFire && prevTouchFire);
            f.AimPressed = Input.GetMouseButtonDown(1) || (TouchAim && !prevTouchAim);
            if (d.aimToggle && f.AimPressed) aimToggled = !aimToggled;
            f.Aim = d.aimToggle ? aimToggled : (Input.GetMouseButton(1) || TouchAim);
            f.Middle = Input.GetMouseButtonDown(2) || TouchMiddle;
            f.Reload = Input.GetKeyDown(KeyCode.R) || TouchReload;
            f.ReloadHeld = Input.GetKey(KeyCode.R) || TouchRotate;
            f.Inspect = Input.GetKeyDown(KeyCode.Q) || TouchInspect;
            f.Use = Input.GetKeyDown(KeyCode.E) || TouchUse;
            f.Kick = Input.GetKeyDown(KeyCode.F) || TouchKick;
            f.Mode = Input.GetKeyDown(KeyCode.B) || TouchMode;
            f.Noclip = Input.GetKeyDown(KeyCode.V);
            f.Delete = Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.X);
            f.Undo = Input.GetKeyDown(KeyCode.Z);
            f.ScaleUp = Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus);
            f.ScaleDown = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus);
            for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) f.Slot = i;
            if (TouchSlot >= 0) f.Slot = TouchSlot;
            f.Scroll = Input.mouseScrollDelta.y + TouchScroll;
            Frame = f;
            ResetTouchEdges();
        }

        static void ResetTouchEdges()
        {
            prevTouchFire = TouchFire; prevTouchJump = TouchJump; prevTouchAim = TouchAim;
            TouchReload = TouchUse = TouchKick = TouchMiddle = TouchMode = TouchInspect = false;
            TouchScroll = 0; TouchSlot = -1; TouchLook = Vector2.zero;
        }
    }
}
