using System.Reflection;
using Xunit;

namespace SoLoudSharp.Tests;

public class SmokeTests
{
    [Fact]
    public void AssemblyEmbedsSoLoudRevision()
    {
        var attr = typeof(SoloudBackend)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SoLoudRevision");

        Assert.NotNull(attr);
        Assert.False(
            string.IsNullOrEmpty(attr!.Value),
            "SoLoudRevision metadata should be populated."
        );
    }

    [Fact]
    public void BackendEnumIncludesMiniAudio()
    {
        Assert.Equal(14u, (uint)SoloudBackend.MiniAudio);
    }

    [SkippableFact]
    public void MiniAudioInitAndDeinit()
    {
        Skip.IfNot(
            NativeLibraryPresent(),
            "SoLoud native library not deployed; per-RID smoke job (package.yml) runs this with the lib staged."
        );

        using var soloud = new Soloud();
        var result = soloud.Init(
            flags: SoloudInitFlags.ClipRoundoff,
            backend: SoloudBackend.MiniAudio,
            samplerate: 0, // AUTO
            bufferSize: 0, // AUTO
            channels: 2
        );
        Assert.Equal(SoloudResult.Ok, result);

        try
        {
            Assert.True(soloud.Version > 0);
            Assert.NotEqual(0u, soloud.BackendSamplerate);
        }
        finally
        {
            soloud.Deinit();
        }
    }

    private static bool NativeLibraryPresent()
    {
        var dir = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(dir, "soloud.dll"))
            || File.Exists(Path.Combine(dir, "libsoloud.so"))
            || File.Exists(Path.Combine(dir, "libsoloud.dylib"));
    }
}
