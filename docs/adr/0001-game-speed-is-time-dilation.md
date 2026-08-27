# Game Speed is time dilation, not a velocity multiplier

The menu's speed setting exists so the player can react to and see the ball on a small court. The original implementation scaled *hit velocities* (player exit speed, AI shot flight time). That warps physics: a slower-launched ball needs a higher arc to reach the same target, which at 0.5× forced the AI's serves into the ceiling and an infinite Serve Let loop, and the safety clamp on solved shot velocity silently broke the AI's aim.

Decided 2026-08-27 (rules-grill session): the setting is **Game Speed** — global time dilation. `Time.timeScale` carries the chosen speed (the Game Menu freezes with 0 while open and restores the *chosen* speed on close, never a hard-coded 1), and `Time.fixedDeltaTime` scales with it (`BallPhysicsTuning.BaseFixedDeltaTime * speed`) so physics keeps stepping ~90× per *real* second — the racket samples the real-time hand at full rate at any speed.

## Consequences

- Every trajectory is identical in shape at every speed; the whole class of "slow ball flies differently" bugs is gone, and the velocity-multiplier code in `PlayerRacket` and `AIOpponent` is deleted rather than fixed.
- The player's hands are real-time (VR tracking ignores timeScale), so at slow speeds the player is effectively faster than the world — that asymmetry is the *point* of the setting.
- Hand-measured velocities entering the physics world must be expressed in game-time units: the racket gets this for free (position deltas over scaled `fixedDeltaTime`); explicit reads of controller velocity (e.g. the ball toss) must divide by `timeScale`.
- Everything stretches uniformly on purpose — between-rally pauses, AI wind-up and reaction delays. Deliberately no unscaled-time exceptions for gameplay; only things that must be real-time regardless of pause (haptic pulse cutoff, menu input repeat guard) use unscaled time.

## Considered options

- Patch the velocity-multiplier model with ceiling-aware arc solving — rejected: even fixed, 0.5× makes the player's own hits mushy while the AI still *reacts* at full speed; it never delivers "the game is slower".
- Scale solved AI velocity directly — rejected: breaks aim by construction (shots land short).
