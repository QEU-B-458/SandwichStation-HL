using Content.Shared._species.Shadekin;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._species.Shadekin;

public sealed class ShadekinNightVisionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private ShadekinNightVisionOverlay _overlay = default!;
    [ViewVariables]
    private EntityUid? _effect;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnDetached);

        _overlay = new ShadekinNightVisionOverlay();
    }

    private void OnAttached(EntityUid uid, NightVisionComponent component, LocalPlayerAttachedEvent args)
    {
        TryEnable(uid, component);
    }

    private void OnDetached(EntityUid uid, NightVisionComponent component, LocalPlayerDetachedEvent args)
    {
        Disable(uid);
    }

    private void OnInit(EntityUid uid, NightVisionComponent component, ComponentInit args)
    {
        TryEnable(uid, component);
    }

    private void OnShutdown(EntityUid uid, NightVisionComponent component, ComponentShutdown args)
    {
        Disable(uid);
    }

    private void TryEnable(EntityUid uid, NightVisionComponent component)
    {
        if (_player.LocalEntity != uid || !component.Active)
            return;

        _overlayManager.AddOverlay(_overlay);

        if (_effect != null)
            return;

        _effect = SpawnAttachedTo(component.EffectPrototype, Transform(uid).Coordinates);
        _transform.SetParent(_effect.Value, uid);
    }

    private void Disable(EntityUid uid)
    {
        if (_player.LocalEntity != uid)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        Del(_effect);
        _effect = null;
    }
}
