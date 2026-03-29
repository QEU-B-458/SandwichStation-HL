using Content.Shared._Sandwich.Silicons.StationAi;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Sandwich.Silicons.StationAi;

[UsedImplicitly]
public sealed class AiServerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AiServerMenu? _menu;

    public AiServerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<AiServerMenu>();

        _menu.NameChanged += name =>
        {
            SendMessage(new AiServerSetNameBuiMessage(name));
        };

        _menu.RemoveModuleButtonPressed += module =>
        {
            SendMessage(new AiServerRemoveModuleBuiMessage(EntMan.GetNetEntity(module)));
        };

        _menu.RemoveAuthIdCardPressed += () =>
        {
            SendMessage(new AiServerRemoveAuthIdCardBuiMessage());
        };

        _menu.RemoveAuthKeyPressed += key =>
        {
            SendMessage(new AiServerRemoveAuthKeyBuiMessage(EntMan.GetNetEntity(key)));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AiServerBuiState msg)
            return;

        // Resolve the server entity from the state (may differ from Owner when opened remotely by AI).
        var serverUid = EntMan.GetEntity(msg.ServerEntity);
        if (EntMan.EntityExists(serverUid))
            _menu?.SetEntity(serverUid);

        _menu?.UpdateState(msg);
    }
}
