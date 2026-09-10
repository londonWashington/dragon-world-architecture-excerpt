# Arena Survival Vertical Slice

Arena Survival is a standalone playable vertical slice built on Dragon World's reusable systems. It demonstrates the interaction between physical flight, combat, Utility AI, environmental traversal, progression, and presentation.

## Core loop

The player explores the ocean arena, learns movement through interaction with fauna, gathers energy objectives, and survives escalating encounters with Nemesis.

The experience tests whether the core dragon systems can support a structured game mode without embedding Arena-specific rules inside reusable locomotion or combat code.

## Scene-level orchestration

Dedicated Arena managers own:

- phase progression;
- objectives and timing;
- boundary and environmental rules;
- boss encounter state;
- checkpoint restoration;
- HUD and tutorial presentation.

Reusable dragon, AI, economy, health, targeting, water, audio, and VFX systems remain independent from the Arena's phase definitions.

## Nemesis

Nemesis uses the same physical flight and combat foundation as other aerial actors, wrapped by encounter-specific state such as phase activation, defensive layers, stagger, and escalation.

The design emphasizes counterplay: readable threats, evasive mastery, tactical resource use, and opportunities to turn environmental danger against the pursuer.

## Nature onboarding

The opening teaches flight and targeting through relationships with dolphins and seagulls rather than through a detached tutorial room. These interactions introduce:

- peaceful target focus;
- surface skimming;
- applied Boost;
- aerobatic control;
- health and stamina recovery;
- spatial guidance toward the first major objective.

Tutorial completion is based on confirmed gameplay results rather than raw key presses, preserving remapping and alternative control schemes.

## Presentation ownership

Arena HUD systems observe semantic events and arbitrate competing messages by context and importance. Hiding the HUD changes presentation only; it does not disable gameplay managers or reset encounter state.

## Vertical-slice purpose

Arena Survival is both a playable experience and a production test bed. It proves that the project's foundational systems can compose into a coherent loop while remaining reusable for broader world and campaign development.
