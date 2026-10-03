# Build and validation record — 2026-10-03

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
