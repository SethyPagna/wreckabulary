using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wreckabulary
{
    /// <summary>What a player asked to do this frame. Edge flags (grab, attack, …) are true for one frame only.</summary>
    public struct PlayerCommands
    {
        public Vector2 move;
        public bool grab, attack;
        public bool spellHeld, spellDown, spellUp;
        public bool up, down;
        /// <summary>Spelling: move between your letters, add the highlighted one, undo the last one.</summary>
        public bool left, right, confirm, back;
        public bool start;
    }

    /// <summary>One player's input source: a gamepad, half a keyboard, or a script in tests.</summary>
    public abstract class InputBinding
    {
        public abstract string Id { get; }
        public abstract void Read(ref PlayerCommands c);
        public abstract bool JoinPressed();
        public abstract bool StartPressed();
        /// <summary>Key names for the spelling controls, shown under the word being spelled.</summary>
        public abstract string SpellHelp { get; }
    }

    /// <summary>
    /// Two players can share a keyboard.
    /// Left: WASD move, Space grab, J attack, K spell (W/S choose).
    /// Right: arrows move, . or Numpad1 grab, / or Numpad2 attack, Right Shift or Numpad3 spell.
    /// </summary>
    public class KeyboardBinding : InputBinding
    {
        public enum Side { Left, Right }

        readonly Side side;
        public KeyboardBinding(Side side) => this.side = side;

        public override string Id => $"keyboard-{side}";

        static float Axis(KeyControl positive, KeyControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);

        public override void Read(ref PlayerCommands c)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (side == Side.Left)
            {
                c.move = new Vector2(Axis(kb.dKey, kb.aKey), Axis(kb.wKey, kb.sKey));
                c.grab = kb.spaceKey.wasPressedThisFrame;
                c.attack = kb.jKey.wasPressedThisFrame;
                c.spellHeld = kb.kKey.isPressed;
                c.spellDown = kb.kKey.wasPressedThisFrame;
                c.spellUp = kb.kKey.wasReleasedThisFrame;
                c.up = kb.wKey.wasPressedThisFrame;
                c.down = kb.sKey.wasPressedThisFrame;
                c.left = kb.aKey.wasPressedThisFrame;
                c.right = kb.dKey.wasPressedThisFrame;
                c.confirm = kb.spaceKey.wasPressedThisFrame;
                c.back = kb.jKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                c.start = kb.enterKey.wasPressedThisFrame;
            }
            else
            {
                c.move = new Vector2(Axis(kb.rightArrowKey, kb.leftArrowKey), Axis(kb.upArrowKey, kb.downArrowKey));
                c.grab = kb.periodKey.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
                c.attack = kb.slashKey.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
                c.spellHeld = kb.rightShiftKey.isPressed || kb.numpad3Key.isPressed;
                c.spellDown = kb.rightShiftKey.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
                c.spellUp = !c.spellHeld && (kb.rightShiftKey.wasReleasedThisFrame || kb.numpad3Key.wasReleasedThisFrame);
                c.up = kb.upArrowKey.wasPressedThisFrame;
                c.down = kb.downArrowKey.wasPressedThisFrame;
                c.left = kb.leftArrowKey.wasPressedThisFrame;
                c.right = kb.rightArrowKey.wasPressedThisFrame;
                c.confirm = c.grab;
                c.back = c.attack;
                c.start = kb.numpadEnterKey.wasPressedThisFrame;
            }
            if (c.move.sqrMagnitude > 1f) c.move.Normalize();
        }

        public override bool JoinPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return side == Side.Left
                ? kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame
                : kb.periodKey.wasPressedThisFrame || kb.slashKey.wasPressedThisFrame ||
                  kb.numpad1Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
        }

        public override bool StartPressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }

        public override string SpellHelp => side == Side.Left
            ? "A/D choose  •  SPACE add  •  J undo  •  S drop  •  K cast"
            : "LEFT/RIGHT choose  •  . add  •  / undo  •  DOWN drop  •  R-SHIFT cast";
    }

    /// <summary>Left stick move, A or RT grab, X attack, hold Y to spell (stick or d-pad up/down to choose).</summary>
    public class GamepadBinding : InputBinding
    {
        public readonly Gamepad Pad;
        float lastStickX, lastStickY;

        public GamepadBinding(Gamepad pad) => Pad = pad;

        public override string Id => $"gamepad-{Pad.deviceId}";

        public override void Read(ref PlayerCommands c)
        {
            if (Pad == null || !Pad.added) return;

            var stick = Pad.leftStick.ReadValue();
            if (stick.magnitude < 0.2f) stick = Vector2.zero;
            c.move = Vector2.ClampMagnitude(stick + Pad.dpad.ReadValue(), 1f);
            c.grab = Pad.buttonSouth.wasPressedThisFrame || Pad.rightTrigger.wasPressedThisFrame;
            c.attack = Pad.buttonWest.wasPressedThisFrame;
            c.spellHeld = Pad.buttonNorth.isPressed;
            c.spellDown = Pad.buttonNorth.wasPressedThisFrame;
            c.spellUp = Pad.buttonNorth.wasReleasedThisFrame;

            // A flick of the stick counts as one step through the word wheel.
            float x = stick.x, y = stick.y;
            c.up = Pad.dpad.up.wasPressedThisFrame || (y > 0.6f && lastStickY <= 0.6f);
            c.down = Pad.dpad.down.wasPressedThisFrame || (y < -0.6f && lastStickY >= -0.6f);
            c.left = Pad.dpad.left.wasPressedThisFrame || (x < -0.6f && lastStickX >= -0.6f);
            c.right = Pad.dpad.right.wasPressedThisFrame || (x > 0.6f && lastStickX <= 0.6f);
            c.confirm = Pad.buttonSouth.wasPressedThisFrame;
            c.back = Pad.buttonEast.wasPressedThisFrame;
            lastStickX = x;
            lastStickY = y;
            c.start = Pad.startButton.wasPressedThisFrame;
        }

        public override bool JoinPressed() =>
            Pad.added && (Pad.buttonSouth.wasPressedThisFrame || Pad.buttonWest.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame);

        public override bool StartPressed() => Pad.added && Pad.startButton.wasPressedThisFrame;

        public override string SpellHelp => "left/right choose  •  (A) add  •  (B) undo  •  down drop  •  (Y) cast";
    }

    /// <summary>Input driven by code, for tests and bots. Edge flags clear after each read.</summary>
    public class ScriptedBinding : InputBinding
    {
        static int count;
        readonly string id = $"scripted-{count++}";
        public PlayerCommands Next;

        public override string Id => id;

        public override void Read(ref PlayerCommands c)
        {
            c = Next;
            Next.grab = Next.attack = Next.spellDown = Next.spellUp = Next.up = Next.down = Next.start = false;
            Next.left = Next.right = Next.confirm = Next.back = false;
        }

        public override bool JoinPressed() => false;
        public override bool StartPressed() => false;
        public override string SpellHelp => "";
    }
}
