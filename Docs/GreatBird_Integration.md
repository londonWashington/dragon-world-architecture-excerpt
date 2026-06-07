# Multi-Creature Integration — Great Bird Case Study

## The Design Goal

Add a completely new flying NPC enemy (the Great Bird) without:
- Modifying the existing Dragon AI or combat systems.
- Duplicating the weather interaction code.
- Creating any `GetComponent<DragonStateManager>()` calls in shared systems.

The result: the Great Bird was integrated in a single new controller file (`GreatBirdController.cs`) by implementing four interfaces. Every existing system — AI, combat, weather, economy — works with the Bird out of the box.

---

## Interface Implementation

`GreatBirdController` implements all four creature interfaces:

```csharp
public class GreatBirdController : MonoBehaviour,
    IUtilityFlyer,          // AI can read state and send flight commands
    ICombatActor,           // AI can trigger attacks; reads ammo
    IThreatReceiver,        // Dragon's attacks can shock or warn the Bird
    IEnvironmentReceiver    // Tornado, water, wind systems work on the Bird
```

This one declaration is all that is needed for the entire game's shared systems to recognise the Bird as a first-class entity.

---

## Shared AI Pipeline

`DragonAIPilot` and `DragonUtilityBrain` were written for the Dragon. They run on the Bird without modification:

```
DragonAIPilot (on Bird)
  └── reads:  flyer = GetComponent<IUtilityFlyer>()  → GreatBirdController
  └── writes: flyer.ProcessAIInput(FlyerInputData)   → GreatBirdController

DragonUtilityBrain (on Bird)
  └── reads:  combatActor = GetComponent<ICombatActor>() → GreatBirdController
  └── calls:  combatActor.ExecuteMeleeAttack(target)     → GreatBirdController.TalonStrikeSequence()
```

The AI does not know it is controlling a bird. It speaks `IUtilityFlyer`. The Bird speaks `IUtilityFlyer`. They connect.

---

## Separate Aerodynamic Configs

Dragon and Bird use separate `AeroSurface` config asset folders:

```
Assets/Scripts/DragonPlayer_States/DragonSurfaceConfigs/
Assets/Scripts/Bird/BirdSurfaceConfigs/
```

`AircraftPhysics` instantiates from whatever configs are assigned — a wing config change for the Bird cannot accidentally alter the Dragon's flight balance.

---

## Talon Strike — Cinematic Attack Architecture

`Action_BirdTalonStrike` demonstrates how a complex, multi-phase cinematic ability is wired into the Utility AI pipeline cleanly.

### Decision (Brain layer — `Action_BirdTalonStrike.Evaluate()`)
```csharp
public override float Evaluate(AIDataContext ctx)
{
    if (forceAttackDebug) return 1f;
    if (considerations == null || considerations.Length == 0) return 0f;
    return base.Evaluate(ctx);  // delegates entirely to AnimationCurve considerations
}
```
No distance checks. No if/else. The designer sets when this fires by tuning curves.

### Execution (Muscle layer — `GreatBirdController.TalonStrikeSequence()`)
The coroutine manages four phases:

| Phase | Trigger | Systems involved |
|-------|---------|-----------------|
| **Approach** | Action selected | `DragonAIPilot` switches to `AIState.TerminalStrike`; Bird boosts toward target |
| **Telegraph** | `timeToImpact ≤ telegraphDuration` | `IThreatReceiver.SetUnderThreat()` opens Perfect Dodge window on Dragon; slow-mo via `TimeManager`; talon VFX; `PointOfInterest` for camera focus |
| **Impact** | Talon distance < threshold | `DamageInfo` with `Instigator`; `IThreatReceiver.ApplyShockState()`; `EconomyEvents` fires hit/miss/interrupt score events; camera impulse |
| **Recovery** | Post-impact | Talons retract; cooldown registered; AI returns to Chase state |

### Interruption
If the Bird takes damage during the dive, `_tookDamageDuringAttack = true` is set via a `Health.OnDamageTaken` event listener. The coroutine checks this flag each frame and fires `ScoreEventType.StormRetribution` — a bonus score event for the player who interrupted the Bird's attack. No polling; purely event-driven.

---

## Weather System Universality

`TornadoController` affects any creature via `IEnvironmentReceiver`:

```csharp
// TornadoController — no Dragon/Bird references anywhere
IEnvironmentReceiver receiver = target.GetComponent<IEnvironmentReceiver>();
if (receiver != null)
{
    receiver.ApplyTornadoBuffeting(intensity);
    receiver.AddExternalWind(windVector, turbulence);
    receiver.PlayStruggleVocalization();
}
```

On the Bird, `ApplyTornadoBuffeting(intensity)` stores the intensity value. `GreatBirdController.UpdateAeroSurfaces()` reads this value each frame and applies Perlin-noise-driven aerodynamic surface destabilisation — the Bird flaps erratically in the tornado without any tornado-specific code paths.

---

## Summary

| Capability | How it works for the Bird |
|---|---|
| AI navigation | `DragonAIPilot` via `IUtilityFlyer` — zero changes |
| AI decisions | `DragonUtilityBrain` + Bird-specific `Action_BirdTalonStrike` — additive |
| Takes damage | `DragonHitbox` auto-initialised in `Awake()` — zero setup |
| Registers as enemy | `Targetable.OnEnable()` → `TargetRegistry.Enemies` — zero setup |
| Receives fire damage | `IThreatReceiver.StartBurning()` — zero changes to `FireBreathHitbox` |
| Affected by tornado | `IEnvironmentReceiver.ApplyTornadoBuffeting()` — zero changes to `TornadoController` |
| Earns/loses score | `EconomyEvents` — zero changes to `OceanPowerManager` |
