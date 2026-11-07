using System.Text.Json;
using System.Text.Json.Serialization;

namespace WisperKit.Core;

public enum TriggerMode { Toggle, Hold }

/// <summary>Réglages persistés dans %APPDATA%\WisperKit\settings.json (mêmes que la version macOS).</summary>
public sealed class AppSettings
{
    public string ModelId { get; set; } = WhisperModel.Default.Id;
    /// <summary>"auto" par défaut : le texte sort dans la langue parlée.</summary>
    public string DictationLanguage { get; set; } = "auto";
    public bool TranslateToEnglish { get; set; }
    public string InterfaceLanguage { get; set; } = "en";
    public bool AutoInsert { get; set; } = true;
    public bool NeverUseClipboard { get; set; }
    public bool RestoreClipboard { get; set; } = true;
    public bool Sounds { get; set; } = true;
    public bool ShowIndicator { get; set; } = true;
    public TriggerMode Mode { get; set; } = TriggerMode.Toggle;
    public bool EscapeCancels { get; set; } = true;
    /// <summary>Raccourci au format « Ctrl+Alt+D ».</summary>
    public string Shortcut { get; set; } = "Ctrl+Alt+D";
    public bool Onboarded { get; set; }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(AppPaths.RoamingData, "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new AppSettings();
        }
        catch (Exception e)
        {
            DiagnosticLog.Write($"réglages illisibles, valeurs par défaut : {e.Message}");
        }
        return new AppSettings();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
