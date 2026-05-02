# Chaos Mode for CastleMiner Z

Random chaos effects trigger every 30 seconds in solo and multiplayer. Features teleports, enemy spawns, item drops, weather changes, and more. Effects sync to all players in multiplayer.

## Features

- **60+ unique chaos effects** that trigger randomly
- **Works in solo and multiplayer** - effects sync to all players
- **On-screen countdown** before each effect triggers
- **Chat notifications** when effects activate
- **Timed effects** that last for a duration and then restore normal gameplay

## Effect Categories

### Player Effects
- **Kill Player** - Instantly kills the player
- **Explode Player** - Launches an explosion at the player's position
- **Launch Player Up** - Launches the player high into the air
- **God Mode** - Player becomes invincible for a duration
- **Flying** - Enables fly mode for the player
- **Air Jump** - Allows jumping in mid-air
- **No Jump** - Disables jumping
- **No Sprint** - Makes sprinting extremely slow
- **Super Sprint** - Makes sprinting extremely fast
- **Quake FOV** - Sets field of view to 140 degrees
- **Reach** - Increases block reach distance

### Combat Effects
- **Explosive Ammo** - All weapons deal explosive damage
- **Infinite Ammo** - Weapons never run out of ammo
- **No Recoil** - Removes weapon recoil
- **Fast Shot** - Increases fire rate
- **Broken Gun** - Weapons jam and misfire
- **Broken Explosive Ammo** - Explosive ammo malfunctions

### Building/Mining Effects
- **Rapid Mine** - Increases mining speed
- **Rapid Knife** - Increases knife attack speed
- **Rapid Place** - Increases block placement speed
- **Block Brush** - Enables block brush tool
- **Cant Dig** - Disables digging
- **Cant Build** - Disables building
- **Cant Craft** - Disables crafting
- **Delete Blocks** - Deletes blocks around the player

### Inventory Effects
- **Give Random Item** - Gives a random item to the player
- **Give Haunted Chainsaw** - Gives a haunted chainsaw
- **Give Diamond Door** - Gives a diamond door
- **Give Tech Door** - Gives a tech door
- **Drop All Items** - Drops all items from inventory
- **Open Inventory** - Opens the block picker
- **Cant Open Inventory** - Disables opening inventory
- **Switch Current Tray** - Switches to a random inventory tray
- **Cant Switch Trays** - Disables switching inventory trays

### Teleport Effects
- **Random Teleport** - Teleports to a random location
- **Teleport To Start** - Teleports to spawn
- **Teleport To Surface** - Teleports to surface level
- **Teleport To Lagoon** - Teleports to Lagoon biome
- **Teleport To Desert** - Teleports to Desert biome
- **Teleport To Mountains** - Teleports to Mountains biome
- **Teleport To Arctic** - Teleports to Arctic biome
- **Teleport To Miners Cove** - Teleports to Miners Cove biome
- **Teleport To Hell On Earth** - Teleports to Hell On Earth biome
- **Around The World** - Teleports to extreme distance
- **Teleport Player To Dragon** - Teleports player to the nearest dragon

### Enemy Effects
- **Spawn Random Enemy** - Spawns a random enemy type
- **Spawn Random Dragon** - Spawns a random dragon type
- **Kill All Enemies** - Kills all enemies in the world
- **Obliterate Enemies** - Destroys all enemies
- **Launch All Enemies Up** - Launches all enemies into the air
- **Teleport All Enemies To Player** - Teleports all enemies to player
- **Attach Random Blocks To All Enemies** - Attaches blocks to all enemies
- **All Enemies Give Up** - Enemies stop attacking
- **All Enemies Speed Up** - Enemies move faster
- **Enemy Name Tags** - Shows name tags on enemies

### Dragon Effects
- **Kill Dragon** - Kills the dragon
- **Dragon Name Tags** - Shows name tag on dragon
- **Attack On Titan** - Spawns multiple dragons

### World Effects
- **Set Time To Day** - Sets time to noon
- **Set Time To Night** - Sets time to midnight
- **Set Random Day** - Sets a random day (restores original after effect ends)
- **Water** - Enables water world
- **Lava** - Enables lava world
- **Random Blocks** - Spawns random blocks around player
- **Spawn Random Floor Of Blocks** - Creates a floor of random blocks

### Visual Effects
- **No HUD** - Hides the HUD
- **Bloom** - Adds bloom effect
- **Bind** - Full-screen blindness effect
- **Dark Mode** - Darkens the world
- **Fullbright** - Makes everything bright
- **Disco** - Rapidly changes sky colors
- **Timelapse** - Speeds up time passage

### Special Effects
- **Nothing** - Does nothing (a breather!)
- **Restart Game** - Restarts the game
- **Force Field** - Creates a protective barrier
- **Lightning** - Spawns lightning

## Installation

1. Copy `ChaosMod.dll` to your CastleMiner Z `!Mods` folder
2. Launch the game
3. Select "Chaos Mode" from the game mode menu

## Known Issues

**Some effects may not work properly in multiplayer.** The mod is currently being developed and certain effects may not apply to all players correctly. This is being actively worked on and will be fixed in a future update.

The following effects are known to have issues:
- Multiplayer sync for some player-specific effects (teleportation, inventory changes, etc.) may not apply to all players
- Some timed effects may not restore properly when they expire

## Multiplayer

- Host a game with Chaos Mode selected
- All players will experience the some effects
- Effects sync automatically to late joiners
- Chat notifications keep everyone informed

## Configuration

Default chaos interval is 30 seconds. This can be modified in the code if needed.

## Author

unknowghost

## Version

3.2.0
