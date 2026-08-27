// Pure rally rules for Court Clash's padel-shaped version (2026-08-26 —
// supersedes BounceFaultRule and wayfinder ticket 01's "walls count as
// bounces" semantics; see PRD.md's padel-conversion section and CONTEXT.md
// for the vocabulary).
//   - Only FLOOR contacts are Bounces. Walls, net, and ceiling are free
//     ricochets — the caller simply never reports them here.
//   - Faults fire the moment they happen, not at the next touch:
//       * the first floor bounce after a hit lands on the HITTER'S OWN half
//         (the shot never cleared the net) — point to the opponent;
//       * a second floor bounce before the return — point to the hitter;
//       * the ball touches the AI's body — point to the player (asymmetric
//         by design: the player's body is untracked).
//   - After any fault the rally is dead: bounces and body hits report
//     nothing until the next hit (the serve) revives it.

public enum FaultKind
{
    DoubleBounce,
    FailedClear,
    BodyHit,
    // The SERVE (first hit of a rally) failing to clear the net is a let,
    // not a point: PointTo carries the side that serves AGAIN, and no score
    // is awarded (2026-08-27, user: "if the player serving does not hit the
    // ball ... let him serve again" — also kills the accidental-touch point
    // drain when the ball resets right in front of the player's racket).
    ServeLet,
}

public readonly struct FaultResult
{
    public readonly Side PointTo;
    public readonly FaultKind Kind;

    public FaultResult(Side pointTo, FaultKind kind)
    {
        PointTo = pointTo;
        Kind = kind;
    }
}

public class PadelRallyRule
{
    public Side? LastTouch { get; private set; }
    public int FloorBounceCount { get; private set; }
    public int HitCount { get; private set; }
    public bool RallyLive { get; private set; }

    public void RegisterHit(Side side)
    {
        LastTouch = side;
        FloorBounceCount = 0;
        HitCount++;
        RallyLive = true;
    }

    public FaultResult? RegisterFloorBounce(Side bounceHalf)
    {
        if (!RallyLive || LastTouch == null) return null;

        FloorBounceCount++;

        if (FloorBounceCount == 1 && bounceHalf == LastTouch.Value)
        {
            // The serve (a rally's very first hit) gets a mulligan: a let,
            // re-served by the same side, instead of a point.
            if (HitCount == 1)
            {
                return EndRally(LastTouch.Value, FaultKind.ServeLet);
            }
            return EndRally(Opponent(LastTouch.Value), FaultKind.FailedClear);
        }
        if (FloorBounceCount > 1)
        {
            return EndRally(LastTouch.Value, FaultKind.DoubleBounce);
        }
        return null;
    }

    public FaultResult? RegisterBodyHit(Side bodySide)
    {
        if (!RallyLive || LastTouch == null) return null;
        return EndRally(Opponent(bodySide), FaultKind.BodyHit);
    }

    public void ResetRally()
    {
        LastTouch = null;
        FloorBounceCount = 0;
        HitCount = 0;
        RallyLive = false;
    }

    private FaultResult EndRally(Side pointTo, FaultKind kind)
    {
        RallyLive = false;
        return new FaultResult(pointTo, kind);
    }

    private static Side Opponent(Side side) => side == Side.Player ? Side.AI : Side.Player;
}
