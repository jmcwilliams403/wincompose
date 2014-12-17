﻿// Copyright: (c) 2013-2014 Sam Hocevar <sam@hocevar.net>
//   This program is free software; you can redistribute it and/or
//   modify it under the terms of the Do What The Fuck You Want To
//   Public License, Version 2, as published by the WTFPL Task Force.
//   See http://www.wtfpl.net/ for more details.using System;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace wincompose
{

static class compose
{
    // Get input from the keyboard hook; return true if the key was handled
    // and needs to be removed from the input chain.
    public static bool on_key(WM ev, VK vk, SC sc, uint flags)
    {
        bool is_keydown = (ev == WM.KEYDOWN || ev == WM.SYSKEYDOWN);
        bool is_keyup = !is_keydown;
        bool is_compose = settings.is_compose_key(vk);

        if (is_compose)
        {
            if (is_keyup)
            {
                m_compose_down = false;
            }
            else if (is_keydown && !m_compose_down)
            {
                m_compose_down = true;
                m_composing = !m_composing;
                if (!m_composing)
                    m_sequence = "";
            }
            return true;
        }

        if (!m_composing)
            return false;

        string str = null;

        // Generate a keystate suitable for ToUnicode()
        GetKeyboardState(m_keystate);
        bool has_shift = (GetKeyState(VK.SHIFT) & 0x80) == 0x80;
        bool has_altgr = (GetKeyState(VK.RMENU) & 0x80) == 0x80
                          && (GetKeyState(VK.LCONTROL) & 0x80) == 0x80;
        bool has_capslock = GetKeyState(VK.CAPITAL) != 0;
        m_keystate[0x10] = has_shift ? (byte)0x80 : (byte)0x00;
        m_keystate[0x11] = has_altgr ? (byte)0x80 : (byte)0x00;
        m_keystate[0x12] = has_altgr ? (byte)0x80 : (byte)0x00;
        m_keystate[0x14] = has_capslock ? (byte)0x01 : (byte)0x00;

        int buflen = 4;
        byte[] buf = new byte[2 * buflen];
        int ret = ToUnicode(vk, sc, m_keystate, buf, buflen, flags);
        if (ret > 0 && ret < buflen)
        {
            buf[ret * 2] = buf[ret * 2 + 1] = 0x00; // Null-terminate the string
            str = Encoding.Unicode.GetString(buf, 0, ret + 1);
        }

        // Don't know what to do with these for now, but we need to handle some
        // of them, for instance the arrow keys
        if (str == null)
            return false;

        // Finally, this is a key we must add to our compose sequence and
        // decide whether we'll eventually output a character.
        if (is_keydown)
        {
            m_sequence += str;

            if (m_sequence == ":")
            {
                // Do nothing, sequence in progress
            }
            else if (m_sequence == ":)")
            {
                // Sequence finished, print it
                send_str("☺");
                m_composing = false;
                m_sequence = "";
            }
            else
            {
                // Unknown characters for sequence, print them
                send_str(m_sequence);
                m_composing = false;
                m_sequence = "";
            }
        }

        return true;
    }

    private static void send_str(string str)
    {
        /* HACK: GTK+ applications behave differently with Unicode, and some
         * applications such as XChat for Windows rename their own top-level
         * window, so we parse through the names we know in order to detect
         * a GTK+ application. */
        bool is_gtk = false;
        const int len = 256;
        StringBuilder buf = new StringBuilder(len);
        if (GetClassName(GetForegroundWindow(), buf, len) > 0)
        {
            string wclass = buf.ToString();
            if (wclass == "gdkWindowToplevel" || wclass == "xchatWindowToplevel")
                is_gtk = true;
        }

        if (is_gtk)
        {
            /* FIXME: this does not work properly yet, but you get the idea. */
            key_down(VK.LCONTROL);
            key_down(VK.LSHIFT);
            foreach (var ch in str)
            {
                key_press((VK)'u');
                for (int i = 0; i < 4; ++i)
                {
                    int nybble = (int)ch >> (12 - 4 * i);
                    if (nybble < 10)
                        key_press((VK)(0x30 + nybble));
                    else
                        key_press((VK)(0x41 + nybble - 10));
                }
                key_press((VK)' ');
            }
            key_up(VK.LSHIFT);
            key_up(VK.LCONTROL);
        }
        else
        {
            INPUT[] input = new INPUT[str.Length];

            for (int i = 0; i < input.Length; i++)
            {
                input[i].type = EINPUT.KEYBOARD;
                input[i].U.ki.wVk = 0;
                input[i].U.ki.wScan = (ScanCodeShort)str[i];
                input[i].U.ki.time = 0;
                input[i].U.ki.dwFlags = KEYEVENTF.UNICODE;
                input[i].U.ki.dwExtraInfo = UIntPtr.Zero;
            }

            SendInput((uint)input.Length, input, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    private static void key_down(VK vk)
    {
        keybd_event(vk, 0, KEYEVENTF.EXTENDEDKEY, 0);
    }

    private static void key_up(VK vk)
    {
        keybd_event(vk, 0, KEYEVENTF.KEYUP, 0);
    }

    private static void key_press(VK vk)
    {
        key_down(vk);
        key_up(vk);
    }

    private static byte[] m_keystate = new byte[256];
    private static string m_sequence = "";
    private static bool m_compose_down = false;
    private static bool m_composing = false;

    internal enum EINPUT : uint
    {
        MOUSE    = 0,
        KEYBOARD = 1,
        HARDWARE = 2,
    }

    [Flags]
    internal enum KEYEVENTF : uint
    {
        EXTENDEDKEY = 0x0001,
        KEYUP       = 0x0002,
        UNICODE     = 0x0004,
        SCANCODE    = 0x0008,
    }

    [Flags]
    internal enum MOUSEEVENTF : uint
    {
        // Not needed
    }

    internal enum VirtualKeyShort : short
    {
    }

    internal enum ScanCodeShort : short
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        internal EINPUT type;
        internal UINPUT U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct UINPUT
    {
        // All union members need to be included, because they contribute
        // to the final size of struct INPUT.
        [FieldOffset(0)]
        internal MOUSEINPUT mi;
        [FieldOffset(0)]
        internal KEYBDINPUT ki;
        [FieldOffset(0)]
        internal HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int dx, dy, mouseData;
        internal MOUSEEVENTF dwFlags;
        internal uint time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal VirtualKeyShort wVk;
        internal ScanCodeShort wScan;
        internal KEYEVENTF dwFlags;
        internal int time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        internal int uMsg;
        internal short wParamL, wParamH;
    }

    [DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint SendInput(uint nInputs,
        [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);
    [DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void keybd_event(VK vk, SC sc, KEYEVENTF flags, int dwExtraInfo); 

    [DllImport("user32", CharSet = CharSet.Auto)]
    private static extern int ToUnicode(VK wVirtKey, SC wScanCode,
                                        byte[] lpKeyState, byte[] pwszBuff,
                                        int cchBuff, uint wFlags);
    [DllImport("user32", CharSet = CharSet.Auto)]
    private static extern int GetKeyboardState(byte[] lpKeyState);
    [DllImport("user32", CharSet = CharSet.Auto)]
    private static extern short GetKeyState(VK nVirtKey);

    [DllImport("user32", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
}

}
