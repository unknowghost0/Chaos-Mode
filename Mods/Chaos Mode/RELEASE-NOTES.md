# Release Notes

## Version 3.2.0

### Bug Fixes

- **Fixed timer reset bug**: The chaos timer was being reset by the host's own CHAOS_START heartbeat messages, preventing it from reaching zero and triggering effects. Timer now correctly decrements and effects trigger as expected.

- **Fixed RandomDay effect**: Now saves the original day before changing it and restores it when the effect expires. Previously, setting a random day over 500 would break the game. Now uses the full range (0-1000000) and properly restores the original day.

- **Fixed SetTimeToDay/SetTimeToNight**: Removed dependency on TimeOfDayMessage.Send which may not work in all scenarios. Now uses direct SetTimeOfDayLocal() for reliable time changes.

- **Fixed NoHUD effect**: Added missing Patch_NoHUD prefix to properly hide the HUD. Previously the effect flag was set but the HUD wasn't actually hidden.

- **Fixed OpenInventory effect**: Removed the IsBlockPickerUp check that prevented the inventory from opening. Now reliably opens the block picker when triggered.

### Improvements

- **Added version logging**: Added version string to Initialize() method to help verify DLL deployment. Check ModLoader.log for "VERSION 2026-05-01-13:30" to confirm the new DLL is loaded.

- **Added ExecuteSyncEffect logging**: Added debug logging to track when clients receive sync messages. This helps diagnose multiplayer sync issues.

### Known Issues

- **Multiplayer sync**: Some effects may not apply to all players in multiplayer. The sync mechanism broadcasts effect IDs to all clients, but certain effect implementations may be local-only. This is being investigated and will be addressed in a future update.

### Documentation

- **Updated mod.json**: Added description to explain the mod's features.
- **Created README.md**: Comprehensive documentation of all 60+ effects organized by category.
- **Created RELEASE-NOTES.md**: This file documenting all changes in this release.

### Installation

1. Copy `ChaosMod.dll` to your CastleMiner Z `!Mods` folder
2. Launch the game
3. Select "Chaos Mode" from the game mode menu

### Multiplayer

- Host a game with Chaos Mode selected
- All players will experience the some effects
- Effects sync automatically to late joiners
- Chat notifications keep everyone informed

---

## Previous Versions

### Version 3.1.0

- Initial release with basic chaos effects
- Multiplayer sync implementation
- On-screen countdown system

### Version 3.0.0

- First public release
- 60+ chaos effects
- Solo and multiplayer support
