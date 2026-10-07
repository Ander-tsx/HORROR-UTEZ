# HORROR-UTEZ: instructions for coding agents

Read `README.md` (team workflow), then `Documentation/Remake/HANDOFF.md`, `Documentation/Remake/ARCHITECTURE.md`
and `Documentation/Remake/TESTING.md`. The user communicates in Spanish. External collaborators now work on
this repository, so keep everything understandable from the Unity editor alone.

## Workspace and branch

- `remake` was merged into `main`. Work on a feature branch and merge into `main`. Check
  `git branch --show-current` before changing anything. Preserve uncommitted user changes.
- Correct workspace on the user's PC: `C:\Users\andre\Software\Projects\HORROR-UTEZ`.
  A DIFFERENT old checkout exists at `C:\Users\andre\Software\HORROR-UTEZ`. Do not confuse the two.
- Playable scene: `Assets/_Project/Scenes/remake.unity`. It references the zone prefabs in
  `Assets/_Project/Prefabs/Map/` (CECADEC, CDS, Auditorio, Techado, Terreno, Bosque, Props_Campus,
  Gameplay_Marcadores). Networked entities (students, enemies, loot, credentials, truck) and the HUD are
  still created by code when Play starts.
- Shop: `Assets/_Project/Scenes/remake_shop.unity`, authored, loaded additively.
- `Assets/_Project/Scenes/Legacy/utez.unity` is the old single-player scene. Not used by the game.

## Map authoring rules (since 2026-10-06)

- The Unity scene and prefabs are the single source of truth for the map. Do NOT add runtime code or
  Blender/Python scripts that generate map geometry, furniture or lights. Edit prefabs instead.
- Gameplay reads marker components from the scene (`Scripts/Remake/Map/`): RemakeLootSpot, RemakeRoom,
  RemakePatrolPoint, RemakeGiantWaypoint, RemakeDoorway, RemakeFlickerLight, RemakeSlidingDoor, RemakeAnchor.
  `RemakeMapMarkers` collects them. Add a marker type rather than hard-coding coordinates.
- Unity must never need Blender: models enter the project as FBX. `.blend` files only live in `Source~`.
- The procedural generators, R.E.P.O. maps and Blender/audio scripts were removed; tag
  `pre-unity-migration` keeps them for reference.

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
- Small pieces may be modelled in Blender and imported as FBX (Documentation/Remake/BLENDER.md).
- Main menu must be original. A mixed modern low-poly/PSX aesthetic is desired.
- Later requests (2026-10-03): player fully modelled (it is online), more detailed models, more ambience,
  music and sounds, polished HUD with horror low-poly fonts, compuaulas dressed as labs (desks, mostly broken
  and knocked-over PCs, few stealables), a very large procedural humanoid enemy that moves inside and outside
  (ground floor only) adapting its posture, slightly smaller students, scarier overall, more R.E.P.O. features.

## Implementation discipline

- Current campus slice uses protocol 4, 26 loot entities (five credentials), a separate
  additive `remake_shop.unity` and five named enemy profiles/manual. Detailed first-person
  hands use SparrowHawk's downloaded CC0 skinned anatomical mesh. Student body uses a
  downloaded human base retargeted to the existing avatar. Editable originals and source
  provenance are in `Assets/_Project/Art/Remake/Source~/DOWNLOADED_MODELS.md`. Never execute
  embedded scripts from downloaded Blender files. Enemy resculpting from this base is pending.

- Do not promise the complete R.E.P.O. migration when delivering a prototype. State
  exactly which systems are implemented, tested, or still missing.
- R.E.P.O. was decompiled for reference in ignored `Library/Tooling/RepoStudy`; no R.E.P.O. C# source is
  in the game. On 2026-10-06 the user asked to delete the three R.E.P.O. expedition maps; they and their
  map selector are gone. Three small authorized R.E.P.O. props remain baked on the campus (Props_Campus /
  "Equipo del campus": trash bin, computer, server rack).
- Do not touch the running interactive Unity instance to build. Use
  `Tools/unity/BuildRemake.ps1 -Isolated` while the user has the editor open.
- Unity-generated assets need their `.meta` files committed. Keep generated executables,
  tooling downloads, caches and decompiled study output out of Git.
- Keep this guide and the handoff documentation current with actual test results and
  limitations. Do not erase unresolved failures from the history.
