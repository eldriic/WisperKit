namespace WisperKit.Core;

/// <summary>Détecte un plantage natif pendant l'usage du GPU (pilote Vulkan défaillant) : un fichier témoin
/// est écrit avant chaque appel GPU et effacé après. S'il existe encore au lancement suivant,
/// le processus est mort dans le code natif (exception non rattrapable) : on repasse sur le processeur.</summary>
public sealed class GpuGuard(string path)
{
    public static GpuGuard Default { get; } = new(Path.Combine(AppPaths.LocalData, "gpu-in-use"));

    public string FilePath { get; } = path;

    /// <summary>Vrai si le dernier usage du GPU ne s'est pas terminé (plantage). Efface le témoin.</summary>
    public bool ConsumeCrash()
    {
        if (!File.Exists(FilePath)) return false;
        try { File.Delete(FilePath); } catch { }
        return true;
    }

    /// <summary>Fermeture normale de l'app (Quitter, fin de session, installeur) : un calcul GPU interrompu
    /// par la sortie du processus n'est pas un plantage.</summary>
    public void Clear()
    {
        try { File.Delete(FilePath); } catch { }
    }

    /// <summary>Encadre un appel natif sur le GPU.</summary>
    public IDisposable Enter()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, "");
        }
        catch
        {
            // Sans témoin, seul le repli automatique est perdu.
        }
        return new Exit(FilePath);
    }

    private sealed class Exit(string path) : IDisposable
    {
        public void Dispose()
        {
            try { File.Delete(path); } catch { }
        }
    }
}
