using WisperKit.Core;

namespace WisperKit.Core.Tests;

sealed class FakeRecorder : IAudioRecorder
{
    public float[] Samples = Enumerable.Repeat(0.1f, 16_000).ToArray(); // 1 s
    public Exception? StartError;
    public bool Running;
    public float Level => Running ? 0.5f : 0;
    public void Start() { if (StartError is not null) throw StartError; Running = true; }
    public float[] Stop() { Running = false; return Samples; }
}

sealed class FakeTranscriber : ITranscriber
{
    public string Text = " Bonjour le monde. ";
    public Exception? Error;
    public TimeSpan Delay = TimeSpan.Zero;
    public bool? LastTranslate;
    public async Task<string> TranscribeAsync(float[] samples, string language, bool translate, CancellationToken ct)
    {
        LastTranslate = translate;
        await Task.Delay(Delay, ct);
        if (Error is not null) throw Error;
        return Text;
    }
}

sealed class FakeOutput : ITextOutput
{
    public readonly List<string> Delivered = [];
    public readonly List<InsertionMethod> Methods = [];
    public DeliveryResult Deliver(string text, bool autoInsert, InsertionMethod method, bool restoreClipboard)
    {
        Delivered.Add(text);
        Methods.Add(method);
        if (!autoInsert) return method == InsertionMethod.Type ? DeliveryResult.NotInserted : DeliveryResult.ClipboardOnly;
        return method == InsertionMethod.Type ? DeliveryResult.Typed : DeliveryResult.Pasted;
    }
}
