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
- Shop: `Assets/_Project/Scenes/remake_shop.unity`, authored, loaded additively. Since 2026-10-12 its
  underground art is "El Sumidero", with `Prefabs/Map/Tienda_Subterranea.prefab`; editor menu
  **HORROR-UTEZ / Tienda / Abrir escena subterranea**. See `Documentation/Remake/SHOP.md` for authoring,
  markers, texture provenance and the distinction between finished art and pending mechanics.
- `Assets/_Project/Scenes/Legacy/utez.unity` is the old single-player scene. Not used by the game.
- Entrance/menu (2026-10-12): `Prefabs/Map/Entrada_UTEZ.prefab` is in `remake.unity` at (120,0,-80).
  Editor menu **HORROR-UTEZ / Entrada UTEZ / Abrir y enfocar entrada**. Move its entire root to relocate
  architecture and menu framing together; `RemakeEntrance` reads its child view/focus markers and
  binds the entrance atlas only. Reusable `Entrada_Reja` and `Entrada_Palmera` prefabs are authored.
  See `Documentation/Remake/ENTRANCE.md`. Connecting roads, gate interactions and extending enemy
  navigation to this provisional area are outside this art task.

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
- **2026-10-12 supersedes the truck requirement:** carry loot on foot to the campus fountain, throw it
  into a semi-deep sewer and jump in to enter the underground shop. Individual entry/return, ladder
  button teleports students above, preloaded additive shop, same physical loot identity/value/damage,
  explicit sale, shared funds, individual upgrades, intentional shared-money gambling risk. No quota
  or time limit for now; an internal clock keeps running in the shop and increases exterior difficulty.
  Paid tools unlock locks/secret routes and more valuable areas. The priority of this task is scene
  modelling. Its art is authored; these new mechanics and exterior fountain are NOT implemented yet.
  The executable still has the old truck/day/quota/individual-credit loop; do not claim otherwise.
- Physical grabbing uses funny stretching arms, not R.E.P.O.'s floating aura.
- Latest feedback: hold the grab button (release drops), shorter arm stretch, improved
  models and restore the previous PSX filter. Automated smoke (solo + loopback host/client)
  passes on current source; interactive feel and visuals still need user review.
- The old implementation revives dead/stranded students without upgrades after quota extraction;
  aboard survivors keep theirs. Replacement death/rescue/expedition-end rules for the sewer loop
  are still to be agreed; do not silently carry the truck rules into the new design.
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
- The shop uses an original four-region texture atlas and decorative roulette texture (ImageGen),
  seven `Shop_*.mat` materials, existing OFL fonts and original-project audio. `RemakeShopAtlasBinder`
  only binds surface properties for correct SRP atlas rendering; it does not generate scene objects.
  `RemakeShopReview` is editor-only reference/collision/capture QA, not a map generator. Keep atlas
  regions and shop anchors editable. Store geometry is still entirely in the authored scene/prefab.
