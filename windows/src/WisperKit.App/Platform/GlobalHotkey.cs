using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using WisperKit.Core;
using static WisperKit.App.Platform.NativeMethods;

namespace WisperKit.App.Platform;

/// <summary>Raccourci au format « Ctrl+Alt+D ».</summary>
internal readonly record struct Shortcut(bool Ctrl, bool Alt, bool Shift, bool Win, int VirtualKey)
{
    public static Shortcut Default { get; } = Parse("Ctrl+Alt+D")!.Value;

    /// <summary>Raccourci lu, ou null s'il est illisible ou refusé par <see cref="ShortcutRules"/>.</summary>
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
        var shortcut = new Shortcut(ctrl, alt, shift, win, vk);
        return shortcut.IsAllowed ? shortcut : null;
    }

    public bool IsAllowed => ShortcutRules.IsAllowed(Ctrl, Alt, Win, VirtualKey);

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
/// Le hook vit sur un thread dédié avec sa propre boucle de messages : si le thread de l'interface
/// est occupé (presse-papiers, rendu…), Windows ne le retire pas pour dépassement de LowLevelHooksTimeout.
/// Les événements sont donc levés sur ce thread : les abonnés doivent repasser sur l'interface.
/// Les touches simulées (LLKHF_INJECTED), y compris celles de WisperKit, sont ignorées.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private readonly LowLevelKeyboardProc _proc; // gardé en champ : sinon collecté par le GC
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private uint _threadId;
    private IntPtr _hook;
    private bool _held;
    private volatile StrongBox<Shortcut> _shortcut = new(Shortcut.Default);
    private volatile Action<Shortcut?>? _capture;

    /// <summary>Raccourci actif (lu depuis le thread du hook).</summary>
    public Shortcut Shortcut
    {
        get => _shortcut.Value;
        set => _shortcut = new StrongBox<Shortcut>(value);
    }

    /// <summary>Échap est capturé seulement quand ceci renvoie vrai (pendant l'écoute). Appelé sur le thread du hook.</summary>
    public Func<bool> EscapeActive { get; set; } = () => false;

    public event Action? Pressed;
    public event Action? Released;
    public event Action? EscapePressed;

    /// <summary>Mode capture : la prochaine combinaison valide est transmise ; null si Échap annule.
    /// Les combinaisons refusées (sans Ctrl/Alt/Win) sont avalées et la capture continue.</summary>
    public Action<Shortcut?>? Capture
    {
        get => _capture;
        set => _capture = value;
    }

    public GlobalHotkey()
    {
        _proc = HookProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "WisperKit keyboard hook" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        using (var module = Process.GetCurrentProcess().MainModule)
            _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(module?.ModuleName), 0);
        if (_hook == IntPtr.Zero)
            DiagnosticLog.Write($"hook clavier impossible (erreur {Marshal.GetLastWin32Error()})");
        _ready.Set();
        // Boucle de messages : indispensable pour que Windows appelle le hook sur ce thread.
        while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                if (Handle(wParam, lParam)) return 1;
            }
            catch (Exception e)
            {
                DiagnosticLog.Write($"hook clavier : {e.Message}");
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>Vrai si la touche est consommée (non transmise à l'app active).</summary>
    private bool Handle(IntPtr wParam, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        if ((info.flags & LLKHF_INJECTED) != 0) return false;
        var msg = (int)wParam;
        var down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        var up = msg is WM_KEYUP or WM_SYSKEYUP;
        var vk = (int)info.vkCode;

        if (_capture is { } capture && down && !IsModifier(vk))
        {
            if (vk == VK_ESCAPE)
            {
                _capture = null;
                capture(null);
                return true;
            }
            var shortcut = new Shortcut(IsDown(VK_CONTROL), IsDown(VK_MENU), IsDown(VK_SHIFT),
                                        IsDown(VK_LWIN) || IsDown(VK_RWIN), vk);
            GuardMenus(shortcut);
            if (!shortcut.IsAllowed) return true; // refusée : on attend une autre combinaison
            _capture = null;
            capture(shortcut);
            return true;
        }

        var current = Shortcut;
        if (vk == current.VirtualKey)
        {
            if (down && (_held || current.ModifiersMatch()))
            {
                if (!_held)
                {
                    _held = true;
                    GuardMenus(current);
                    Pressed?.Invoke();
                }
                return true; // ne pas taper la lettre dans l'app active (répétition comprise)
            }
            if (up && _held)
            {
                _held = false;
                Released?.Invoke();
                return true;
            }
        }

        if (down && vk == VK_ESCAPE && EscapeActive())
        {
            EscapePressed?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>La touche principale étant avalée, Windows verrait Win ou Alt pressés puis relâchés seuls
    /// et ouvrirait le menu Démarrer ou la barre de menus (qui volerait le texte collé).
    /// Une touche non attribuée injectée entre les deux l'en empêche.</summary>
    private static void GuardMenus(Shortcut s)
    {
        if (!ShortcutRules.NeedsMenuGuard(s.Alt, s.Win)) return;
        // Envoi immédiat, depuis le hook : la touche doit précéder à coup sûr le relâchement de Win/Alt.
        // Elle repasse dans ce hook marquée LLKHF_INJECTED, donc ignorée.
        Send(Key(VK_UNASSIGNED, false), Key(VK_UNASSIGNED, true));
    }

    private static bool IsModifier(int vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN
        or VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or VK_LMENU or VK_RMENU;

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
