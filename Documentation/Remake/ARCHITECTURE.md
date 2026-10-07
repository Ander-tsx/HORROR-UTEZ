# Runtime architecture

Assembly `HorrorUtez.Remake` depends on Core, World, Rendering, Input System, uGUI and
URP. Its nested Editor assembly handles setup, importing and builds. No dependency on
the old first-person player assembly is needed in the new runtime controller.

| File | Responsibility |
|---|---|
| `RemakeGame.cs` | World setup, sessions, authoritative commands, phases, quota, death, progression, snapshots, doors, ground-floor navigation grid |
| `RemakeStudent.cs` | Local controller (friction-smoothed velocity, sprint ramp/energy, crouch-slide, crawl, coyote/jump buffers, landings), tumble capsule, grab point/rotate/scroll input, caretaker aim-pull; peer avatars/animation; stretchy arms |
| `RemakeCameraRig.cs` | Layered first-person camera: smoothed aim, footstep bob (drives footsteps), turn/strafe tilt, jump/land/hit kick springs, shake, sprint/scare FOV |
| `RemakeLoot.cs` | Grab-point spring with per-student lift cap, camera-relative orientation torque, cart steering and in-cart tally/stabilisation, fragility/durability damage tiers, shattering, replication |
| `RemakeBody.cs` | Procedural animation for segmented enemy models: joint hierarchy from rest pose, two-bone IK legs/arms, planted stepping (biped or diagonal quadruped), stand-to-crawl posture, head tracking with twitches, arm-swipe attack |
| `RemakeDressing.cs` | Runtime dressing of CECADEC's ground-floor rooms as wrecked compuaulas (fixed seed, identical on every peer), invisible ceiling slabs, flickering fluorescents (brown out near the giant), indoor loot spots and giant patrol points |
| `RemakeCampusExpansion.cs` | CDS offices on both floors, auditorium seats/stage, aligned CDS entrance, exterior props and deterministic denser forest |
| `RemakeRepoMaps.cs` | Authorized static module meshes/textures/colliders reconstructed from JSON; three compact layouts, three original modules per layout, PSX/URP materials |
| `RemakeAudio.cs` | Adaptive music (explore/chase crossfade), heartbeat, tired breathing, wind, 3D one-shot pool, loops, random distant scares, giant footfall camera shake |
| `RemakeEnemy.cs` | Caretakers and the giant "El Rector": hearing/vision, grid paths, ceiling probe that folds the giant into a crawl, wind-up attacks with knockback, stuns from thrown gear, replicated flags |
| `RemakeWire.cs` | TCP listener/client, framing, connection cap, bounded queues, per-peer reader/writer tasks and serializable DTOs |
| `RemakeVoice.cs` | Opt-in microphone capture, PCM downsampling, push-to-talk, spatial streamed playback and Android permission |
| `RemakeHud.cs` | Horror-styled menu/HUD (VT323 + Creepster OFL fonts), health/energy/haul, reactive crosshair, damage and low-health overlays, fallen/spectator banner, results with the 6-line upgrade shop, touch controls |
| `RemakeSmoke.cs` | Command-line opt-in executable integration test; never attached in normal play |
| `RemakeCapture.cs` | Opt-in diagnostic screenshots; `-remakeTour` captures labs, corridor, player model, truck, enemies and the awake giant outdoors and crawling indoors |
| `Editor/RemakeBuild.cs` | FBX importer, materials, copied playable scene, serialized shaders, Windows/Android build commands |

## Authority and wire protocol

Host = local student ID 0. Up to four peer sockets are accepted. The host allocates IDs;
incoming client `id` values do not decide identity. `hello` carries protocol version 3 in
`day` (26 loot IDs and session seed); the host sends `welcome`, then `state`. Joining is allowed during active salvage.

Frames: network-order int32 UTF-8 length, followed by JSON. Maximum 128 KiB. Inbox is
bounded indirectly by disconnecting abusive senders at 256 queued frames; each writer
has a 48-message bounded queue. One reader and writer task per peer, concurrent inbox,
all Unity callbacks and JSON/state processing on the main thread. Socket disposal ends
loops. Snapshots every 75 ms, local pose reports every 50 ms.

