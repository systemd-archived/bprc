using System;
using UnityEngine;

#if !ENABLE_INPUT_SYSTEM
// Heat UI is written against the new Input System package (UnityEngine.InputSystem): eleven of its
// scripts touch Keyboard.current, Mouse.current, Gamepad.current or Touchscreen.current. This project
// does not have that package, and installing it would flip PlayerSettings' active input handling and
// put the game's own legacy Input.GetKey* calls at risk.
//
// So this file supplies the slice of the API Heat UI actually uses, on top of the legacy input the
// game already relies on:
//
//   Keyboard.current.enterKey.wasPressedThisFrame   -> legacy Return / KeypadEnter
//   Mouse.current.position.ReadValue()             -> legacy Input.mousePosition
//   Touchscreen.current.position.ReadValue()       -> legacy Input.mousePosition
//   Gamepad.current                                -> null, which is what Heat UI's own null checks
//                                                     already expect on a desktop machine
//   InputAction.triggered / IsInProgress / Enable() -> inert, for the serialized hotkey fields
//
// It lives in the HeatUI assembly, not Assembly-CSharp, and is behind #if !ENABLE_INPUT_SYSTEM: the
// moment the real package is installed and the new backend enabled, Unity defines ENABLE_INPUT_SYSTEM,
// these stand-ins disappear and Heat UI binds to the genuine types. Without that guard the two
// definitions would collide.
namespace UnityEngine.InputSystem
{
    /// <summary>Stand-in for a serialized input action; Heat UI only polls it and calls <c>Enable</c>.</summary>
    public class InputAction
    {
        public string name = string.Empty;

        /// <summary>Never fires: hotkeys need a gamepad, which this desktop shim reports as absent.</summary>
        public bool triggered => false;

        /// <summary>See <see cref="triggered"/>.</summary>
        public bool IsInProgress() => false;

        public void Enable() { }

        public void Disable() { }
    }

    public class KeyControl
    {
        public virtual bool wasPressedThisFrame =>
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        public virtual bool isPressed =>
            Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
    }

    public class Vector2Control
    {
        public virtual Vector2 ReadValue() => Input.mousePosition;
    }

    public class StickControl
    {
        public virtual float ReadValue() => 0f;
    }

    public class Stick
    {
        public StickControl x = new StickControl();
        public StickControl y = new StickControl();
    }

    public class Keyboard
    {
        private static readonly Keyboard s_current = new Keyboard();

        public static Keyboard current => s_current;

        public KeyControl enterKey = new KeyControl();
    }

    public class Mouse
    {
        private static readonly Mouse s_current = new Mouse();

        public static Mouse current => s_current;

        public Vector2Control position = new Vector2Control();
    }

    public class Touchscreen
    {
        private static readonly Touchscreen s_current = new Touchscreen();

        public static Touchscreen current => s_current;

        public Vector2Control position = new Vector2Control();
    }

    /// <summary>
    /// Null on purpose. Heat UI guards every <c>Gamepad.current</c> use with a null check and falls back
    /// to keyboard/mouse, which is the correct behaviour for a desktop build with no gamepad.
    /// </summary>
    public class Gamepad
    {
        public static Gamepad current => null;

        public string displayName = string.Empty;

        public Stick rightStick = new Stick();
    }
}

namespace UnityEngine.InputSystem.XInput
{
    /// <summary>Type only: exists so Heat UI's <c>Gamepad.current is XInputController</c> pattern compiles.</summary>
    public class XInputController : UnityEngine.InputSystem.Gamepad { }
}

namespace UnityEngine.InputSystem.DualShock
{
    /// <summary>Type only, for the same reason as <see cref="UnityEngine.InputSystem.XInput.XInputController"/>.</summary>
    public class DualSenseGamepadHID : UnityEngine.InputSystem.Gamepad { }
}
#endif
