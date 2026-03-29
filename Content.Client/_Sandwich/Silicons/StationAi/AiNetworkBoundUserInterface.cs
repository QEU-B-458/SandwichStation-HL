using Content.Shared._Sandwich.Silicons.StationAi;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Sandwich.Silicons.StationAi;

[UsedImplicitly]
public sealed class AiNetworkBoundUserInterface : BoundUserInterface
{
    private AiNetworkMenu? _menu;

    public AiNetworkBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<AiNetworkMenu>();

        _menu.JumpPressed += target =>
        {
            SendMessage(new AiNetworkJumpMessage(target));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AiNetworkBuiState msg)
            return;

        _menu?.UpdateState(msg);
    }
}
