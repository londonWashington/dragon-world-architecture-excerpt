# Multiplayer Readiness

Dragon World is a solo indie project, but every architectural decision was made with multiplayer/co-op in mind. The foundations that would need to be added are networking transport and authority management — the game logic itself requires no structural changes.

---

## Instigator Pattern

Every damage event carries the source of the action:

```csharp
public struct DamageInfo
{
    public float     Damage;
    public Vector3   HitDirection;
    public float     ImpactForce;
    public Vector3   HitPoint;
    public GameObject Instigator;   // Who caused this — never null in a valid hit
}
```

`Health.cs` tracks the last attacker and reports it on death:

```csharp
private void Die()
{
    if (_lastAttacker != null)
        EconomyEvents.OnDiscreteActionTriggered?.Invoke(
            _lastAttacker,
            ScoreEventType.EnemyKill_Small,
            transform.position);
}
```

In a networked game, `Instigator` becomes the network player identity. No changes to damage or scoring logic are needed.

---

## No Player Singletons

There is no `Player.Instance`, `GameObject.FindWithTag("Player")`, or any other global player reference in the codebase.

Creature ownership is determined by a `FactionMember` component:

```csharp
// CheckCombatProximity — faction-based, not control-type-based
foreach (var target in TargetRegistry.AllTargets)
{
    if (_myFactionMember.IsHostileTo(target.FactionMember))
    {
        // this works for any number of players / factions
    }
}
```

In multiplayer, adding a second `FactionMember` with a different team ID is all that is needed. No logic changes.

---

## Seat-Switch Mechanic

The player can transfer control between Dragon and Rider mid-flight. When the player is on the Rider:

1. `DragonStateManager.isAIControlled = true` — the AI brain takes over the Dragon.
2. The Dragon continues earning score for `Faction.PlayerTeam` — because score is determined by the Dragon's `FactionMember`, not by who is pressing buttons.
3. `DragonAIPilot` + `DragonUtilityBrain` seamlessly handle flight and combat as a co-pilot.

This works because the codebase never checks `if (!isAIControlled)` before awarding score — only faction matters.

---

## Event Bus Decoupling

All game systems communicate via `EconomyEvents` (a static C# event bus):

```csharp
// Producer (physics/combat script):
EconomyEvents.OnDiscreteActionTriggered?.Invoke(source, ScoreEventType.PawStrike, position);

// Consumer (economy manager):
EconomyEvents.OnDiscreteActionTriggered += (src, type, pos) => { /* award points */ };
```

In a networked build, a `NetworkEventRelay` MonoBehaviour would sit between them — intercepting events on the server, broadcasting to clients, and re-firing locally. The producers and consumers remain unchanged.

---

## TargetRegistry

All targetable entities register themselves globally:

```csharp
public static class TargetRegistry
{
    public static HashSet<Targetable> Enemies = new HashSet<Targetable>();
    public static HashSet<PointOfInterest> POIs = new HashSet<PointOfInterest>();
}
```

`Targetable.OnEnable()` registers; `Targetable.OnDisable()` unregisters. No manual list management anywhere in the codebase.

In a networked build, `RegisterEnemy()` and `UnregisterEnemy()` become the integration points for network object spawning/despawning. The rest of the system is unchanged.

---

## What Would Need to Be Added for Multiplayer

| System | Required addition |
|---|---|
| Physics authority | Designate one machine as physics authority per creature; sync `Rigidbody` state |
| Input routing | Replace `Input.GetAxis()` calls with network input structs; `FlyerInputData` is already the clean boundary |
| `Instigator` | Replace `GameObject` reference with a network player ID (e.g. `ulong clientId` in Netcode for GameObjects) |
| `TargetRegistry` | Add network-aware registration for remotely spawned objects |
| `EconomyEvents` | Add `NetworkEventRelay` to forward events to the authoritative server |

None of these additions require changing existing game logic. The architecture was built to make this a bolt-on, not a rewrite.