Messages: `hello`, `welcome`, `state`, `pose`, `grab`, `drop`, `door`, `extract`, `upgrade`,
`next`, `voice`, `sound`. Host validates finite/bounded poses, reach, proximity/line of
sight to loot, door distance, alive/phase status, quota/cargo containment and upgrade funds.
Snapshots contain students, loot transforms/value, enemy positions, door targets, phase and map ID.
Clients interpolate non-owned visuals and use kinematic loot; only the host applies forces.

This is a trusted personal prototype. It is not authenticated, encrypted or production
anti-cheat. TCP is reliable but can suffer head-of-line stalls under poor networks.

## Physics and extraction invariants

Layers 9 = students, 10 = loot. World probes exclude both so held objects don't hide walls.
R.E.P.O. reference (2026-10-03): PlayerController, Camera{Aim,Bob,Tilt,Jump,Zoom}, PhysGrabber,
PhysGrabObject, PhysGrabCart, PlayerTumble and PhysGrabObjectImpactDetector were decompiled into
ignored `Library/Tooling/RepoStudy` and used to model these systems.

Grab: the client sends the hit point in object-local space (`grabLocal`) and the object's rotation
relative to the camera (`hold`); poses carry `hold`, eye height, reach and tumble. Host pulls the
grab point (not the centre) toward eyes + aim * reach: acceleration = 85·error − 13·point velocity,
capped per living holder at 9.81·1.6·(16 + 6·strength)/mass, applied at the grab point so objects
hang and swing. Holders add up: one student cannot lift the 36 kg UPS, two can (smoke-tested).
Orientation torque aims at camera·hold. Reach .75–1.9 m (wheel ±.2), arms stretch up to 2.1 m.
Grip releases if the grab point is >3.2 m from the puller. Cart (kind 6): the first holder steers
its velocity to 1.9–2.5 m ahead (max 5 m/s) and yaws the handle toward them; items inside are
tallied, eased to the cart velocity and protected from damage.

Damage: impacts ≥1.6 m/s make noise; force = speed·fragility/100 gives light (≥2.2), medium (≥4.2)
or heavy (≥6.5) tiers losing 1/5/10 % of base value × (1 + 9·(100 − durability)/100). Below 15 % of
base value the item shatters (deactivated, value 0, loud noise) until the next day. First pickup gives
0.5 s of immunity. Clients mirror losses as floating “-$X” markers from snapshots.

Tumble: Q (or a caretaker hit, with knockback and a 1.2 s lock) swaps the CharacterController for a
60 kg rolling capsule simulated by its owner. Other students cannot grab tumbling players yet.

Truck origin `(12, .06, -2)`. Its fixed compound shell is hollow; ramp climbs to .84 m.
Visual FBX rotates 180 degrees to align with local shell coordinates. Positive local Z
is the cabin; rear entry is negative Z. Player must be inside the cargo bay, not merely
near the truck. Count only positive-value items with full collider bounds inside the bay,
no holders and speed below 1.5 m/s. Objects remain physical and can lose value by impacts.

Phases: 0 menu, 1 salvage, 2 five-second departure, 3 success/report, 4 expedition failed.
Day 1 quota $1600, time 8 minutes; later quota rises $220/day, capped after five increases.
Departure cancels if the quota ceases to be paid or no living student remains aboard.
On success, aboard survivors retain upgrades and earn $100 + surplus/8. Others reset
strength and currency; next day everyone revives to 100 HP. Grip upgrade costs $80,
maximum level 3. Only host advances/restarts a day; peers can buy their own upgrades.

## Visuals, input and audio

Latest source selects `PsxVisualStyle.RetroPixel` to restore the user's previous filter.
The additional `Salvage` preset combines 720-row sampling and mild retro effects.
Grabbing requires holding left mouse/G/right shoulder or the touch grip button; release
drops the object (opening pause also drops it). R/left shoulder rotates the held object.
Movement: walk 2.6, sprint 5.4 (ramp 1.4/s), crouch 1.3 m/s; energy 40, drain 4.5/s, recharge 3/s
after 1 s; slide costs 2. Gravity 18, jump 5.6 m/s, coyote and input buffers .25 s.
Fog/screen shaders must be serialized in renderer feature assets or the
player build strips them. Menu/HUD are programmatic uGUI with the built-in LegacyRuntime
font. Pause releases cursor but does not freeze the shared world.

Local controls use Input System directly plus pointer-event touch pads. Mobile runs 30 fps,
has touch move/look/action buttons and no local flashlight shadows; desktop defaults 60.
Peer character models reuse the existing Humanoid prefab/controller with its old controller
driver disabled and animation parameters updated from replicated motion.

