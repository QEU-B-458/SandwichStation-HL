# Surgery / Body Part API Notes

This documents the Frontier body part surgery API that was removed during the ss14-rebase migration.
Port these when adding bionic limb replacement or surgery features to `_HL`.

---

## What Was Deleted

Two files were deleted because they depend on Frontier's body surgery API which doesn't exist in vanilla SS14:

- `Content.Shared/_HL/Body/BionicPartReplacerComponent.cs`
- `Content.Server/_HL/Body/BionicPartReplacerSystem.cs`

---

## What They Did

`BionicPartReplacerComponent` was an item component. When used on a player:
1. It found the target body part by type + symmetry
2. Called `BodySystem.ReplaceOrInsertBodyPart()` to swap the part out with a new prototype

### Component Fields

```csharp
[DataField]
public BodyPartType TargetType = BodyPartType.Leg;   // Which part type to replace

[DataField]
public BodyPartSymmetry Symmetry = BodyPartSymmetry.None; // Left / Right / None

[DataField(required: true)]
public EntProtoId ReplacementProto;  // What entity to spawn as the replacement

[DataField]
public bool ReplaceIfPresent = true; // Replace even if a part already exists
```

---

## Missing Frontier API

These types/methods do not exist in vanilla SS14 and need to be ported from Frontier or implemented from scratch:

| Type / Method | Notes |
|---------------|-------|
| `BodyPartType` enum | Frontier enum: `Head`, `Torso`, `Arm`, `Hand`, `Leg`, `Foot`, `Tail`, etc. |
| `BodyPartSymmetry` enum | Frontier enum: `None`, `Left`, `Right` |
| `Content.Shared.Body.Part` namespace | Frontier's body part component/system namespace |
| `BodySystem.ReplaceOrInsertBodyPart(uid, type, symmetry, proto, replaceIfPresent, body)` | Frontier method on the body system that spawns and attaches a new body part |

---

## How to Port

1. Copy `BodyPartType` and `BodyPartSymmetry` enums from Frontier's `Content.Shared/Body/Part/` directory
2. Port or reimplement `ReplaceOrInsertBodyPart` on `BodySystem`
3. Restore `BionicPartReplacerComponent.cs` and `BionicPartReplacerSystem.cs`
4. Add YAML prototypes for bionic parts using `BionicPartReplacer` component

---

## Locale Keys Needed

```
replacer-success = Bionic part installed successfully.
replacer-fail    = Could not install bionic part.
```
