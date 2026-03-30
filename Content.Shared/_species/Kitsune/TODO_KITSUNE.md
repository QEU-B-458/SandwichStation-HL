# Kitsune - Missing Features

## Eye Color → Foxfire Color
- Original used `ProfileLoadFinishedEvent` from `_Shitmed` to read eye color from `HumanoidAppearanceComponent`
- Neither event nor component exist in vanilla
- Foxfire currently defaults to purple (`Color.Purple`) instead of matching eye color
- **Fix**: Find a way to read eye color from the humanoid marking/profile system, or make it a data field on `KitsuneComponent`
- **File**: `Content.Shared/_species/Kitsune/SharedKitsuneSystem.cs` → `OnMapInit()`
