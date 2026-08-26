Type: grilling
Status: resolved

## Question

Define the match/session structure for a single playthrough of Court Clash:

- What starts a match — a menu/button, or automatic on app launch?
- What's the scoring unit — points per fault, first-to-N, best-of-N rallies, a timer, or something else?
- What ends a match (score threshold, timer, or player choice), and what happens after (auto-restart, return to a menu, result screen)?

`PRD.md`'s core-mechanics section covers the fault rule but not how a rally's outcome accumulates into an actual game with a start and an end.

## Answer

No menu system for v1 — the whole loop lives diegetically in-world:

1. **Start**: the ball sits on a pedestal/floor marker at scene load; the AI opponent is idle-ready; the match begins the instant the player first grabs the ball. Chosen specifically to give the player a beat to orient in-headset before anything moves — no menu UI needed.
2. **Scoring**: first-to-11 points, single game, no sets. A fault (bounce-limit violation, per ticket 01, or a missed catch) awards the other side a point.
3. **End**: match ends the instant either side hits 11. Show simple win/lose feedback (floating text or color flash, no results panel), reset the ball to its pedestal. Grabbing it again starts a new match — same trigger as start, so there's exactly one interaction to build for the whole session lifecycle.
