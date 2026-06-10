using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SoLoudSharp;

/// <summary>
/// Managed wrapper around the SoLoud audio engine root object. Owns the
/// native <c>Soloud</c> context and exposes the public mixer / playback /
/// 3D listener API.
/// </summary>
public sealed partial class Soloud : IDisposable
{
    private const string LibraryName = "soloud";

    private nint _handle;

    /// <summary>True after <see cref="Dispose"/> (or finalization) has freed the native context.</summary>
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

    public Soloud()
    {
        var ptr = Soloud_create();
        if (ptr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Soloud_create returned null.");
        }
        _handle = ptr;
    }

    public Soloud(
        SoloudInitFlags flags,
        SoloudBackend backend = SoloudBackend.Auto,
        uint samplerate = 0,
        uint bufferSize = 0,
        uint channels = 2
    )
        : this()
    {
        var r = Init(flags, backend, samplerate, bufferSize, channels);
        if (r != SoloudResult.Ok)
        {
            Dispose();
            throw new InvalidOperationException($"Soloud_init failed: {r} ({(int)r}).");
        }
    }

    ~Soloud() => DisposeCore();

    /// <summary>
    /// Releases the native engine. Dispose every <see cref="IAudioSource"/> that
    /// has been played on this engine <i>first</i>: their native destructors call
    /// back into the engine, so disposing the engine while a played source is
    /// still alive leaves that source's finalizer dereferencing freed memory.
    /// (GC-order is handled automatically — sources pin the engine — but explicit
    /// out-of-order Dispose is not.)
    /// </summary>
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
            Soloud_destroy(h);
        }
    }

    // -----------------------------------------------------------------
    // Engine lifecycle
    // -----------------------------------------------------------------

    public SoloudResult Init() => (SoloudResult)Soloud_init(Handle);

    public SoloudResult Init(
        SoloudInitFlags flags,
        SoloudBackend backend,
        uint samplerate = 0,
        uint bufferSize = 0,
        uint channels = 2
    ) =>
        (SoloudResult)Soloud_initEx(
            Handle,
            (uint)flags,
            (uint)backend,
            samplerate,
            bufferSize,
            channels
        );

    public void Deinit() => Soloud_deinit(Handle);

    public SoloudResult Pause() => (SoloudResult)Soloud_pause(Handle);

    public SoloudResult Resume() => (SoloudResult)Soloud_resume(Handle);

    public uint Version => Soloud_getVersion(Handle);

    public string GetErrorString(SoloudResult errorCode) =>
        Marshal.PtrToStringUTF8(Soloud_getErrorString(Handle, (int)errorCode)) ?? string.Empty;

    public SoloudBackend Backend => (SoloudBackend)Soloud_getBackendId(Handle);

    public string BackendString =>
        Marshal.PtrToStringUTF8(Soloud_getBackendString(Handle)) ?? string.Empty;

    public uint BackendChannels => Soloud_getBackendChannels(Handle);

    public uint BackendSamplerate => Soloud_getBackendSamplerate(Handle);

    public uint BackendBufferSize => Soloud_getBackendBufferSize(Handle);

    // -----------------------------------------------------------------
    // Speaker geometry
    // -----------------------------------------------------------------

    public SoloudResult SetSpeakerPosition(uint channel, float x, float y, float z) =>
        (SoloudResult)Soloud_setSpeakerPosition(Handle, channel, x, y, z);

    public SoloudResult SetSpeakerPosition(uint channel, Vector3 position) =>
        SetSpeakerPosition(channel, position.X, position.Y, position.Z);

    public unsafe SoloudResult GetSpeakerPosition(
        uint channel,
        out float x,
        out float y,
        out float z
    )
    {
        float xv,
            yv,
            zv;
        var result = (SoloudResult)Soloud_getSpeakerPosition(Handle, channel, &xv, &yv, &zv);
        x = xv;
        y = yv;
        z = zv;
        return result;
    }

    public SoloudResult GetSpeakerPosition(uint channel, out Vector3 position)
    {
        var r = GetSpeakerPosition(channel, out float x, out float y, out float z);
        position = new Vector3(x, y, z);
        return r;
    }

    // -----------------------------------------------------------------
    // Playback
    // -----------------------------------------------------------------

    public VoiceHandle Play(
        IAudioSource sound,
        float volume = -1.0f,
        float pan = 0.0f,
        bool paused = false,
        uint bus = 0
    )
    {
        sound.AttachEngine(this);
        return new(this, Soloud_playEx(Handle, sound.AudioSourceHandle, volume, pan, paused, bus));
    }

    public VoiceHandle PlayClocked(
        double soundTime,
        IAudioSource sound,
        float volume = -1.0f,
        float pan = 0.0f,
        uint bus = 0
    )
    {
        sound.AttachEngine(this);
        return new(
            this,
            Soloud_playClockedEx(Handle, soundTime, sound.AudioSourceHandle, volume, pan, bus)
        );
    }

    public VoiceHandle Play3D(
        IAudioSource sound,
        float posX,
        float posY,
        float posZ,
        float velX = 0.0f,
        float velY = 0.0f,
        float velZ = 0.0f,
        float volume = 1.0f,
        bool paused = false,
        uint bus = 0
    )
    {
        sound.AttachEngine(this);
        return new(
            this,
            Soloud_play3dEx(
                Handle,
                sound.AudioSourceHandle,
                posX,
                posY,
                posZ,
                velX,
                velY,
                velZ,
                volume,
                paused,
                bus
            )
        );
    }

    public VoiceHandle Play3DClocked(
        double soundTime,
        IAudioSource sound,
        float posX,
        float posY,
        float posZ,
        float velX = 0.0f,
        float velY = 0.0f,
        float velZ = 0.0f,
        float volume = 1.0f,
        uint bus = 0
    )
    {
        sound.AttachEngine(this);
        return new(
            this,
            Soloud_play3dClockedEx(
                Handle,
                soundTime,
                sound.AudioSourceHandle,
                posX,
                posY,
                posZ,
                velX,
                velY,
                velZ,
                volume,
                bus
            )
        );
    }

    public VoiceHandle PlayBackground(
        IAudioSource sound,
        float volume = -1.0f,
        bool paused = false,
        uint bus = 0
    )
    {
        sound.AttachEngine(this);
        return new(
            this,
            Soloud_playBackgroundEx(Handle, sound.AudioSourceHandle, volume, paused, bus)
        );
    }

    // -----------------------------------------------------------------
    // Voice control
    // -----------------------------------------------------------------

    public SoloudResult Seek(VoiceHandle voice, double seconds) =>
        (SoloudResult)Soloud_seek(Handle, voice.Handle, seconds);

    public void Stop(VoiceHandle voice) => Soloud_stop(Handle, voice.Handle);

    public void StopAll() => Soloud_stopAll(Handle);

    public void StopAudioSource(IAudioSource sound) =>
        Soloud_stopAudioSource(Handle, sound.AudioSourceHandle);

    public int CountAudioSource(IAudioSource sound) =>
        Soloud_countAudioSource(Handle, sound.AudioSourceHandle);

    public double GetStreamTime(VoiceHandle voice) => Soloud_getStreamTime(Handle, voice.Handle);

    public double GetStreamPosition(VoiceHandle voice) =>
        Soloud_getStreamPosition(Handle, voice.Handle);

    public bool GetPause(VoiceHandle voice) => Soloud_getPause(Handle, voice.Handle) != 0;

    public float GetVolume(VoiceHandle voice) => Soloud_getVolume(Handle, voice.Handle);

    public float GetOverallVolume(VoiceHandle voice) =>
        Soloud_getOverallVolume(Handle, voice.Handle);

    public float GetPan(VoiceHandle voice) => Soloud_getPan(Handle, voice.Handle);

    public float GetSamplerate(VoiceHandle voice) => Soloud_getSamplerate(Handle, voice.Handle);

    public bool GetProtectVoice(VoiceHandle voice) =>
        Soloud_getProtectVoice(Handle, voice.Handle) != 0;

    public uint ActiveVoiceCount => Soloud_getActiveVoiceCount(Handle);
    public uint VoiceCount => Soloud_getVoiceCount(Handle);

    public bool IsValidVoiceHandle(VoiceHandle voice) =>
        Soloud_isValidVoiceHandle(Handle, voice.Handle) != 0;

    public float GetRelativePlaySpeed(VoiceHandle voice) =>
        Soloud_getRelativePlaySpeed(Handle, voice.Handle);

    public float PostClipScaler
    {
        get => Soloud_getPostClipScaler(Handle);
        set => Soloud_setPostClipScaler(Handle, value);
    }

    public Resampler MainResampler
    {
        get => (Resampler)Soloud_getMainResampler(Handle);
        set => Soloud_setMainResampler(Handle, (uint)value);
    }

    public float GlobalVolume
    {
        get => Soloud_getGlobalVolume(Handle);
        set => Soloud_setGlobalVolume(Handle, value);
    }

    public uint MaxActiveVoiceCount => Soloud_getMaxActiveVoiceCount(Handle);

    public SoloudResult SetMaxActiveVoiceCount(uint count) =>
        (SoloudResult)Soloud_setMaxActiveVoiceCount(Handle, count);

    public bool GetLooping(VoiceHandle voice) => Soloud_getLooping(Handle, voice.Handle) != 0;

    public void SetLooping(VoiceHandle voice, bool looping) =>
        Soloud_setLooping(Handle, voice.Handle, looping);

    public bool GetAutoStop(VoiceHandle voice) => Soloud_getAutoStop(Handle, voice.Handle) != 0;

    public void SetAutoStop(VoiceHandle voice, bool autoStop) =>
        Soloud_setAutoStop(Handle, voice.Handle, autoStop);

    public double GetLoopPoint(VoiceHandle voice) => Soloud_getLoopPoint(Handle, voice.Handle);

    public void SetLoopPoint(VoiceHandle voice, double loopPoint) =>
        Soloud_setLoopPoint(Handle, voice.Handle, loopPoint);

    public void SetInaudibleBehavior(VoiceHandle voice, bool mustTick, bool kill) =>
        Soloud_setInaudibleBehavior(Handle, voice.Handle, mustTick, kill);

    public void SetPause(VoiceHandle voice, bool pause) =>
        Soloud_setPause(Handle, voice.Handle, pause);

    public void SetPauseAll(bool pause) => Soloud_setPauseAll(Handle, pause);

    public SoloudResult SetRelativePlaySpeed(VoiceHandle voice, float speed) =>
        (SoloudResult)Soloud_setRelativePlaySpeed(Handle, voice.Handle, speed);

    public void SetProtectVoice(VoiceHandle voice, bool protect) =>
        Soloud_setProtectVoice(Handle, voice.Handle, protect);

    public void SetSamplerate(VoiceHandle voice, float samplerate) =>
        Soloud_setSamplerate(Handle, voice.Handle, samplerate);

    public void SetPan(VoiceHandle voice, float pan) => Soloud_setPan(Handle, voice.Handle, pan);

    public void SetPanAbsolute(VoiceHandle voice, float lVolume, float rVolume) =>
        Soloud_setPanAbsolute(Handle, voice.Handle, lVolume, rVolume);

    public void SetChannelVolume(VoiceHandle voice, uint channel, float volume) =>
        Soloud_setChannelVolume(Handle, voice.Handle, channel, volume);

    public void SetVolume(VoiceHandle voice, float volume) =>
        Soloud_setVolume(Handle, voice.Handle, volume);

    public void SetDelaySamples(VoiceHandle voice, uint samples) =>
        Soloud_setDelaySamples(Handle, voice.Handle, samples);

    // -----------------------------------------------------------------
    // Fades / oscillations
    // -----------------------------------------------------------------

    public void FadeVolume(VoiceHandle voice, float to, double time) =>
        Soloud_fadeVolume(Handle, voice.Handle, to, time);

    public void FadePan(VoiceHandle voice, float to, double time) =>
        Soloud_fadePan(Handle, voice.Handle, to, time);

    public void FadeRelativePlaySpeed(VoiceHandle voice, float to, double time) =>
        Soloud_fadeRelativePlaySpeed(Handle, voice.Handle, to, time);

    public void FadeGlobalVolume(float to, double time) =>
        Soloud_fadeGlobalVolume(Handle, to, time);

    public void SchedulePause(VoiceHandle voice, double time) =>
        Soloud_schedulePause(Handle, voice.Handle, time);

    public void ScheduleStop(VoiceHandle voice, double time) =>
        Soloud_scheduleStop(Handle, voice.Handle, time);

    public void OscillateVolume(VoiceHandle voice, float from, float to, double time) =>
        Soloud_oscillateVolume(Handle, voice.Handle, from, to, time);

    public void OscillatePan(VoiceHandle voice, float from, float to, double time) =>
        Soloud_oscillatePan(Handle, voice.Handle, from, to, time);

    public void OscillateRelativePlaySpeed(VoiceHandle voice, float from, float to, double time) =>
        Soloud_oscillateRelativePlaySpeed(Handle, voice.Handle, from, to, time);

    public void OscillateGlobalVolume(float from, float to, double time) =>
        Soloud_oscillateGlobalVolume(Handle, from, to, time);

    // -----------------------------------------------------------------
    // Filter parameters
    // -----------------------------------------------------------------

    public void SetFilterParameter(
        VoiceHandle voice,
        uint filterId,
        uint attributeId,
        float value
    ) => Soloud_setFilterParameter(Handle, voice.Handle, filterId, attributeId, value);

    public float GetFilterParameter(VoiceHandle voice, uint filterId, uint attributeId) =>
        Soloud_getFilterParameter(Handle, voice.Handle, filterId, attributeId);

    public void FadeFilterParameter(
        VoiceHandle voice,
        uint filterId,
        uint attributeId,
        float to,
        double time
    ) => Soloud_fadeFilterParameter(Handle, voice.Handle, filterId, attributeId, to, time);

    public void OscillateFilterParameter(
        VoiceHandle voice,
        uint filterId,
        uint attributeId,
        float from,
        float to,
        double time
    ) =>
        Soloud_oscillateFilterParameter(
            Handle,
            voice.Handle,
            filterId,
            attributeId,
            from,
            to,
            time
        );

    public void SetVisualizationEnable(bool enable) =>
        Soloud_setVisualizationEnable(Handle, enable);

    public float GetApproximateVolume(uint channel) => Soloud_getApproximateVolume(Handle, channel);

    public uint GetLoopCount(VoiceHandle voice) => Soloud_getLoopCount(Handle, voice.Handle);

    public float GetInfo(VoiceHandle voice, uint infoKey) =>
        Soloud_getInfo(Handle, voice.Handle, infoKey);

    // -----------------------------------------------------------------
    // Voice groups
    // -----------------------------------------------------------------

    public uint CreateVoiceGroup() => Soloud_createVoiceGroup(Handle);

    public SoloudResult DestroyVoiceGroup(uint group) =>
        (SoloudResult)Soloud_destroyVoiceGroup(Handle, group);

    public SoloudResult AddVoiceToGroup(uint group, VoiceHandle voice) =>
        (SoloudResult)Soloud_addVoiceToGroup(Handle, group, voice.Handle);

    public bool IsVoiceGroup(uint group) => Soloud_isVoiceGroup(Handle, group) != 0;

    public bool IsVoiceGroupEmpty(uint group) => Soloud_isVoiceGroupEmpty(Handle, group) != 0;

    // -----------------------------------------------------------------
    // 3D
    // -----------------------------------------------------------------

    public void Update3DAudio() => Soloud_update3dAudio(Handle);

    public SoloudResult Set3DSoundSpeed(float speed) =>
        (SoloudResult)Soloud_set3dSoundSpeed(Handle, speed);

    public float Get3DSoundSpeed() => Soloud_get3dSoundSpeed(Handle);

    public void Set3DListenerParameters(
        float posX,
        float posY,
        float posZ,
        float atX,
        float atY,
        float atZ,
        float upX,
        float upY,
        float upZ,
        float velX = 0.0f,
        float velY = 0.0f,
        float velZ = 0.0f
    ) =>
        Soloud_set3dListenerParametersEx(
            Handle,
            posX,
            posY,
            posZ,
            atX,
            atY,
            atZ,
            upX,
            upY,
            upZ,
            velX,
            velY,
            velZ
        );

    public void Set3DListenerPosition(float x, float y, float z) =>
        Soloud_set3dListenerPosition(Handle, x, y, z);

    public void Set3DListenerAt(float x, float y, float z) =>
        Soloud_set3dListenerAt(Handle, x, y, z);

    public void Set3DListenerUp(float x, float y, float z) =>
        Soloud_set3dListenerUp(Handle, x, y, z);

    public void Set3DListenerVelocity(float x, float y, float z) =>
        Soloud_set3dListenerVelocity(Handle, x, y, z);

    public void Set3DSourceParameters(
        VoiceHandle voice,
        float posX,
        float posY,
        float posZ,
        float velX = 0.0f,
        float velY = 0.0f,
        float velZ = 0.0f
    ) => Soloud_set3dSourceParametersEx(Handle, voice.Handle, posX, posY, posZ, velX, velY, velZ);

    public void Set3DSourcePosition(VoiceHandle voice, float x, float y, float z) =>
        Soloud_set3dSourcePosition(Handle, voice.Handle, x, y, z);

    public void Set3DSourceVelocity(VoiceHandle voice, float x, float y, float z) =>
        Soloud_set3dSourceVelocity(Handle, voice.Handle, x, y, z);

    public void Set3DSourceMinMaxDistance(VoiceHandle voice, float minDist, float maxDist) =>
        Soloud_set3dSourceMinMaxDistance(Handle, voice.Handle, minDist, maxDist);

    public void Set3DSourceAttenuation(
        VoiceHandle voice,
        AttenuationModel model,
        float rolloffFactor
    ) => Soloud_set3dSourceAttenuation(Handle, voice.Handle, (uint)model, rolloffFactor);

    public void Set3DSourceDopplerFactor(VoiceHandle voice, float dopplerFactor) =>
        Soloud_set3dSourceDopplerFactor(Handle, voice.Handle, dopplerFactor);

    // -----------------------------------------------------------------
    // Manual mixing (advanced)
    // -----------------------------------------------------------------

    public unsafe void Mix(Span<float> buffer, uint samples)
    {
        fixed (float* p = buffer)
        {
            Soloud_mix(Handle, p, samples);
        }
    }

    public unsafe void MixSigned16(Span<short> buffer, uint samples)
    {
        fixed (short* p = buffer)
        {
            Soloud_mixSigned16(Handle, p, samples);
        }
    }

    // =================================================================
    // Native P/Invoke surface — soloud_c.h via LibraryImport.
    // =================================================================

    [LibraryImport(LibraryName)]
    private static partial nint Soloud_create();

    [LibraryImport(LibraryName)]
    private static partial void Soloud_destroy(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_init(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_initEx(
        nint soloud,
        uint flags,
        uint backend,
        uint samplerate,
        uint bufferSize,
        uint channels
    );

    [LibraryImport(LibraryName)]
    private static partial int Soloud_pause(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_resume(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_deinit(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getVersion(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial nint Soloud_getErrorString(nint soloud, int errorCode);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getBackendId(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial nint Soloud_getBackendString(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getBackendChannels(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getBackendSamplerate(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getBackendBufferSize(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_setSpeakerPosition(
        nint soloud,
        uint channel,
        float x,
        float y,
        float z
    );

    [LibraryImport(LibraryName)]
    private static unsafe partial int Soloud_getSpeakerPosition(
        nint soloud,
        uint channel,
        float* x,
        float* y,
        float* z
    );

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_playEx(
        nint soloud,
        nint sound,
        float volume,
        float pan,
        [MarshalAs(UnmanagedType.I4)] bool paused,
        uint bus
    );

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_playClockedEx(
        nint soloud,
        double soundTime,
        nint sound,
        float volume,
        float pan,
        uint bus
    );

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_play3dEx(
        nint soloud,
        nint sound,
        float posX,
        float posY,
        float posZ,
        float velX,
        float velY,
        float velZ,
        float volume,
        [MarshalAs(UnmanagedType.I4)] bool paused,
        uint bus
    );

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_play3dClockedEx(
        nint soloud,
        double soundTime,
        nint sound,
        float posX,
        float posY,
        float posZ,
        float velX,
        float velY,
        float velZ,
        float volume,
        uint bus
    );

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_playBackgroundEx(
        nint soloud,
        nint sound,
        float volume,
        [MarshalAs(UnmanagedType.I4)] bool paused,
        uint bus
    );

    [LibraryImport(LibraryName)]
    private static partial int Soloud_seek(nint soloud, uint voiceHandle, double seconds);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_stop(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_stopAll(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_stopAudioSource(nint soloud, nint sound);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_countAudioSource(nint soloud, nint sound);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setFilterParameter(
        nint soloud,
        uint voiceHandle,
        uint filterId,
        uint attributeId,
        float value
    );

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getFilterParameter(
        nint soloud,
        uint voiceHandle,
        uint filterId,
        uint attributeId
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_fadeFilterParameter(
        nint soloud,
        uint voiceHandle,
        uint filterId,
        uint attributeId,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_oscillateFilterParameter(
        nint soloud,
        uint voiceHandle,
        uint filterId,
        uint attributeId,
        float from,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial double Soloud_getStreamTime(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial double Soloud_getStreamPosition(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_getPause(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getVolume(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getOverallVolume(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getPan(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getSamplerate(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_getProtectVoice(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getActiveVoiceCount(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getVoiceCount(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_isValidVoiceHandle(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getRelativePlaySpeed(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getPostClipScaler(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getMainResampler(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getGlobalVolume(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getMaxActiveVoiceCount(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_getLooping(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_getAutoStop(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial double Soloud_getLoopPoint(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setLoopPoint(
        nint soloud,
        uint voiceHandle,
        double loopPoint
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setLooping(
        nint soloud,
        uint voiceHandle,
        [MarshalAs(UnmanagedType.I4)] bool looping
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setAutoStop(
        nint soloud,
        uint voiceHandle,
        [MarshalAs(UnmanagedType.I4)] bool autoStop
    );

    [LibraryImport(LibraryName)]
    private static partial int Soloud_setMaxActiveVoiceCount(nint soloud, uint count);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setInaudibleBehavior(
        nint soloud,
        uint voiceHandle,
        [MarshalAs(UnmanagedType.I4)] bool mustTick,
        [MarshalAs(UnmanagedType.I4)] bool kill
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setGlobalVolume(nint soloud, float volume);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setPostClipScaler(nint soloud, float scaler);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setMainResampler(nint soloud, uint resampler);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setPause(
        nint soloud,
        uint voiceHandle,
        [MarshalAs(UnmanagedType.I4)] bool pause
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setPauseAll(
        nint soloud,
        [MarshalAs(UnmanagedType.I4)] bool pause
    );

    [LibraryImport(LibraryName)]
    private static partial int Soloud_setRelativePlaySpeed(
        nint soloud,
        uint voiceHandle,
        float speed
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setProtectVoice(
        nint soloud,
        uint voiceHandle,
        [MarshalAs(UnmanagedType.I4)] bool protect
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setSamplerate(
        nint soloud,
        uint voiceHandle,
        float samplerate
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setPan(nint soloud, uint voiceHandle, float pan);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setPanAbsolute(
        nint soloud,
        uint voiceHandle,
        float lVolume,
        float rVolume
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setChannelVolume(
        nint soloud,
        uint voiceHandle,
        uint channel,
        float volume
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setVolume(nint soloud, uint voiceHandle, float volume);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setDelaySamples(nint soloud, uint voiceHandle, uint samples);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_fadeVolume(
        nint soloud,
        uint voiceHandle,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_fadePan(
        nint soloud,
        uint voiceHandle,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_fadeRelativePlaySpeed(
        nint soloud,
        uint voiceHandle,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_fadeGlobalVolume(nint soloud, float to, double time);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_schedulePause(nint soloud, uint voiceHandle, double time);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_scheduleStop(nint soloud, uint voiceHandle, double time);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_oscillateVolume(
        nint soloud,
        uint voiceHandle,
        float from,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_oscillatePan(
        nint soloud,
        uint voiceHandle,
        float from,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_oscillateRelativePlaySpeed(
        nint soloud,
        uint voiceHandle,
        float from,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_oscillateGlobalVolume(
        nint soloud,
        float from,
        float to,
        double time
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_setVisualizationEnable(
        nint soloud,
        [MarshalAs(UnmanagedType.I4)] bool enable
    );

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getApproximateVolume(nint soloud, uint channel);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_getLoopCount(nint soloud, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_getInfo(nint soloud, uint voiceHandle, uint infoKey);

    [LibraryImport(LibraryName)]
    private static partial uint Soloud_createVoiceGroup(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_destroyVoiceGroup(nint soloud, uint group);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_addVoiceToGroup(nint soloud, uint group, uint voiceHandle);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_isVoiceGroup(nint soloud, uint group);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_isVoiceGroupEmpty(nint soloud, uint group);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_update3dAudio(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial int Soloud_set3dSoundSpeed(nint soloud, float speed);

    [LibraryImport(LibraryName)]
    private static partial float Soloud_get3dSoundSpeed(nint soloud);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dListenerParametersEx(
        nint soloud,
        float posX,
        float posY,
        float posZ,
        float atX,
        float atY,
        float atZ,
        float upX,
        float upY,
        float upZ,
        float velX,
        float velY,
        float velZ
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dListenerPosition(
        nint soloud,
        float x,
        float y,
        float z
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dListenerAt(nint soloud, float x, float y, float z);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dListenerUp(nint soloud, float x, float y, float z);

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dListenerVelocity(
        nint soloud,
        float x,
        float y,
        float z
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dSourceParametersEx(
        nint soloud,
        uint voiceHandle,
        float posX,
        float posY,
        float posZ,
        float velX,
        float velY,
        float velZ
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dSourcePosition(
        nint soloud,
        uint voiceHandle,
        float x,
        float y,
        float z
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dSourceVelocity(
        nint soloud,
        uint voiceHandle,
        float x,
        float y,
        float z
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dSourceMinMaxDistance(
        nint soloud,
        uint voiceHandle,
        float minDist,
        float maxDist
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dSourceAttenuation(
        nint soloud,
        uint voiceHandle,
        uint model,
        float rolloffFactor
    );

    [LibraryImport(LibraryName)]
    private static partial void Soloud_set3dSourceDopplerFactor(
        nint soloud,
        uint voiceHandle,
        float dopplerFactor
    );

    [LibraryImport(LibraryName)]
    private static unsafe partial void Soloud_mix(nint soloud, float* buffer, uint samples);

    [LibraryImport(LibraryName)]
    private static unsafe partial void Soloud_mixSigned16(nint soloud, short* buffer, uint samples);
}

/// <summary>
/// Common abstraction over native audio sources (<see cref="Wav"/>,
/// <see cref="WavStream"/>, <see cref="Bus"/>, <see cref="Ay"/>) so playback
/// APIs can accept any of them.
/// </summary>
/// <remarks>
/// Lifetime contract: once played on a <see cref="Soloud"/>, this source must
/// be disposed <i>before</i> that engine — the native destructor calls back
/// into the engine. GC finalization order is handled internally (the source
/// pins the engine while reachable); explicit out-of-order Dispose is not.
/// </remarks>
public interface IAudioSource
{
    /// <summary>Native handle pointer. Internal use only.</summary>
    internal nint AudioSourceHandle { get; }

    // Called by Soloud.Play*() the first time this source touches an engine.
    // Implementations hold a strong reference so the GC cannot finalize the
    // engine while a played AudioSource is still reachable — C++'s
    // AudioSource::stop() in ~AudioSource dereferences mSoloud unconditionally.
    internal void AttachEngine(Soloud engine);
}
