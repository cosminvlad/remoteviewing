using RemoteViewing.ServerExample.Native;
using RemoteViewing.Vnc;
using System;

namespace RemoteViewing.ServerExample
{
    /// <summary>
    /// Provides a framebuffer with pixels copied from the screen.
    /// </summary>
    public partial class ScreenFramebufferSource : IVncRemoteKeyboard, IVncRemoteController
    {
        [Flags]
        private enum X11PressedButtons
        {
            None = 0,
            Left = 1,
            Middle = 2,
            Right = 4,
            WheelUp = 8,
            WheelDown = 16,
        }

        private const int WHEEL_DELTA = 120;

        private bool MouseLeftDown { get; set; }

        private bool MouseMiddleDown { get; set; }

        private bool MouseRightDown { get; set; }

        private const int UNICODE_FLAG = 0x1000000;

        public unsafe void HandleTouchEvent(object sender, PointerChangedEventArgs e)
        {
            //User32.SetCursorPos(e.X, e.Y);
            var mouseInput = new MOUSEINPUT
            {
                dx = (int)(((double)e.X / captureDevice.Size.Width) * 65535),
                dy = (int)(((double)e.Y / captureDevice.Size.Height) * 65535),
                dwFlags = MOUSEEVENTF.ABSOLUTE | MOUSEEVENTF.MOVE,
            };

            if (((X11PressedButtons)e.PressedButtons).HasFlag(X11PressedButtons.Left))
            {
                mouseInput.dwFlags &= MOUSEEVENTF.LEFTDOWN;
                MouseLeftDown = true;
            }
            else if (MouseLeftDown)
            {
                mouseInput.dwFlags &= MOUSEEVENTF.LEFTUP;
                MouseLeftDown = false;
            }

            if (((X11PressedButtons)e.PressedButtons).HasFlag(X11PressedButtons.Middle))
            {
                mouseInput.dwFlags &= MOUSEEVENTF.MIDDLEDOWN;
                MouseMiddleDown = true;
            }
            else if (MouseMiddleDown)
            {
                mouseInput.dwFlags &= MOUSEEVENTF.MIDDLEUP;
                MouseMiddleDown = false;
            }

            if (((X11PressedButtons)e.PressedButtons).HasFlag(X11PressedButtons.Right))
            {
                mouseInput.dwFlags &= MOUSEEVENTF.RIGHTDOWN;
                MouseRightDown = true;
            }
            else if (MouseRightDown)
            {
                mouseInput.dwFlags &= MOUSEEVENTF.RIGHTUP;
                MouseRightDown = false;
            }

            if (((X11PressedButtons)e.PressedButtons).HasFlag(X11PressedButtons.WheelUp))
            {
                mouseInput.dwFlags &= MOUSEEVENTF.WHEEL;
                mouseInput.mouseData = WHEEL_DELTA;
            }

            if (((X11PressedButtons)e.PressedButtons).HasFlag(X11PressedButtons.WheelDown))
            {
                mouseInput.dwFlags &= MOUSEEVENTF.WHEEL;
                mouseInput.mouseData = -WHEEL_DELTA;
            }

            User32.SendInput(1, new[]
            {
                new INPUT
                {
                    type = InputType.INPUT_MOUSE,
                    U = new InputUnion
                    {
                        mi = mouseInput,
                    },
                },
            }, sizeof(INPUT));
        }

