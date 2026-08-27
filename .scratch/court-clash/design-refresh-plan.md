# Design Refresh Plan (2026-08-27)

Decided in a /grill-me session while another agent works in the code. **Plan only — implement when the code is free** (pull their work first; Scoreboard/MatchController/BallFaultTracker have new events from their session: `BallRecovered`, dead-ball watchdog, `BallContactSounds`).

Decisions were made interactively with the user; each feature below is confirmed scope.

## 1. Padel-blue floor (distinct from walls)

- Goal: floor clearly lighter/different from the dark walls, padel-court blue.
- How: keep `CourtFloorAI`'s circuit texture; new material variant `CourtFloorPadelBlue.mat` = blue base tint **plus a soft blue emission lift** (a tint alone multiplies and can only darken — emission is what makes it read lighter). Wire into `CourtBuilder.floorMaterial`.
- Fallback if it doesn't read light enough in the headset: Unity AI `GenerateMaterial` for a proper padel-blue floor texture (blue synthetic-turf look). White court lines are a possible later add as thin strips — not in this round.

## 2. Ball-impact ring ripple

- On every ball contact with walls / floor / ceiling / net (NOT rackets — those already have haptics+sound): an expanding Tron-style ring flares at the exact contact point, oriented to the surface, fading over ~0.4s. Radius and brightness scale with impact speed; soft touches are barely visible.
- Build: new `BallImpactEffects.cs` on the Ball with its own `OnCollisionEnter` (filter by collider name `CourtWall*`/`CourtFloor`/`CourtCeiling`/`CourtNet`), pooled ring quads (~8), custom unlit ring shader (SDF ring, progress-driven expansion + alpha fade, HDR cyan). The ring material must be a scene-referenced asset so its shader ships in device builds.

## 3. Score Pulse — glow trim strips

- Constraint discovered: the cyan lines on walls/floor are baked into the AI textures and cannot be recolored individually. Chosen approach: **new emissive trim strips** as the pulse surface.
- `CourtBuilder` builds `GlowTrim` strips: floor perimeter (4), wall-top edges at the ceiling junction (4), vertical wall corners (4), net top (1). Thin boxes (~0.03m), unlit emissive material, idle = faint cyan (Tron edge lighting).
- New `CourtGlowPulse.cs`: subscribes `MatchController.PointDetail` → ~1s ease-out surge of the trim emission — **green when the player scores, red when the AI scores** — then back to idle cyan. `MatchEnded` gets a longer pulse in the winner's color. Runs on a material instance shared by all strips (one material, one pulse).

## 4. AI robot recolor — light silver + orange

- Regenerate the shell texture via Unity AI `GenerateMaterial`: light titanium-silver body, red-orange accent lines kept (team identity; clearly distinct from the player's white/cyan). New asset `AIRobotShellSilver.mat`, applied to `AIRobot_Visual`. The AI's Tron racket stays as is.
- Tinting the existing dark texture cannot lighten it (multiply-only) — regeneration is the correct path, and material generation has been reliable in this project.

## 5. AI skins — 4 texture skins, X-button cycle

- **Textures on the existing robot mesh ONLY. No mesh generation** — AI-generated meshes previously shipped ~750k vertices and crashed the Quest (see PRD 2026-08-26); retopology timed out once. Texture skins are Unity AI's proven sweet spot here.
- Set of 4: the new light-silver+orange default, plus 3 generated themes (proposal: dark stealth, gold luxury, neon-green circuit — user can rename/redirect at generation time).
- New `AISkinSelector.cs`: **X button (left controller, `OVRInput.Button.One` on LTouch — currently unused)** cycles skins; applies the material to the robot's renderers; persists via PlayerPrefs (`CourtClash.AISkin`); fires `SkinChanged(name)` → Scoreboard announces it (same pattern as difficulty).
- Button map after this: A(R)=difficulty, B(R,paused/menu)=reset, X(L)=skin, MENU(L)=open menu (pause), right thumbstick(menu)=ball speed, A(menu)=start/resume, left grip=ball recall, left trigger=regrip. While the menu is open, menu bindings win.

## 6. Pause menu screen (ball speed, start, reset)

- The MENU button (☰, left controller) now opens a **world-space menu panel** instead of a bare pause: spawned ~1.5m in front of the player's head, facing them, game frozen underneath (extends `MatchPauseController`; same `Time.timeScale = 0`). Built with the scoreboard's tech (dark translucent panel + TextMesh rows — no canvas/laser-pointer dependency).
- Rows and controls (button-per-row, no cursor):
  - **BALL SPEED ×N** — right thumbstick left/right steps through `[0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.4]`. Persisted in PlayerPrefs, shown live on the row.
  - **(A) START / RESUME** — closes the menu and unfreezes; if no match is running, the ball resets to the pedestal ready to serve ("hit the ball to start" flow unchanged).
  - **(B) RESET MATCH** — existing `MatchController.ResetMatch()` (0-0, idle, player serve), stays in the menu so the player can adjust speed before starting.
- **Ball speed multiplier semantics** (user: multiply the speeds we set in code, don't change the physics): gravity, bounciness, and materials untouched. New `BallSpeedSettings` (static, PlayerPrefs-backed) consumed at the three velocity-setting sites:
  - `PlayerRacket.ApplyHit` — outgoing velocity × multiplier (maxBallSpeed scales with it);
  - `BallRecall` toss — release velocity × multiplier;
  - `AIOpponent.PlayShot` — **via flight time** (`shotFlightTime / multiplier`), NOT raw velocity: AI shots are ballistic-solved to land on a target, so raw scaling would break the arcs (0.5× would put every AI shot into the net). Longer flight = slower ball, target still reached.
- Input gating: while the menu is open, gameplay button handlers (A = difficulty, X = skin) are suppressed so the menu owns the controller. Optional later consolidation: fold difficulty and skin into menu rows.

## Order of work (single build+deploy at the end)

1. Floor material (fastest, visible immediately)
2. Pause menu + ball speed multiplier (gameplay-affecting — earliest feedback wanted)
3. Glow trims + Score Pulse (the centerpiece)
4. Impact ripple (shader work)
5. AI recolor (generation round-trip)
6. Skins ×3 + selector
7. Editor Play verification → device build → in-headset review

## Risks / notes

- All new materials must be asset files referenced from scene components (shader-variant stripping in device builds).
- Keep draw calls tame: trims are static geometry, ripples pooled; no real-time lights added.
- Pulse green vs the (already green) ball is fine — the pulse lives on court edges, not near the ball's usual sightline; the player win-flash on the ball is white and stays.
- CONTEXT.md gains: Score Pulse, Impact Ripple, Skin (at implementation time).
- Coordinate with the concurrent agent: `git pull` before starting; merge their Scoreboard/MatchController event additions carefully.
