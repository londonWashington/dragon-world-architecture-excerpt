# Physics-Based Flight

Dragon World treats flight as embodied movement rather than free camera translation.

## Physical foundation

The creature is an authoritative Rigidbody affected by gravity, momentum, aerodynamic force, and environmental influence. Multiple configurable surfaces distributed across the body generate lift, drag, and torque.

As a result, flight naturally expresses:

- acceleration and energy retention;
- speed-dependent turn radius;
- gliding and altitude loss;
- diving to gain speed;
- banking and high-authority maneuvers;
- stalls and recovery;
- body lag during rapid changes in direction.

## Biological control

The dragon banks by changing complete wing orientation rather than imitating conventional aircraft ailerons. Procedural wing pose and physical surface configuration cooperate so that the visible motion and physical response tell the same story.

## Stability without rail movement

Assistance systems damp unwanted oscillation and can preserve a chosen course, but they act through physical control authority. They do not replace the Rigidbody with a scripted spline or transform animation during ordinary gameplay.

## Look and Fly

The standard control mode converts the player's gaze into a physical flight objective. Camera-offset compensation prevents the creature from steering toward the camera itself and keeps travel aligned with the intended view direction.

## High-G maneuvering

A separate maneuvering mode allows sharper body-relative turns while preserving inertia and physical limits. This provides expressive combat control without changing the underlying movement model.

## Water transition

The same creature moves continuously between air, the ocean surface, and underwater locomotion. Water interaction coordinates:

- immersion state;
- surface height and wave contact;
- splashes and body droplets;
- drag and control changes;
- skimming rewards;
- extinguishing and wet-state behavior.

The transition does not require a loading screen or a separate nonphysical avatar.

## Reusable creature profiles

Dragons and Great Birds share the aerodynamic engine while using independent physical profiles. This keeps tuning isolated and allows each creature to preserve its own mass, speed, maneuverability, and silhouette.
