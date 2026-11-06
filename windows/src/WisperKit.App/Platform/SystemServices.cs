using Microsoft.Win32;
using NAudio.Wave;

namespace WisperKit.App.Platform;

/// <summary>Lancement à l'ouverture de session (HKCU\…\Run).</summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "WisperKit";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(Name, $"\"{Environment.ProcessPath}\" --background");
            else key.DeleteValue(Name, throwOnMissingValue: false);
        }
    }
}

/// <summary>Réglage de confidentialité Windows « Autoriser les applications de bureau à accéder au micro ».</summary>
internal static class MicrophonePrivacy
{
    private const string Store = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    public static bool IsBlocked
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Store);
            using var desktop = Registry.CurrentUser.OpenSubKey(Store + @"\NonPackaged");
            return string.Equals(key?.GetValue("Value") as string, "Deny", StringComparison.OrdinalIgnoreCase)
                || string.Equals(desktop?.GetValue("Value") as string, "Deny", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool HasDevice => WaveIn.DeviceCount > 0;

    public static void OpenSettings() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute = true });
}

/// <summary>Petits signaux sonores générés (début, fin, texte inséré, erreur).</summary>
internal static class Sounds
{
    public static bool Enabled { get; set; } = true;

    public static void Start() => Play([(880, 70)]);
    public static void Stop() => Play([(660, 60)]);
    public static void Delivered() => Play([(990, 60), (1320, 90)]);
    public static void Error() => Play([(220, 160)]);

    private static void Play((int Hz, int Ms)[] notes)
    {
        if (!Enabled) return;
        Task.Run(() =>
        {
            try
            {
                const int rate = 44100;
                var samples = new List<float>();
                foreach (var (hz, ms) in notes)
                {
                    var n = rate * ms / 1000;
                    for (var i = 0; i < n; i++)
                    {
                        var env = Math.Min(1, Math.Min(i / (rate * 0.005), (n - i) / (rate * 0.02)));
                        samples.Add((float)(Math.Sin(2 * Math.PI * hz * i / rate) * 0.18 * env));
                    }
                }
                var bytes = new byte[samples.Count * 4];
                Buffer.BlockCopy(samples.ToArray(), 0, bytes, 0, bytes.Length);
                using var stream = new RawSourceWaveStream(new System.IO.MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(rate, 1));
                using var output = new WaveOut();
                output.Init(stream);
                output.Play();
                Thread.Sleep(notes.Sum(n => n.Ms) + 80);
            }
            catch
            {
                // Pas de sortie audio : silencieux.
            }
        });
    }
}
