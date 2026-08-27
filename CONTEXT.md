# Court Clash

A 1v1 padel-like VR sport for Meta Quest 2: player and AI opponent on opposite halves of a Guardian-sized walled court, separated by a net, rallying with rackets. Wall ricochets are free on every surface — the walls are the game's identity. This glossary is the shared language; PRD.md holds requirements and history.

## Language

### Court

**Net**:
The mid-court barrier splitting the player's half (+Z) from the AI's half (−Z). A shot must clear it; a ball that clips the top and crosses is live.

**Ceiling**:
The visible lid high above the walls (with invisible panels closing the band between wall top and lid). Purely containment — a ceiling touch is a free ricochet, never a fault.

**Half**:
One side's territory, defined by the net. Each side plays only from its own Half.

### Racket (player)

**Racket**:
The player's hitting implement, always attached to the right hand (never free-floating).

**Grip** / **Default Grip** / **Regrip** / **Grab in place**:
The racket's pose relative to the owning hand; the padel-style shipped pose; the two-hand gesture that captures a new persistent Grip (left trigger grab + right grip snap); taking hold without the object moving. (Unchanged — see PRD 2026-08-26.)

### Rally and rules

**Rally**:
Play between a Serve and the next point. Ends the instant a Fault fires; after a short pause the next Serve begins.

**Serve**:
The shot that starts a rally. Alternates every point, player first. The player serves by Tossing the ball and hitting it; the AI's serve is a computed shot after a wind-up. A serve is an ordinary shot — it must clear the Net, nothing more.

**Turn**:
Whose hit it is, tracked as Last Touch. After one side's racket hit, only the other side may legally return.

**Last Touch**:
The side whose racket last contacted the ball. Attribution source for every Fault.

**Bounce**:
Ball contact with the FLOOR only. Wall and ceiling contacts are free ricochets and count toward nothing. (Supersedes the pre-padel rule where walls counted identically.)

**Hit**:
Racket-ball contact. The player's hit velocity is computed from the real swing; the AI's from its shot logic. A Hit sets Last Touch and can start the match.

**Body Hit**:
The ball touching the AI's body during a live rally (any Last Touch). Always a point to the player. Asymmetric by design: the player's body is untracked, so no body rule applies to them.

**Fault**:
Ends the rally, awards the point, fires the moment it happens (not at next touch). The faults: second floor Bounce before the return (point to Last Touch side); a shot floor-bouncing on the hitter's own Half without clearing the Net (point to the opponent); a Body Hit (point to the player). Faults during the between-rally pause are void.

**Let**:
A Serve (the rally's first hit) that fails to clear the Net. No point — the same side serves again.
_Avoid_: Serve fault (as a scoring event), mulligan

**Pause**:
The frozen game state toggled by the left controller's menu button. While paused, B resets the match to idle.

**Toss**:
Releasing the held ball from the left hand with inherited hand velocity. Scoring-neutral; the serve's first half.

**Ball Recall**:
Left-grip hold that brings the ball to the left hand until released (release ends in a Toss).

### Opponent

**AI Swing**:
The AI's animated racket swing whose contact moment applies a computed shot (aimed over the net within a difficulty cone) — the swing is presentation, the shot is deterministic.

**Dodge**:
The AI's movement away from the ball's path when it is not the AI's Turn, driven by the Body Hit rule.

### Presentation

**Scoreboard**:
Semi-transparent panel on the wall behind the AI: both scores, whose Serve it is, and the match result.
