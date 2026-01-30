# Phase C: Hallmark Depth Implementation Guide

This document covers the Phase C systems that add tactical depth to the combat system, including BG3-style features like action economy, surfaces, height advantage, and reactions.

## Systems Implemented

### 1. Action Economy (`Combat/Actions/`)

**Files:**
- `ActionBudget.cs` - Tracks action, bonus action, movement, and reaction
- `ActionType.cs` - Enum of action types

**Usage:**
```csharp
var budget = new ActionBudget();
budget.ResetForTurn(30); // 30ft movement

// Check and consume resources
if (budget.CanPayCost(ActionType.Action, 1))
{
    budget.ConsumeCost(ActionType.Action, 1);
}

// Movement
if (budget.CanPayCost(ActionType.Movement, 15))
{
    budget.ConsumeMovement(15);
}

// Dash grants extra movement
budget.Dash(30);
```

### 2. Position System

**Combatant.Position** - Vector3 for 3D position (x, y, z)

Position is loaded from scenarios:
```json
{
  "x": 10,
  "y": 0,
  "z": 5
}
```

### 3. Movement System (`Combat/Movement/`)

**Files:**
- `MovementService.cs` - Basic movement with budget consumption
- `MovementType.cs` - Enum of movement types
- `SpecialMovementService.cs` - Jump, climb, teleport, swim, fly

**Basic Movement:**
```csharp
var result = movementService.MoveTo(combatant, targetPosition);
if (result.Success)
{
    // Movement completed, budget consumed
}
```

**Special Movement:**
```csharp
var specialMove = new SpecialMovementService();

// Jump (STR-based distance)
var jumpResult = specialMove.AttemptJump(combatant, target, hasRunningStart: true);

// Climb (double movement cost)
var climbResult = specialMove.AttemptClimb(combatant, ledge);

// Teleport (no movement budget, no opportunity attacks)
var teleportResult = specialMove.AttemptTeleport(combatant, target, maxRange: 30);
```

### 4. Reaction System (`Combat/Reactions/`)

**Files:**
- `ReactionTrigger.cs` - Trigger types and contexts
- `ReactionDefinition.cs` - Defines a reaction (like opportunity attack)
- `ReactionPrompt.cs` - UI prompt for player decisions
- `ReactionSystem.cs` - Tracks eligibility and prompts
- `ResolutionStack.cs` - Manages nested action/reaction resolution

**Trigger Types:**
- `EnemyLeavesReach` - Opportunity attack
- `BeingAttacked` - Shield spell, Uncanny Dodge
- `TakingDamage` - Hellish Rebuke
- `AllyCast` - Counterspell
- `EnemyCast` - Counterspell
- `ForcedMovement` - Sentinel
- `TurnStart`, `TurnEnd`

**Usage:**
```csharp
var reactions = new ReactionSystem(eventBus);

// Register reaction type
reactions.RegisterReaction(new ReactionDefinition
{
    Id = "opportunity_attack",
    Name = "Opportunity Attack",
    TriggerType = ReactionTriggerType.EnemyLeavesReach,
    Range = 5
});

// When trigger occurs
var context = new ReactionTriggerContext
{
    TriggerType = ReactionTriggerType.EnemyLeavesReach,
    TriggeringCombatantId = "goblin",
    SourcePosition = goblin.Position
};

var eligibleReactors = reactions.GetEligibleReactors(context, allCombatants);
```

### 5. Resolution Stack

Manages nested action/reaction execution:

```csharp
var stack = new ResolutionStack { MaxDepth = 10 };

// Push attack onto stack
var attackItem = stack.Push("attack", "fighter", "goblin");

// Goblin uses reaction - push onto stack
var reactionItem = stack.Push("uncanny_dodge", "goblin");

// Cancel the attack with reaction?
if (canCancel) stack.CancelCurrent();

// Modifiers
stack.ModifyCurrent("shield", -5); // Add AC from shield spell

// Resolve in order
stack.Pop(); // Resolve reaction
stack.Pop(); // Resolve attack (may be cancelled/modified)
```

### 6. Surfaces System (`Combat/Environment/`)

**Files:**
- `SurfaceDefinition.cs` - Defines surface types
- `SurfaceInstance.cs` - Runtime instance
- `SurfaceManager.cs` - Manages active surfaces

