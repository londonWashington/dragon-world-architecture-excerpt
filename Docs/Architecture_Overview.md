# Architecture Overview — Brain · Muscles · Wings

## Core Principle

The entire Dragon World codebase is built around one architectural rule:

> **Separate what happens (Logic) from how it looks and sounds (Presentation) from what moves it (Physics).**

This is called the **Brain–Muscles–Wings** pattern.

---

## The Three Layers

### 🧠 Brain (Logic)
Scripts that own state, make decisions, and process input.

Examples: `DragonStateManager`, `DragonUtilityBrain`, `DragonAIPilot`, `LightningStormSystem`

**Rules:**
- May read from any system.
- Sends commands to Muscles via method calls (`PlayFireBreath()`, `PlayGrowl()`).
- **Must NOT** call `AudioSource.Play()`, `Instantiate(VFX)`, or manipulate IK bones directly.

### 💪 Muscles (Presentation)
Pure executor scripts. They are APIs — they wait for commands and respond with visuals, audio, and kinematics.

Examples: `DragonVFXController`, `DragonAudioController`, `RigControl`, `CameraEffectsManager`

**Rules:**
- **Never** make gameplay decisions.
- **Never** store game state that affects logic.
- Respond to: `PlayFireBreath()` → start VFX Graph parameters. Nothing more.

### 🪽 Wings (Physics)
An independent simulation engine. It does not know what creature it is simulating.

Examples: `AircraftPhysics`, `AirplaneController`, `AeroSurface`

**Rules:**
- Receives only numerical inputs (pitch angle, thrust percent, surface span).
- Calculates aerodynamic forces and applies them to `Rigidbody`.
- Has zero references to `DragonStateManager`, `GreatBirdController`, or any creature class.

---

## Communication via Interfaces

All cross-system communication happens through four contracts. This makes every environmental and AI system 100% creature-agnostic:

```csharp
IUtilityFlyer         // AI reads flight state; sends FlyerInputData
ICombatActor          // AI triggers melee/ranged attacks; reads ammo state
IThreatReceiver       // External threats register shock, dodge windows, burns
IEnvironmentReceiver  // Weather systems apply tornado, wind, water states
```

### Why this matters

Without interfaces, adding a Great Bird enemy would require duplicating or modifying all combat, weather, and AI systems to handle a new class. With interfaces, `TornadoController.ApplyBuffeting(IEnvironmentReceiver receiver)` works on Dragon, Bird, or any future creature — written once, never revisited.

---

## Anti-Patterns (Enforced Violations)

These patterns are explicitly banned in code review:

| Anti-pattern | Why it is banned |
|---|---|
| `AudioSource.Play()` in a state script | Breaks Brain/Muscles separation; cannot be overridden per platform |
| `GetComponent<DragonStateManager>()` in weather/AI scripts | Hard coupling; prevents the Bird from using the same system |
| `FindObjectOfType` in `Update()` | O(n) scene scan every frame; performance killer |
| `if (distanceToTarget < 15f) return 1f;` in an AI Action | Defeats the purpose of Utility AI; should be an `AnimationCurve` Consideration |
| `Time.timeScale = 0.3f` direct assignment | Race condition; two systems fight over the value. Use `TimeManager.RequestSlowMo()` |
| `Player.Instance` in any scoring or damage script | Blocks multiplayer; use Instigator + Factions instead |

---

## Input Data Flow

```
Player / AI Brain
        │
        ▼
  FlyerInputData              ← data packet, not virtual button presses
  { aimPosition,
    pitchYawInput,
    rollInput,
    isFlapping,
    useSharpTurn ... }
        │
        ▼
  IUtilityFlyer.ProcessAIInput(input)
        │
        ▼
  GreatBirdController / DragonStateManager
  (translates to AirplaneController.Pitch/Yaw/Roll)
        │
        ▼
  AircraftPhysics
  (calculates lift, drag, torque → Rigidbody)
```

The AI never touches `AircraftPhysics` directly. The creature controller is the only translator between intent and physics.

---

## Event-Driven Economy

Scoring and UI are completely passive observers:

```
PhysicsScript fires:
  EconomyEvents.OnDiscreteActionTriggered?.Invoke(source, ScoreEventType.PawStrike, position)

OceanPowerManager listens:
  EconomyEvents.OnDiscreteActionTriggered += HandleAction;
  // calculates combo, multiplier, awards points

UIJuiceManager listens:
  EconomyEvents.OnComboTierChanged += ShowFloatingText;
```

No physics or combat script ever calls `ScoreManager.AddPoints()` directly. This makes scoring trivially extensible and multiplayer-safe.

---

## Multiplayer Readiness

Every design decision anticipates a future co-op or PvP build:

1. **Instigator pattern** — every `DamageInfo` carries `GameObject Instigator`. Death events report who did the killing.
2. **No singletons for player identity** — `FactionMember` component on each creature determines team. The economy checks faction, not `isAIControlled`.
3. **Seat-Switch** — transferring player control from Dragon to Rider is a flag flip (`isAIControlled`). The AI seamlessly takes over the Dragon while the player controls the Rider. Score continues accumulating for the same faction.
4. **`TargetRegistry`** — a static `HashSet` that any system can query. No per-script enemy arrays.
