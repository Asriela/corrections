# Hunter ScriptableAgent Interaction Setup

This is the complete **Hunter `ScriptableAgent` interaction setup** for the current hunting loop:

> 🎯 **Track prey → kill tracked prey → harvest any valid corpse → stop when full**

## 1. 🔍 Track

The hunter finds prey and gradually increases its shared tracking progress.

### Main settings

```text
Type: Track
Target Tags:
- Prey

Priority: Job
Chance: 1
Range: 1
Cooldown: 1
Value: 20
Can Enter Target Building: False
```

`Value: 20` means five successful tracking actions reach 100%. Adjust it to control tracking speed.

### Conditions

```text
Condition 1:
- Condition: onDuty
- Invert: False

Condition 2:
- Condition: doneTrackingTarget
- Invert: True
```

### Why

- `onDuty` prevents hunting outside working hours.
- Inverted `doneTrackingTarget` allows tracking when there is no target or the current target is not fully tracked.
- Once tracking reaches 100%, this interaction becomes unavailable.
- `FindTrackingTarget()` either retains the current target or selects another suitable prey animal.

---

## 2. 🔫 Kill tracked prey

Use your existing `Combat` interaction to kill the animal after tracking reaches 100%.

### Main settings

```text
Type: Combat
Target Tags:
- Prey

Priority: Combat
Chance: 1
Range: 1
Cooldown: 1
Value: 25
Can Enter Target Building: False
```

`Value: 25` means an animal with 100 health takes four attacks to kill. Raise it for quicker kills.

### Conditions

```text
Condition 1:
- Condition: onDuty
- Invert: False

Condition 2:
- Condition: hasTarget
- Invert: False

Condition 3:
- Condition: doneTrackingTarget
- Invert: False
```

### Why

The hunter can attack only when:

- They are working.
- They have a tracked target.
- That target has reached 100% tracking progress.

`doneTrackingTarget` already requires the animal to be alive, so you do not need an inverted `targetDead` condition.

---

## 3. 🦴 Harvest any corpse

The hunter harvests their own kill first. If that kill has no inventory remaining, they find the nearest valid corpse with loot.

### Main settings

```text
Type: HarvestAnimal
Target Tags:
- Prey

Priority: Combat
Chance: 1
Range: 1
Cooldown: 1
Value: 0
Can Enter Target Building: False
```

### Conditions

```text
Condition 1:
- Condition: onDuty
- Invert: False

Condition 2:
- Condition: carryingFullLoad
- Invert: True
```

### Do not add these conditions

```text
hasTarget
doneTrackingTarget
targetDead
targetHasInventory
```

Your dedicated `FindHarvestTarget()` and `IsValidHarvestTarget()` already ensure the target:

- Is dead.
- Still has inventory.
- Has the `Prey` tag.

Avoiding the tracked-target conditions makes the Inspector setup clearer and guarantees harvesting is not unnecessarily tied to the hunter’s own kill.

---

## 4. 📦 Deposit resources

### Do not add this interaction yet

```text
Type: DepositResources
```

Your deposit action is still a placeholder and currently kills its agent target:

```csharp
AgentDeath.HurtAgent(
    target,
    deathType.bleeding,
    9999f
);
```

The hunter will correctly stop harvesting when full, but depositing should remain disabled until it targets the Hunter’s Hut or another `Building`.

Eventually, its main condition should be:

```text
Condition 1:
- Condition: onDuty
- Invert: False

Condition 2:
- Condition: carryingFullLoad
- Invert: False
```

Its priority should be:

```text
Priority: CompleteJob
```

That will make depositing beat harvesting, killing, and tracking.

---

# 📋 Inspector order

Put the interactions in this order:

```text
1. HarvestAnimal
2. Combat
3. Track
```

Although priorities already separate most of them, this gives harvesting precedence if two interactions ever share the same priority and are both eligible.

When deposit is implemented:

```text
1. DepositResources
2. HarvestAnimal
3. Combat
4. Track
```

## Priority structure

```text
DepositResources: CompleteJob
HarvestAnimal: Combat
Combat: Combat
Track: Job
```

`HarvestAnimal` and `Combat` can share the same priority because they target mutually exclusive states:

- Harvest requires a dead animal.
- Combat requires the hunter’s living, fully tracked target.

---

# 🦌 Prey setup

Animals that hunters may track, kill, and harvest need:

```text
Tags:
- Animal
- Prey
```

For example:

```text
Big Horn Sheep:
- Animal
- Prey
```

Only agents with the `Prey` tag will qualify for Hunter interactions.

Their `startingInventory` should contain corpse drops such as:

```text
Food: 3
Skin: 1
```

When the animal dies, those items should be copied into its corpse inventory by your existing `FillCorpseInventory` logic.

---

# ✅ Final Hunter setup

## Track

```text
Type: Track
Tags: Prey
Priority: Job
Chance: 1
Range: 1
Cooldown: 1
Value: 20

Conditions:
✅ onDuty
🚫 doneTrackingTarget
```

## Combat

```text
Type: Combat
Tags: Prey
Priority: Combat
Chance: 1
Range: 1
Cooldown: 1
Value: 25

Conditions:
✅ onDuty
✅ hasTarget
✅ doneTrackingTarget
```

## HarvestAnimal

```text
Type: HarvestAnimal
Tags: Prey
Priority: Combat
Chance: 1
Range: 1
Cooldown: 1
Value: 0

Conditions:
✅ onDuty
🚫 carryingFullLoad
```

Here, 🚫 means the condition has **Invert enabled**.

## Hunting flow

```text
No tracked prey
→ Track

Tracking below 100%
→ Continue tracking

Tracking reaches 100%
→ Combat

Animal dies
→ Harvest corpse

Corpse emptied
→ Find another corpse or resume tracking

Inventory full
→ Stop harvesting until deposit is implemented
```
