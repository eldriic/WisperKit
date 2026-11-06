using System.Reflection;
using System.Text;
using System.Text.Json;

namespace WisperKit.Core;

/// <summary>Langue de l'interface (anglais par défaut). Les textes sources sont en anglais ;
/// les traductions viennent de Localization/&lt;langue&gt;.json, généré depuis les fichiers
/// .strings de la version macOS (mêmes clés, mêmes traductions).</summary>
public static class Localizer
{
    public static IReadOnlyList<(string Id, string NativeName)> Languages { get; } =
        [("en", "English"), ("fr", "Français")];

    private static Dictionary<string, string> _strings = [];

    public static string Language { get; private set; } = "en";

    /// <summary>Déclenché après un changement de langue (rafraîchissement à chaud).</summary>
    public static event Action? LanguageChanged;

    public static void SetLanguage(string language)
    {
        var known = Languages.Any(l => l.Id == language) ? language : "en";
        _strings = known == "en" ? [] : LoadTable(known);
        Language = known;
        LanguageChanged?.Invoke();
    }

    /// <summary>Texte traduit ; <paramref name="args"/> remplit les %@ / %d du texte source anglais.</summary>
    public static string L(string key, params object[] args)
    {
        var format = _strings.TryGetValue(key, out var value) ? value : key;
        return args.Length == 0 ? format : Format(format, args);
    }

    public static IReadOnlyDictionary<string, string> Table(string language) => LoadTable(language);

    /// <summary>Format façon printf limité à %@, %d et %% (syntaxe des .strings Apple).</summary>
    internal static string Format(string format, object[] args)
    {
        var sb = new StringBuilder();
        var next = 0;
        for (var i = 0; i < format.Length; i++)
        {
            if (format[i] == '%' && i + 1 < format.Length)
            {
                var c = format[i + 1];
                if (c == '%') { sb.Append('%'); i++; continue; }
                if ((c == '@' || c == 'd') && next < args.Length) { sb.Append(args[next++]); i++; continue; }
            }
            sb.Append(format[i]);
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> LoadTable(string language)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"WisperKit.Localization.{language}.json");
        if (stream is null) return [];
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }
}
