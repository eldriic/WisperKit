using System.Text.RegularExpressions;

namespace WisperKit.Core;

/// <summary>Nettoie la sortie de Whisper avant de l'insérer (mêmes règles que la version macOS).</summary>
public static partial class TextCleaner
{
    /// <summary>Phrases « hallucinées » par Whisper sur du silence ; supprimées seulement si elles
    /// constituent tout le texte, pour ne jamais tronquer une vraie dictée.</summary>
    internal static readonly HashSet<string> Hallucinations =
    [
        "sous-titres réalisés par la communauté d'amara.org",
        "sous-titrage st' 501",
        "sous-titrage société radio-canada",
        "merci d'avoir regardé cette vidéo",
        "merci d'avoir regardé",
        "merci de votre attention",
        "abonnez-vous",
        "thank you for watching",
        "thanks for watching",
        "you",
    ];

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*")]
    private static partial Regex NonVerbal();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static string Clean(string raw)
    {
        var text = NonVerbal().Replace(raw, " ");
        text = Spaces().Replace(text, " ").Trim();
        var key = text.ToLowerInvariant().Trim(' ', '.', '!', '…');
        return Hallucinations.Contains(key) ? "" : text;
    }
}
