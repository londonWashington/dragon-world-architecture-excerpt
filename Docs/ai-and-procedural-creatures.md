# AI and Procedural Creatures

Dragon World's creatures combine Utility AI, physical navigation, authored animation, and procedural response.

## Decision and navigation layers

High-level Utility AI evaluates context and selects an action. A separate navigation layer turns that intent into physical steering.

This separation allows an actor to decide to pursue, reposition, recover, or attack without embedding flight mathematics inside every tactical action.

## Utility-based behavior

Actions receive scores from reusable considerations such as:

- distance and alignment;
- health and resources;
- target state;
- environmental danger;
- ability availability.

Designer-authored response curves shape personality and encounter difficulty. Action-specific rules validate whether an action is currently legal, while general tactical preference remains data-driven.

## Physical AI

AI pilots operate the same flight body as the player. They must manage speed, altitude, momentum, line of sight, obstacles, water, and recovery. They do not receive a second, simplified movement model.

## Procedural animation

The animation stack layers physical context over authored motion:

- head and gaze tracking;
- spine bending;
- adaptive foot placement;
- wing and tail response;
- surface-aware stepping;
- additive turbulence, shock, and impact reactions.

Temporary reactions are additive wherever possible. A creature can continue its base movement while reacting to wind, damage, or loss of balance.

## Great Bird and Rider

The Great Bird reuses the shared flight and AI foundation with its own aerodynamic and presentation profile. The Rider adds physics-driven saddle inertia, distributed spine aiming, obstruction-aware leaning, and physical ranged projectiles.

Creature and rider communicate through focused events, preserving the presentation that the Rider commands the mount without coupling rider audio or animation to flight physics.

## Faction-aware ownership

Friend-or-foe decisions use faction ownership rather than controller type. A creature can change between player and AI control without changing which team receives its score, reactions, or presentation.
