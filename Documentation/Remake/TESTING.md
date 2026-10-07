# Build and validation record

## Current procedure (2026-10-06, editor-authored map)

Unity 6000.6.3f1. From the repository root (editor closed, or add `-Isolated` while it is open):

```powershell
./Tools/unity/BuildRemake.ps1            # -> Builds/Remake/Windows/HORROR-UTEZ.exe, log Library/remake-build.log
$exe = Join-Path $PWD 'Builds/Remake/Windows/HORROR-UTEZ.exe'
& $exe -remakeSolo -remakeSmoke -logFile "$PWD/Library/smoke-solo.log"          # ALL COMPLETE, exit 0
& $exe -remakeHost -remakeSmoke -remakePort 27891 -logFile "$PWD/Library/smoke-host.log"
& $exe -remakeJoin 127.0.0.1 -remakeSmoke -remakePort 27891 -logFile "$PWD/Library/smoke-client.log"
& $exe -remakeSolo -remakeTour -logFile "$PWD/Library/tour.log"                 # tour-*.png next to the exe
```

Run host and client as separate processes. Check exit codes and `[RemakeSmoke] PASS/FAIL`; failure exits 3.
Builds no longer modify `remake.unity`: the scene is authored and only `EnsureProjectSettings` runs first.

### 2026-10-06 migration results (Claude Code)

- Scene audit (batch editor): before conversion 2,822 renderers, 0 missing meshes/materials/scripts. After
  .blend→FBX conversion: identical. After baking: 3,877 renderers, 0 missing meshes/materials/scripts,
  0 unsaved procedural meshes; markers: 13 loot, 11 rooms, 21 patrol, 27 route, 4 doorways, 6 flicker,
  1 sliding door, 1 anchor. Two renderers use a material embedded in an FBX (not investigated; possibly a misnamed landmark slot).
- Bake counts equal the previous runtime logs: 480 lab renderers, 96 CDS (each floor), 48 auditorium,
  277 forest, 27 giant waypoints.
- Windows build of migrated source: success, no `error CS`.
- Solo smoke: 54 PASS / 0 FAIL, ALL COMPLETE, exit 0 — same 54 assertions as `remake-skins-final-solo.log`
  (only a chase distance differs: 6.6 vs 6.4 m). Log: `Library/migr-solo.log`.
- Loopback host/client (port 27891): host 54 PASS ALL COMPLETE, client 21 PASS CLIENT COMPLETE, both exit 0
  (same counts as before). Logs: `Library/migr-host.log`, `Library/migr-client.log`.
- Tour: exit 0, 41 captures; reviewed CC9 lab, east glass door (inside/outside), CDS interior with the giant
  and the truck: furniture, lights and doors match the pre-migration look.
- Smoke runs used `-batchmode`; the tour ran windowed. Not run: legacy PlayMode tests, Android, macOS,
  interactive play by the user.

## History


Use Unity 6000.6.3f1 and branch `remake` in the Projects checkout. Do not terminate or
reuse the user's open editor for batch work. From the repository root:

```powershell
& ./Tools/unity/BuildRemake.ps1 -Isolated
```

This refreshes Assets/Packages/ProjectSettings into `Builds/Remake/ValidationProject`,
uses its own Library, prepares the scene and builds Windows. Output is copied to
`Builds/Remake/Windows/HORROR-UTEZ.exe`. `-PrepareOnly` prepares without building.
The isolated copy is a disposable snapshot, not a Git checkout. Never develop there.
Read `Library/remake-build.log` and check the exit code; file existence alone is insufficient.

**Preparation recreates remake.unity from utez.unity and overwrites manual remake-scene
edits.** Put reproducible setup in `RemakeBuild` or preserve edits before preparing.
Editor menu: HORROR-UTEZ / Remake / Prepare playable scene, Open playable scene,
Build Windows, Build Android. Original authored `utez.unity` must remain intact.

After a fresh successful build, execute strict checks:

```powershell
$exe = Join-Path $PWD 'Builds/Remake/Windows/HORROR-UTEZ.exe'
& $exe -remakeSolo -remakeSmoke -logFile "$PWD/Library/remake-solo-smoke.log"
```

