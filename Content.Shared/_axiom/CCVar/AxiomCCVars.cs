using Robust.Shared.Configuration;

namespace Content.Shared._axiom.CCVar;

[CVarDefs]
public sealed partial class SandwichCCVars
{
    /// <summary>
    /// Client-side jukebox volume multiplier.
    /// </summary>
    public static readonly CVarDef<float> JukeboxVolume =
        CVarDef.Create("sandwich.audio.jukebox_volume", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Base dB offset applied to all jukebox songs on playback.
    /// </summary>
    public static readonly CVarDef<float> JukeboxBaseVolume =
        CVarDef.Create("sandwich.audio.jukebox_base_volume", 0f, CVar.SERVERONLY,
            desc: "Base dB offset applied to all jukebox songs.");
}
