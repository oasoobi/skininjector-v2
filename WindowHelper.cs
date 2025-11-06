using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT;

public static class WindowHelper
{
    private static readonly Dictionary<IntPtr, SubclassProc> _subclassProcs = new();

    public static void SetMinSize(Window window, int minWidth, int minHeight)
    {
        var hwnd = window.As<IWindowNative>().WindowHandle;

        SubclassProc proc = (hWnd, msg, wParam, lParam, uIdSubclass, dwRefData) =>
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                uint dpi = GetDpiForWindow(hWnd);
                float scale = dpi / 96f;
                MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                int safeMinWidth = Math.Max(minWidth, 200);
                int safeMinHeight = Math.Max(minHeight, 200);
                mmi.ptMinTrackSize.x = (int)(safeMinWidth * scale);
                mmi.ptMinTrackSize.y = (int)(safeMinHeight * scale);
                Marshal.StructureToPtr(mmi, lParam, false);
            }
            return DefSubclassProc(hWnd, msg, wParam, lParam);
        };

        _subclassProcs[hwnd] = proc;

        SetWindowSubclass(hwnd, proc, (UIntPtr)1, IntPtr.Zero);
    }

    public static void RemoveMinSize(Window window)
    {
        var hwnd = window.As<IWindowNative>().WindowHandle;
        RemoveWindowSubclass(hwnd, _subclassProcs[hwnd], (UIntPtr)1);
        _subclassProcs.Remove(hwnd);
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
    internal interface IWindowNative
    {
        IntPtr WindowHandle { get; }
    }
}