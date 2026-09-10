# Dragon World — Technical Showcase

Dragon World is a physics-driven dragon game focused on expressive flight, large-scale aerial encounters, procedural creature motion, and a reactive ocean ecosystem.

This repository is a **public technical showcase**, not the production source repository. It presents the project's architectural reasoning and verified capabilities while intentionally excluding proprietary implementation details, private infrastructure, production assets, credentials, and unreleased design material.

## What this showcase demonstrates

- Rigidbody-based aerodynamic flight rather than rail or transform-driven movement.
- A clear separation between gameplay decisions, physical simulation, and presentation.
- Player and AI control operating through the same creature capabilities.
- Procedural animation layered over authored animation.
- Event-driven combat, world interaction, UI, audio, and VFX.
- A reusable technical foundation supporting the Arena Survival vertical slice.

## Documentation

* [Architecture Overview](Docs/architecture-overview.md)
* [Physics-Based Flight](Docs/physics-based-flight.md)
* [AI and Procedural Creatures](Docs/ai-and-procedural-creatures.md)
* [Combat and Reactive World](Docs/combat-and-reactive-world.md)
* [Arena Survival Vertical Slice](Docs/arena-survival.md)
* [Public Technical Scope](Docs/public-technical-scope.md)

## Current development state

The project has a playable Arena Survival build supported by mature core systems for dragon locomotion, combat, AI flight, procedural animation, targeting, environmental interaction, and presentation.

The architecture remains under active development. This public edition describes stable system boundaries and player-facing results rather than publishing implementation recipes or internal issue tracking.

## Repository policy

- Production source code is omitted. The /Scripts directory contains only reference architectural contracts, decoupled interfaces, and sample subsystems demonstrating the framework targeted for C++ migration.
- No complete algorithms, tuning values, backend topology, or private roadmap are included.
- Names of selected systems are shown only where they clarify architectural responsibility.
- Public material is curated separately from the internal source of truth.

Copyright © 2026 Dragon World project. All rights reserved. See [NOTICE.md](NOTICE.md).