Voice: manually enable mic, hold V/touch VOZ, 100 ms packets, signed little-endian 16-bit
mono PCM at 8 kHz encoded as base64. Host relays and emits hearing noise. Receiver uses
bounded float queues and streaming AudioClip callbacks protected by a lock. Spatial source
follows sender, linear attenuation to 20 m; dead students are muted. No local audio files
are recorded. Actual-device audio needs manual validation.

## Second R.E.P.O./horror pass (2026-10-03, evening)

- Art: `Tools/blender/remake_kit.py` authors in Unity coordinates (reflection mapping, fitted UVs computed in
  Unity space); `gen_remake_textures.py` makes 51 pixel textures; RemakeBuild turns each into a PSX/Lit material
  via `PsxMaterialDefaults` (kind chosen by name). `gen_remake_lab.py` = 26 lab pieces in `Resources/Lab`.
  `gen_remake_props.py` (v2) = detailed loot (+ oscilloscope, network switch), truck and SEGMENTED caretaker/giant.
  No runtime 180-degree flips remain. Player body: `gen_player_body_v3.py` (see BLENDER.md); RemakeBuild
  `SyncPlayerPrefab` copies materials/bones from the FBX into the unpacked prefab.
- Students are 1.5 m (eye 1.38 m) and peer avatars are scaled 0.88, so the campus and the giant loom larger.
- Navigation grid: 0.7 m cells, 8-way without corner cutting, line-of-sight string pulling; furniture blocks cells.
- Giant: 3.55 m standing, 1.45 m crawling; ceiling/doorway probe (SphereCast from 0.5 m, plus 1.4 m ahead) sets
  the crawl factor; CharacterController height follows it. Damage 45 + knock (9 m/s + 6 up). Thrown gear stuns
  when speed*sqrt(mass) >= 16 (caretaker 7). Enemies live on layer 9 so world probes ignore them.
- Hurt(id, dmg, knock): knock and hurtCount ride in StudentState; the victim's own client tumbles with it.
- Revive: five hidden ID-card loot items (kind 9, ids 13..17) exist on every peer. Death drops one at the body
  (owner = student id, replicated in LootState.owner); a settled card inside the cargo bay revives its owner in
  the truck with a third of max health. Fallen students spectate living teammates.
- Upgrades (StudentState): strength, stamina (+10 energy), range (+0.25 m reach), speed (+0.45 m/s sprint),
  vitality (+20 max HP), jumps (air jumps). Cost 60 + 45 x level (+20 strength). Stranded students lose all.
- Audio: `Tools/audio/gen_remake_audio.py` synthesizes 37 WAVs (numpy, offline) into `Resources/Audio`.
- BuildRemake.ps1 `-Isolated` mirrors Assets into the validation project, then copies generated .meta files,
  materials, the prepared remake.unity and the synced player prefab back (robocopy /XO).

## 2026-10-05 expansion (supersedes historical cart/perception details above)

- Kind 6 remains reserved in the old asset tables, but no cart is spawned or exposed in the HUD.
  Loot ID 10 now contains a network switch; the 18-item total and credential IDs stay stable.
- Enemies use occluded head/chest rays even at close range; standing field of view is 130 degrees,
  crouched 90. Exposure needs .6 s standing / 1.2 s crouched, except at close range or during an
  established chase. Crouched sight range is reduced. Hearing through walls has 30% range.
  No periodic hunt receives hidden player coordinates, and chase steering uses lastKnown only.
- Giant/caretaker sight 24/18 m, memory 4/3 s, initial chase speed 3.25/3 m/s; day scaling caps at +.45.
  Attacks recheck visibility at windup and impact, allow .75/.6 s to dodge, deal 30/18 HP and have
  3.2/2.5 s cooldowns. Stuns investigate the last visible position.
- Navigation samples actual ground height under each cell and rejects steps over .35 m. It stays
  ground floor only; furniture above .75 m is not accepted as navigable floor.
- CDS receives office partitions, wrecked PCs, shelves and lighting on both floors; the authored
  south storefront and generic shell openings are aligned at runtime. Stairwell space is reserved.
  Auditorium receives stage/seats/control equipment. Forest adds deterministic vegetation by reusing
  the existing project's meshes, preserving the paved campus. Weather's ambient/moon light runs at 50%.
