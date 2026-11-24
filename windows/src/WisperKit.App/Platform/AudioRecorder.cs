using NAudio.Wave;
using WisperKit.Core;

namespace WisperKit.App.Platform;

/// <summary>Micro par défaut via NAudio, 16 kHz mono, entièrement en mémoire.
/// WaveIn (NAudio 3) livre les tampons sur son propre thread : l'arrêt peut ainsi attendre le tampon
/// en cours (la fin de la dernière syllabe) sans dépendre de la boucle de messages de l'interface.</summary>
internal sealed class AudioRecorder : IAudioRecorder
{
    private const int BufferMilliseconds = 50;
    private readonly object _lock = new();
    private readonly List<float> _samples = [];
    private WaveIn? _wave;
    private float _level;
    private ManualResetEventSlim? _tail;

    public float Level { get { lock (_lock) return _level; } }

    public void Start()
    {
        if (WaveIn.DeviceCount == 0) throw new InvalidOperationException("No microphone available.");
        lock (_lock) { _samples.Clear(); _level = 0; _tail = null; }
        var wave = new WaveIn
        {
            WaveFormat = new WaveFormat(AudioFormat.SampleRate, 16, 1),
            BufferMilliseconds = BufferMilliseconds,
        };
        wave.DataAvailable += OnData;
        wave.StartRecording();
        _wave = wave;
    }

    public float[] Stop()
    {
        var wave = _wave;
        _wave = null;
        if (wave is not null)
        {
            // Le tampon en cours contient l'instant du relâchement : on attend qu'il soit livré
            // (au plus un tampon, avec une marge) avant d'arrêter, sinon la fin de phrase est perdue.
            using var tail = new ManualResetEventSlim();
            lock (_lock) _tail = tail;
            tail.Wait(BufferMilliseconds * 3);
            lock (_lock) _tail = null;
            wave.DataAvailable -= OnData;
            try { wave.StopRecording(); } catch (Exception e) { DiagnosticLog.Write($"arrêt du micro : {e.Message}"); }
            wave.Dispose();
        }
        lock (_lock)
        {
            var result = _samples.ToArray();
            _samples.Clear();
            _level = 0;
            return result;
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var count = e.BytesRecorded / 2;
        var chunk = new float[count];
        for (var i = 0; i < count; i++)
            chunk[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        var level = AudioFormat.NormalizedLevel(chunk);
        lock (_lock)
        {
            _samples.AddRange(chunk);
            _level = level;
            _tail?.Set();
        }
    }
}
