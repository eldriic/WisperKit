namespace WisperKit.Core;

/// <summary>Journal de diagnostic : %LOCALAPPDATA%\WisperKit\Logs\wisperkit.log.
/// Ne contient jamais le texte dicté, seulement des longueurs et des états.</summary>
public static class DiagnosticLog
{
    public static string FilePath { get; } = Path.Combine(AppPaths.LocalData, "Logs", "wisperkit.log");
    private const long MaxSize = 512 * 1024;
    private static readonly object Lock = new();

    public static void Write(string message)
    {
        var line = $"{DateTime.UtcNow:HH:mm:ss.fff} {message}{Environment.NewLine}";
        ThreadPool.QueueUserWorkItem(_ =>
        {
            lock (Lock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxSize) File.Delete(FilePath);
                    File.AppendAllText(FilePath, line);
                }
                catch
                {
                    // Le diagnostic ne doit jamais faire échouer l'app.
                }
            }
        });
    }
}
