using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._axiom.Silicons.StationAi;

/// <summary>
/// UI key for the Boris pairing code entry screen on borgs.
/// </summary>
[Serializable, NetSerializable]
public enum BorisUiKey : byte
{
    Key
}

/// <summary>
/// State sent to the Boris pairing UI on the borg.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorisBuiState : BoundUserInterfaceState
{
    public bool IsPaired;
    public string? PairedServerName;

    public BorisBuiState(bool isPaired, string? pairedServerName = null)
    {
        IsPaired = isPaired;
        PairedServerName = pairedServerName;
    }
}

/// <summary>
/// Crew submits a 4-digit pairing code on the borg's Boris module UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorisSubmitCodeBuiMessage : BoundUserInterfaceMessage
{
    public string Code;

    public BorisSubmitCodeBuiMessage(string code)
    {
        Code = code;
    }
}

/// <summary>
/// Crew requests to unpair the borg from its AI server.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorisUnpairBuiMessage : BoundUserInterfaceMessage
{
}

// ===== AI-side Boris Control Panel (opened via action button on AI brain) =====

/// <summary>
/// UI key for the Boris Control panel shown to the AI player.
/// </summary>
[Serializable, NetSerializable]
public enum BorisControlUiKey : byte
{
    Key
}

/// <summary>
/// State sent to the Boris Control panel on the AI brain.
/// Shows pairing code, paired borgs, and transfer controls.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorisControlBuiState : BoundUserInterfaceState
{
    public string PairingCode;
    public List<BorisControlBorgEntry> PairedBorgs;
    public bool IsTransferred; // True if AI mind is currently in a borg.
    public NetEntity? CurrentBorg; // The borg the AI is currently controlling, if any.

    public BorisControlBuiState(
        string pairingCode,
        List<BorisControlBorgEntry> pairedBorgs,
        bool isTransferred = false,
        NetEntity? currentBorg = null)
    {
        PairingCode = pairingCode;
        PairedBorgs = pairedBorgs;
        IsTransferred = isTransferred;
        CurrentBorg = currentBorg;
    }
}

[Serializable, NetSerializable]
public sealed class BorisControlBorgEntry
{
    public NetEntity Entity;
    public string Name;

    public BorisControlBorgEntry(NetEntity entity, string name)
    {
        Entity = entity;
        Name = name;
    }
}

/// <summary>
/// AI requests to transfer mind into a paired borg.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorisTransferToBorgMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;

    public BorisTransferToBorgMessage(NetEntity target)
    {
        Target = target;
    }
}

/// <summary>
/// AI requests to return mind from borg back to core.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorisReturnToCoreMessage : BoundUserInterfaceMessage
{
}

// ===== Action Events =====

/// <summary>
/// Raised when AI presses the Boris Control action button.
/// Opens the Boris Control panel showing pairing code + paired borgs.
/// </summary>
public sealed partial class ToggleBorisControlEvent : InstantActionEvent { }

/// <summary>
/// Raised when AI presses the Return to Core action button (while in a borg).
/// </summary>
public sealed partial class BorisReturnToCoreActionEvent : InstantActionEvent { }