Use separate terminals/processes for `-remakeHost -remakeSmoke` and
`-remakeJoin 127.0.0.1 -remakeSmoke`, each with its own log file. Inspect exit codes and
`[RemakeSmoke] PASS/FAIL`. Failure exits with code 3. These routines are opt-in and
do not run during normal play. `-remakeTouch` enables desktop touch UI;
`-remakeCapture` requests a diagnostic screenshot. Hidden-window captures have used
URP render requests; screen-capture alone initially returned black images.

## Actual results, not inferred coverage

- Windows build succeeded before the newest input/model/filter changes.
- Initial solo assertions covered loot count, grounding/camera, death, spring grip,
  partial-cargo rejection, quota, extraction, upgrade, revival and retained upgrades.
  An initialization NullReference in voice UI occurred; voice creation order was fixed.
- Stricter solo run passed gameplay assertions but failed the no-runtime-errors assertion:
  PSX fog/screen shaders were stripped. Serialized renderer shader references were fixed.
- A subsequent isolated Windows build succeeded. Its parent script stalled before copying
  output; build was copied manually and script now uses process.WaitForExit().
- Last failed root-build smoke still used a stale executable. No clean strict rerun after
  all fixes is recorded. Do not present shader fixes as verified in the current build.
- Latest held-grab, shorter arms, refined meshes, hand prefab and RetroPixel changes:
  Blender generation passed; Unity compilation/import/runtime tests NOT performed.
- Host/client smoke implemented but NOT run. Five players, real microphones, remote
  devices, internet sessions, mobile performance and Android builds NOT validated.
- Existing original PlayMode tests were not run in this session.

### 2026-10-03 afternoon rerun (Claude Code), current source

- Isolated build of current source (held grab, short arms, new meshes, hand prefab,
  RetroPixel): compiled with no `error CS` (only CS0618 obsolete-API warnings), imported
  new FBX, `[RemakeBuild] Windows ready`, Unity exit 0. `WaitForExit()` no longer stalls.
  The script returned 3 because robocopy's success code leaked; it now resets to 0.
- Strict solo smoke on that build: all PASS including "no runtime errors during complete
  loop" — the PSX shader stripping fix is verified. Exit 0.
- FIRST host/client loopback smoke (two processes, 127.0.0.1): host ALL COMPLETE, client
  CLIENT COMPLETE (join, client grab/release acknowledged by host, no errors). Both exit 0.
- Pause-while-gripping retained the object (confirmed in code). Fixed in RemakeStudent;
  new assertion "opening pause while gripping drops cargo". Rebuilt: solo 19 PASS, exit 0;
  host/client rerun on that final build also both exit 0.
- `-remakeSolo -remakeCapture` image (spawn view): PSX pixelation/dither/vignette visible,
  truck cargo opening faces spawn with cab beyond, side text readable, both hands visible
  with plausible scale. Large world text ("RECUPERA…", quota "$0 / $…") looks oversized
  and overlaps the box at spawn distance — needs interactive review. Not a full visual QA.
- Still NOT validated: interactive Play-mode feel of hold/release and reach, hand axes in
  motion, enemies visually, 3–5 players, real mics, remote machines, Android.

### 2026-10-03 R.E.P.O.-feel pass (Claude Code), user's editor closed

New controller/camera rig, grab-point physics, tumble, cart steering and damage tiers.
Each step: isolated build exit 0 with no `error CS`, then smoke. Final build results:
- Solo strict smoke: 29/29 PASS, exit 0. New assertions: one student cannot lift the
  36 kg UPS; two students lift it; tumble becomes a physics body and gets back up; cart
  tallies cargo, grabs by handle and follows the student; hard impact reduces value;
  broken gear shatters; new day restores it.
- Host/client loopback smoke: host ALL COMPLETE, client CLIENT COMPLETE, both exit 0; no
  client exceptions while the host broke/destroyed gear afterwards.
- Spawn capture after the camera rewrite: view, HUD, arms and PSX filter render.
  Truck world text was shrunk afterwards (not re-captured).
- NOT validated: how movement, camera bob/tilt/shake, slide, tumble, rotate-object (R),
  wheel distance, cart handling and the caretaker aim-pull FEEL with real input; values
  are first-pass tuning. Tumbling players cannot be grabbed by others. Client-side
  enemy aim-pull uses approach speed, not the host's chase state.

