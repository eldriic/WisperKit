using NAudio.Wave;
using WisperKit.Core;

namespace WisperKit.App.Platform;

/// <summary>Micro par défaut via NAudio (WaveIn), 16 kHz mono, entièrement en mémoire.</summary>
internal sealed class AudioRecorder : IAudioRecorder
{
    private readonly object _lock = new();
    private readonly List<float> _samples = [];
    private WaveIn? _wave;
    private float _level;

    public float Level { get { lock (_lock) return _level; } }

    public void Start()
    {
        if (WaveIn.DeviceCount == 0) throw new InvalidOperationException("No microphone available.");
        lock (_lock) { _samples.Clear(); _level = 0; }
        var wave = new WaveIn
        {
            WaveFormat = new WaveFormat(AudioFormat.SampleRate, 16, 1),
            BufferMilliseconds = 50,
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
            wave.DataAvailable -= OnData;
            wave.StopRecording();
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
        }
    }
}
