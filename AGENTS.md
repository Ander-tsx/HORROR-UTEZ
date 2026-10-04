# HORROR-UTEZ: instructions for coding agents

Read `Documentation/Remake/HANDOFF.md` first, then `Documentation/Remake/ARCHITECTURE.md`
and `Documentation/Remake/TESTING.md`. The user communicates in Spanish.

## Workspace and branch

- All new-version work belongs on branch `remake`. Check `git branch --show-current`
  before changing anything. Preserve uncommitted user changes.
- Correct workspace: `C:\Users\andre\Software\Projects\HORROR-UTEZ`.
- A DIFFERENT checkout exists at `C:\Users\andre\Software\HORROR-UTEZ` on `main`.
  Unity Hub previously opened that other checkout. Do not confuse the two.
- Playable remake scene: `Assets/_Project/Scenes/remake.unity`. The scene constructs
  its truck, loot, players and HUD at runtime. Press Play to see the new menu.
- Original authored campus: `Assets/_Project/Scenes/utez.unity`. Do not regenerate or
  overwrite this hand-edited scene. `utez_blockout.unity` is the procedural reference.

## Product requirements already agreed with the user

- Personal experimental/school project, not intended for publication.
- Unity URP is mandatory; Windows and mobile are required. Windows testing has priority
  now; the user explicitly deferred Android testing during this session.
- Player-hosted cooperative sessions, maximum FIVE humans/students. Initial slice may
  use two players but network capacity must allow five.
- ONE extraction: a moving-company truck. Physically arrange valuable university and
  technology objects in its cargo bay; players board the same truck to leave.
- Physical grabbing uses funny stretching arms, not R.E.P.O.'s floating aura.
- Latest feedback: hold the grab button (release drops), shorter arm stretch, improved
  models and restore the previous PSX filter. Automated smoke (solo + loopback host/client)
  passes on current source; interactive feel and visuals still need user review.
- With a paid quota and at least one survivor extracting, dead/stranded students revive
  next day without upgrades. Students aboard keep their upgrades.
- Preserve the UTEZ campus layout; improve assets and fill missing details imaginatively.
- Use the established Blender import conventions. Sources and references are documented.
- Main menu must be original. A mixed modern low-poly/PSX aesthetic is desired.
- Later requests (2026-10-03): player fully modelled (it is online), more detailed models, more ambience,
  music and sounds, polished HUD with horror low-poly fonts, compuaulas dressed as labs (desks, mostly broken
  and knocked-over PCs, few stealables), a very large procedural humanoid enemy that moves inside and outside
  (ground floor only) adapting its posture, slightly smaller students, scarier overall, more R.E.P.O. features.

## Implementation discipline

- Do not promise the complete R.E.P.O. migration when delivering a prototype. State
  exactly which systems are implemented, tested, or still missing.
- R.E.P.O. was decompiled for reference in ignored `Library/Tooling/RepoStudy`.
  No R.E.P.O. source/assets have been copied into the current playable implementation.
- Do not touch the running interactive Unity instance to build. Use
  `Tools/unity/BuildRemake.ps1 -Isolated` while the user has the editor open.
- Unity-generated assets need their `.meta` files committed. Keep generated executables,
  tooling downloads, caches and decompiled study output out of Git.
- Keep this guide and the handoff documentation current with actual test results and
  limitations. Do not erase unresolved failures from the history.
