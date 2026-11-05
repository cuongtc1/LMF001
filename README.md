# Lumi's Feast

Cute 2D side-scrolling action game for PC. Godot 4.7.2 (.NET) + Spine 4.3, C#.

## Setup
1. Install Git LFS, then run `git lfs install`.
2. Install the .NET 10 SDK.
3. Use the Godot **C#** editor with the Spine module (`godot-editor-windows-mono.zip`, branch 4.3, Godot 4.7.2, from the spine-godot docs). Copy its packages from `GodotSharp/Tools/nupkgs/` into `godot-nuget/`. `nuget.config` already takes every `Godot*` package from that folder. If the official Godot packages are already in `~/.nuget/packages`, delete them first.
4. Run the logic tests: `dotnet test tests/LumiFeast.Logic.Tests`

## Run
Open this folder with the editor from step 3 and press F5. Controls: arrows move, Up jump (again in the air), Down fast fall, tap a direction twice to sprint, Z attack, Shift dash, F1 debug panel, F5 in game reloads `data/`.
Smoke test: add `-- --autoplay=data/debug/autoplay_smoke.json` to the command line.
Prototype art comes from `python tools/art/prepare_prototype_art.py`.

## Layout
| Folder | Holds |
|---|---|
| `src/Logic/` | Pure game rules, no Godot. Unit tested. `Common/` holds shared helpers (`*Utils`) |
| `src/Game/` | Godot nodes. `Common/` holds shared helpers; `Gameplay/`, `Systems/`, `UI/` by feature; `Chapters/` only for code unique to one chapter |
| `tests/` | Unit tests for `src/Logic` |
| `data/` | All balance numbers (`balance/` holds difficulty and tier scaling) and `messages/messages.properties` for logs |
| `scenes/` | Godot scenes |
| `assets/` | Game-ready files only: Spine exports, audio, backgrounds, UI |
| `art_source/` | Working art (Spine projects, cut parts). Ignored by Godot |

## Rules
- No balance numbers in code: stats, timings and scaling live in `data/`. Log text uses keys from `MessageKeys.cs`.
- Code used twice moves to a `*Utils` or `*Common` file.
- File and folder names in `assets/`, `data/`, `scenes/` use `snake_case`.
- One Spine skeleton per character. A costume is a Spine **skin**, not a new skeleton, so all animations are shared.

## Adding art
- **Costume**: parts in `art_source/characters/<character>/skins/<skin_id>/`, a skin with the same name in the Spine project, then an entry in `data/characters/<character>_skins.json`.
- **Monster**: `assets/monsters/chapter_NN/<monster_id>/`.
- **Background**: `assets/backgrounds/chapter_NN/<location_id>/` with one file per parallax layer (`far`, `mid`, `play`, `front`).
- **Skill**: `assets/skills/<skill_id>/` (reused effects go in `assets/vfx/common/`).
