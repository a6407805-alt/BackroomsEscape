# CLAUDE.md - Backrooms Escape (Unity 6000.6.8f1, URP 17.6, Input System only)

Owner's brief (2026-11-23): an original game with controls like their friend's G.H.O.S.T, set in the Backrooms - an
infinite level whose only goal is to escape - with different entities, using real SCPs with credit (models and
animations from SCP - Containment Breach, CC BY-SA 3.0), a launcher reused from IronFauna for installs and updates.
Do not copy anything from G.H.O.S.T's build (its README forbids extraction); only match its feel by playing it.

## Premise (owner, 2026-11-23)
Aaron's drum set was taken apart and hidden in the Backrooms; Aaron is very angry. Walk the levels, find the pieces,
bring them back to Aaron (his corner is at the start of every level; he drums on whatever he has, so you can follow the
noise). His door onward unlocks when that level's pieces are back. Levels: Lobby → Habitable Zone → The Porcelain Pit
(owner's nightmare: an inverted stepped pyramid, Great-Pyramid sized, of toilets 12 ft apart, every one reachable by
walkways and stairs) → Pipe Dreams. Entities: real SCPs with credit; owner wants lots of all kinds, a different random
line-up (`rosterSize`) every level/run.

## Code map (namespace `Backrooms`, everything built from code, one scene `Assets/Scenes/Main.unity` with `Game`)
- `Game/GameBoot` - flow (boot → menu (attract mode) → run → levels → death / escape), pause, fog/ambient, post FX, sanity FX.
- `Core/GameConfig` - all tunables, loaded from `StreamingAssets/Config/game.json` (`PlayerTuning`, `WorldSettings`, `LevelTheme[]`).
- `Core/Core.cs` - `Hash` (deterministic world), `Mats` (URP templates in `Resources/BackroomsMaterials`), `ProcTex`, `ProcAudio`.
- `World/MazeField` - pure functions of (seed, cell): walls (each cell owns E/N), halls, pillars, lights, dark chunks, exits (one per region, min distance), pickups.
- `World/ChunkManager` - builds/unloads 8x8-cell chunks around the player (combined wall mesh + collider, light panels, point lights, `Flicker`), `MeshBuilder`.
- `Game/DrumKit` (pieces, procedural models + drum sounds, `DrumPickup`), `Game/AaronRoom` (riser, kit, Aaron, locked door, beat sequencer).
- `World/PitLevel` - the toilet pyramid (tiers, stairs, GPU-instanced toilets, `ToiletSeat` interact).
- `World/Interactables` - `IInteractable`, `ExitDoor` (hums, light), `Pickup` (battery, almond water).
- `Player/PlayerController` - CharacterController FPS, stamina, crouch, jump, flashlight battery, sanity, interact ray, death cam; `NoiseSystem`.
- `Entities/` - `EntityDef` (json schema), `EntityLibrary` (loads defs + ogg sounds + glb via glTFast with `LitMaterialGenerator`, fits model to height and floor), `EntityAgent` (behaviours hunter/listener/shy/stalker/apparition/trap/noisemaker, flashlight-only visibility, groups, A* on cells), `EntityDirector` (spawning, chase music), `Pathfinder`.
- `UI/` - `UIKit` (code-built uGUI), `GameUI` (menu, settings, auto-scrolling credits from entity credit blocks, HUD meters, pause, death, win).
- `Editor/BackroomsProjectSetup` (scene, build settings, material templates), `Editor/BackroomsPublisher` (Publish Update, same as IronFauna).
- `Launcher/` + `.github/workflows/launcher.yml` - the IronFauna launcher, renamed (repo a6407805-alt/BackroomsEscape, `BackroomsEscape-Launcher.exe`, `BackroomsEscape-Windows.zip`, `%LOCALAPPDATA%\BackroomsEscape`).
- `Tools/Converters/b3d2glb.py` - SCP:CB .b3d → .glb converter (see its README).

## Entity model notes
- Converted CB models face glTF -Z: humanoids use `modelEuler [0,180,0]`; 939 and 860-2 are animated Z-up in CB (CB pitches them -90) so they use `[90,180,0]` - verify in game and adjust in entity.json.
- UVs from assimp are already top-left; do not flip (checked with uvtest.py).
- SCP-173: do not use CB's model (based on the non-CC sculpture); give it a new design if added.