Useful local logs: `Library/initial-import.log`, `Library/remake-build.log`,
`Library/remake-solo-smoke.log`, `Library/remake-host-smoke.log`,
`Library/remake-client-smoke.log`, `Library/remake-gameplay-capture.log`, `Library/remake-menu-capture.log`,
`Library/remake-interactive.log`. Images may exist in the Windows build folder and may
represent stale source. Caches/logs/builds are ignored and unavailable on a clean clone.

## Next acceptance checks

Compile/import current source, verify shaders without exceptions, inspect truck orientation
and hand axes/scale, test hold/release and pause while holding, and verify the PSX look.
Then run solo and host/client smoke; manually test two through five players, disconnects,
cooperative heavy lifting, voice, cargo quota and next-day penalties. Record actual results
here. Do not expand testing to Android until desktop is sound or user changes priority.

### 2026-10-03 second horror/R.E.P.O. pass (Claude Code)

Diagnostic tour: `HORROR-UTEZ.exe -remakeSolo -remakeTour -logFile ...` writes `tour-*.png` next to the exe
(labs, corridor, peer player model, truck, caretakers, giant idle/walking/close/crawling indoors).
Editor probe: `BuildRemake.ps1 -Isolated -Method HorrorUtez.Remake.Editor.RemakeDiagnostics.Probe` -> `Library/remake-method.log`.

Final results on the last build:
- Isolated build exit 0, no `error CS`; prefab sync logged 6 body materials + 22 bones.
- Solo strict smoke 31/31 PASS, exit 0 (new: 18 loot incl. 5 ID cards, ID card drop, ID card revive).
- Host/client loopback: host ALL COMPLETE, client CLIENT COMPLETE, both exit 0.
- Tour reviewed visually: labs dressed and readable (whiteboard/poster text correct way round), peer avatar with
  the new body and correct materials, giant striding outdoors, under the ceiling indoors, attack hit + damage overlay.

Problems found and fixed during this pass (do not re-introduce):
- New kit models were mirrored in X (wrong mapping) -> reflection mapping + reversed winding.
- Indoor ceilings had no colliders, so the giant walked through them -> invisible ceiling slabs.
- Desk-floor raycasts started above desks (towers ended on desktops) -> start .45 m above the floor.
- PowerShell `$method` shadowed `-Method` (case-insensitive) and skipped copying the exe -> renamed.
- PowerShell Get-/Set-Content re-encoded a UTF-8 file as ANSI (mojibake) -> use bash/perl or Edit for sources.
- Unpacked player prefab kept the old material/bone order -> RemakeBuild.SyncPlayerPrefab.
- Lift test was fragile indoors (furniture) -> runs in the open plaza.
- After the host failed once, the client window received stray UI clicks (menu reload, "solo"); cause not
  identified. The client smoke now quits 4 s after completing. Watch for spurious UI input in hidden windows.

NOT validated: real-input feel of the new giant/caretaker animation, audio mix by ear (all audio is synthesized
and was never listened to by a human), shop/spectator/revive in a real two-machine session, performance of
~480 dressing renderers + lights on weaker PCs or mobile, upper floor (not dressed; giant stays ground floor).

### 2026-10-03 night: giant routine, chase and east glass door (Claude Code)

User report: the east paving ended at a wall, the giant had no routine and never chased them.
Root causes found with new diagnostics (`-remakeWatchGiant` logs state/route legs/room reachability and writes
`nav-map.png` + `nav-cecadec.png` next to the exe):
- Paving_EastDoor (entrance landmark) sits ~6.4 m south of the v3 corridor door: it led to solid wall.
- HingedDoor.Awake ran after RemakeGame.Awake and closed every door again -> most rooms were sealed for the
  navigation (and closed for players until pressed E). Doors now open in RemakeGame.Start.
- 0.7 m grid cells could not fit 0.9 m doorways; NW rooms (sliding fronts) and the back room had no opening at all.
- The giant woke at 70 s, wandered random points, saw with one ray, had no memory; stuck detection was fooled by
  oscillation and by long detours.
