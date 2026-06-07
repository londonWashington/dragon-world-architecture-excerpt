# Dragon World — Architecture Portfolio

> **Note:** This repository is a **curated portfolio excerpt**, not the full game source.  
> It is shared to illustrate systems thinking, architectural decisions, and production-quality code patterns.  
> Core gameplay logic, proprietary shaders, and content assets are not included.

---

## About the Project

**Dragon World** is an indie 3D game built in Unity 6 HDRP, centred around a fully physics-driven flying creature — a dragon controlled either by a player or an AI bot. The project grew into a testing ground for advanced gameplay systems engineering: custom flight physics, procedural animation, Utility AI, and a multiplayer-ready decoupled architecture.

The YouTube showcase reached **2.9M views**: [Watch here](https://www.youtube.com/watch?v=LINAzm_sg0c)

---

## Core Engineering Highlights

### 1. Physics-Driven Flight System
A custom aerodynamic engine where **procedural skeletal animation directly drives real-time flight forces**.

- 9 independent `AeroSurface` components per creature (wings, tail fins, head), each with its own `chord`, `span`, and `zeroLiftAoA` config.
- Lift and drag forces are proportional to velocity squared (V²), producing realistic stall behaviour at low speed.
- The state machine dynamically repositions aerodynamic surfaces each frame — physically moving the Centre of Lift relative to the Centre of Mass to generate pitch/roll torques.
- A hybrid **PD stabiliser** (manual pitch trim + derivative damper) maintains stable level flight during variable-speed manoeuvres.
- **Wing Warping roll control:** roll is achieved by physically rotating the entire wing geometry along the pitch axis, generating massive aerodynamic torque — no aileron deflection. `rollControlSensitivity = 0` on `AirplaneController` is intentional, not a shortcut.
- The same `AircraftPhysics` engine powers both the Dragon and the Great Bird NPC, completely unaware of which creature it is simulating — pure physics.

### 2. Procedural Animation & IK System (`RigControl`)
A 4-limbed procedural locomotion system built on Unity Animation Rigging, running on top of keyframe animation:

- **Spine & Head IK:** Real-time spine bending driven by cursor/target aim. Head obstacle avoidance via raycasts.
- **Feet IK:** Dynamic ground-plane detection using `OverlapSphereNonAlloc` + `ComputePenetration`. Each foot independently calculates step placement on arbitrary 3D terrain.
- **Wing/Tail Physics:** Procedural wing-digit folding on obstacle contact, tail physics simulation.
- **Additive Procedural Offsets:** A `ProceduralAnimationOffsets` system layers chaotic effects (tornado buffeting, shock convulsions) *additively* on top of running base animations — no coroutine overrides, no animation conflicts.
- **Visuals drive Physics:** Head rotation (visual IK) shifts the aerodynamic head surface's local position, which in turn steers the physics engine. The visual skeleton *is* the flight controller.

### 3. "Brain–Muscles–Wings" Architecture Pattern

The entire codebase enforces a strict separation of concerns:

```
Brain  (Logic)      → DragonStateManager, DragonUtilityBrain, DragonAIPilot
Muscles (Presentation) → DragonVFXController, DragonAudioController, RigControl
Wings  (Physics)    → AircraftPhysics, AirplaneController, AeroSurface
```

- **Brain** scripts make decisions and own state. They never call `AudioSource.Play()` or `Instantiate(VFX)` directly.
- **Muscles** scripts are pure APIs — they wait for commands, execute presentation, and never make decisions.
- **Wings** are physics-only. The engine does not know whether it is simulating a dragon, a bird, or any other flying entity.

**Communication is exclusively via interfaces.** No `GetComponent<DragonStateManager>()` exists in any environmental system.

### 4. Utility AI System

A data-driven Utility AI replaces hard-coded if/else logic for all NPC decision-making:

- `AIDataContext` — a world-state snapshot updated once per frame.
- `DragonAIAction` (ScriptableObject) — each ability is a self-contained asset evaluated by a list of `AIActionConsideration` curves.
- `AnimationCurve` response curves allow designers to tune AI behaviour entirely from the Inspector — no code changes.
- **Rule enforced:** `Evaluate()` must always call `base.Evaluate(ctx)` before any custom logic. Hard-coded distance checks inside `Evaluate()` are an architectural violation.
- The same AI pipeline drives both the Dragon bot and the Great Bird NPC via `IUtilityFlyer` and `ICombatActor` interfaces.
- **Bang-Bang oscillation fix:** AI steering uses proportional P-gain (angle / 90°) + a Lerp low-pass filter on outputs, simulating pilot reaction inertia. This eliminates frame-to-frame ±1 oscillation without needing Lerp on the muscle side.

### 5. Interface-Driven Universality

Four key interfaces decouple all external systems from specific creature classes:

| Interface | Purpose |
|-----------|---------|
| `IUtilityFlyer` | Flight state + input channel for any flying entity |
| `ICombatActor` | Combat abilities (melee, ranged, ammo state) |
| `IThreatReceiver` | Receives cinematic threats, shock states, perfect-dodge windows |
| `IEnvironmentReceiver` | Reacts to weather (tornado, water, wind) |

A `TornadoController`, `LightningStormSystem`, or any future environmental hazard works on *any* creature implementing these interfaces — one integration, zero coupling.

### 6. Combat & Damage Pipeline

- **Hitbox-first damage:** Damage is never applied by searching for `Health` on a collider. Every non-trigger collider auto-initialises a `DragonHitbox` in `Awake()`, which knows its `BodyPartType` and `damageMultiplier`. Self-damage is structurally impossible — the hitbox holds a reference to `mainHealth`.
- **`DamageInfo` struct** carries `Instigator`, `HitPoint`, `HitDirection`, and `ImpactForce` — `AddForceAtPosition` automatically calculates the correct torque from a single call.
- **`TargetRegistry`** — a static `HashSet`-based global registry. All targetable entities self-register via `Targetable.OnEnable()`. No manual list management, no `FindObjectsOfType`.
- **`EconomyEvents`** event bus — all scoring, combo, and economy logic is purely reactive. Physics and combat scripts fire events; they never call `ScoreManager` directly.

### 7. Multiplayer-Ready by Design

- Every damage event carries an `Instigator` (`GameObject source`) — the economy always knows who performed the action, regardless of whether a human or AI is in control.
- No `Player.Instance` singletons. Creature identity is determined by a **Faction/Team component**, not by control type.
- **Seat-Switch mechanic** is architecturally supported: the player can transfer control to a rider while the AI takes over the dragon — the dragon continues earning score for the player's faction.

---

## Repository Structure

```
Scripts/
├── Interfaces/          # IUtilityFlyer, ICombatActor, IThreatReceiver, IEnvironmentReceiver
├── AI/                  # Utility AI — DragonUtilityBrain, DragonAIPilot, AIDataContext,
│                        #              DragonAIAction, AIActionConsideration, FlyerInputData
│                        #              Action_BirdTalonStrike (example action)
├── Combat/              # Health, DamageInfo, Targetable, TargetRegistry, FireBreathHitbox
└── Flight/              # GreatBirdController (IUtilityFlyer + ICombatActor implementation)

Docs/
├── Architecture_Overview.md       # Brain-Muscles-Wings pattern, pillars, anti-patterns
├── AI_System.md                   # Utility AI deep-dive
├── Flight_Physics.md              # Aerodynamics engine documentation
├── GreatBird_Integration.md       # Multi-creature integration example
└── Multiplayer_Readiness.md       # Instigator, Factions, Seat-Switch design
```

---

## What Is NOT Included

To protect proprietary systems and commercial assets, the following are omitted:

- `DragonStateManager` (full state machine — ~5000 lines across partial classes)
- `RigControl` and all IK/procedural animation systems (~3000 lines across partial classes, custom movement algorithms)
- `AircraftPhysics` / `AirplaneController` / `AeroSurface` (all aerodynamics physisc)
- All ScriptableObject AI action and consideration assets
- VFX, audio, and shader systems
- Economy, scoring, and progression systems
- External entities: Tornado, Lightning, Rider, dolphins etc
- Audiomanagers, GameDirectorManagers, Ocean-controller, UI-controllers, additional "game-feel" systems (hearBeat controller, adrenaline manager, CameraEffects manager etc).

The included scripts are sufficient to understand the architectural patterns, interface contracts, and AI decision pipeline.

---

## Tech Stack

- **Engine:** Unity 6 HDRP
- **Language:** C#
- **Physics:** Custom aerodynamic simulation (Rigidbody + AeroSurface components)
- **Animation:** Unity Animation Rigging + custom procedural IK
- **AI:** Custom Utility AI (ScriptableObject-based)
- **Rendering:** HDRP, VFX Graph, custom MaterialPropertyBlock GPU skinning
- **Targeting:** Custom static HashSet registry (no `FindObjectsOfType`)

---

*Last updated: June 2026*
