using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GazeStick.Helpers;

public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly IntPtr _hwnd;
    private int _hotkeyId = 1;
    private Action? _callback;
    private bool _disposed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotkeyManager(IntPtr handle)
    {
        _hwnd = handle;
    }

    public bool Register(string keyString, Action callback)
    {
        Unregister();

        if (!TryParseHotkey(keyString, out var modifiers, out var vk))
            return false;

        if (!RegisterHotKey(_hwnd, _hotkeyId, modifiers, vk))
            return false;

        _callback = callback;
        return true;
    }

    public void Unregister()
    {
        if (_callback != null)
        {
            UnregisterHotKey(_hwnd, _hotkeyId);
            _callback = null;
        }
    }

    public bool ProcessMessage(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)_hotkeyId && _callback != null)
        {
            _callback();
            return true;
        }
        return false;
    }

    private static bool TryParseHotkey(string keyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        try
        {
            var parts = keyString.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim().ToUpperInvariant();
                switch (trimmed)
                {
                    case "ALT": modifiers |= 0x0001; break;
                    case "CONTROL":
                    case "CTRL": modifiers |= 0x0002; break;
                    case "SHIFT": modifiers |= 0x0004; break;
                    case "WIN": modifiers |= 0x0008; break;
                    default:
                        if (Enum.TryParse<Keys>(trimmed, out var key))
                            vk = (uint)key;
                        else
                            return false;
                        break;
                }
            }
            return vk != 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Unregister();
            _disposed = true;
        }
    }
}