# Utility AI System

## Why Utility AI?

Hard-coded if/else AI produces brittle, unmaintainable bots:
```csharp
// BAD — buried in code, untweakable by a designer
if (distanceToTarget < 15f && fuel > 0.3f) return 1f;
```

Dragon World uses a **Utility AI** architecture where every decision is expressed as a numerical score, derived from designer-tunable `AnimationCurve` assets. The same AI pipeline drives both the Dragon bot and the Great Bird NPC.

---

## System Components

### `AIDataContext`
A plain data class — a snapshot of the world state, updated once per frame by `DragonUtilityBrain`. Passed by reference to every `Action` and `Consideration`.

```csharp
public class AIDataContext
{
    // Self state
    public float staminaPercentage;
    public float healthPercentage;
    public float rollAngle;

    // World
    public float distanceToTarget;
    public float angleToTarget;
    public bool  isTargetInLineOfSight;
    public float dangerLevel;

    // Combat
    public bool  hasFuelForFireBreath;
    public float fuelPercentage;
    public bool  isTargetInShock;
    public float targetBlindSpotAngle; // 180 = directly behind target

    // References
    public IUtilityFlyer  flyer;
    public ICombatActor   combatActor;
    public DragonAIPilot  aiPilot;
}
```

### `AIActionConsideration` (ScriptableObject)
Maps one context value to a 0–1 utility score via an `AnimationCurve`.

```csharp
public abstract class AIActionConsideration : ScriptableObject
{
    public AnimationCurve responseCurve;
    public abstract float Evaluate(AIDataContext ctx);
}
```

Example implementation — *"How desirable is melee if the target is close?"*:
```csharp
public class Cons_TargetMeleeRange : AIActionConsideration
{
    public override float Evaluate(AIDataContext ctx)
    {
        float normalized = ctx.distanceToTarget / 15f; // 0 at 0m, 1 at 15m
        float raw = responseCurve.Evaluate(normalized);  // designer tunes the curve
        return Mathf.Clamp01(raw);
    }
}
```

The designer sees a curve editor in the Inspector — no code change required to re-balance behaviour.

### `DragonAIAction` (ScriptableObject)
Combines N considerations into a single utility score using **multiplicative scoring**:

```csharp
public virtual float Evaluate(AIDataContext ctx)
{
    float score = 1f;
    foreach (var consideration in considerations)
    {
        score *= Mathf.Clamp01(consideration.Evaluate(ctx));
        if (score == 0f) return 0f; // veto — early exit
    }

    // Compensation factor: prevents N-consideration actions from being
    // systematically disadvantaged vs single-consideration actions
    float modFactor  = 1f - (1f / considerations.Length);
    float makeupValue = (1f - score) * modFactor;
    return (score + makeupValue * score) * baseWeight;
}
```

**Rule:** Any subclass overriding `Evaluate()` must call `base.Evaluate(ctx)` first, then apply unique logic (cooldowns, hysteresis, prerequisites) as a multiplier:

```csharp
// CORRECT
public override float Evaluate(AIDataContext ctx)
{
    float baseScore = base.Evaluate(ctx);          // all AnimationCurve considerations
    if (IsOnCooldown()) return 0f;                 // hard veto
    if (ctx.isFireBreathing) baseScore += 0.2f;   // hysteresis / momentum
    return Mathf.Clamp01(baseScore);
}
```

### `DragonUtilityBrain`
The decision loop — runs once per frame:

```
1. UpdateContext()     → populate AIDataContext from live game state
2. DecideBestAction()  → score all available actions, pick highest
3. ExecuteAction()     → call Execute(ctx) on the winner
```

When a new action wins, `OnActionExited(ctx)` is called on the previous winner — allowing clean state cleanup (e.g. stopping fire breath if `Action_FireBreath` is deselected mid-stream).

---

## Implemented Actions

| Action | Trigger conditions (via Considerations) |
|--------|----------------------------------------|
| `Action_Flap` | Default cruise state — always scores above zero |
| `Action_Boost` | High distance to target + sufficient stamina |
| `Action_OceanRegen` | Low stamina + water nearby |
| `Action_PawStrike` | Distance < ~15m + target in front (< 45°) |
| `Action_FireBreath` | Medium range + line of sight + fuel > threshold; with hysteresis momentum bonus |
| `Action_Reposition` | Low fuel + target not in blind spot → "Boom and Zoom" retreat |
| `Action_BirdTalonStrike` | Great Bird special — cinematic dive with telegraphing and Perfect Dodge window |

---

## Action Momentum (Hysteresis)

Without momentum, the AI oscillates between two actions when their scores are equal. `Action_FireBreath` solves this with a simple state bonus:

```csharp
private bool _isFiring = false;

public override float Evaluate(AIDataContext ctx)
{
    float score = base.Evaluate(ctx);
    if (_isFiring) score += 0.2f;  // "keep firing" inertia
    return Mathf.Clamp01(score);
}

public override void OnActionExited(AIDataContext ctx)
{
    _isFiring = false;
    ctx.combatActor.ExecuteRangedAttack(Vector3.zero, false); // stop firing
}
```

This ensures the dragon fires in sustained bursts rather than single-frame pulses — a gameplay feel requirement, enforced architecturally.

---

## Navigation vs Decision — Separation of Responsibilities

```
DragonAIPilot        →  WHERE to fly (steering, obstacle avoidance, altitude)
DragonUtilityBrain   →  WHAT to do   (boost, fire, retreat, strike)
```

`DragonAIPilot` writes navigation inputs into `FlyerInputData`. `DragonUtilityBrain` writes ability inputs into the same struct. `DragonAIPilot.Update()` then **merges** both before sending the final packet to the creature:

```csharp
// Navigation result
FlyerInputData navInputs = ConvertSteeringToInputs(steering);

// Brain result (already written into ctx.currentInputData)
navInputs.isFlapping = navInputs.isFlapping || ctx.currentInputData.isFlapping;
navInputs.moveInput.y = Mathf.Max(navInputs.moveInput.y, ctx.currentInputData.moveInput.y);

// Send merged packet
flyer.ProcessAIInput(navInputs);
```

Neither system can corrupt the other's output — they only OR / Max their contributions.

---

## Cooldown System

`CooldownConsideration` is a shared static registry keyed by `(flyerInstanceID, actionName)`. Any action can register a cooldown on exit:

```csharp
// In ResetMeleeAttack():
CooldownConsideration.RecordExecution(gameObject.GetInstanceID(), "TalonStrike");
```

The corresponding `Cons_Cooldown` consideration returns 0 until the cooldown elapses, then returns 1 — a hard veto implemented as a standard consideration curve.

---

## Designer Workflow

1. Open `Assets/Settings/AI_Profiles/`.
2. Select any `Consideration` asset.
3. Adjust the `AnimationCurve` in the Inspector.
4. Hit Play — behaviour changes immediately, no recompile.

More aggressive pursuit: raise the left half of `Cons_TargetFar` curve.  
More cautious retreat: lower `Cons_FuelRemaining` threshold curve.  
No code involved.
