# Log Analysis - final_thinned.log

This document categorizes the errors and warnings found in the final_thinned.log file.

## Missing Items

### Missing RSI Files
The following RSI files could not be loaded:

- `/Textures/_species/Chitinid/Objects/Specific/Species/chitinid.rsi`
- `/Textures/_species/Kitsune/Mobs/Customization/kitsune/foxform.rsi`
- `/Textures/_species/Kitsune/Structures/Specific/Species/Kitsune/foxfire.rsi`
- `/Textures/_species/Resomi/Mobs/Species/Resomi/displacement.rsi`
- `/Textures/_species/Thaven/Mobs/Species/Thaven/displacement.rsi`
- `_species/Harpy/Mobs/Customization/EE/harpy_ears.rsi`
- `_species/Harpy/Mobs/Customization/EE/harpy_tails.rsi`
- `_species/Harpy/Mobs/Customization/EE/harpy_tails48x48.rsi`
- `_species/Harpy/Mobs/Customization/EE/harpy_wings.rsi`
- `_species/Kitsune/Mobs/Customization/kitsune/ears.rsi`
- `_species/Kitsune/Mobs/Customization/kitsune/tails.rsi`
- `_species/Shadekin/Mobs/Customization/shadekin/ears.rsi`
- `_species/Shadekin/Mobs/Customization/shadekin/tails64x32.rsi`
- `_species/Thaven/Mobs/Customization/thaven/thaven.rsi`
- `_species/Thaven/Mobs/Customization/thaven/thaven_hair.rsi`

### Missing States in RSI Files
The following states are missing from the specified RSI files:

- State 'torso' not found in RSI: `/Textures/_species/IPC/Mobs/Species/parts.rsi`
- State 'head' not found in RSI: `/Textures/_species/IPC/Mobs/Species/parts.rsi`

### Missing Entity Prototypes
The following entity prototype IDs could not be resolved:

- `NFGoblinMadeVehicleDumpster`
- `BannerGoblin01`
- `NFGoblinMadeTrashPouch`
- `NFGoblinMadeClothingBackpackTrashBlue`
- `NFGoblinMadeClothingBackpackDuffelTrashBlue`

### Missing Layer States
The following sprite layers have no RSI to load states from:

- appendix
- back
- brain
- chitzite
- chitzite_glow
- ears
- eyeball-l
- eyeball-r
- feet
- foxfire
- heart-on
- jumpsuit
- kidney-l
- kidney-r
- kitsune_fox_body
- kitsune_fox_innerear
- liver
- lung-l
- lung-r
- stomach
- tongue

## Other Issues

### Warnings

#### Prototype Registration
- `[WARN] proto: Registering an ignored prototype Content.Shared.Maps.GameMapPrototype`

#### Unknown Localization Messages (59 total)
The following message IDs are missing from the localization database:

- "Core Rules"
- "Job Expectations"
- "Rodentia"
- "Roleplay Rules"
- "Rules Changelog"
- "SandwichStation Rules"
- "marking-EarsShadekin"
- "marking-FelinidEarsBasic"
- "marking-FelinidEarsCurled"
- "marking-FelinidEarsDroopy"
- "marking-FelinidEarsFuzzy"
- "marking-FelinidEarsStubby"
- "marking-FelinidEarsTall"
- "marking-FelinidEarsTorn"
- "marking-FelinidEarsWide"
- "marking-FelinidTailBasic"
- "marking-FelinidTailBasicWithBell"
- "marking-FelinidTailBasicWithBow"
- "marking-FelinidTailBasicWithBowAndBell"
- "marking-FelionoidFacialHairBeard"
- "marking-FelionoidFacialHairColonel"
- "marking-FelionoidFacialHairFu"
- "marking-FelionoidFacialHairMane"
- "marking-FelionoidFacialHairNeck"
- "marking-FelionoidHairCrestedQuills"
- "marking-FelionoidHairFlowing"
- "marking-FelionoidHairHawk"
- "marking-FelionoidHairKeelQuills"
- "marking-FelionoidHairKeetQuills"
- "marking-FelionoidHairKingly"
- "marking-FelionoidHairMange"
- "marking-FelionoidHairNights"
- "marking-FelionoidTailAnimated"
- "marking-MobIPCHeadDefault"
- "marking-MobIPCLArmDefault"
- "marking-MobIPCLFootDefault"
- "marking-MobIPCLHandDefault"
- "marking-MobIPCLLegDefault"
- "marking-MobIPCRArmDefault"
- "marking-MobIPCRFootDefault"
- "marking-MobIPCRHandDefault"
- "marking-MobIPCRLegDefault"
- "marking-MobIPCTorsoDefault"
- "marking-MobIPCTorsoFemaleDefault"
- "markings-layer-RArmExtension"
- "marking-TailShadekin"
- "marking-TattooFelionoidHeartLeftArm"
- "marking-TattooFelionoidHeartRightArm"
- "marking-TattooFelionoidHiveChest"
- "marking-TattooFelionoidNightlingChest"
- "marking-ThavenTailDraconicLong"
- "marking-VoxLArmScales"
- "marking-VoxLFootScales"
- "marking-VoxLHandScales"
- "marking-VoxLLegScales"
- "marking-VoxRArmScales"
- "marking-VoxRFootScales"
- "marking-VoxRHandScales"
- "marking-VoxRLegScales"

### Duplicate Entries
- `[ERRO] guidebook: Adding duplicate guide entry: SandwichShipRules`

### Window Event Errors
- `[ERRO] clyde.win: Error dispatching window event DEventKeyUp:`

## Summary

This log file contains errors primarily related to missing assets for the custom species in the `_species` directory. The main issues are:

1. **Missing RSI files** - Species sprite resources that don't exist
2. **Missing states** - Specific animation states missing from existing RSI files
3. **Missing prototypes** - Entity definitions that don't exist (mostly Goblin-related)
4. **Missing localization** - Text labels that need translation entries
5. **Duplicate entries** - Guidebook entry being registered twice

The species most affected are: **Shadekin, Harpy, Kitsune, Thaven, Chitinid, IPC, and Felinid**.
