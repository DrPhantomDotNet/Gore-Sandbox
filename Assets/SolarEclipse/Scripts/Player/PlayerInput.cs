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
            if (VRRig.I != null) { Frame = VRFrame(); ResetTouchEdges(); return; }
            // Movement
            float x = 0, y = 0;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            f.Move = Vector2.ClampMagnitude(new Vector2(x, y) + TouchMove, 1f);

            // Phones/tablets: Unity turns the first finger into a pretend mouse, so the finger on the move stick
            // would also turn the camera (and "hold the fire button"). With touch controls, the mouse is ignored
            // here; MobileControls turns fingers into input instead.
            bool useMouse = !Settings.TouchControlsActive;
            float mx = useMouse ? Input.GetAxisRaw("Mouse X") : 0f, my = useMouse ? Input.GetAxisRaw("Mouse Y") : 0f;
            f.Look = new Vector2(mx, my * (d.invertY ? -1f : 1f)) * d.mouseSensitivity + TouchLook * d.touchSensitivity;

            f.Jump = Input.GetKeyDown(KeyCode.Space) || (TouchJump && !prevTouchJump);
            f.JumpHeld = Input.GetKey(KeyCode.Space) || TouchJump;
            bool sprintKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (d.sprintToggle && Input.GetKeyDown(KeyCode.LeftShift)) sprintToggled = !sprintToggled;
            f.Sprint = (d.sprintToggle ? sprintToggled : sprintKey) || TouchSprint;
            bool crouchKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            if (d.crouchToggle && (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))) crouchToggled = !crouchToggled;
            f.Crouch = (d.crouchToggle ? crouchToggled : crouchKey) || TouchCrouch;

            f.Fire = (useMouse && Input.GetMouseButtonDown(0)) || (TouchFire && !prevTouchFire);
            f.FireHeld = (useMouse && Input.GetMouseButton(0)) || TouchFire;
            f.FireUp = (useMouse && Input.GetMouseButtonUp(0)) || (!TouchFire && prevTouchFire);
            bool mouseAimDown = useMouse && Input.GetMouseButtonDown(1);
            if (d.aimToggle && mouseAimDown) aimToggled = !aimToggled;
            bool mouseAim = useMouse && (d.aimToggle ? aimToggled : Input.GetMouseButton(1));
            f.AimPressed = mouseAimDown || (TouchAim && !prevTouchAim);
            f.Aim = mouseAim || TouchAim; // touch: MobileControls already handles hold/toggle
            if (TouchAim) f.Sprint = false; // aiming always wins over (auto-)sprint, so you can aim while walking
            f.Middle = (useMouse && Input.GetMouseButtonDown(2)) || TouchMiddle;
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
            f.Scroll = (useMouse ? Input.mouseScrollDelta.y : 0f) + TouchScroll;
            Frame = f;
            ResetTouchEdges();
        }

        /// <summary>
        /// VR controllers (VR/VRRig.cs handles turning, grabbing, mags and melee itself):
        /// L stick move · L stick click sprint · X crouch · A jump · R trigger fire · B reload (guns without manual mags)
        /// · L trigger interact · R stick click kick. With the Spawn Gun: A spawns, B undoes, R stick click deletes.
        /// </summary>
        static InputFrame VRFrame()
        {
            var f = new InputFrame { Slot = -1 };
            var L = VRManager.Left; var R = VRManager.Right; var rig = VRRig.I;
            var mv = L.Stick;
            f.Move = mv.magnitude < 0.15f ? Vector2.zero : Vector2.ClampMagnitude(mv, 1f);
            bool spawnGun = PlayerController.Instance != null && PlayerController.Instance.Inventory != null && PlayerController.Instance.Inventory.Current is SpawnGun;
            f.Sprint = rig.Sprint;
            f.Crouch = rig.CrouchToggle;
            f.Fire = R.TriggerDown; f.FireHeld = R.TriggerHeld; f.FireUp = R.TriggerUp;
            f.Use = L.TriggerDown;
            if (spawnGun)
            {
                f.Middle = R.PrimaryDown;
                f.Undo = R.SecondaryDown;
                f.Delete = R.StickClickDown;
                float sy = R.Stick.y;
                bool holding = PlayerController.Instance.Inventory.Current is SpawnGun sg && sg.Holding;
                f.Scroll = holding && Mathf.Abs(sy) > 0.3f ? sy * Time.deltaTime * 6f : 0f; // push / pull what you hold
            }
            else
            {
                f.Jump = R.PrimaryDown; f.JumpHeld = R.Primary;
                var cur = PlayerController.Instance != null ? PlayerController.Instance.Inventory?.Current : null;
                bool manualMag = cur is Firearm fa && fa.VRManualMag;
                f.Reload = R.SecondaryDown && !manualMag;
                f.Kick = R.StickClickDown;
            }
            return f;
        }

        static void ResetTouchEdges()
        {
            prevTouchFire = TouchFire; prevTouchJump = TouchJump; prevTouchAim = TouchAim;
            TouchReload = TouchUse = TouchKick = TouchMiddle = TouchMode = TouchInspect = false;
            TouchScroll = 0; TouchSlot = -1; TouchLook = Vector2.zero;
        }
    }
}
