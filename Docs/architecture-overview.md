# Architecture Overview

Dragon World is structured around a simple principle: a creature's decisions, physical body, and audiovisual presentation should remain independently replaceable.

## Brain–Muscles–Wings

### Brain

The Brain layer owns intent and state:

- player or AI commands;
- locomotion and combat state transitions;
- resource and ability rules;
- target selection;
- semantic gameplay events.

It decides **what should happen**, but does not implement particle, audio, camera, or bone-level presentation.

### Muscles

The Muscles layer turns semantic intent into visible and audible behavior:

- procedural spine, head, feet, wings, and tail motion;
- authored animation blending;
- creature audio;
- visual effects;
- camera feedback.

Presentation components expose narrow commands and do not decide gameplay outcomes.

### Wings

The Wings layer is the physical flight simulation:

- Rigidbody motion;
- aerodynamic lift and drag;
- configurable lifting surfaces;
- physical momentum and energy management;
- surface interaction.

The flight engine is reusable across different creatures and does not depend on a particular player character.

## Shared contracts

Small interfaces connect systems without coupling them to one concrete creature implementation. They represent capabilities such as:

- receiving flight commands;
- performing combat actions;
- reacting to environmental forces;
- receiving telegraphed threats;
- reporting semantic aerobatic state.

This allows the dragon, Great Bird, and future flying actors to share AI, hazards, and world interactions while keeping creature-specific behavior isolated.

## Event-driven integration

Gameplay systems publish meaningful events rather than controlling unrelated presentation directly. For example:

1. Combat reports a confirmed gameplay result.
2. Health and scoring process the result independently.
3. UI, audio, camera, and VFX listeners present it for the relevant local player.

This prevents physics, scoring, UI, and creature presentation from becoming a single monolithic controller.

## Data-driven tuning

Aerodynamic profiles, attack balance, AI utility curves, resource rules, and presentation intensity are configured as data. Designers can tune different creatures and encounters without rewriting the shared foundation.

## Player/AI symmetry

Player and AI control ultimately operate the same physical creature capabilities. AI does not bypass stalls, inertia, turn radius, water, or environmental forces. This symmetry makes encounters more readable and keeps future control switching technically feasible.
