# Combat and Reactive World

Combat in Dragon World is built around physical motion, readable intent, and coordinated feedback.

## Damage and targeting

Targets register with a shared world registry. Faction rules determine hostility, while body-part hitboxes route damage to an actor's health and apply contextual response.

Health remains independent from UI and effects. It publishes state changes; presentation systems decide how those changes should look and sound.

## Physical attacks

Claw strikes, bites, fire breath, rider projectiles, and environmental threats each preserve a distinction between:

- the gameplay simulation that determines contact and outcome;
- the visual simulation that communicates motion and impact.

These two paths share semantic events and spatial information so that visible effects remain aligned with gameplay.

## Fire breath

Fire breath behaves as a moving physical stream rather than a static damage cone. It interacts with actors, geometry, water, wind, and defensive shockwaves while its audio and VFX continue travelling through the world after emission.

Exposure, ignition, ongoing burn state, and extinguishing are distinct gameplay concepts. This supports readable tuning without tying damage timing to particle density.

## Threat communication and Perfect Dodge

Dangerous attacks publish a threat before impact. Dragon Sense turns that warning into directional body effects, audio anticipation, camera language, and diegetic indicators.

The player can answer during a defined skill window. A successful Perfect Dodge is confirmed from the gameplay outcome before rewards and presentation are triggered.

## Roar and world shockwaves

The dragon's roar produces a shared spherical shockwave representation used by compatible world and projectile systems. Each receiving system owns the strength and style of its reaction, allowing fire, clouds, water, and future objects to respond differently while remaining synchronized.

## Ocean ecosystem

The ocean is both environment and resource system:

- surface skimming restores energy and supports score play;
- recently used areas can temporarily deplete;
- fish restore resources through a physical feeding interaction;
- dolphins and seagulls respond to player trust;
- trusted fauna can unlock traversal and recovery opportunities.

Fauna publishes world events rather than controlling the player's camera, sound, or HUD directly.

## Interactive atmosphere

Clouds, water, lightning, and tornadoes are connected to gameplay state:

- clouds deform around fast-moving creatures and can conceal targets;
- lightning combines procedural presentation with dodge gameplay;
- tornadoes apply external wind, turbulence, and physical displacement;
- water state changes locomotion, sound, fire, and presentation.

The objective is a world in which spectacle and mechanics reinforce each other instead of existing as separate layers.
