# Shadekin - Missing Features

## PassiveDamage DamageCap
- Vanilla `PassiveDamageComponent` does not have a `DamageCap` field
- The original Shadekin system used `DamageCap` to limit passive healing based on light exposure level
- **Fix**: Either add `DamageCap` to `PassiveDamageComponent` (upstream change) or create a custom `ShadekinPassiveDamageComponent` with the cap logic
- **File**: `Content.Server/_species/Shadekin/ShadekinSystem.cs` → `SetPassiveBuff()`

## NightVisionComponent (stub)
- Created as a stub marker component
- Original used a real night vision system that adjusted overlay/lighting for the player
- **Fix**: Implement actual night vision rendering (client-side overlay darkening/brightening)
- **File**: `Content.Shared/_species/Shadekin/NightVisionComponent.cs`

## DarkLightComponent (stub)
- Created as a stub marker component
- Used to mark lights that shouldn't count towards Shadekin light exposure
- This works as-is (mappers just add the component to lights), but no lights currently use it
- **File**: `Content.Shared/_species/Shadekin/DarkLightComponent.cs`

## EyeColorInitEvent / Eye Glow
- Original had `OnEyeColorChange` handler that set `humanoid.EyeGlowing = false`
- Vanilla doesn't have `HumanoidAppearanceComponent` or `EyeGlowing` property
- **Fix**: Find vanilla equivalent for eye glow control if it exists
