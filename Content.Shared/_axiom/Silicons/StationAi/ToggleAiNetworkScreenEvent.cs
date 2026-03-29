using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._axiom.Silicons.StationAi;

public sealed partial class ToggleAiNetworkScreenEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public enum AiNetworkUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AiNetworkBuiState : BoundUserInterfaceState
{
    public List<AiNetworkEntry> Entries;

    public AiNetworkBuiState(List<AiNetworkEntry> entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class AiNetworkEntry
{
    public NetEntity Entity;
    public string Name;
    public bool Active;

    public AiNetworkEntry(NetEntity entity, string name, bool active)
    {
        Entity = entity;
        Name = name;
        Active = active;
    }
}

[Serializable, NetSerializable]
public sealed class AiNetworkJumpMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;

    public AiNetworkJumpMessage(NetEntity target)
    {
        Target = target;
    }
}
