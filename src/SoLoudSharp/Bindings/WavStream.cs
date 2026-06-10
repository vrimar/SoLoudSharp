using System.Runtime.InteropServices;
using System.Threading;

namespace SoLoudSharp;

/// <summary>
/// Streamed audio source — decodes on demand instead of holding the whole
/// clip in memory. Use for long-form audio (music, ambient beds).
/// </summary>
public sealed partial class WavStream : IAudioSource, IDisposable
{
    private const string LibraryName = "soloud";

    private nint _handle;

    // Pins the engine alive while this AudioSource is reachable — the native
    // ~WavStream() unconditionally calls AudioSource::stop() which derefs mSoloud.
    private Soloud? _engine;

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

    void IAudioSource.AttachEngine(Soloud engine) => _engine ??= engine;

    public WavStream()
    {
        var ptr = WavStream_create();
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("WavStream_create returned null.");
        }
        _handle = ptr;
    }

    ~WavStream() => DisposeCore();

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
            WavStream_destroy(h);
        }
        _engine = null;
    }

    public SoloudResult Load(string filename) => (SoloudResult)WavStream_load(Handle, filename);

    /// <summary>
    /// Loads the whole file into memory up front. Trades memory for faster
    /// random-access vs. the streaming reader.
    /// </summary>
    public SoloudResult LoadToMem(string filename) =>
        (SoloudResult)WavStream_loadToMem(Handle, filename);

    public unsafe SoloudResult LoadMem(ReadOnlySpan<byte> data)
    {
        fixed (byte* p = data)
        {
            return (SoloudResult)WavStream_loadMemEx(
                Handle,
                p,
                (uint)data.Length,
                aCopy: true,
                aTakeOwnership: false
            );
        }
    }

    public unsafe SoloudResult LoadMemUnsafe(
        nint mem,
        uint length,
        bool copy = false,
        bool takeOwnership = false
    ) => (SoloudResult)WavStream_loadMemEx(Handle, (byte*)mem, length, copy, takeOwnership);

    public double Length => WavStream_getLength(Handle);

    public void SetVolume(float volume) => WavStream_setVolume(Handle, volume);

    public void SetLooping(bool loop) => WavStream_setLooping(Handle, loop);

    public void SetAutoStop(bool autoStop) => WavStream_setAutoStop(Handle, autoStop);

    public void Set3DMinMaxDistance(float minDist, float maxDist) =>
        WavStream_set3dMinMaxDistance(Handle, minDist, maxDist);

    public void Set3DAttenuation(AttenuationModel model, float rolloffFactor) =>
        WavStream_set3dAttenuation(Handle, (uint)model, rolloffFactor);

    public void Set3DDopplerFactor(float dopplerFactor) =>
        WavStream_set3dDopplerFactor(Handle, dopplerFactor);

    public void Set3DListenerRelative(bool listenerRelative) =>
        WavStream_set3dListenerRelative(Handle, listenerRelative);

    public void Set3DDistanceDelay(int distanceDelay) =>
        WavStream_set3dDistanceDelay(Handle, distanceDelay);

    public void SetInaudibleBehavior(bool mustTick, bool kill) =>
        WavStream_setInaudibleBehavior(Handle, mustTick, kill);

    public void SetLoopPoint(double loopPoint) => WavStream_setLoopPoint(Handle, loopPoint);

    public double LoopPoint => WavStream_getLoopPoint(Handle);

    public void Stop() => WavStream_stop(Handle);

    // =================================================================
    // Native P/Invoke surface
    // =================================================================

    [LibraryImport(LibraryName)]
    private static partial nint WavStream_create();

    [LibraryImport(LibraryName)]
    private static partial void WavStream_destroy(nint wavStream);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int WavStream_load(nint wavStream, string filename);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int WavStream_loadToMem(nint wavStream, string filename);

    [LibraryImport(LibraryName)]
    private static unsafe partial int WavStream_loadMemEx(
        nint wavStream,
        byte* data,
        uint dataLen,
        [MarshalAs(UnmanagedType.I4)] bool aCopy,
        [MarshalAs(UnmanagedType.I4)] bool aTakeOwnership
    );

    [LibraryImport(LibraryName)]
    private static partial double WavStream_getLength(nint wavStream);

    [LibraryImport(LibraryName)]
    private static partial void WavStream_setVolume(nint wavStream, float volume);

    [LibraryImport(LibraryName)]
    private static partial void WavStream_setLooping(
        nint wavStream,
        [MarshalAs(UnmanagedType.I4)] bool loop
    );

    [LibraryImport(LibraryName)]
    private static partial void WavStream_setAutoStop(
        nint wavStream,
        [MarshalAs(UnmanagedType.I4)] bool autoStop
    );

    [LibraryImport(LibraryName)]
    private static partial void WavStream_set3dMinMaxDistance(
        nint wavStream,
        float minDist,
        float maxDist
    );

    [LibraryImport(LibraryName)]
    private static partial void WavStream_set3dAttenuation(
        nint wavStream,
        uint model,
        float rolloffFactor
    );

    [LibraryImport(LibraryName)]
    private static partial void WavStream_set3dDopplerFactor(nint wavStream, float dopplerFactor);

    [LibraryImport(LibraryName)]
    private static partial void WavStream_set3dListenerRelative(
        nint wavStream,
        [MarshalAs(UnmanagedType.I4)] bool listenerRelative
    );

    [LibraryImport(LibraryName)]
    private static partial void WavStream_set3dDistanceDelay(nint wavStream, int distanceDelay);

    [LibraryImport(LibraryName)]
    private static partial void WavStream_setInaudibleBehavior(
        nint wavStream,
        [MarshalAs(UnmanagedType.I4)] bool mustTick,
        [MarshalAs(UnmanagedType.I4)] bool kill
    );

    [LibraryImport(LibraryName)]
    private static partial void WavStream_setLoopPoint(nint wavStream, double loopPoint);

    [LibraryImport(LibraryName)]
    private static partial double WavStream_getLoopPoint(nint wavStream);

    [LibraryImport(LibraryName)]
    private static partial void WavStream_stop(nint wavStream);
}
