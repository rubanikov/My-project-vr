# Court Clash

A 1v1 VR racket sport for Meta Quest 2: the player hits a bouncy ball with a racket inside a Guardian-sized court against an AI opponent. This glossary is the shared language for the game's interactions — PRD.md holds requirements and history; this file holds only terms.

## Language

### Racket

**Racket**:
The player's single hitting implement, always attached to the right hand (never free-floating in the world).
_Avoid_: Paddle (the earlier table-tennis-style shape this replaced), table

**Grip**:
The racket's pose (position + rotation) relative to the hand that owns it. There is always exactly one active Grip.
_Avoid_: Offset, anchor pose

**Default Grip**:
The padel-style Grip shipped with the game: handle continuing the forearm line, head in front of the fist, face vertical. Active until a Regrip replaces it, and restored by Grip reset.

**Regrip**:
The two-hand adjustment gesture: left trigger grabs the Racket in place, the right hand takes hold where the grip should be, right grip press captures that pose as the new persistent Grip.
_Avoid_: Calibration, grip tuning

**Grab in place**:
Taking hold of an object without it moving — the grabbing hand adopts the object's current relative pose rather than snapping the object to a canned attach point.
_Avoid_: Snap-to-hand, summon

### Hitting and throwing

**Hit**:
Racket-ball contact. The ball's outgoing velocity is computed from the swing (racket velocity plus reflection off the face), not left to raw engine collision response. A Hit counts as a player touch and can start the match.
_Avoid_: Bounce (reserved for wall/floor contacts), collision

**Swing velocity**:
The racket's real velocity at the moment of a Hit, derived from controller tracking.
_Avoid_: Hand speed

**Toss**:
Releasing the held ball from the left hand, inheriting the hand's tracked velocity scaled by Throw Power. Scoring-neutral: never a touch, never starts the match.
_Avoid_: Throw-in, serve (no formal serve exists)

**Ball Recall**:
Left-grip hold that brings the ball to the left hand and keeps it there (kinematic) until released. Release ends in a Toss.
_Avoid_: Ball magnet, retrieve

### Court and rules (pre-existing)

**Bounce**:
Ball contact with a wall or the floor. Counted per possession for the fault rule; wall and floor count identically.

**Touch**:
A side making contact with the ball (player Hit, or AI catch), resetting the bounce counter.

**Fault**:
More than one Bounce before the opposing side touches the ball; awards the point to the other side.
