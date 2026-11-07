using WisperKit.Core;
using static WisperKit.Core.DictationController;

namespace WisperKit.Core.Tests;

public class DictationControllerTests
{
    [Fact]
    public async Task ToggleRecordsThenDeliversCleanText()
    {
        var rec = new FakeRecorder(); var output = new FakeOutput();
        // Délai : hors de l'app (pas de contexte de synchronisation), la transcription finit
        // sur un autre thread et pourrait se terminer avant la vérification de l'état.
        var c = new DictationController(rec, new FakeTranscriber { Delay = TimeSpan.FromMilliseconds(150) }, output);
        var events = new List<string>();
        c.EventRaised += e => events.Add(e.GetType().Name);

        c.Toggle();
        Assert.Equal(DictationState.Recording, c.State);
        Assert.True(rec.Running);
        Assert.Equal(0.5f, c.AudioLevel);

        c.Toggle();
        Assert.Equal(DictationState.Transcribing, c.State);
        await c.WaitForCompletionAsync();

        Assert.Equal(DictationState.Idle, c.State);
        Assert.Equal(["Bonjour le monde."], output.Delivered);
        Assert.Equal("Bonjour le monde.", c.LastText);
        Assert.Equal(["Started", "Stopped", "Delivered"], events);
    }

    [Fact]
    public void TooShortRecordingIsIgnored()
    {
        var rec = new FakeRecorder { Samples = new float[1_000] };
        var output = new FakeOutput();
        var c = new DictationController(rec, new FakeTranscriber(), output);
        c.Start(); c.Stop();
        Assert.Equal(DictationState.Idle, c.State);
        Assert.Empty(output.Delivered);
    }

    [Fact]
    public void CancelDuringRecordingDropsAudio()
    {
        var rec = new FakeRecorder(); var output = new FakeOutput();
        var c = new DictationController(rec, new FakeTranscriber(), output);
        c.Start(); c.Cancel();
        Assert.Equal(DictationState.Idle, c.State);
        Assert.False(rec.Running);
        Assert.Empty(output.Delivered);
    }

    [Fact]
    public async Task CancelDuringTranscriptionDeliversNothing()
    {
        var output = new FakeOutput();
        var c = new DictationController(new FakeRecorder(), new FakeTranscriber { Delay = TimeSpan.FromMilliseconds(200) }, output);
        c.Start(); c.Stop();
        Assert.Equal(DictationState.Transcribing, c.State);
        c.Cancel();
        await Task.Delay(300);
        Assert.Equal(DictationState.Idle, c.State);
        Assert.Empty(output.Delivered);
    }

    [Fact]
    public async Task TranscriptionErrorSurfacesAndCanRestart()
    {
        var c = new DictationController(new FakeRecorder(), new FakeTranscriber { Error = new Exception("boum") }, new FakeOutput());
        c.Start(); c.Stop();
        await c.WaitForCompletionAsync();
        Assert.Equal(DictationState.Failed, c.State);
        Assert.Equal("boum", c.ErrorMessage);
        c.Start();
        Assert.Equal(DictationState.Recording, c.State);
    }

    [Fact]
    public void MicrophoneErrorFails()
    {
        var rec = new FakeRecorder { StartError = new InvalidOperationException("No microphone available.") };
        var c = new DictationController(rec, new FakeTranscriber(), new FakeOutput());
        c.Start();
        Assert.Equal(DictationState.Failed, c.State);
        Assert.Equal("No microphone available.", c.ErrorMessage);
    }

    [Fact]
    public async Task HallucinationOnSilenceIsNotInserted()
    {
        var output = new FakeOutput();
        var c = new DictationController(new FakeRecorder(), new FakeTranscriber { Text = " Merci d'avoir regardé ! " }, output);
        c.Start(); c.Stop();
        await c.WaitForCompletionAsync();
        Assert.Empty(output.Delivered);
        Assert.Null(c.LastText);
    }

    [Fact]
    public async Task TypeModeIsPassedToOutput()
    {
        var output = new FakeOutput();
        var c = new DictationController(new FakeRecorder(), new FakeTranscriber(), output) { InsertionMethod = InsertionMethod.Type };
        DeliveryResult? result = null;
        c.EventRaised += e => { if (e is Event.Delivered d) result = d.Result; };
        c.Start(); c.Stop();
        await c.WaitForCompletionAsync();
        Assert.Equal([InsertionMethod.Type], output.Methods);
        Assert.Equal(DeliveryResult.Typed, result);
    }
}

public class TextCleanerTests
{
    [Fact] public void TrimsAndCollapsesSpaces() => Assert.Equal("Bonjour à tous", TextCleaner.Clean("  Bonjour   à\n tous  "));
    [Fact] public void RemovesNonVerbalTokens() => Assert.Equal("Salut ça va", TextCleaner.Clean("[Musique] Salut (rires) ça va"));

    [Fact]
    public void DropsFullHallucinationOnly()
    {
        Assert.Equal("", TextCleaner.Clean("Sous-titres réalisés par la communauté d'Amara.org"));
        Assert.Equal("Merci d'avoir regardé le schéma, on passe au code.",
                     TextCleaner.Clean("Merci d'avoir regardé le schéma, on passe au code."));
    }
}

public class AudioLevelTests
{
    [Fact]
    public void SilenceIsZeroAndLoudIsOne()
    {
        Assert.Equal(0, AudioFormat.NormalizedLevel(new float[1600]));
        var loud = Enumerable.Range(0, 1600).Select(i => (float)Math.Sin(i * 0.1) * 0.8f).ToArray();
        Assert.Equal(1, AudioFormat.NormalizedLevel(loud));
        var speech = Enumerable.Range(0, 1600).Select(i => (float)Math.Sin(i * 0.1) * 0.03f).ToArray();
        Assert.InRange(AudioFormat.NormalizedLevel(speech), 0.2f, 0.8f);
    }
}

public class DecodingPlanTests
{
    [Fact]
    public void TranscribesInTheSpokenLanguageByDefault()
    {
        Assert.Equal(new DecodingPlan(false, null), DecodingPlan.Make("auto", false, true));
        Assert.Equal(new DecodingPlan(false, "fr"), DecodingPlan.Make("fr", false, false));
    }

    [Fact]
    public void TranslatesWithWhisperTaskWhenTheModelSupportsIt()
    {
        Assert.Equal(new DecodingPlan(true, null), DecodingPlan.Make("auto", true, false));
        Assert.Equal(new DecodingPlan(true, "fr"), DecodingPlan.Make("fr", true, false));
    }

    [Fact]
    public void TurboForcesEnglishOutputInstead() =>
        Assert.Equal(new DecodingPlan(false, "en"), DecodingPlan.Make("fr", true, true));

    [Fact]
    public async Task ControllerPassesTranslateFlag()
    {
        var transcriber = new FakeTranscriber();
        var c = new DictationController(new FakeRecorder(), transcriber, new FakeOutput()) { TranslateToEnglish = true };
        c.Start(); c.Stop();
        await c.WaitForCompletionAsync();
        Assert.True(transcriber.LastTranslate);
    }
}
