using System.Runtime.InteropServices;
using System.Threading;

namespace SoLoudSharp;

/// <summary>
/// Submix bus — accepts audio sources and routes them through its own
/// fader / filter / FFT chain before mixing into the master output.
/// </summary>
public sealed partial class Bus : IAudioSource, IDisposable
{
    private const string LibraryName = "soloud";

    private nint _handle;

    // Pins the engine alive while this AudioSource is reachable — the native
    // ~Bus() unconditionally calls AudioSource::stop() which derefs mSoloud.
    // Also propagated to sources played through this bus (see Play* overloads).
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

    public Bus()
    {
        var ptr = Bus_create();
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Bus_create returned null.");
        }
        _handle = ptr;
    }

    ~Bus() => DisposeCore();

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
            Bus_destroy(h);
        }
        _engine = null;
    }

    public BusHandle Play(
        IAudioSource source,
        float volume = 1.0f,
        float pan = 0.0f,
        bool paused = false
    )
    {
        PinSourceToEngine(source);
        return new(Bus_playEx(Handle, source.AudioSourceHandle, volume, pan, paused));
    }

    public BusHandle PlayClocked(
        double soundTime,
        IAudioSource source,
        float volume = 1.0f,
        float pan = 0.0f
    )
    {
        PinSourceToEngine(source);
        return new(Bus_playClockedEx(Handle, soundTime, source.AudioSourceHandle, volume, pan));
    }

    public BusHandle Play3D(
        IAudioSource source,
        float posX,
        float posY,
        float posZ,
        float velX = 0.0f,
        float velY = 0.0f,
        float velZ = 0.0f,
        float volume = 1.0f,
        bool paused = false
    )
    {
        PinSourceToEngine(source);
        return new(
            Bus_play3dEx(
                Handle,
                source.AudioSourceHandle,
                posX,
                posY,
                posZ,
                velX,
                velY,
                velZ,
                volume,
                paused
            )
        );
    }

    public BusHandle Play3DClocked(
        double soundTime,
        IAudioSource source,
        float posX,
        float posY,
        float posZ,
        float velX = 0.0f,
        float velY = 0.0f,
        float velZ = 0.0f,
        float volume = 1.0f
    )
    {
        PinSourceToEngine(source);
        return new(
            Bus_play3dClockedEx(
                Handle,
                soundTime,
                source.AudioSourceHandle,
                posX,
                posY,
                posZ,
                velX,
                velY,
                velZ,
                volume
            )
        );
    }

    // The bus's native ::play* calls set mSoloud on the source from the bus's
    // own engine, so the source must outlive that engine just like the bus does.
    private void PinSourceToEngine(IAudioSource source)
    {
        if (_engine is { } engine)
        {
            source.AttachEngine(engine);
        }
    }

    public SoloudResult SetChannels(uint channels) =>
        (SoloudResult)Bus_setChannels(Handle, channels);

    public void SetVisualizationEnable(bool enable) => Bus_setVisualizationEnable(Handle, enable);

    public void AnnexSound(VoiceHandle voice) => Bus_annexSound(Handle, voice.Handle);

    /// <summary>FFT bins (length 256) for the most recent mix window.</summary>
    public unsafe ReadOnlySpan<float> CalcFFT() => new(Bus_calcFFT(Handle), 256);

    /// <summary>Time-domain samples (length 256) from the most recent mix.</summary>
    public unsafe ReadOnlySpan<float> GetWave() => new(Bus_getWave(Handle), 256);

    public float GetApproximateVolume(uint channel) => Bus_getApproximateVolume(Handle, channel);

    public uint ActiveVoiceCount => Bus_getActiveVoiceCount(Handle);

    public Resampler Resampler
    {
        get => (Resampler)Bus_getResampler(Handle);
        set => Bus_setResampler(Handle, (uint)value);
    }

    public void SetVolume(float volume) => Bus_setVolume(Handle, volume);

    public void SetLooping(bool loop) => Bus_setLooping(Handle, loop);

    public void SetAutoStop(bool autoStop) => Bus_setAutoStop(Handle, autoStop);

    public void Set3DMinMaxDistance(float minDist, float maxDist) =>
        Bus_set3dMinMaxDistance(Handle, minDist, maxDist);

    public void Set3DAttenuation(AttenuationModel model, float rolloffFactor) =>
        Bus_set3dAttenuation(Handle, (uint)model, rolloffFactor);

    public void Set3DDopplerFactor(float dopplerFactor) =>
        Bus_set3dDopplerFactor(Handle, dopplerFactor);

    public void Set3DListenerRelative(bool listenerRelative) =>
        Bus_set3dListenerRelative(Handle, listenerRelative);

    public void Set3DDistanceDelay(int distanceDelay) =>
        Bus_set3dDistanceDelay(Handle, distanceDelay);

    public void SetInaudibleBehavior(bool mustTick, bool kill) =>
        Bus_setInaudibleBehavior(Handle, mustTick, kill);

    public void SetLoopPoint(double loopPoint) => Bus_setLoopPoint(Handle, loopPoint);

    public double LoopPoint => Bus_getLoopPoint(Handle);

    public void Stop() => Bus_stop(Handle);

    // =================================================================
    // Native P/Invoke surface
    // =================================================================

    [LibraryImport(LibraryName)]
    private static partial nint Bus_create();

    [LibraryImport(LibraryName)]
    private static partial void Bus_destroy(nint bus);

    [LibraryImport(LibraryName)]
    private static partial uint Bus_playEx(
        nint bus,
        nint sound,
        float volume,
        float pan,
        [MarshalAs(UnmanagedType.I4)] bool paused
    );

    [LibraryImport(LibraryName)]
    private static partial uint Bus_playClockedEx(
        nint bus,
        double soundTime,
        nint sound,
        float volume,
        float pan
    );

    [LibraryImport(LibraryName)]
    private static partial uint Bus_play3dEx(
        nint bus,
        nint sound,
        float posX,
        float posY,
        float posZ,
        float velX,
        float velY,
        float velZ,
        float volume,
        [MarshalAs(UnmanagedType.I4)] bool paused
    );

    [LibraryImport(LibraryName)]
    private static partial uint Bus_play3dClockedEx(
        nint bus,
        double soundTime,
        nint sound,
        float posX,
        float posY,
        float posZ,
        float velX,
        float velY,
        float velZ,
        float volume
    );

    [LibraryImport(LibraryName)]
    private static partial int Bus_setChannels(nint bus, uint channels);

    [LibraryImport(LibraryName)]
    private static partial void Bus_setVisualizationEnable(
        nint bus,
        [MarshalAs(UnmanagedType.I4)] bool enable
    );

    [LibraryImport(LibraryName)]
    private static partial void Bus_annexSound(nint bus, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static unsafe partial float* Bus_calcFFT(nint bus);

    [LibraryImport(LibraryName)]
    private static unsafe partial float* Bus_getWave(nint bus);

    [LibraryImport(LibraryName)]
    private static partial float Bus_getApproximateVolume(nint bus, uint channel);

    [LibraryImport(LibraryName)]
    private static partial uint Bus_getActiveVoiceCount(nint bus);

    [LibraryImport(LibraryName)]
    private static partial uint Bus_getResampler(nint bus);

    [LibraryImport(LibraryName)]
    private static partial void Bus_setResampler(nint bus, uint resampler);

    [LibraryImport(LibraryName)]
    private static partial void Bus_setVolume(nint bus, float volume);

    [LibraryImport(LibraryName)]
    private static partial void Bus_setLooping(nint bus, [MarshalAs(UnmanagedType.I4)] bool loop);

    [LibraryImport(LibraryName)]
    private static partial void Bus_setAutoStop(
        nint bus,
        [MarshalAs(UnmanagedType.I4)] bool autoStop
    );

    [LibraryImport(LibraryName)]
    private static partial void Bus_set3dMinMaxDistance(nint bus, float minDist, float maxDist);

    [LibraryImport(LibraryName)]
    private static partial void Bus_set3dAttenuation(nint bus, uint model, float rolloffFactor);

    [LibraryImport(LibraryName)]
    private static partial void Bus_set3dDopplerFactor(nint bus, float dopplerFactor);

    [LibraryImport(LibraryName)]
    private static partial void Bus_set3dListenerRelative(
        nint bus,
        [MarshalAs(UnmanagedType.I4)] bool listenerRelative
    );

    [LibraryImport(LibraryName)]
    private static partial void Bus_set3dDistanceDelay(nint bus, int distanceDelay);

    [LibraryImport(LibraryName)]
    private static partial void Bus_setInaudibleBehavior(
        nint bus,
        [MarshalAs(UnmanagedType.I4)] bool mustTick,
        [MarshalAs(UnmanagedType.I4)] bool kill
    );

    [LibraryImport(LibraryName)]
    private static partial void Bus_setLoopPoint(nint bus, double loopPoint);

    [LibraryImport(LibraryName)]
    private static partial double Bus_getLoopPoint(nint bus);

    [LibraryImport(LibraryName)]
    private static partial void Bus_stop(nint bus);
}
