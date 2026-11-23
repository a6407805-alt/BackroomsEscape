# Backrooms Escape

An endless Backrooms survival game with SCP entities (Unity 6, URP). You noclipped out of reality: walk the
infinite yellow halls, find the humming exit door on each level, and don't get caught.

**Play:** download `BackroomsEscape-Launcher.exe` from the [latest release](https://github.com/a6407805-alt/BackroomsEscape/releases/latest).
It installs the game and keeps it up to date.

## Controls
WASD move · Shift sprint (stamina) · Ctrl/C crouch (silent) · Space jump · F flashlight (battery) · E use / pick up · Esc pause

## The game
- **Endless levels**: the maze is generated from a seed as you walk (`MazeField`), built and unloaded in chunks
  around you (`ChunkManager`). Level 0 "The Lobby" → Level 1 "Habitable Zone" → Level 2 "Pipe Dreams" → escape.
- **Exits** are rare doors spilling light; they hum, so you can find them by ear.
- **Sanity** drops in the dark and when you see an entity; low sanity distorts the screen and brings them sooner.
- **Pickups**: batteries (flashlight) and Almond Water (sanity + stamina).

## Entities (SCP, CC BY-SA 3.0)
| Entity | Behaviour |
|---|---|
| SCP-049 Plague Doctor | `hunter` - sees you, follows you |
| SCP-096 Shy Guy | `shy` - harmless until you look at it, then unstoppable |
| SCP-106 Old Man | `stalker` - rises from the floor, walks through walls |
| SCP-939 With Many Voices | `listener` - blind, hunts by sound; crouch to stay silent |
| SCP-860-2 Forest Monster | `hunter` that prefers the dark |

## Modding (no Unity needed)
Everything moddable lives in the game's `BackroomsEscape_Data/StreamingAssets` folder
(`Assets/StreamingAssets` in the project):
- `Config/game.json` - every number: movement speeds, stamina, flashlight, sanity, noise ranges, maze density, the
  levels (colours, lights, fog, which entities spawn, how often). Written with defaults on first run in the editor.
- `Entities/<id>/` - one folder per entity: `entity.json` (name, behaviour, speeds, senses, clip names, sounds, spawn
  rules, credits), `model.glb` (rigged + animated glTF) and `.ogg` sounds. Add a folder to add an entity.
  `modelEuler` stands the model up / turns it to face forward if it comes in sideways.

## Building & publishing
- Open the folder in Unity **6000.6.8f1**. On first load `Backrooms > Repair Project Setup` runs by itself: it creates
  `Assets/Scenes/Main.unity` (the only scene) and the material templates.
- `Backrooms > Publish Update...` builds Windows, zips it and uploads it to the `latest` release with a fine-grained
  GitHub token (Contents read/write on this repo). The launcher picks it up.
- The launcher (`Launcher/`, WPF .NET 8) is built by GitHub Actions on every change (`.github/workflows/launcher.yml`).

## Credits & licences
- SCP content is based on the SCP Wiki (scpwiki.com), CC BY-SA 3.0. Entity models, animations and sounds are from
  [SCP - Containment Breach](https://github.com/Regalis11/scpcb) by Joonas Rikkonen ("Regalis") and contributors,
  CC BY-SA 3.0, converted to glTF. Full per-entity credits are in each entity folder and on the in-game Credits screen.
  See [LICENSE-ASSETS.md](LICENSE-ASSETS.md).
- Code: see [LICENSE](LICENSE).
