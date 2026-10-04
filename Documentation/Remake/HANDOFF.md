# Remake handoff — 2026-10-03 (end of the Claude Code session; next session: Codex)

The user communicates in Spanish. Their last words: "va tomando forma pero aún le falta mucho trabajo".
Read [AGENTS.md](../../AGENTS.md) first, then this file, [ARCHITECTURE.md](ARCHITECTURE.md),
[TESTING.md](TESTING.md), [BLENDER.md](BLENDER.md) and [PLAY.md](PLAY.md).

## 1. Where things are

- Checkout: `C:\Users\andre\Software\Projects\HORROR-UTEZ`, branch `remake` (nothing pushed; `main` untouched).
  A DIFFERENT old checkout lives at `C:\Users\andre\Software\HORROR-UTEZ` on `main`. Unity Hub currently lists
  only the correct one.
- Unity `6000.6.3f1` (URP) at `C:\Program Files\Unity\Hub\Editor\6000.6.3f1`. Installed modules: Windows,
  WebGL and (new this session) **Mac Build Support (Mono)**. No Android module.
- Blender: `C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe` (its bundled Python has numpy;
  the shell's `python` is only a Store alias). Blender is also handy for running Python scripts (e.g. zipping).
- Playable scene: `Assets/_Project/Scenes/remake.unity`. Almost everything (truck, loot, students, enemies,
  classroom dressing, glass door, HUD, menu) is BUILT AT RUNTIME when Play starts. The scene view outside Play
  looks like the original campus; that is expected, not "the old version".
- Original hand-authored campus: `Assets/_Project/Scenes/utez.unity`. Never regenerate/overwrite it.
- Git identity is not configured globally on this PC. Commits so far used
  `git -c user.name="Ander-tsx" -c user.email="andres.one.dev@gmail.com" commit ...`.

## 2. What the user wants (cumulative)

A personal/school cooperative horror game inspired by R.E.P.O., set on the real UTEZ campus:
- 1–5 students, player-hosted (host-authoritative TCP). Windows and macOS executables now; mobile later.
- Only extraction: a moving truck. Load fragile valuable university equipment into its cargo bay, everyone
  aboard leaves. Quota + at least one survivor = next day; the fallen revive without upgrades.
- Grabbing with comically stretching human arms (hold to grab, release drops).
- Measured UTEZ geometry preserved, presentation improved, original menu, low-poly/PSX horror look.
- Player fully modelled (online), detailed models, ambience/music/SFX, horror HUD fonts.
- Classrooms are compuaulas: desks, mostly broken/knocked-over PCs, few stealables.
- A huge procedural humanoid enemy ("El Rector") moving inside and outside (ground floor) adapting posture,
  with a patrol routine through the whole university, entering/leaving buildings and chasing players.
- The east concrete entrance must be a big glass door (done).
- Smaller students, scarier overall, and "all the R.E.P.O. features still missing" (see section 5).

## 3. What exists and works (automated evidence in TESTING.md)

- Menu (solo / host / join by IP / microphone), horror HUD (VT323 + Creepster fonts), pause, results, shop.
- R.E.P.O.-style controller: energy sprint, slide, crawl, jump buffers, landings, tumble (Q); layered camera
  rig (aim smoothing, bob, tilt, kick, shake, FOV).
- Grab physics: grab point spring, rotate (R), wheel distance, cooperative lift (UPS needs two), cart steering,
  fragility/durability damage with "-$X" popups and shattering, stuns by thrown objects.
- Truck with ramp, cargo tally, quota, departure countdown, day progression, upgrades (reach, energy, sprint,
  extra jumps), ID-card revive at the truck, spectator for the fallen.
- Dressed CECADEC ground floor: 9 rooms (compuaulas, Lab de Procesos, back room), ceiling colliders, flickering
  lights that brown out near the giant. Fixed seed (`RemakeDressing.cs`).
- El Rector: procedural IK body, crouch/crawl under ceilings, route loop (plaza -> CECADEC north door -> corridor
  -> CC9 -> back room -> Lab de Procesos -> east glass door -> east side -> auditorium -> canopy -> CDS -> plaza),
  hunts a student every 55–85 s, chases on sight (34 m) or proximity (7 m) with 8 s memory, opens doors.
  Two smaller caretaker enemies. Navigation: 0.4 m grid, doorway carving, string pulling (`RemakeGame.cs`).
- Synthesized audio (38 sounds + adaptive music), proximity push-to-talk voice (8 kHz PCM).
- Builds: `Tools/unity/BuildRemake.ps1 -Isolated -Mac` builds release Windows + macOS (universal x64/ARM64,
  Mono, unsigned). Zips were produced in `Builds/Remake/Release/` (not in Git).

Last validation (this session, release builds): solo smoke 36/36 PASS; loopback host/client on port 27877 both
exit 0. The macOS app was built and inspected (bundle, plist, microphone description) but NEVER run on a Mac.

## 4. Not validated / known problems

- Never tested by a human with real input on two machines; microphones, internet play, 3–5 players untested.
- macOS app untested on hardware; Gatekeeper workaround documented in PLAY.md.
- Audio was never listened to by a human; the mix may be bad.
- Giant: west/south outdoor legs are pruned (raised planters/kerbs split the navigation grid); CC9 doorway is
  tight (momentary stuck, recovers); upper floor not navigable.
- Upper floor, CDS and auditorium interiors are not dressed.
- Performance with ~480 dressing renderers + lights on weak PCs is unmeasured.
- Networking is prototype quality: TCP JSON snapshots, no prediction/interpolation tuning, no relay/NAT
  traversal, no host migration, raw PCM voice. Internet needs TCP 27777 forwarded or a LAN VPN.
- Progression lives in memory only (no save files).
- If the user's editor is in Play hosting, it holds port 27777: automated tests must pass `-remakePort 27877`
  (an earlier run accidentally joined the user's live session).

## 5. Suggested next work (ask the user which first)

1. Fix whatever the user reports from the first real two-player session (this has priority).
2. Giant coverage of the west/south campus (fix the grid splits, or ramps/route changes) and widen CC9 for it.
3. More R.E.P.O.-like content: grabbing tumbling teammates, more enemy archetypes, items (health packs, map,
   throwables/weapons), animated truck departure, more valuables with distinct handling.
4. Dress upper floor / other buildings; enemy stair traversal.
5. Network polish (interpolation, compression, Opus-like voice codec), save files, Android build.

## 6. How to work here (short)

- Never build with the user's open editor. Use `Tools/unity/BuildRemake.ps1 -Isolated` (mirrors into
  `Builds/Remake/ValidationProject`, builds, copies the exe back and syncs generated `.meta`/materials/scene).
  `-Mac` adds the macOS app; `-Method <Namespace.Class.Method>` runs an editor diagnostic.
- Test flags on the exe: `-remakeSolo -remakeSmoke`, `-remakeHost -remakeSmoke -remakePort 27877`,
  `-remakeJoin 127.0.0.1 -remakeSmoke -remakePort 27877`, `-remakeTour` (screenshots), `-remakeWatchGiant`
  (giant log + nav-map.png). Logs go where `-logFile` points (we use `Library/remake-*.log`).
- Art is generated by scripts in `Tools/blender/` (kit, textures, lab, props, player body) and audio by
  `Tools/audio/gen_remake_audio.py`. Kit authoring is in Unity coordinates; see BLENDER.md for the mapping.
- Don't edit sources with PowerShell Get-/Set-Content (re-encodes UTF-8). Commit `.meta` files; keep builds,
  caches and `Library/Tooling` out of Git.
- Record real results (including failures) in TESTING.md and update this file at the end of the session.

## History

Earlier sessions in short: Codex built the first playable slice (menu, grab, truck, caretakers, TCP, voice) and
diagnosed that Unity Hub had opened the wrong checkout. Claude Code then validated it, did the R.E.P.O.-feel
pass (controller, camera, grab, cart, tumble, damage), the horror/art pass (new kit, player body, compuaulas,
El Rector, audio, HUD, shop, revive), the giant routine/chase + east glass door pass, and the Windows/macOS
release builds. Per-pass test records with root causes are in TESTING.md.
