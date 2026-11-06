using System.Runtime.InteropServices;
using WisperKit.Core;
using static WisperKit.App.Platform.NativeMethods;

namespace WisperKit.App.Platform;

/// <summary>Écrit le texte dans l'application active : collage (Ctrl+V) ou frappe Unicode.
/// Aucune autorisation n'est nécessaire sous Windows, sauf pour écrire dans une app lancée en administrateur.</summary>
internal sealed class TextInserter : ITextOutput
{
    private static readonly uint ExcludeFromMonitor = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint CanIncludeInHistory = RegisterClipboardFormatW("CanIncludeInClipboardHistory");
    private static readonly uint CanUploadToCloud = RegisterClipboardFormatW("CanUploadToCloudClipboard");

    public DeliveryResult Deliver(string text, bool autoInsert, InsertionMethod method, bool restoreClipboard)
    {
        var front = ForegroundProcessName();
        DiagnosticLog.Write($"livraison {text.Length} car. → app={front} méthode={method} auto={autoInsert}");

        if (method == InsertionMethod.Type)
        {
            if (!autoInsert) return DeliveryResult.NotInserted;
            _ = TypeAsync(text, front);
            return DeliveryResult.Typed;
        }

        var saved = restoreClipboard ? ClipboardSnapshot.Take() : null;
        // Texte collé automatiquement : marqué pour rester hors de l'historique Win+V et des gestionnaires.
        if (!SetClipboardText(text, transient: autoInsert && restoreClipboard))
        {
            DiagnosticLog.Write("presse-papiers indisponible");
            return DeliveryResult.NotInserted;
        }
        if (!autoInsert) return DeliveryResult.ClipboardOnly;

        _ = PasteAsync(saved, front);
        return DeliveryResult.Pasted;
    }

    private static async Task PasteAsync(ClipboardSnapshot? saved, string front)
    {
        await WaitForModifiersReleasedAsync();
        var sequence = GetClipboardSequenceNumber();
        var sent = Send(Key(VK_CONTROL, false), Key(VK_V, false), Key(VK_V, true), Key(VK_CONTROL, true));
        DiagnosticLog.Write(sent == 4 ? $"Ctrl+V envoyé à {front}" : $"Ctrl+V bloqué ({front} lancé en administrateur ?)");
        if (saved is null) return;
        // Laisse l'app cible lire le presse-papiers avant de restaurer l'ancien contenu.
        await Task.Delay(1000);
        if (GetClipboardSequenceNumber() == sequence) saved.Restore();
    }

    private static async Task TypeAsync(string text, string front)
    {
        await WaitForModifiersReleasedAsync();
        var normalized = text.Replace("\r\n", "\n");
        var inputs = new List<INPUT>();
        foreach (var c in normalized)
        {
            if (c == '\n') { inputs.Add(Key(VK_RETURN, false)); inputs.Add(Key(VK_RETURN, true)); }
            else { inputs.Add(Unicode(c, false)); inputs.Add(Unicode(c, true)); }
        }
        uint sent = 0;
        foreach (var batch in inputs.Chunk(64))
        {
            sent += Send(batch);
            await Task.Delay(8);
        }
        DiagnosticLog.Write(sent == inputs.Count ? $"texte tapé dans {front}" : $"frappe bloquée ({front} lancé en administrateur ?)");
    }

    /// <summary>En mode « maintenir », Ctrl/Alt peuvent encore être enfoncés : on attend (1,5 s max)
    /// pour que Ctrl+V ne devienne pas Ctrl+Alt+V.</summary>
    private static async Task WaitForModifiersReleasedAsync()
    {
        for (var i = 0; i < 75 && (IsDown(VK_MENU) || IsDown(VK_SHIFT) || IsDown(VK_LWIN) || IsDown(VK_RWIN) || IsDown(VK_CONTROL)); i++)
            await Task.Delay(20);
    }

    private static string ForegroundProcessName()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return "?"; }
    }

    internal static bool SetClipboardText(string text, bool transient)
    {
        if (!OpenClipboardWithRetry()) return false;
        try
        {
            EmptyClipboard();
            SetData(CF_UNICODETEXT, System.Text.Encoding.Unicode.GetBytes(text + "\0"));
            if (transient)
            {
                SetData(ExcludeFromMonitor, []);
                SetData(CanIncludeInHistory, BitConverter.GetBytes(0));
                SetData(CanUploadToCloud, BitConverter.GetBytes(0));
            }
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    internal static bool OpenClipboardWithRetry()
    {
        for (var i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            Thread.Sleep(20);
        }
        return false;
    }

    internal static void SetData(uint format, byte[] bytes)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)Math.Max(bytes.Length, 1));
        var ptr = GlobalLock(handle);
        if (bytes.Length > 0) Marshal.Copy(bytes, 0, ptr, bytes.Length);
        GlobalUnlock(handle);
        if (SetClipboardData(format, handle) == IntPtr.Zero) GlobalFree(handle);
    }

    /// <summary>Copie des formats mémoire du presse-papiers (texte, RTF, HTML, images DIB…)
    /// pour les restaurer après le collage.</summary>
    private sealed class ClipboardSnapshot
    {
        // Formats à base de handles GDI : non copiables tels quels, ignorés.
        private static readonly HashSet<uint> Skipped = [2 /*BITMAP*/, 3 /*METAFILEPICT*/, 9 /*PALETTE*/, 14 /*ENHMETAFILE*/, 15 /*HDROP*/];
        private readonly List<(uint Format, byte[] Data)> _items = [];

        public static ClipboardSnapshot? Take()
        {
            if (!OpenClipboardWithRetry()) return null;
            var snapshot = new ClipboardSnapshot();
            try
            {
                uint format = 0;
                while ((format = EnumClipboardFormats(format)) != 0)
                {
                    if (Skipped.Contains(format)) continue;
                    var handle = GetClipboardData(format);
                    if (handle == IntPtr.Zero) continue;
                    var size = (int)GlobalSize(handle);
                    var ptr = GlobalLock(handle);
                    if (ptr == IntPtr.Zero) continue;
                    var data = new byte[size];
                    Marshal.Copy(ptr, data, 0, size);
                    GlobalUnlock(handle);
                    snapshot._items.Add((format, data));
                }
            }
            finally
            {
                CloseClipboard();
            }
            return snapshot;
        }

        public void Restore()
        {
            if (!OpenClipboardWithRetry()) return;
            try
            {
                EmptyClipboard();
                foreach (var (format, data) in _items) SetData(format, data);
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}
