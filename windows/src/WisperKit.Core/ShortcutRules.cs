namespace WisperKit.Core;

/// <summary>Règles d'un raccourci global acceptable (Windows) : il ne doit pas confisquer une touche de frappe.</summary>
public static class ShortcutRules
{
    public const int VK_ESCAPE = 0x1B, VK_F1 = 0x70, VK_F24 = 0x87;

    /// <summary>Ctrl, Alt ou Win obligatoire (Maj seule ne suffit pas : Maj+D bloquerait le « D » majuscule),
    /// sauf pour les touches de fonction F1…F24. Échap n'est jamais accepté : elle annule la capture et la dictée.</summary>
    public static bool IsAllowed(bool ctrl, bool alt, bool win, int virtualKey)
    {
        if (virtualKey is 0 or VK_ESCAPE) return false;
        return ctrl || alt || win || virtualKey is >= VK_F1 and <= VK_F24;
    }

    /// <summary>Win ou Alt relâchés « seuls » (la touche principale étant avalée par le hook) ouvrent
    /// le menu Démarrer ou activent la barre de menus de l'app active : il faut alors injecter une touche neutre.</summary>
    public static bool NeedsMenuGuard(bool alt, bool win) => win || alt;
}
