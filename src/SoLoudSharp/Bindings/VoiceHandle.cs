namespace SoLoudSharp;

/// <summary>
/// A weak handle to a SoLoud voice (active sound instance). The native
/// engine may invalidate the handle at any time — for example when a
/// non-looping voice finishes playback. Call <see cref="IsValid"/>
/// before issuing further commands.
/// </summary>
public readonly struct VoiceHandle
{
    internal readonly Soloud Owner;
    internal readonly uint Handle;

    internal VoiceHandle(Soloud owner, uint handle)
    {
        Owner = owner;
        Handle = handle;
    }

    /// <summary>
    /// Whether the underlying voice slot is still alive. A freshly stopped
    /// or auto-killed voice reports <c>false</c>.
    /// </summary>
    public bool IsValid => Owner is not null && Owner.IsValidVoiceHandle(this);

    /// <summary>The zero value, useful as a sentinel.</summary>
    public static VoiceHandle None => default;
}

/// <summary>
/// A handle returned from <see cref="Bus.Play(IAudioSource, float, float, bool)"/>
/// representing a voice attached to a specific bus. Lifetime semantics
/// match <see cref="VoiceHandle"/>; query validity through the owning
/// <see cref="Soloud"/> instance.
/// </summary>
public readonly struct BusHandle
{
    internal readonly uint Handle;

    internal BusHandle(uint handle)
    {
        Handle = handle;
    }

    /// <summary>Returns this bus voice handle as a <see cref="VoiceHandle"/> for use against <see cref="Soloud"/>.</summary>
    public VoiceHandle AsVoiceHandle(Soloud owner) => new(owner, Handle);
}