- Automated test windows received the human user's mouse/keyboard (player left the truck, phantom "REINTENTAR").
Fixes: big automatic glass door at the paving (facade walls trimmed, lining rebuilt), Lab de Procesos re-laid as a
lane, NW sliding panels opened, back-room doorway cut, doorway carving in a 0.4 m grid, enemies open doors,
giant route (18 connected waypoints after automatic pruning of detours), patrol/investigate/hunt/chase with
memory, net-displacement unstick, multi-sample ceiling probe, automated runs ignore real input.
Results: solo smoke 36/36 PASS (new: route navigable, all 9 rooms reachable, giant chases a visible student
15.0 m -> 2.3 m in 3 s, glass door opens/passable, navigation crosses it); host/client both exit 0; watch run:
full loop with one momentary stuck at the CC9 doorway (recovers by skipping). West/south outdoor legs are pruned
because raised planters/kerbs split the campus for the navigation grid.

### 2026-10-03 late: Windows + macOS release builds (Claude Code)

- Installed Mac Build Support (Mono) via `Unity Hub.exe -- --headless install-modules --version 6000.6.3f1 -m mac-mono`.
- `BuildRemake.ps1 -Isolated -Mac`: both builds succeed (Windows ~168 MB, macOS universal x64ARM64 ~182 MB);
  builds are now release (no development flag). Zipped to `Builds/Remake/Release/` (Mac zip keeps exec bits).
