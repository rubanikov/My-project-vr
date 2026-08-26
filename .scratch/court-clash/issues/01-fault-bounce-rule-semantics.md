Type: grilling
Status: resolved

## Question

Nail down the exact "no more than one bounce" fault rule for Court Clash:

- Does the bounce counter reset every time a side catches and re-throws the ball (strict "each throw gets at most one bounce"), or does it run across a longer rally without resetting on catch?
- Are floor bounces and wall bounces counted the same, or treated differently?
- Does the same rule apply on the very first throw of a rally, or is a serve exempt?

`PRD.md`'s "Fault rule" section flags this as an explicit open question, with instruction to "pick a reasonable default... and flag it clearly so it can be adjusted once it's actually felt in play." This ticket locks the default before build instead.

## Answer

All three resolved, uniform and simple:

1. **Counter resets on every catch/re-throw** — it's a per-throw scoped count ("bounces since the ball was last touched"), not a running rally total. Reset the counter in the same handler that fires on grab; increment on any collision.
2. **Floor and wall bounces count identically** — any surface contact increments the same counter. No special-casing by collider type.
3. **No serve exemption** — the rule applies uniformly from the very first throw of a rally. No `isServe` state needed.

Net implementation shape: a single `bounceCount` int on the ball, reset to 0 on grab, incremented on any collision with court geometry, checked against a limit of 1 when the opposing side touches/catches it.
