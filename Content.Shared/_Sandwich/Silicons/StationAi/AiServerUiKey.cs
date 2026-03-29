using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Sandwich.Silicons.StationAi;

[Serializable, NetSerializable]
public enum AiServerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AiServerBuiState : BoundUserInterfaceState
{
    public NetEntity ServerEntity;
    public bool HasLinkedCore;
    public NetEntity? LinkedCore;

    // Auth module data
    public bool HasAuthModule;
    public NetEntity? AuthIdCard;
    public List<NetEntity> AuthEncryptionKeys;

    public AiServerBuiState(
        NetEntity serverEntity,
        bool hasLinkedCore,
        NetEntity? linkedCore,
        bool hasAuthModule = false,
        NetEntity? authIdCard = null,
        List<NetEntity>? authEncryptionKeys = null)
    {
        ServerEntity = serverEntity;
        HasLinkedCore = hasLinkedCore;
        LinkedCore = linkedCore;
        HasAuthModule = hasAuthModule;
        AuthIdCard = authIdCard;
        AuthEncryptionKeys = authEncryptionKeys ?? new List<NetEntity>();
    }
}

[Serializable, NetSerializable]
public sealed class AiServerSetNameBuiMessage : BoundUserInterfaceMessage
{
    public string Name;

    public AiServerSetNameBuiMessage(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class AiServerRemoveModuleBuiMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;

    public AiServerRemoveModuleBuiMessage(NetEntity module)
    {
        Module = module;
    }
}

[Serializable, NetSerializable]
public sealed class AiServerRemoveAuthIdCardBuiMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AiServerRemoveAuthKeyBuiMessage : BoundUserInterfaceMessage
{
    public NetEntity Key;

    public AiServerRemoveAuthKeyBuiMessage(NetEntity key)
    {
        Key = key;
    }
}

/// <summary>
/// Raised when the AI presses the Server Panel action button.
/// Opens the linked AI Controller Server's UI remotely.
/// </summary>
public sealed partial class ToggleAiServerPanelEvent : InstantActionEvent { }