**Default Surfaces:**
- Fire: 5 damage on enter/turn start
- Water: Applies "wet" status
- Poison: 3 damage, applies "poisoned"
- Oil: 1.5x movement cost
- Ice: 2x movement cost (difficult terrain)

**Interactions:**
- Fire + Water = Steam
- Fire + Oil = Fire (persists, oil burns)
- Ice + Fire = Water (ice melts)

**Usage:**
```csharp
var surfaces = new SurfaceManager(eventBus);

// Create surface
var fire = surfaces.CreateSurface("fire", position, radius: 5, creatorId: "wizard");

// Process movement
surfaces.ProcessEnter(combatant, newPosition);
surfaces.ProcessLeave(combatant, oldPosition);

// Turn processing
surfaces.ProcessTurnStart(combatant);
surfaces.ProcessTurnEnd(combatant);

// Tick at round end (reduces duration)
surfaces.ProcessRoundEnd();
```

### 7. Line of Sight & Cover (`Combat/Environment/LOSService.cs`)

**Cover Levels:**
- None: No bonus
- Half: +2 AC
- Three-Quarters: +5 AC
- Full: No line of sight

**Usage:**
```csharp
var los = new LOSService();

// Register obstacles
los.RegisterObstacle(new Obstacle
{
    Id = "wall",
    Position = position,
    Width = 2,
    ProvidedCover = CoverLevel.Half
});

// Check LOS
var result = los.CheckLOS(attacker, target);
if (result.HasLineOfSight)
{
    int acBonus = result.GetACBonus();
    // Apply cover bonus to target AC
}

// Flanking detection
bool flanked = los.IsFlanked(target, adjacentEnemies);
```

### 8. Height Advantage (`Combat/Environment/HeightService.cs`)

**Attack Modifiers:**
- Higher ground: +2 to attack
- Lower ground: -2 to attack

**Ranged Damage:**
- Higher ground: +15% damage
- Lower ground: -10% damage

**Fall Damage:**
- Safe fall: 10ft
- Damage: 1d6 per 10ft (average 3.5)
- Lethal: 200ft+

**Usage:**
```csharp
var height = new HeightService();

// Check advantage
if (height.HasHeightAdvantage(attacker, target))
{
    attackRoll += 2;
}

// Ranged damage modifier
float damageMultiplier = height.GetDamageModifier(attacker, target, isRanged: true);

// Fall damage
var fallResult = height.ApplyFallDamage(combatant, fallDistance);
```

### 9. Forced Movement (`Combat/Movement/ForcedMovementService.cs`)

**Types:**
- Push: Away from source
- Pull: Toward source
- Knockback: Specific direction

**Features:**
- Collision detection (obstacles, other combatants)
- Collision damage
- Surface triggering
- Fall damage

**Usage:**
```csharp
var forced = new ForcedMovementService(eventBus, surfaces, height);

// Push 10 feet away from caster
var result = forced.Push(target, caster.Position, distance: 10);

// Pull toward attacker
var pullResult = forced.Pull(target, attacker.Position, distance: 5);

// Check result
if (result.WasBlocked)
{
    // Collision damage already applied
}
if (result.TriggeredSurface)
{
    // Surface effects already processed
}
```

## Test Scenarios

- `Data/Scenarios/reaction_test.json` - Reaction triggers
- `Data/Scenarios/surface_test.json` - Surface effects and interactions
- `Data/Scenarios/movement_test.json` - Action economy and movement
- `Data/Scenarios/height_los_test.json` - Height and cover

## Integration Points

### CombatContext Extensions

Add Phase C services to CombatContext:
```csharp
public SurfaceManager Surfaces { get; }
public LOSService LOS { get; }
public HeightService Height { get; }
public ReactionSystem Reactions { get; }
public ForcedMovementService ForcedMovement { get; }
```

### Event Subscriptions

Key events to subscribe to:
- `CombatantMoved` - Check surfaces, opportunity attacks
- `AttackDeclared` - Check reactions (Shield, Counterspell)
- `DamageTaken` - Check reactions (Hellish Rebuke)
- `TurnStarted` - Surface tick damage
- `RoundEnded` - Tick surface durations

## Next Steps (Phase D)

- AI decision making
- Buff/debuff UI
- Combat log enhancements
- Save/load combat state
