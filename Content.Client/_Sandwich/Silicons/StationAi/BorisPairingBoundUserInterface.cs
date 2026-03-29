using Content.Shared._Sandwich.Silicons.StationAi;
using Robust.Client.UserInterface;

namespace Content.Client._Sandwich.Silicons.StationAi;

public sealed class BorisPairingBoundUserInterface : BoundUserInterface
{
    private BorisPairingMenu? _menu;

    public BorisPairingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<BorisPairingMenu>();

        _menu.OnSubmitCode += code =>
        {
            SendMessage(new BorisSubmitCodeBuiMessage(code));
        };

        _menu.OnUnpair += () =>
        {
            SendMessage(new BorisUnpairBuiMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BorisBuiState borisState)
            _menu?.UpdateState(borisState);
    }
}
