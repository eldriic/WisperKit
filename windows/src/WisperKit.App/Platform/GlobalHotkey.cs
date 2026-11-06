using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using static WisperKit.App.Platform.NativeMethods;

namespace WisperKit.App.Platform;

/// <summary>Raccourci au format « Ctrl+Alt+D ».</summary>
internal readonly record struct Shortcut(bool Ctrl, bool Alt, bool Shift, bool Win, int VirtualKey)
{
    public static Shortcut Default { get; } = Parse("Ctrl+Alt+D")!.Value;

    public static Shortcut? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        bool ctrl = false, alt = false, shift = false, win = false;
        int vk = 0;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": ctrl = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                case "win": win = true; break;
                default:
                    if (!Enum.TryParse<Key>(part, true, out var key)) return null;
                    vk = KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return vk == 0 ? null : new Shortcut(ctrl, alt, shift, win, vk);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey(VirtualKey).ToString());
        return string.Join("+", parts);
    }

    public bool ModifiersMatch() =>
        Ctrl == IsDown(VK_CONTROL) && Alt == IsDown(VK_MENU) && Shift == IsDown(VK_SHIFT)
        && Win == (IsDown(VK_LWIN) || IsDown(VK_RWIN));
}

/// <summary>Hook clavier global (appui et relâchement) : bascule, mode « maintenir » et Échap.
/// Les touches simulées par WisperKit lui-même (LLKHF_INJECTED) sont ignorées.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private readonly LowLevelKeyboardProc _proc; // gardé en champ : sinon collecté par le GC
    private IntPtr _hook;
    private bool _held;

    public Shortcut Shortcut { get; set; } = Shortcut.Default;
    /// <summary>Échap est capturé seulement quand ceci renvoie vrai (pendant l'écoute).</summary>
    public Func<bool> EscapeActive { get; set; } = () => false;

    public event Action? Pressed;
    public event Action? Released;
    public event Action? EscapePressed;

    /// <summary>Mode capture (enregistrement d'un nouveau raccourci dans les réglages).</summary>
    public Action<Shortcut>? Capture { get; set; }

    public GlobalHotkey()
    {
        _proc = HookProc;
        using var module = Process.GetCurrentProcess().MainModule;
        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(module?.ModuleName), 0);
        if (_hook == IntPtr.Zero)
            WisperKit.Core.DiagnosticLog.Write($"hook clavier impossible (erreur {Marshal.GetLastWin32Error()})");
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var injected = (info.flags & LLKHF_INJECTED) != 0;
            var msg = (int)wParam;
            var down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            var up = msg is WM_KEYUP or WM_SYSKEYUP;
            var vk = (int)info.vkCode;

            if (!injected && Capture is { } capture && down && !IsModifier(vk))
            {
                var shortcut = new Shortcut(IsDown(VK_CONTROL), IsDown(VK_MENU), IsDown(VK_SHIFT),
                                            IsDown(VK_LWIN) || IsDown(VK_RWIN), vk);
                Capture = null;
                capture(shortcut);
                return 1;
            }

            if (!injected && vk == Shortcut.VirtualKey)
            {
                if (down && (_held || Shortcut.ModifiersMatch()))
                {
                    if (!_held) { _held = true; Pressed?.Invoke(); }
                    return 1; // ne pas taper la lettre dans l'app active (répétition comprise)
                }
                if (up && _held)
                {
                    _held = false;
                    Released?.Invoke();
                    return 1;
                }
            }

            if (!injected && down && vk == VK_ESCAPE && EscapeActive())
            {
                EscapePressed?.Invoke();
                return 1;
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsModifier(int vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN
        or VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or VK_LMENU or VK_RMENU;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
