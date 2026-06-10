using System.Runtime.CompilerServices;
using Xunit;

namespace SoLoudSharp.Tests;

public class AudioSourceLifetimeTests
{
    // Regression: ~Wav -> AudioSource::stop() unconditionally dereferences
    // mSoloud, so if the GC ran Soloud's finalizer first the Wav finalizer
    // hit freed memory and crashed RunFinalizers with 0xC0000005. The fix
    // pins Soloud from any played AudioSource — assert (a) Soloud survives
    // GC while a played Wav is reachable, and (b) it is collectible once
    // the Wav has been disposed.
    [SkippableFact]
    public void PlayedSource_PinsSoloud_UntilDisposed()
    {
        Skip.IfNot(NativeLibraryPresent(), "SoLoud native library not deployed.");

        var (soloudWeak, wav) = SetUp();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.True(
            soloudWeak.IsAlive,
            "Soloud must stay alive while a played AudioSource is reachable."
        );

        wav.Dispose();
        wav = null!;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(
            soloudWeak.IsAlive,
            "Soloud should be collectible once the played AudioSource is gone."
        );
    }

    // NoInlining so the JIT cannot extend the lifetime of the local `soloud`
    // into the caller's stack frame — only the returned WeakReference / Wav
    // can keep state alive across the GC cycle.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, Wav) SetUp()
    {
        var soloud = new Soloud(SoloudInitFlags.ClipRoundoff, SoloudBackend.MiniAudio, 0, 0, 2);
        var wav = new Wav();
        var r = wav.LoadRawWave16(new short[1024], 44100f, 1);
        Assert.Equal(SoloudResult.Ok, r);
        _ = soloud.Play(wav, paused: true);
        return (new WeakReference(soloud), wav);
    }

    private static bool NativeLibraryPresent()
    {
        var dir = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(dir, "soloud.dll"))
            || File.Exists(Path.Combine(dir, "libsoloud.so"))
            || File.Exists(Path.Combine(dir, "libsoloud.dylib"));
    }
}
