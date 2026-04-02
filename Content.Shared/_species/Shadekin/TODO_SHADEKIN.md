# Shadekin - Missing Features

## PassiveDamage DamageCap
- Vanilla `PassiveDamageComponent` does not have a `DamageCap` field
- The original Shadekin system used `DamageCap` to limit passive healing based on light exposure level
- **Fix**: Either add `DamageCap` to `PassiveDamageComponent` (upstream change) or create a custom `ShadekinPassiveDamageComponent` with the cap logic
- **File**: `Content.Server/_species/Shadekin/ShadekinSystem.cs` → `SetPassiveBuff()`

## NightVisionComponent
- Upgraded from a stub marker to a real networked component
- Species-local client overlay and shader support now exist
- Remaining work is integrating the full upstream flash-immunity and nullspace overlay coordination
- **Files**:
  - `Content.Shared/_species/Shadekin/NightVisionComponent.cs`
  - `Content.Client/_species/Shadekin/ShadekinNightVisionSystem.cs`
  - `Content.Client/_species/Shadekin/ShadekinNightVisionOverlay.cs`

## DarkLightComponent (stub)
- Created as a stub marker component
- Used to mark lights that shouldn't count towards Shadekin light exposure
- This works as-is (mappers just add the component to lights), but no lights currently use it
- **File**: `Content.Shared/_species/Shadekin/DarkLightComponent.cs`

## EyeColorInitEvent / Eye Glow
- Original had `OnEyeColorChange` handler that set `humanoid.EyeGlowing = false`
- Vanilla doesn't have `HumanoidAppearanceComponent` or `EyeGlowing` property
- **Fix**: Find vanilla equivalent for eye glow control if it exists

## NullSpace / NullPhase
- Core Shadekin phasing components, action event, server/client systems, and action prototype now exist
- Remaining work is upstream parity polish: view-through equipment, faction suppression, pressure/gravity/stealth extras, and any repo-specific eye-color behavior
- **Files**:
  - `Content.Shared/_species/Shadekin/NullSpace/*`
  - `Content.Server/_species/Shadekin/NullSpace/*`
  - `Content.Client/_species/Shadekin/ShadekinNullSpace*`
  - `Resources/Prototypes/_species/Shadekin/actions.yml`

## NullPhase / Psionics Spawn Chance (NOT IMPLEMENTED)
- The NullPhase ability (`NullPhaseComponent`) is **not** guaranteed on spawn — it is an optional trait, not a baseline racial ability
- Shadekin have a **higher chance than most species** to spawn with both the NullPhase ability and psionic powers
- This requires a spawn randomization system (e.g. trait pool or species-specific startup event) that rolls whether the entity receives `NullPhaseComponent` and/or psionic components at roundstart
- Currently `MobShadekin` has `NullPhase` and `NullSpace` hardcoded in the prototype — these should be removed once the spawn chance system is implemented and replaced with conditional grants
- **TODO**: Implement a species trait randomization system or hook into an existing psionics/trait system to grant these at spawn with weighted probability
