using Content.Shared._species.Shadekin.NullSpace;
using Robust.Client.Graphics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Client._species.Shadekin;

public sealed class ShadekinNullSpaceSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private ShadekinNullSpaceOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullSpaceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NullSpaceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullSpaceComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NullSpaceComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<NullSpaceComponent, PreventCollideEvent>(OnPreventCollision);
        SubscribeLocalEvent<NullSpaceComponent, AfterAutoHandleStateEvent>(OnStateChanged);

        _overlay = new();
    }

    private void OnInit(EntityUid uid, NullSpaceComponent component, ComponentInit args)
    {
        if (uid != _playerManager.LocalEntity || !component.Activated)
            return;

        _overlayManager.AddOverlay(_overlay);
    }

    private void OnShutdown(EntityUid uid, NullSpaceComponent component, ComponentShutdown args)
    {
        if (uid == _playerManager.LocalEntity)
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(EntityUid uid, NullSpaceComponent component, LocalPlayerAttachedEvent args)
    {
        if (component.Activated)
            _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, NullSpaceComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnStateChanged(EntityUid uid, NullSpaceComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (uid != _playerManager.LocalEntity)
            return;

        if (component.Activated)
            _overlayManager.AddOverlay(_overlay);
        else
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPreventCollision(EntityUid uid, NullSpaceComponent component, ref PreventCollideEvent args)
    {
        if (component.Activated)
            args.Cancelled = true;
    }
}
