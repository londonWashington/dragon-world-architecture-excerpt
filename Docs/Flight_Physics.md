# Flight Physics System

## Overview

The flight engine is a custom aerodynamic simulator built on Unity's `Rigidbody`. It is fully independent of any creature class — it receives numerical inputs and outputs physical forces. The same engine drives both the Player Dragon and the Great Bird NPC.

---

## AeroSurface Layout

Each flying creature has 9 independent aerodynamic surfaces:

| Surface | Role |
|---------|------|
| Head | Pitch authority at low speed; visual "nose steering" |
| Body | Passive lift contribution |
| Wing_L / Wing_R (Roll) | Primary roll control via full-wing rotation |
| Wing_L / Wing_R (Pitch/Flap) | Pitch and high-lift flap panels |
| Tail_Horizontal_L/R | Pitch stability and elevator authority |
| Tail_Vertical | Yaw stability (rudder) |

Each surface holds an `AeroSurfaceConfig` asset containing:
- `chord` — surface depth (lift area)
- `span` — surface width
- `zeroLiftAoA` — angle of attack at which lift is zero (trim point)

Dragon and Bird use **separate config asset folders** so balance changes to one creature never affect the other.

---

## Core Physics Principles

### Lift and Drag

Lift and drag forces are calculated per-surface as:

```
Lift  = ½ · ρ · V² · CL · chord · span
Drag  = ½ · ρ · V² · CD · chord · span
```

Because force scales with **V²**, an aerodynamic configuration tuned at cruise speed (e.g. 30 m/s) will be under- or over-trimmed at significantly different speeds. This is a physical reality, not a bug.

### Torque from Surface Placement

The rotational moment (torque) that pitches or rolls the creature is determined entirely by the **offset of each surface's Centre of Lift from the body's Centre of Mass**.

```
Torque = Force × moment_arm
```

The state machine exploits this by dynamically repositioning surfaces each frame:
- Moving `Wing_Roll` surfaces outward along the body's X axis during flapping increases the roll moment arm → more agile roll response.
- Moving surfaces inward (folded wings) removes roll authority during manoeuvres like the Dodge barrel roll.

### Visuals Drive Physics

The head's aerodynamic surface is parented to the visual head bone. When `RigControl` rotates the head toward a target (IK), it physically displaces the aerodynamic surface. `AircraftPhysics` then calculates the resulting pitch torque. The visual skeleton **is** the flight controller.

---

## Stabilisation Systems

### Manual Pitch Trim
A static `manualPitchTrim` offset on the elevator surfaces compensates for cruise-speed aerodynamic imbalance. Adjusted per creature in the Inspector.

### Course Lock — PD Stabiliser
When the player activates course lock (e.g. turret mode), the dragon must maintain a fixed pitch attitude regardless of speed changes. A hybrid PD controller handles this:

```csharp
// On entering Course Lock:
lockedPitchAngle = currentPitchAngle;

// Each frame:
float pitchError   = lockedPitchAngle - currentPitchAngle;
float pitchDamping = -rb.angularVelocity.x;       // derivative term
autoPitchTrim = pitchError * P_gain + pitchDamping * D_gain;
```

The D-term prevents integral windup and oscillation — a pure P controller on pitch produces pitch-hunting at variable speeds.

### Stability Augmentation System (SAS)
A lightweight angular-velocity damper prevents the creature from oscillating after the player releases input:

```csharp
dampingTrim = -rb.angularVelocity.x * dampGain;
```

Applied as a short-lived trim offset that decays to zero.

---

## Wing Warping Roll Control

Most flight simulations use aileron deflection (`flapAngle`) for roll. Dragon World uses **whole-wing rotation**:

```csharp
surface.transform.localEulerAngles = new Vector3(0, -90, rollAngle * surface.LeftOrRight);
```

Rotating the entire wing along the pitch axis generates a massive asymmetric lift differential — a far more powerful and visually organic roll mechanism than a small aileron deflection.

**Critical:** `AirplaneController.rollControlSensitivity = 0` is hardcoded to prevent the system from also applying aileron-based roll on top of wing warping. This is intentional and must not be changed.

---

## Scale-Dependence and Common Pitfalls

- Aerodynamic torque scales with wing area (`chord × span`) and moment arm. A larger creature at the same speed generates proportionally more torque and will be harder to control unless configs are scaled accordingly.
- The simplest stability fix for an oversized NPC is reducing `span` — not adding artificial damping forces.
- `fixedDeltaTime` affects simulation precision. At high speeds (linearVelocity > 80 m/s), sub-stepping or a reduced fixed timestep is advisable to prevent numerical instability.

---

## AI Flight Control — Bang-Bang Fix

Early iterations of `DragonAIPilot` used a pure proportional controller:

```csharp
// BAD — produces ±1 oscillation ("bang-bang")
float yawInput = yawAngle > 0 ? 1f : -1f;
```

The fix uses two techniques together:

**1. Soft P-gain** — divide the angle by a large denominator so small errors produce small inputs:
```csharp
float yawInput  = Mathf.Clamp(yawAngle  / 90f, -1f, 1f);
float rollInput = Mathf.Clamp(-rollError / 60f, -1f, 1f);
```

**2. Low-pass filter on AI output** — simulates the physical reaction time of a pilot's brain:
```csharp
_smoothedPitchYaw = Vector2.Lerp(_smoothedPitchYaw, targetPitchYaw, Time.deltaTime * 6f);
_smoothedRoll     = Mathf.Lerp(_smoothedRoll, targetRoll, Time.deltaTime * 6f);
```

The Lerp lives in `DragonAIPilot` (the Brain), **not** in `GreatBirdController` (the Muscles). This keeps the muscle layer clean and responsive for human input while the AI receives pre-smoothed signals.
