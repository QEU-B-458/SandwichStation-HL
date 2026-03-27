using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Audio.Jukebox;

/// <summary>
/// Soundtrack that's visible on the jukebox list.
/// </summary>
[Prototype]
public sealed partial class JukeboxPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// User friendly name to use in UI.
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public SoundPathSpecifier Path = default!;

    // Sandwich: Volume slider
    /// <summary>
    /// Additional dB offset for this specific track.
    /// </summary>
    [DataField]
    public float Volume = 0f;
}