        /// <inheritdoc/>
        public unsafe void HandleKeyEvent(object sender, KeyChangedEventArgs e)
        {
            var virtualKeyShort = GetVirtualKeyCode(e.Keysym);

            var keyboardInput = new KEYBDINPUT();

            if (virtualKeyShort != 0)
            {
                keyboardInput.wVk = virtualKeyShort;
            }
            else if (((int)e.Keysym & UNICODE_FLAG) == UNICODE_FLAG)
            {
                keyboardInput.wScan = (ScanCodeShort)((int)e.Keysym & ~UNICODE_FLAG);
                keyboardInput.dwFlags |= KEYEVENTF.UNICODE;
            }
            else
            {
                var intSym = (int)e.Keysym;
                if ((intSym >= 32 && intSym <= 126) || (intSym >= 160 && intSym <= 255))
                {
                    keyboardInput.wScan = (ScanCodeShort)e.Keysym;
                    keyboardInput.dwFlags |= KEYEVENTF.UNICODE;
                }
            }

            //if (MapToVirtualKey(e.Keysym, out VirtualKeyShort keyCode, out bool extended))
            //{
            //    keyboardInput.wScan = User32.MapVirtualKey(keyCode, 0);
            //    if (extended)
            //    {
            //        keyboardInput.dwFlags |= KEYEVENTF.EXTENDEDKEY;
            //    }
            //}
            //else if (((int)e.Keysym & UNICODE_FLAG) == UNICODE_FLAG)
            //{
            //    keyboardInput.wScan = (ScanCodeShort)((int)e.Keysym & ~UNICODE_FLAG);
            //    keyboardInput.dwFlags |= KEYEVENTF.UNICODE;
            //}
            //else
            //{
            //    var intSym = (int)e.Keysym;
            //    if ((intSym >= 32 && intSym <= 126) || (intSym >= 160 && intSym <= 255))
            //    {
            //        keyboardInput.wScan = (ScanCodeShort)e.Keysym;
            //        keyboardInput.dwFlags |= KEYEVENTF.UNICODE;
            //    }
            //}


            //If pvMapToVirtualKey(uEvent.Key, uInput.wVk, bExtended) Then
            //uInput.wScan = MapVirtualKey(uInput.wVk, 0)
            //If bExtended Then
            //uInput.dwFlags = uInput.dwFlags Or KEYEVENTF_EXTENDEDKEY
            //End If
            //ElseIf(uEvent.Key And UNICODE_FLAG) <> 0 Then
            //uInput.wScan = uEvent.Key And Not UNICODE_FLAG
            //uInput.dwFlags = uInput.dwFlags Or KEYEVENTF_UNICODE
            //Else
            //    Select Case uEvent.Key
            //    Case 32 To 126, 160 To 255
            //uInput.wScan = uEvent.Key
            //uInput.dwFlags = uInput.dwFlags Or KEYEVENTF_UNICODE
            //End Select
            //End If

            if (!e.Pressed)
            {
                keyboardInput.dwFlags |= KEYEVENTF.KEYUP;
            }

            User32.SendInput(1, new[]
            {
                new INPUT
                {
                    type = InputType.INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = keyboardInput,
                    },
                },
            }, sizeof(INPUT));
        }

        private bool MapToVirtualKey(KeySym keySym, out VirtualKeyShort virtualKeyShort, out bool extended)
        {
            throw new NotImplementedException();
        }

