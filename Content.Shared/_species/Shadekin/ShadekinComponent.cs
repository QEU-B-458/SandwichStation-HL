using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._species.Shadekin;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ShadekinComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> ShadekinAlert = "Shadekin";

    [ViewVariables(VVAccess.ReadOnly), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
        public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(1f);

    [ViewVariables(VVAccess.ReadOnly)]
    public float LightExposure = 0;

    [DataField]
    public float FlashExposureBonus = 15f;

    [DataField]
    public TimeSpan FlashExposureDuration = TimeSpan.FromSeconds(3f);

    [ViewVariables(VVAccess.ReadOnly), AutoPausedField]
    public TimeSpan FlashExposureUntil = TimeSpan.Zero;
}

public sealed partial class ShadekinAlertEvent : BaseAlertEvent;
