using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._species.Chitinid;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CoughingUpChitziteComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextCough;

    [DataField]
    public TimeSpan CoughUpTime = TimeSpan.FromSeconds(2.15);
}