- Release Windows solo smoke: 36/36 PASS.
- First host/client run: client FAILED "session starts" because the user's open editor was in Play hosting on
  27777 (host fell back to local play, client joined the user's session). Added `-remakePort N`; rerun on
  27877: host ALL COMPLETE, client CLIENT COMPLETE, both exit 0.
- macOS app inspected only (bundle, ad-hoc `_CodeSignature`, bundle id `mx.utez.horror.remake`, min macOS 12,
  `NSMicrophoneUsageDescription`). NOT run on a Mac. Real two-machine play NOT yet tested.


### 2026-10-05: authorized modules, campus expansion and fair enemies

Final isolated Windows build: exit 0, no compiler errors. Interactive Unity was not used.
Final smoke logs (Library, ignored):
- remake-final-solo-0.log: ALL COMPLETE, exit 0; no cart, 18 loot, 11 rooms reachable,
  giant route 27 waypoints, walls block nearby sight and melee; visible chase 15.0 -> 6.3 m.
- remake-final-solo-1/2/3.log: SECTOR COMPLETE 1/2/3, all exit 0. Three module interiors
  and loot access tested per map, truck quota/extraction and next day pass.
- remake-final-host.log / remake-final-client.log: ALL COMPLETE / CLIENT COMPLETE,
  both exit 0 on port 27877. Host authority for client grab/release passes.
- remake-final-map-host.log / remake-final-map-client.log: SECTOR COMPLETE 2 / CLIENT
  COMPLETE, both exit 0 on port 27878; host-selected Museum loads for the joining client.
- remake-final-tour.log: captures of CDS, auditorium, forest and existing campus/enemies.
  Tour exits 0. CDS, auditorium and forest captures reviewed visually; PSX filter remains active.

Intermediate failures retained in logs and fixed:
- Initial Arctic modules had closed connector variants; exporter now selects connected variants.
- Museum Double Column contained non-trigger boxes in metadata RoomVolume descendants,
  incorrectly sealing the interior; exporter now omits room/valuable volume subtrees.
- CDS entrance did not match its original hinged doors; rebuilt the runtime shell opening
  at local x -2.027782, moved office partitions clear and bridged the planter.
- A .36 m landing step exceeded navigation .35 m limit; .30 m landing fixes connectivity.
- Floor probes landed on chairs/desks, isolating NW1 navigation; scenery layer 11 is now
  excluded from floor sampling but included in body clearance. Layer 8 PlayerHead preserved.
- One diagnostic variable shadowing error and an intermediate cart cleanup compilation
  failure were corrected before the successful final build.

Unvalidated: actual human difficulty/stealth feel, audio mix, real separate-machine play,
3-5 peers, weaker-PC forest performance, Android and new macOS output. Imported maps
are compact static assemblies, not full original generator/gameplay reproduction.

User review after delivery: imported R.E.P.O. maps have missing wall textures and
invisible walls. Automated navigation/extraction passes did not validate visual fidelity.
These rendering defects remain unresolved. User prioritizes the UTEZ campus over further
work on the optional maps.

### 2026-10-05: loot/day/shop/bestiary and downloaded anatomical bases

Cycle implementation: 26 loot entities (five credentials), 16 valuable types, eight new
props, protocol 3/day seed, four day conditions, five named enemy profiles/menu manual,
moving truck extraction, additive remake_shop scene, proximity-authoritative purchases.

Intermediate complete solo and loopback logs `remake-cycle-solo/host/client.log` passed.
An early navigation initialization then regressed CDS reception reachability in
`remake-cycle-final-host.log`; corrected by keeping deterministic room-centre candidates
independent of the enemy route. `remake-cycle-acceptance-solo/host/client/tour.log` all
passed (solo/host ALL COMPLETE, client CLIENT COMPLETE). One launch used incorrectly
cased opt-in arguments and ran no tests; those diagnostic processes were stopped.

Additional strict access test failed in `remake-cycle-review-solo.log`: original UPS and
printer positions could not be approached within pickup distance. Both now use open
room centres. `remake-cycle-access-solo.log` ALL COMPLETE: every active valuable has a
reachable pickup location, palms contact the held collider, shop scene actually loads,
remote purchases rejected, physical purchase, exit proximity, next-day changes, survivors
retain upgrades, scene unload and all-dead retry restores first-day loot. No runtime errors.

User rejected primitive procedural hands; that prototype is superseded by SparrowHawk's
downloaded CC0 anatomical mesh with original weights/22 deform bones. New complete human
base also downloaded and retargeted to the existing student avatar. No downloaded rig
scripts executed. Final hand/body validation results follow once collected. Android,
Mac regeneration, 3–5 humans, real network and human grip/difficulty review remain pending.

Downloaded-base isolated build passed. `remake-base-final-solo/host/client/tour.log`
all exited 0: solo/host ALL COMPLETE, client CLIENT COMPLETE (port 27879). The new hand
check verifies TWO skinned anatomical meshes, >4,000 vertices and 22 bones each; palm
contact, weight/cooperative lift and the full shop/extraction/day loop pass online.
Reviewed captures: imported student body in idle animation, first-person grips on laptop,
camera and trophy, shop strength display, and menu manual. Capture review found excessive
hand fill lighting and a gap at the preserved photo-head neckline; reduced fill .7 -> .08
and extended the imported neck to its head joint. These final visual adjustments are
being rebuilt/rechecked; they do not alter networking or day/shop commands.

`remake-base-polish-solo.log` ALL COMPLETE and `remake-base-polish-tour.log` exit 0.
Visual review then adjusted the hand fill to .2 (the .08 experiment was too dark), and
assigned the imported neck skin instead of the sleeve material. These are visual-only
changes; final isolated output/captures are recorded below.

Final delivery build: isolated Windows exit 0; `remake-base-delivery-tour.log` exits 0.
Final student/neck and laptop-grip captures reviewed after .2 hand lighting/neck material
corrections. `git diff --check` clean; authored `utez.unity` unchanged. No additional
networking/physics changes after the successful solo/loopback runs above. The downloaded
human is used for the student's body; enemy mesh resculpting from it remains future work.
# Student skins / soft hands — 2026-10-06

Initial isolated builds compiled, but `remake-soft-solo/host/client.log`,
`remake-soft-fixed-solo.log` and `remake-soft-export-solo.log` exited 3 at the
five blendshape assertion. The character asset postprocessor overrode import settings
to false; fixed it to preserve character blendshapes. FBX export now also disables
mesh modifier baking, which otherwise discards keys. These failures are retained.
`remake-soft-rig-solo.log` ALL COMPLETE, exit 0; `remake-soft-rig-tour.log` exit 0.
New checks: selected local skin, five face resources, body/head each with five shape
keys and finite anatomical hand deformation under grip. Reviewed all five identities,
Erick front/back close-ups, crouch and four grips. Fixed idle head sideways orientation
and portrait UV placement. Menu layout/other face feature placement were polished after
this run; final isolated/network checks are recorded below once completed.
Human feel/appearance review and mobile performance remain pending.

FINAL: isolated Windows build exit 0. `remake-skins-final-solo/host/client/tour.log`
all exit 0; solo/host ALL COMPLETE and client CLIENT COMPLETE on loopback port 27881.
Host used Cesar (2), client Sebas (4), solo Erick (1); selected skins and client skin
persistence through shop/day pass. No runtime errors. `remake-skins-final-menu.log`
exit 0, selector/manual/map buttons visually reviewed without overlap. Final face,
crouch and grip captures reviewed. `git diff --check` clean after removing Unity's
generated empty-name trailing space; original authored utez.unity unchanged.
