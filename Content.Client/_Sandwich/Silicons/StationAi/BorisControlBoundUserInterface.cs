using Content.Shared._Sandwich.Silicons.StationAi;
using Robust.Client.UserInterface;

namespace Content.Client._Sandwich.Silicons.StationAi;

public sealed class BorisControlBoundUserInterface : BoundUserInterface
{
    private BorisControlMenu? _menu;

    public BorisControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<BorisControlMenu>();

        _menu.OnTransferToborg += target =>
        {
            SendMessage(new BorisTransferToBorgMessage(target));
        };

        _menu.OnReturnToCore += () =>
        {
            SendMessage(new BorisReturnToCoreMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BorisControlBuiState borisState)
            _menu?.UpdateState(borisState);
    }
}
