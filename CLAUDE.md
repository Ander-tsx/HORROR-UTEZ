# HORROR-UTEZ

Read [README.md](README.md), [AGENTS.md](AGENTS.md) and [the session handoff](Documentation/Remake/HANDOFF.md)
before doing any work. Those are the shared source of instructions for Claude Code and Codex.
All work goes on feature branches merged into `main` (the old `remake` branch was merged into `main`).

Since 2026-10-06 the map is authored in the Unity editor: `Assets/_Project/Scenes/remake.unity` loads the
zone prefabs in `Assets/_Project/Prefabs/Map/`. Do not reintroduce procedural map generation (runtime C# or
Blender scripts); edit scenes/prefabs and use the marker components in `Scripts/Remake/Map/`.

Open `Assets/_Project/Scenes/remake.unity` and press Play.
