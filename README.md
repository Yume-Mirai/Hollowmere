# Hollowmere

A **free** first-person horror/survival game for Windows, built in Unity 6 with the Built-in
Render Pipeline.

Find the fuses scattered through a dark procedurally generated facility, install them in the
fuse box, and reach the exit — while something hunts you by sight and by sound.

---

## Requirements

- **Unity 6 LTS (6000.0.x)** with the **Windows Build Support (IL2CPP)** module.
- Nothing else. No packages to import, no assets to download.

`ProjectVersion.txt` pins `6000.0.51f1`. If Unity Hub offers a different `6000.0.x` patch,
open with that — it will upgrade the file silently and harmlessly.

## Running it

1. Unity Hub → **Add** → select this folder.
2. Open it. On first open the project configures itself (product name, colour space,
   IL2CPP, always-included shaders, tags) and creates `Assets/Scenes/Main.unity`.
3. Open that scene and press **Play**.

The scene is intentionally **empty** in the editor. Everything — level, player, entity, UI —
is constructed at runtime by `GameBootstrap`. If the editor auto-setup ever fails to run,
do it by hand from the **Hollowmere** menu → *Configure Project*.

## Controls

| Input | Action |
|---|---|
| WASD | Move |
| Shift | Sprint (drains stamina, very loud) |
| Ctrl / C | Crouch (slow, quiet) |
| F | Torch on/off (drains battery) |
| E | Interact |
| Right mouse | Hold breath (quieter, drains breath) |
| Esc | Pause |

## The design, in one paragraph

Three resources are in tension: **battery** (you can only see by spending it), **stamina**
(you can only outrun the entity by spending it), and **noise** (everything you do to make
progress is audible). The entity is a four-state machine — Patrol, Investigate, Chase,
Search — over grid A*. It never teleports and never cheats: it telegraphs with a 3D growl
before it arrives, it loses you if you break line of sight and stay quiet, and how far it
can see you depends on how lit *you* are. Hiding in a locker blinds it, unless it watched
you climb in.

## Layout

```
Assets/
  Editor/
    HollowmereSetup.cs     Auto-configures the project + creates the scene
    BuildScript.cs         One-click Windows build; also a CLI entry point
  Scripts/
    Core/                  Bootstrap, run lifecycle, settings, save
    Gen/                   Level generation, A* grid, procedural materials + audio
    Player/                Movement, torch, interaction, stamina/breath/fear
    Horror/                Entity AI and the noise system
    Interact/              Pickups, fuse box, exit, hiding spots
    UI/                    Canvas, HUD, menus (all built from code)
    Steam/                 Steamworks wrapper, compiled out until you add the SDK
```

**There are no binary assets and no prefabs.** Every material is a generated noise texture,
every sound is synthesised from raw samples, every UI element is built in code. That keeps
the whole game in reviewable, diffable text and makes scene merge conflicts impossible.
The cost is that it all looks and sounds like *programmer* art — which is the right trade
this early, but it is the first thing to replace.

## Tuning

The numbers that matter most are public fields at the top of their classes:

| Want to change | Where |
|---|---|
| How long the torch lasts | `Flashlight.drainPerSecond` (0.0125 ≈ 80s of light) |
| How far the entity sees | `Entity.viewDistanceLit` / `viewDistanceDark` |
| How loud you are | `PlayerController.sprintNoise` / `walkNoise` / `crouchNoise` |
| Chase forgiveness | `Entity.loseSightGrace`, `Entity.searchDuration` |
| Level size / density | `LevelGenerator.GridW`, `RoomAttempts`, `MinRoom`, `MaxRoom` |
| Fuses per run | `GameManager.fusesRequired` |

## Building

**Hollowmere → Build Windows x64** (Ctrl+Shift+B), or from the command line:

```bash
Unity.exe -quit -batchmode -projectPath . -executeMethod Hollowmere.EditorTools.BuildScript.BuildWindows64 -logFile build.log
```

Output goes to `Build/Windows64/Hollowmere.exe`.

## Shipping on Steam

See [Steam/README.md](Steam/README.md). Short version: the code side is scaffolded and
compiles out cleanly until you import Steamworks.NET — but the account, the $100 Steam
Direct fee, the tax and banking verification, and pressing *publish* are all things you
have to do yourself.

## Status

Playable vertical slice. Complete: generation, movement, torch, stamina/breath/fear,
entity AI, hiding, the fuse→power→exit objective loop, menus, settings, save, and the
build pipeline.

Not done yet: no music, no voice, no hand-authored levels, one enemy type, one objective
type, placeholder art throughout.
