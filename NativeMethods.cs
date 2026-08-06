using System.Runtime.InteropServices;
using DrawingColor = System.Drawing.Color;

namespace StickyMarkNative;

internal static class NativeMethods
{
    public const int WmHotKey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    public static void BeginWindowDrag(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        ReleaseCapture();
        _ = SendMessage(hwnd, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }

    public static void BeginWindowResize(IntPtr hwnd, string direction)
    {
        if (hwnd == IntPtr.Zero) return;
        var hitTest = direction switch
        {
            "NorthWest" => HtTopLeft,
            "NorthEast" => HtTopRight,
            "SouthWest" => HtBottomLeft,
            "SouthEast" => HtBottomRight,
            "North" => HtTop,
            "South" => HtBottom,
            "West" => HtLeft,
            "East" => HtRight,
            _ => HtBottomRight,
        };
        ReleaseCapture();
        _ = SendMessage(hwnd, WmNcLButtonDown, (IntPtr)hitTest, IntPtr.Zero);
    }

    public static void SetTopmost(IntPtr hwnd, bool topmost)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowPos(
            hwnd,
            topmost ? HwndTopMost : HwndNoTopMost,
            0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    public static void ApplyWindowChrome(IntPtr hwnd, DrawingColor borderColor)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            var rounded = 2; // DWMWCP_ROUND
            _ = DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE
            var colorRef = borderColor.R | (borderColor.G << 8) | (borderColor.B << 16);
            _ = DwmSetWindowAttribute(hwnd, 34, ref colorRef, sizeof(int)); // DWMWA_BORDER_COLOR
            _ = DwmSetWindowAttribute(hwnd, 35, ref colorRef, sizeof(int)); // DWMWA_CAPTION_COLOR
        }
        catch (DllNotFoundException)
        {
            // Older Windows versions may not expose DWM attributes.
        }
    }

    public static bool TryParseHotkey(string value, out uint modifiers, out uint virtualKey, out string error)
    {
        modifiers = ModNoRepeat;
        virtualKey = 0;
        error = string.Empty;
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = "快捷键至少需要一个修饰键和一个按键。";
            return false;
        }

        string? key = null;
        foreach (var rawPart in parts)
        {
            var part = rawPart.ToUpperInvariant();
            switch (part)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModWin;
                    break;
                default:
                    if (key is not null)
                    {
                        error = "快捷键只能包含一个主按键。";
                        return false;
                    }
                    key = part;
                    break;
            }
        }

        if (key is null || modifiers == ModNoRepeat)
        {
            error = "请填写类似 Ctrl+Alt+Space 的快捷键。";
            return false;
        }

        var namedKeys = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["SPACE"] = 0x20, ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B, ["TAB"] = 0x09,
            ["ENTER"] = 0x0D, ["RETURN"] = 0x0D, ["BACKSPACE"] = 0x08, ["INSERT"] = 0x2D,
            ["DELETE"] = 0x2E, ["HOME"] = 0x24, ["END"] = 0x23, ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22, ["LEFT"] = 0x25, ["UP"] = 0x26, ["RIGHT"] = 0x27, ["DOWN"] = 0x28,
        };
        if (namedKeys.TryGetValue(key, out var namedKey))
        {
            virtualKey = namedKey;
            return true;
        }
        if (key.Length >= 2 && key[0] == 'F' && int.TryParse(key[1..], out var functionNumber) && functionNumber is >= 1 and <= 12)
        {
            virtualKey = (uint)(0x70 + functionNumber - 1);
            return true;
        }
        if (key.Length == 1 && char.IsLetterOrDigit(key[0]))
        {
            virtualKey = key[0];
            return true;
        }

        error = "无法识别主按键，请使用字母、数字、F1-F12 或 Space。";
        return false;
    }
}
