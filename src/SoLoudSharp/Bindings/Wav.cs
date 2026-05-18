using System.Runtime.InteropServices;
using System.Threading;

namespace SoLoudSharp;

/// <summary>
/// Decoded, fully-in-memory audio source. Suitable for short clips and
/// frequently re-triggered sounds.
/// </summary>
public sealed partial class Wav : IAudioSource, IDisposable
{
    private const string LibraryName = "soloud";

    private nint _handle;

    public bool IsDisposed => _handle == IntPtr.Zero;

    internal nint Handle
    {
        get
        {
            var h = _handle;
            ObjectDisposedException.ThrowIf(h == IntPtr.Zero, this);
            return h;
        }
    }

    nint IAudioSource.AudioSourceHandle => Handle;

    public Wav()
    {
        var ptr = Wav_create();
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Wav_create returned null.");
        }
        _handle = ptr;
    }

    ~Wav() => DisposeCore();

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    private void DisposeCore()
    {
        var h = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (h != IntPtr.Zero)
        {
            Wav_destroy(h);
        }
    }

    // -----------------------------------------------------------------
    // Loading
    // -----------------------------------------------------------------

    public SoloudResult Load(string filename) => (SoloudResult)Wav_load(Handle, filename);

    /// <summary>Loads from a managed byte span, copying so the caller's buffer can be reused.</summary>
    public unsafe SoloudResult LoadMem(ReadOnlySpan<byte> data)
    {
        fixed (byte* p = data)
        {
            return (SoloudResult)Wav_loadMemEx(Handle, p, (uint)data.Length, aCopy: true, aTakeOwnership: false);
        }
    }

    /// <summary>
    /// Loads directly from native memory without copying. The caller must keep
    /// the memory alive for the lifetime of this Wav (unless takeOwnership=true,
    /// in which case SoLoud will free it).
    /// </summary>
    public unsafe SoloudResult LoadMemUnsafe(nint mem, uint length, bool copy = false, bool takeOwnership = false) =>
        (SoloudResult)Wav_loadMemEx(Handle, (byte*)mem, length, copy, takeOwnership);

    public unsafe SoloudResult LoadRawWave8(ReadOnlySpan<byte> samples, float sampleRate, uint channels)
    {
        fixed (byte* p = samples)
        {
            return (SoloudResult)Wav_loadRawWave8Ex(Handle, p, (uint)samples.Length, sampleRate, channels);
        }
    }

    public unsafe SoloudResult LoadRawWave16(ReadOnlySpan<short> samples, float sampleRate, uint channels)
    {
        fixed (short* p = samples)
        {
            return (SoloudResult)Wav_loadRawWave16Ex(Handle, p, (uint)samples.Length, sampleRate, channels);
        }
    }

    public unsafe SoloudResult LoadRawWave(ReadOnlySpan<float> samples, float sampleRate, uint channels)
    {
        fixed (float* p = samples)
        {
            return (SoloudResult)Wav_loadRawWaveEx(Handle, p, (uint)samples.Length, sampleRate, channels, aCopy: true, aTakeOwnership: false);
        }
    }

    public double Length => Wav_getLength(Handle);

    public void SetVolume(float volume) => Wav_setVolume(Handle, volume);
    public void SetLooping(bool loop) => Wav_setLooping(Handle, loop);
    public void SetAutoStop(bool autoStop) => Wav_setAutoStop(Handle, autoStop);
    public void Set3DMinMaxDistance(float minDist, float maxDist) => Wav_set3dMinMaxDistance(Handle, minDist, maxDist);
    public void Set3DAttenuation(AttenuationModel model, float rolloffFactor) => Wav_set3dAttenuation(Handle, (uint)model, rolloffFactor);
    public void Set3DDopplerFactor(float dopplerFactor) => Wav_set3dDopplerFactor(Handle, dopplerFactor);
    public void Set3DListenerRelative(bool listenerRelative) => Wav_set3dListenerRelative(Handle, listenerRelative);
    public void Set3DDistanceDelay(int distanceDelay) => Wav_set3dDistanceDelay(Handle, distanceDelay);
    public void SetInaudibleBehavior(bool mustTick, bool kill) => Wav_setInaudibleBehavior(Handle, mustTick, kill);
    public void SetLoopPoint(double loopPoint) => Wav_setLoopPoint(Handle, loopPoint);
    public double LoopPoint => Wav_getLoopPoint(Handle);
    public void Stop() => Wav_stop(Handle);

    // =================================================================
    // Native P/Invoke surface
    // =================================================================

    [LibraryImport(LibraryName)] private static partial nint Wav_create();
    [LibraryImport(LibraryName)] private static partial void Wav_destroy(nint wav);
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)] private static partial int Wav_load(nint wav, string filename);
    [LibraryImport(LibraryName)] private static unsafe partial int Wav_loadMemEx(nint wav, byte* mem, uint length, [MarshalAs(UnmanagedType.I4)] bool aCopy, [MarshalAs(UnmanagedType.I4)] bool aTakeOwnership);
    [LibraryImport(LibraryName)] private static unsafe partial int Wav_loadRawWave8Ex(nint wav, byte* mem, uint length, float samplerate, uint channels);
    [LibraryImport(LibraryName)] private static unsafe partial int Wav_loadRawWave16Ex(nint wav, short* mem, uint length, float samplerate, uint channels);
    [LibraryImport(LibraryName)] private static unsafe partial int Wav_loadRawWaveEx(nint wav, float* mem, uint length, float samplerate, uint channels, [MarshalAs(UnmanagedType.I4)] bool aCopy, [MarshalAs(UnmanagedType.I4)] bool aTakeOwnership);
    [LibraryImport(LibraryName)] private static partial double Wav_getLength(nint wav);
    [LibraryImport(LibraryName)] private static partial void Wav_setVolume(nint wav, float volume);
    [LibraryImport(LibraryName)] private static partial void Wav_setLooping(nint wav, [MarshalAs(UnmanagedType.I4)] bool loop);
    [LibraryImport(LibraryName)] private static partial void Wav_setAutoStop(nint wav, [MarshalAs(UnmanagedType.I4)] bool autoStop);
    [LibraryImport(LibraryName)] private static partial void Wav_set3dMinMaxDistance(nint wav, float minDist, float maxDist);
    [LibraryImport(LibraryName)] private static partial void Wav_set3dAttenuation(nint wav, uint model, float rolloffFactor);
    [LibraryImport(LibraryName)] private static partial void Wav_set3dDopplerFactor(nint wav, float dopplerFactor);
    [LibraryImport(LibraryName)] private static partial void Wav_set3dListenerRelative(nint wav, [MarshalAs(UnmanagedType.I4)] bool listenerRelative);
    [LibraryImport(LibraryName)] private static partial void Wav_set3dDistanceDelay(nint wav, int distanceDelay);
    [LibraryImport(LibraryName)] private static partial void Wav_setInaudibleBehavior(nint wav, [MarshalAs(UnmanagedType.I4)] bool mustTick, [MarshalAs(UnmanagedType.I4)] bool kill);
    [LibraryImport(LibraryName)] private static partial void Wav_setLoopPoint(nint wav, double loopPoint);
    [LibraryImport(LibraryName)] private static partial double Wav_getLoopPoint(nint wav);
    [LibraryImport(LibraryName)] private static partial void Wav_stop(nint wav);
}
