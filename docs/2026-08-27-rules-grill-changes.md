# Rules-grill session — decisions and changes (2026-08-27)

Outcome of a read-then-decide session over the five playtest reports (serve-snap, difficulty, speed, bounces, AI racket). Every decision is recorded in `CONTEXT.md` (glossary) and, for the Game Speed semantics, `docs/adr/0001-game-speed-is-time-dilation.md`. This file is the implementation map — written for whichever session touches these files next.

## Root causes established

1. **Ball snap during AI serve**: `BallRecall` had no gating at all; a grip-hold during the AI's kinematic serve wind-up fought `AIOpponent.ServeRoutine` for the ball, and `PlayShot()` then fired the serve from the player's hand.
2. **Difficulty "stuck on Easy"**: the A button was double-booked (cycle difficulty in gameplay / resume in menu). The unified `GameMenu` already de-raced it (close on A-*release*), but the menu still advertised "(A) DIFFICULTY" while A closes the menu, and current difficulty was displayed nowhere.
3. **Speed slider → AI serves into ceiling forever**: velocity-multiplier semantics; at 0.5× the stretched flight time solved a ~7 m-apex arc → ceiling → ball drops on AI's own half → Serve Let → identical re-serve → loop. The `ClampMagnitude` on solved shot velocity also silently broke aim when it engaged.
4. **"Multi-bounce, no point"**: same incident as #3 — dead-rally bounces during the let loop. The pure rule (`PadelRallyRule`) was verified correct. Two independent soft spots found: warm-up is visually indistinguishable from a match, and flat skims (1–3 m/s) fire no bounce events but are too fast for the dead-ball watchdog.
5. **AI racket floating / robotic swing**: the racket is a prefab child of the robot root at a fixed offset; the robot is an unrigged single mesh; the swing was one rotation of one transform.

## Changes (all implemented this session)

| # | Change | Files |
|---|--------|-------|
| 1 | Ball Recall gated: legal only when no match is in progress, or the player's serve is pending with no live rally. Illegal hold force-drops (AI serve takes the ball). | `BallRecall.cs` |
| 2 | Difficulty is a Game Menu row (right stick up/down), visible at all times in the menu; the global A binding is deleted. `CycleDifficulty` → `StepDifficulty(int)`. | `GameMenu.cs`, `AIOpponent.cs`, `Scoreboard.cs` (idle text) |
| 3 | Game Speed = time dilation: menu row renamed, `timeScale` + scaled `fixedDeltaTime` carry the setting, menu-close restores the chosen speed. Velocity-multiplier code deleted (`BallPhysicsTuning.SpeedMultiplier`, both `PlayerRacket` uses, the `AIOpponent` flight-time divide). Toss velocity converted to game units (`/ timeScale`). | `GameMenu.cs`, `BallPhysicsTuning.cs`, `PlayerRacket.cs`, `AIOpponent.cs`, `BallRecall.cs` |
| 4a | "WARM-UP" label above the scoreboard whenever the session is un-armed. | `Scoreboard.cs` |
| 4b | Dead-ball watchdog widened: ball continuously below bounce height (0.35 m) for 1 s after its first bounce = second bounce at any speed (speed condition removed). | `BallFaultTracker.cs` |
| 5 | Procedural arm: runtime-built Torso→Shoulder→HandSocket chain, racket parented into the hand socket (genuinely held), swing drives hip yaw + shoulder arc + late wrist snap through wind-up/sweep/follow-through. Tron-material arm/hand visuals, colliders stripped. | `AIOpponent.cs` |

## Deliberately unchanged

- `PadelRallyRule.cs` and its tests — the rule engine was verified correct; nothing in it moved.
- `MatchController.cs` — serve alternation, let handling, and the fault-swallow window are all correct as-is (the warm-up label polls `SessionArmed` instead of adding an event).
- Serve Let stays unbounded (glossary rule): the AI re-randomizes its target every serve, and with time dilation the doomed-arc loop cannot recur.

## Same-day follow-ups (post-deploy playtest)

- **"Holding Y does not realign the court"**: two fixes in `CourtBuilder.cs`. (a) The hold timer now uses unscaled time (at Game Speed 0.5× the old scaled timer needed a 2 s real hold). (b) Root cause: play-area corners were flattened to an axis-aligned box in tracking space, so the court was built rotated relative to the drawn Guardian rectangle and a rebuild reproduced the identical mismatch. Now the rectangle's own edges are measured and the OVR tracking space is yawed so the room lands axis-aligned in world space — the court and all world-axis rule math stay canonical. Debug.Log lines from the device never reached logcat during diagnosis (still unexplained; note for future sessions).
- **"You added a third arm"**: the Tripo robot mesh has arms baked in; the procedural capsule arm overlapped its right arm. Capsule/hand visuals removed, then the real fix landed the same day: the robot was auto-rigged via Unity AI `RigMesh` (Tripo Rigging 1.0 Biped → `Assets/Models/AIRobotSource_Assets/selected.glb`, 41 bones, same 16.6k-vert mesh; needed `com.unity.cloud.gltfast`). The scene robot is now the rigged GLB (yawed −90° for glTF axes, auto-scaled ×1.72 to the old height, grounded so bounds-min sits at y=0), `AIOpponent.BuildArm` binds Waist/Upperarm/Forearm/Hand and parents the racket into the actual hand — chosen by proximity to the racket's authored spot, because the glTF handedness flip mirrors the rig's L_/R_ names — and the swing drives the real bones (hips lead, shoulder arcs, elbow follows, wrist snaps). The invisible socket chain remains as the unrigged fallback. `AISkinSelector` now targets `Renderer` (the rigged robot draws via SkinnedMeshRenderer).

## Known follow-ups

- The arm's rest pose (`shoulderLocalPosition`, segment thickness) is tuned blind — needs one on-device look.
- PlayerPrefs key migrated `CourtClash.BallSpeed` → `CourtClash.GameSpeed` (old value read once as fallback).
