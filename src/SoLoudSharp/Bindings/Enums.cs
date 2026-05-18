namespace SoLoudSharp;

/// <summary>
/// Backend identifiers accepted by <c>Soloud_initEx</c>. The shipped native
/// library is built with <c>WITH_MINIAUDIO</c> only; <see cref="MiniAudio"/>
/// is the only backend that will succeed in practice.
/// </summary>
public enum SoloudBackend : uint
{
    Auto = 0,
    Sdl1 = 1,
    Sdl2 = 2,
    PortAudio = 3,
    WinMM = 4,
    XAudio2 = 5,
    Wasapi = 6,
    Alsa = 7,
    Jack = 8,
    Oss = 9,
    OpenAL = 10,
    CoreAudio = 11,
    OpenSles = 12,
    VitaHomeBrew = 13,
    MiniAudio = 14,
    NoSound = 15,
    NullDriver = 16,
}

/// <summary>
/// Bit flags accepted by <c>Soloud_initEx</c>.
/// </summary>
[Flags]
public enum SoloudInitFlags : uint
{
    None = 0,
    ClipRoundoff = 1,
    EnableVisualization = 2,
    LeftHanded3D = 4,
    NoFpuRegisterChange = 8,
}

/// <summary>
/// Result/error codes returned by SoLoud APIs that report failure.
/// </summary>
public enum SoloudResult
{
    Ok = 0,
    InvalidParameter = 1,
    FileNotFound = 2,
    FileLoadFailed = 3,
    DllNotFound = 4,
    OutOfMemory = 5,
    NotImplemented = 6,
    Unknown = 7,
}

/// <summary>
/// Per-voice instance flags inspected via SoLoud's voice handle API.
/// </summary>
[Flags]
public enum AudioSourceInstanceFlags
{
    None = 0,
    Looping = 1,
    Protected = 2,
    Paused = 4,
    Process3D = 8,
    ListenerRelative = 16,
    Inaudible = 32,
    InaudibleKill = 64,
    InaudibleTick = 128,
    DisableAutostop = 256,
}

/// <summary>
/// Audio source level flags (defaults applied to new voices spawned from a source).
/// </summary>
[Flags]
public enum AudioSourceFlags
{
    None = 0,
    ShouldLoop = 1,
    SingleInstance = 2,
    VisualizationData = 4,
    Process3D = 8,
    ListenerRelative = 16,
    DistanceDelay = 32,
    InaudibleKill = 64,
    InaudibleTick = 128,
    DisableAutostop = 256,
}

/// <summary>
/// Oscillator waveform shapes used by <c>Sfxr</c> / <c>Speech</c> and similar sources.
/// </summary>
public enum WaveForm
{
    Square = 0,
    Saw,
    Sin,
    Triangle,
    Bounce,
    Jaws,
    Humps,
    FSquare,
    FSaw,
}

/// <summary>
/// Resampler kernels accepted by SoLoud for mixing / source sample-rate conversion.
/// </summary>
public enum Resampler : uint
{
    Point = 0,
    Linear = 1,
    CatmullRom = 2,
}

/// <summary>
/// 3D attenuation model selectors.
/// </summary>
public enum AttenuationModel : uint
{
    None = 0,
    InverseDistance,
    LinearDistance,
    ExponentialDistance,
}

/// <summary>
/// Parameter scalar type for filter attributes.
/// </summary>
public enum FilterParamType
{
    Float = 0,
    Int,
    Bool,
}
