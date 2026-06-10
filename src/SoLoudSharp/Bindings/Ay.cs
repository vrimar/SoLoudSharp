using System.Runtime.InteropServices;
using System.Threading;

namespace SoLoudSharp;

/// <summary>
/// AY-3-8910 chip emulator audio source (used by retro-style synths).
/// </summary>
public sealed partial class Ay : IAudioSource, IDisposable
{
    private const string LibraryName = "soloud";

    private nint _handle;

    // Pins the engine alive while this AudioSource is reachable — the native
    // ~Ay() unconditionally calls AudioSource::stop() which derefs mSoloud.
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

    public Ay()
    {
        var ptr = Ay_create();
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Ay_create returned null.");
        }
        _handle = ptr;
    }

    ~Ay() => DisposeCore();

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
            Ay_destroy(h);
        }
        _engine = null;
    }

    public void SetVolume(float volume) => Ay_setVolume(Handle, volume);

    public void SetLooping(bool loop) => Ay_setLooping(Handle, loop);

    public void SetAutoStop(bool autoStop) => Ay_setAutoStop(Handle, autoStop);

    public void Set3DMinMaxDistance(float minDist, float maxDist) =>
        Ay_set3dMinMaxDistance(Handle, minDist, maxDist);

    public void Set3DAttenuation(AttenuationModel model, float rolloffFactor) =>
        Ay_set3dAttenuation(Handle, (uint)model, rolloffFactor);

    public void Set3DDopplerFactor(float dopplerFactor) =>
        Ay_set3dDopplerFactor(Handle, dopplerFactor);

    public void Set3DListenerRelative(bool listenerRelative) =>
        Ay_set3dListenerRelative(Handle, listenerRelative);

    public void Set3DDistanceDelay(int distanceDelay) =>
        Ay_set3dDistanceDelay(Handle, distanceDelay);

    public void SetInaudibleBehavior(bool mustTick, bool kill) =>
        Ay_setInaudibleBehavior(Handle, mustTick, kill);

    public void SetLoopPoint(double loopPoint) => Ay_setLoopPoint(Handle, loopPoint);

    public double LoopPoint => Ay_getLoopPoint(Handle);

    public void Stop() => Ay_stop(Handle);

    // =================================================================
    // Native P/Invoke surface
    // =================================================================

    [LibraryImport(LibraryName)]
    private static partial nint Ay_create();

    [LibraryImport(LibraryName)]
    private static partial void Ay_destroy(nint ay);

    [LibraryImport(LibraryName)]
    private static partial void Ay_setVolume(nint ay, float volume);

    [LibraryImport(LibraryName)]
    private static partial void Ay_setLooping(nint ay, [MarshalAs(UnmanagedType.I4)] bool loop);

    [LibraryImport(LibraryName)]
    private static partial void Ay_setAutoStop(
        nint ay,
        [MarshalAs(UnmanagedType.I4)] bool autoStop
    );

    [LibraryImport(LibraryName)]
    private static partial void Ay_set3dMinMaxDistance(nint ay, float minDist, float maxDist);

    [LibraryImport(LibraryName)]
    private static partial void Ay_set3dAttenuation(nint ay, uint model, float rolloffFactor);

    [LibraryImport(LibraryName)]
    private static partial void Ay_set3dDopplerFactor(nint ay, float dopplerFactor);

    [LibraryImport(LibraryName)]
    private static partial void Ay_set3dListenerRelative(
        nint ay,
        [MarshalAs(UnmanagedType.I4)] bool listenerRelative
    );

    [LibraryImport(LibraryName)]
    private static partial void Ay_set3dDistanceDelay(nint ay, int distanceDelay);

    [LibraryImport(LibraryName)]
    private static partial void Ay_setInaudibleBehavior(
        nint ay,
        [MarshalAs(UnmanagedType.I4)] bool mustTick,
        [MarshalAs(UnmanagedType.I4)] bool kill
    );

    [LibraryImport(LibraryName)]
    private static partial void Ay_setLoopPoint(nint ay, double loopPoint);

    [LibraryImport(LibraryName)]
    private static partial double Ay_getLoopPoint(nint ay);

    [LibraryImport(LibraryName)]
    private static partial void Ay_stop(nint ay);
}