        private VirtualKeyShort GetVirtualKeyCode(KeySym keySym)
        {
            return keySym switch
            {
                //KeySym.VoidSymbol => VirtualKeyShort.,
                KeySym.Backspace => VirtualKeyShort.BACK,
                KeySym.Tab => VirtualKeyShort.TAB,
                //KeySym.LineFeed => VirtualKeyShort.,
                KeySym.Clear => VirtualKeyShort.CLEAR,
                KeySym.Return => VirtualKeyShort.RETURN,
                KeySym.Pause => VirtualKeyShort.PAUSE,
                KeySym.ScrollLock => VirtualKeyShort.SCROLL,
                //KeySym.SysReq => VirtualKeyShort.,
                KeySym.Escape => VirtualKeyShort.ESCAPE,
                KeySym.Delete => VirtualKeyShort.DELETE,
                KeySym.Home => VirtualKeyShort.HOME,
                KeySym.Left => VirtualKeyShort.LEFT,
                KeySym.Up => VirtualKeyShort.UP,
                KeySym.Right => VirtualKeyShort.RIGHT,
                KeySym.Down => VirtualKeyShort.DOWN,
                KeySym.Prior => VirtualKeyShort.PRIOR,
                KeySym.Next => VirtualKeyShort.NEXT,
                KeySym.End => VirtualKeyShort.END,
                KeySym.Begin => VirtualKeyShort.HOME,
                KeySym.Select => VirtualKeyShort.SELECT,
                KeySym.Print => VirtualKeyShort.PRINT,
                KeySym.Execute => VirtualKeyShort.EXECUTE,
                KeySym.Insert => VirtualKeyShort.INSERT,
                //KeySym.Undo => VirtualKeyShort.,
                //KeySym.Redo => VirtualKeyShort.,
                KeySym.Menu => VirtualKeyShort.MENU,
                KeySym.Find => VirtualKeyShort.BROWSER_SEARCH,
                KeySym.Cancel => VirtualKeyShort.CANCEL,
                KeySym.Help => VirtualKeyShort.HELP,
                KeySym.Break => VirtualKeyShort.CANCEL,
                KeySym.ModeSwitch => VirtualKeyShort.MODECHANGE,
                KeySym.Num_Lock => VirtualKeyShort.NUMLOCK,
                KeySym.NumPadSpace => VirtualKeyShort.SPACE,
                KeySym.NumPadTab => VirtualKeyShort.TAB,
                KeySym.NumPadEnter => VirtualKeyShort.RETURN,
                //KeySym.NumPadF1 => expr,
                //KeySym.NumPadF2 => expr,
                //KeySym.NumPadF3 => expr,
                //KeySym.NumPadF4 => expr,
                //KeySym.NumPadHome => expr,
                KeySym.NumPadLeft => VirtualKeyShort.LEFT,
                KeySym.NumPadUp => VirtualKeyShort.UP,
                KeySym.NumPadRight => VirtualKeyShort.RIGHT,
                KeySym.NumPadDown => VirtualKeyShort.DOWN,
                KeySym.NumPadPrior => VirtualKeyShort.PRIOR,
                KeySym.NumPadNext => VirtualKeyShort.NEXT,
                KeySym.NumPadEnd => VirtualKeyShort.END,
                KeySym.NumPadBegin => VirtualKeyShort.HOME,
                //KeySym.NumPadInsert => expr,
                //KeySym.NumPadDelete => expr,
                //KeySym.NumPadEqual => expr,
                KeySym.NumPadMultiply => VirtualKeyShort.MULTIPLY,
                KeySym.NumPadAdd => VirtualKeyShort.ADD,
                KeySym.NumPadSeparator => VirtualKeyShort.SEPARATOR,
                KeySym.NumPadSubtract => VirtualKeyShort.SUBTRACT,
                KeySym.NumPadDecimal => VirtualKeyShort.DECIMAL,
                KeySym.NumPadDivide => VirtualKeyShort.DIVIDE,
                KeySym.NumPad0 => VirtualKeyShort.NUMPAD0,
                KeySym.NumPad1 => VirtualKeyShort.NUMPAD1,
                KeySym.NumPad2 => VirtualKeyShort.NUMPAD2,
                KeySym.NumPad3 => VirtualKeyShort.NUMPAD3,
                KeySym.NumPad4 => VirtualKeyShort.NUMPAD4,
                KeySym.NumPad5 => VirtualKeyShort.NUMPAD5,
                KeySym.NumPad6 => VirtualKeyShort.NUMPAD6,
                KeySym.NumPad7 => VirtualKeyShort.NUMPAD7,
                KeySym.NumPad8 => VirtualKeyShort.NUMPAD8,
                KeySym.NumPad9 => VirtualKeyShort.NUMPAD9,
                KeySym.F1 => VirtualKeyShort.F1,
                KeySym.F2 => VirtualKeyShort.F2,
                KeySym.F3 => VirtualKeyShort.F3,
                KeySym.F4 => VirtualKeyShort.F4,
                KeySym.F5 => VirtualKeyShort.F5,
                KeySym.F6 => VirtualKeyShort.F6,
                KeySym.F7 => VirtualKeyShort.F7,
                KeySym.F8 => VirtualKeyShort.F8,
                KeySym.F9 => VirtualKeyShort.F9,
                KeySym.F10 => VirtualKeyShort.F10,
                KeySym.F11 => VirtualKeyShort.F11,
                KeySym.F12 => VirtualKeyShort.F12,
                KeySym.F13 => VirtualKeyShort.F13,
                KeySym.F14 => VirtualKeyShort.F14,
                KeySym.F15 => VirtualKeyShort.F15,
                KeySym.F16 => VirtualKeyShort.F16,
                KeySym.F17 => VirtualKeyShort.F17,
                KeySym.F18 => VirtualKeyShort.F18,
                KeySym.F19 => VirtualKeyShort.F19,
                KeySym.F20 => VirtualKeyShort.F20,
                KeySym.F21 => VirtualKeyShort.F21,
                KeySym.F22 => VirtualKeyShort.F22,
                KeySym.F23 => VirtualKeyShort.F23,
                KeySym.F24 => VirtualKeyShort.F24,
                KeySym.ShiftLeft => VirtualKeyShort.SHIFT,
                KeySym.ShiftRight => VirtualKeyShort.RSHIFT,
                KeySym.ControlLeft => VirtualKeyShort.CONTROL,
                KeySym.ControlRight => VirtualKeyShort.RCONTROL,
                KeySym.CapsLock => VirtualKeyShort.CAPITAL,
                //KeySym.ShiftLock => VirtualKeyShort.,
                //KeySym.MetaLeft => ,
                //KeySym.MetaRight => expr,
                KeySym.AltLeft => VirtualKeyShort.MENU,
                KeySym.AltRight => VirtualKeyShort.RMENU,
                //KeySym.SuperLeft => expr,
                //KeySym.SuperRight => expr,
                //KeySym.HyperLeft => expr,
                //KeySym.HyperRight => expr,
                KeySym.Space => VirtualKeyShort.SPACE,
                //KeySym.Exclamation => expr,
                //KeySym.Quote => expr,
                //KeySym.NumberSign => expr,
                //KeySym.Dollar => expr,
                //KeySym.Percent => expr,
                //KeySym.Ampersand => VirtualKeyShort.,
                //KeySym.Apostrophe => VirtualKeyShort.,
                //KeySym.ParenthesisLeft => VirtualKeyShort.,
                //KeySym.ParenthesisRight => expr,
                //KeySym.Asterisk => VirtualKeyShort.,
                KeySym.Plus => VirtualKeyShort.OEM_PLUS,
                KeySym.Comma => VirtualKeyShort.OEM_COMMA,
                KeySym.Minus => VirtualKeyShort.OEM_MINUS,
                //KeySym.Period => expr,
                //KeySym.Slash => expr,
                //KeySym.D0 => expr,
                //KeySym.D1 => expr,
                //KeySym.D2 => expr,
                //KeySym.D3 => expr,
                //KeySym.D4 => expr,
                //KeySym.D5 => expr,
                //KeySym.D6 => expr,
                //KeySym.D7 => expr,
                //KeySym.D8 => expr,
                //KeySym.D9 => expr,
                //KeySym.Colon => expr,
                //KeySym.Semicolon => expr,
                //KeySym.Less => expr,
                //KeySym.Equal => expr,
                //KeySym.Greater => expr,
                //KeySym.Question => expr,
                //KeySym.At => VirtualKeyShort.,
                KeySym.A => VirtualKeyShort.KEY_A,
                KeySym.B => VirtualKeyShort.KEY_B,
                KeySym.C => VirtualKeyShort.KEY_C,
                KeySym.D => VirtualKeyShort.KEY_D,
                KeySym.E => VirtualKeyShort.KEY_E,
                KeySym.F => VirtualKeyShort.KEY_F,
                KeySym.G => VirtualKeyShort.KEY_G,
                KeySym.H => VirtualKeyShort.KEY_H,
                KeySym.I => VirtualKeyShort.KEY_I,
                KeySym.J => VirtualKeyShort.KEY_J,
                KeySym.K => VirtualKeyShort.KEY_K,
                KeySym.L => VirtualKeyShort.KEY_L,
                KeySym.M => VirtualKeyShort.KEY_M,
                KeySym.N => VirtualKeyShort.KEY_N,
                KeySym.O => VirtualKeyShort.KEY_O,
                KeySym.P => VirtualKeyShort.KEY_P,
                KeySym.Q => VirtualKeyShort.KEY_Q,
                KeySym.R => VirtualKeyShort.KEY_R,
                KeySym.S => VirtualKeyShort.KEY_S,
                KeySym.T => VirtualKeyShort.KEY_T,
                KeySym.U => VirtualKeyShort.KEY_U,
                KeySym.V => VirtualKeyShort.KEY_V,
                KeySym.W => VirtualKeyShort.KEY_W,
                KeySym.X => VirtualKeyShort.KEY_X,
                KeySym.Y => VirtualKeyShort.KEY_Y,
                KeySym.Z => VirtualKeyShort.KEY_Z,
                //KeySym.BracketLeft => expr,
                //KeySym.Backslash => expr,
                //KeySym.Bracketright => expr,
                //KeySym.AsciiCircum => expr,
                //KeySym.Underscore => expr,
                //KeySym.Grave => VirtualKeyShort.,
                //KeySym.a => expr,
                //KeySym.b => expr,
                //KeySym.c => expr,
                //KeySym.d => expr,
                //KeySym.e => expr,
                //KeySym.f => expr,
                //KeySym.g => expr,
                //KeySym.h => expr,
                //KeySym.i => expr,
                //KeySym.j => expr,
                //KeySym.k => expr,
                //KeySym.l => expr,
                //KeySym.m => expr,
                //KeySym.n => expr,
                //KeySym.o => expr,
                //KeySym.p => expr,
                //KeySym.q => expr,
                //KeySym.r => expr,
                //KeySym.s => expr,
                //KeySym.t => expr,
                //KeySym.u => expr,
                //KeySym.v => expr,
                //KeySym.w => expr,
                //KeySym.x => expr,
                //KeySym.y => expr,
                //KeySym.z => expr,
                //KeySym.BraceLeft => expr,
                //KeySym.Bar => expr,
                //KeySym.BraceRight => expr,
                //KeySym.AsciiTilde => expr,
                _ => (VirtualKeyShort)0
            };
        }
    }
}
