Type: grilling
Status: resolved

## Question

Scope the AI opponent's v1 behavior beyond the PRD's baseline (predict trajectory → move to intercept → catch → throw back, "can start simple — random aim within a cone"):

- Is a single fixed difficulty acceptable for v1, or does v1 need a difficulty setting?
- Any movement-speed/positioning limit on the AI (so it can't just teleport to any intercept point), or is "always reaches the intercept point in time" fine for v1?
- Any explicit throw-aim behavior for v1 beyond "random aim within a cone" (e.g. aim toward whichever side of the court the player isn't near), or is random-cone genuinely sufficient to call v1 done?

This determines how much of the "genuinely interesting... expected to take real iteration" AI system needs to be nailed down before implementation starts vs. left to iterate on live.

## Answer

1. **Single fixed difficulty, no selector.** Consistent with the no-menu-UI decision on Match/session structure — a difficulty picker would reintroduce the UI surface that was deliberately avoided. Tuned by feel during playtesting; a selector is a later stretch feature.
2. **Capped movement speed, plus a human-like reaction delay** — the AI does not always reach the intercept point. Two stacking mechanisms make it beatable:
   - A tunable max travel speed (start around a fast human lateral step, tune during playtesting).
   - A reaction delay before the AI starts moving/re-predicting on each bounce: a base delay (~150–200ms) always applies, plus extra delay scaled by the angle deviation between the ball's velocity just before vs. just after that bounce — sharper, more unexpected caroms (e.g. off an angled wall) cost more reaction time, up to a capped maximum. This only gates *when* the AI reacts, not the speed cap itself; they stack.
3. **Random aim within a cone is the full v1 aim behavior** — matches the PRD's own baseline. Gap-aiming is deliberately deferred: build the simple version first, playtest with the speed cap + reaction delay in place, and only add gap-aiming if the AI still feels too easy to beat.
