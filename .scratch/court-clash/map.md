# Court Clash — build-spec map

Label: wayfinder:map

## Destination

A locked build spec for Court Clash v1 — every open mechanic/scope decision (fault-rule semantics, match/session structure, AI opponent v1 scope, XR testing path, Week 8 rubric constraints) resolved and written down as an addendum to `PRD.md`, so implementation can start immediately after with no open "how should this work?" pauses.

## Notes

- Domain: a grounded VR 1v1 ball-sport game for Meta Quest 2 ("Court Clash", working title). Full background lives in `PRD.md` (this repo, one level down from this map) and `../MISSION.md`, `../NOTES.md`, `../RESOURCES.md`, `../learning-records/` (one level up, in the teaching workspace — this Unity project is nested inside it).
- Concept, stack, and out-of-scope list are already locked (see `PRD.md`) — this map does not reopen those; it only resolves what's left before build starts.
- The human collaborator is new to coding entirely. Once this map is walked, implementation is AI-paired (Claude writes the C#/scene directly), not something the map itself carries out — plan, don't do, applies here.
- Use `/grilling` and `/domain-modeling` for the grilling-type tickets. Use the meta-vr plugin's doc search (`meta_docs_search`) and `metavr_run` for the XR-Simulator research ticket.
- Sibling effort in a different repo (`WK08_PC`) is planning an unrelated Star-Fox-style dogfight game — not this project, ignore for context.

## Decisions so far

- [XR Simulator testing path — research](issues/04-xr-simulator-testing-path-research.md) — no documented fix for the file-write block; default to the device-only test loop (Unity Build-and-Run to Quest 2, or `metavr_app`/`take_screenshot`/`get_device_logcat`) instead of chasing the Simulator install. Full findings on branch `research/xr-simulator-testing-path` (commit `b8cad32`). Unblocks ticket 05.
- [Fault/bounce-rule semantics](issues/01-fault-bounce-rule-semantics.md) — per-throw bounce counter (resets on catch/re-throw), floor and wall bounces count identically, no serve exemption. A single `bounceCount` on the ball, limit of 1.
- [Match/session structure](issues/02-match-session-structure.md) — no menu for v1: match starts on first grab of the ball, first-to-11 single game, ends on 11 with simple win/lose feedback, ball reset, re-grab to restart.
- [AI opponent v1 scope](issues/03-ai-opponent-v1-scope.md) — single fixed difficulty; capped movement speed + bounce-angle-scaled reaction delay make it beatable; random-cone aim only (gap-aiming deferred past v1).
- [Week 8 rubric](issues/06-week8-rubric.md) — no formal constraints; deadline Friday; bar is "usable," a prototype is fine. Confirms locked v1 scope is already sized right.
- [XR Simulator testing path — decide](issues/05-xr-simulator-testing-path-decide.md) — Simulator install got fixed (retry succeeded) and activated as the system OpenXR runtime (`activate_simulator.ps1`, elevated); it's now the primary no-headset iteration loop, with the device-only pipeline as fallback/final-verification once the Quest 2 arrives.

## Not yet specified

- Convai AI-rival banter stretch goal — explicitly deferred until the core loop (court, throw, bounce, fault rule, AI opponent) is playable and demoable; not decidable until then.
- Fine-tuning constants (exact ball size/bounciness, wall angles/materials, court proportions within the Guardian boundary) — expected to be tuned during implementation via playtesting, not pre-decided. May graduate into tickets later if that assumption turns out wrong.

## Out of scope

(none yet — see `PRD.md`'s own "Explicitly out of scope" section for the project-wide list; this map doesn't restate it)
