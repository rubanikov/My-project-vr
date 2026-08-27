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

**Warm-Up**:
Free play while no match session is armed — the AI rallies, dodges, and serves back as usual, but nothing ever scores. Entered by closing the Game Menu without starting a match. Always labeled with the word "WARM-UP" above the Scoreboard, so an unscored rally can never be mistaken for a match. (2026-08-27.)

**Rally**:
Play between a Serve and the next point. Ends the instant a Fault fires; after a short pause the next Serve begins.

**Serve**:
The shot that starts a rally. Alternates every point, player first. The player serves by Tossing the ball and hitting it; the AI's serve is a computed shot after a wind-up. A serve is an ordinary shot — it must clear the Net, nothing more.

**Turn**:
Whose hit it is, tracked as Last Touch. After one side's racket hit, only the other side may legally return.

**Last Touch**:
The side whose racket last contacted the ball. Attribution source for every Fault.

**Bounce**:
Ball contact with the FLOOR only. Wall and ceiling contacts are free ricochets and count toward nothing. (Supersedes the pre-padel rule where walls counted identically.) A ball that stays skimming or rolling near the floor after its first Bounce counts as the second Bounce at any speed — the receiver never returned it. (2026-08-27 — widens the earlier low-speed dead-ball rule.)

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
The frozen game state while the Game Menu is open — "paused" and "menu open" are the same condition. Closing the menu resumes at the chosen Game Speed, never a hard-coded normal.

**Game Speed**:
The world's clock, chosen in the Game Menu. Everything on the court — ball flight, AI movement and reactions, pauses, wind-ups — runs uniformly slower or faster; every trajectory is identical in shape at every speed. The player's head and hands always move in real time, so a slower Game Speed means more time to see and react. (2026-08-27 — supersedes Ball Speed, which multiplied hit velocities and warped the AI's arcs.)
_Avoid_: Ball Speed, speed multiplier

**Toss**:
Releasing the held ball from the left hand with inherited hand velocity. Scoring-neutral; the serve's first half.

**Ball Recall**:
Left-grip hold that brings the ball to the left hand until released (release ends in a Toss). Legal only while the ball is waiting on the player's Serve — pre-match idle, or the player is the serving side with no live Rally. At any other moment (a live Rally, the AI's Serve pending or winding up) the grip does nothing. If the AI's Serve begins while the player is holding the ball, the serve takes the ball and the hold ends. (2026-08-27 — supersedes the unconditional recall.)

### Opponent

**AI Swing**:
The AI's racket swing — presentation only; the contact moment applies the computed shot (aimed over the net within a difficulty cone). The racket is held in a hand socket at the end of an articulated procedural arm, and a swing carries hip turn, shoulder arc, and wrist snap through wind-up, sweep, and follow-through. (2026-08-27 — supersedes the single-pivot racket rotation floating beside the body.)

**Dodge**:
The AI's movement away from the ball's path when it is not the AI's Turn, driven by the Body Hit rule.

**Difficulty**:
The Easy / Normal / Hard preset shaping the AI — movement speed, reaction delay, reach, shot pace, and whiff chance. Chosen only in the Game Menu as a visible setting (2026-08-27 — supersedes the in-game A-button cycle), persists between sessions, and takes effect on the AI's next turn.

### Presentation

**Scoreboard**:
Semi-transparent panel on the wall behind the AI: both scores, whose Serve it is, and the match result.

**Score Pulse**:
The court's edge trim strips surging green (player point) or red (AI point) for about a second before easing back to idle cyan.

**Impact Ripple**:
The expanding ring that flares where the ball touches a wall, the floor, the ceiling, or the net — larger and brighter for harder hits. Racket contacts speak through haptics and sound instead.

**Skin**:
A shell material for the AI robot, cycled with X. Textures only — the robot's body never changes shape.

**Game Menu**:
The single floating panel shown at launch, on the menu button, and at match end. The game is frozen while it is open and the menu owns the controller. It holds the match settings — Game Speed and Difficulty — and is the only place a match can be started; closing it without starting leaves free warm-up play. (2026-08-27 — supersedes the separate Start Screen and Pause Menu.)
_Avoid_: Start Screen, Pause Menu (as separate surfaces)