- Authorized static R.E.P.O. assets are exported with UnityPy into `Art/RepoAuthorized/Resources/Repo`.
  Three optional maps use three distinct original modules each. JSON preserves local transform hierarchy,
  geometry, UV, submeshes and colliders; textures load into copied PSX/Lit materials. Connector variants
  are opened, animated door leaves omitted. The original C# scripts, Photon, generator and events are
  not loaded. Four meshes are additionally exported for campus props (three currently placed).
- Host chooses a map in the menu; snapshots replicate map ID, and the client reconstructs it before
  applying loot/enemy poses. Both peers must use protocol version 3 / this build. `-remakeMap 1..3`
  selects maps for diagnostics. Map smoke checks all three module interiors, loot access, extraction,
  second day and runtime errors. A failed layout setup restores the campus before returning to menu.
- Isolated builds now synchronize metadata for new runtime scripts and the authorized asset directory.

## 2026-10-05 campus day-cycle pass (in progress)

- `RemakeLootCatalog`: eight additional modelled valuables, kinds 10..17 and IDs 18..25.
  Existing credential IDs 13..17 stay stable. Total 26 entities, 16 valuable types;
  two special valuables are absent per day. Impact fragility and durability vary by type.
- `RemakeDayCycle`: shared session seed, ordered room-center candidates, per-day shuffle,
  special-loot selection, value variation and enemy activation/spawns. First-day plaza
  loot is preserved. Four conditions: thicker fog, partial fluorescent outage, all five
  enemies on duty, reduced moonlight. Campus walls/layout stay authored and unchanged.
  Use room centers, not the dynamically pruned giant route, as canonical campus loot candidates.
- `RemakeShop` / `remake_shop.unity`: distinct additive scene at x=200, six physical
  upgrade displays, counter, lamps and return terminal. Session, students and voice stay
  in the campus scene. On success, three-second truck/cargo/boarded-student motion precedes
  loading the shop. Failed/stranded students reset upgrades and currency; all can walk
  in the safe shop. Only survivors buy; host validates distance, funds and max level.
  Only the host near the exit starts a new day. Shop unloads and truck resets on return.
- `RemakeBestiary`: Carsi (hearing, short sight), Hugo (giant), Ulises (flashlight-sensitive
  sight), Cristian (valuable guard/hauling students), Derick (two-second sprint, three-second
  recovery). New profiles reuse and modify the segmented caretaker base, with visor,
  keys, runner details and tint. Five manual pages are available in the main menu.
- Lift cap reduced to 21 + 7 x strength kg per holder; original item masses increased.
  Movement and visible effort use mass shared between holders. Reach reduced to 1.6 m.
- The procedural `RemakeHandRig` prototype was explicitly rejected visually by the user
  and has been replaced with SparrowHawk's downloaded CC0 anatomical mesh. Resources
  `AnatomicalHand.json` preserves vertices, UVs, author weights and 22 deform bones;
  runtime uses SkinnedMeshRenderer, mirrored left-hand base, collider-limited joint curl
  with fixed bone lengths, effort tremor and imported forearm/arm connection. JSON avoids
  legacy Blender FBX constraint/axis ambiguity; source .blend/exporter remain editable.
- Student body now derives from abdoubouam's downloaded human basemesh, remapped to the
  existing avatar bones; photo head, uniform/accessories and animation avatar preserved.
  Original complete rigged human and adapted StudentFromHumanBase.blend are retained.
  Enemy animation still uses existing segmented models; the human source is available
  for future resculpting. See Source~/DOWNLOADED_MODELS.md for source/licenses/commands.
- Weather `FogScale` defaults to 1 for original scenes; remake sets day/store scales.
  Version 4 snapshots carry the session seed and per-student skin; older clients cannot join.

2026-10-06: RemakeSkins supplies five selectable identities (Ander, Erick, Cesar, Juan,
Sebas), face textures, clothing/hand tint and body/head blendshapes. Selection persists
in PlayerPrefs, is transmitted in hello and authoritative StudentState, and survives
day transitions. Erick preserves the original portrait with a slimmer body; the other
faces are original stylized identities, not likenesses from photographs.
Remote idle arms relax through humanoid bones; crouch uses the animator rather than
scaling the body. Head orientation retains the rig's neutral frame and follows aim.
Downloaded first-person hands use smoothed weights and CPU dual quaternion skinning,
gradual collider-aware finger curl and reversible visual compression. This is visual
deformation, not a physical soft-body solver. Mobile performance remains untested.
