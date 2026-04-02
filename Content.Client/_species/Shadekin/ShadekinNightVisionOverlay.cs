using Content.Shared._species.Shadekin;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._species.Shadekin;

public sealed class ShadekinNightVisionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "ModernNightVisionShader";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;

    public ShadekinNightVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShaderId).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalSession?.AttachedEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null)
            return false;

        return _entityManager.TryGetComponent<NightVisionComponent>(player.Value, out var comp) && comp.Active;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null ||
            !_entityManager.TryGetComponent<NightVisionComponent>(player.Value, out var comp) ||
            !comp.Active)
            return;

        var worldHandle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("NightVisionBoost", 1.0f + comp.Strength);
        _shader.SetParameter("NightVisionThreshold", 0.15f + (0.15f * comp.Strength));
        _shader.SetParameter("BlueTintIntensity", 0.15f + (0.45f * comp.Strength));

        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
