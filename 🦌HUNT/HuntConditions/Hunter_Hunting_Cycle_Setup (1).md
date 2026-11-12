# Hunter Social Action Setup

This document contains the complete Inspector setup for the Hunter's current hunting cycle:

> 🔍 **Track prey → 🔫 kill tracked prey → 🦴 harvest a corpse → 📦 return loot for deposit**

---

# Complete Hunter Interaction List

## Current safe order

Use this order until `DepositResources` has been implemented:

```text
Hunter
├── 1. HarvestAnimal
├── 2. Combat
└── 3. Track
```

`HarvestAnimal` and `Combat` both use `Combat` priority. Equal-priority interactions are resolved by their order in the Inspector, so harvesting receives first consideration.

They normally do not conflict because:

- `HarvestAnimal` looks for a dead animal with inventory.
- `Combat` requires a living, fully tracked animal.

## Final order after depositing is implemented

```text
Hunter
├── 1. DepositResources
├── 2. HarvestAnimal
├── 3. Combat
└── 4. Track
```

---

# 1. 🔍 Track Prey

```text
Track
├── Target Tags
│   └── Prey
├── Priority: Job
├── Chance: 1
├── Range: 1
├── Cooldown: 1
├── Value: 20
├── Can Enter Target Building: False
│
└── Conditions
    ├── onDuty
    │   ├── Evaluation: Boolean
    │   └── Required State: True
    │
    ├── doneTrackingTarget
    │   ├── Evaluation: Boolean
    │   └── Required State: False
    │
    └── carryingFullLoad
        ├── Evaluation: Boolean
        └── Required State: False
```

## Meaning

The hunter may track when:

- ✅ They are on duty.
- ✅ Their current living target has not been fully tracked.
- ✅ Their loot inventory is not full.

Each successful Track action adds:

```text
20 tracking progress
```

Therefore, five successful actions produce:

```text
100 tracking progress
```

Once the prey reaches 100 tracking progress, `doneTrackingTarget` becomes true. Because Track requires it to be false, Track stops and Combat can take over.

---

# 2. 🔫 Kill Fully Tracked Prey

Use the existing `Combat` interaction for the hunting kill.

```text
Combat
├── Target Tags
│   └── Prey
├── Priority: Combat
├── Chance: 1
├── Range: 1
├── Cooldown: 1
├── Value: 25
├── Can Enter Target Building: False
│
└── Conditions
    ├── onDuty
    │   ├── Evaluation: Boolean
    │   └── Required State: True
    │
    ├── hasTarget
    │   ├── Evaluation: Boolean
    │   └── Required State: True
    │
    ├── doneTrackingTarget
    │   ├── Evaluation: Boolean
    │   └── Required State: True
    │
    └── carryingFullLoad
        ├── Evaluation: Boolean
        └── Required State: False
```

## Meaning

The hunter may attack when:

- ✅ They are on duty.
- ✅ They have a remembered tracking target.
- ✅ That target has reached at least 100 tracking progress.
- ✅ The hunter still has carrying space.

## Damage value

With:

```text
Value: 25
```

an animal with 100 health requires four successful attacks.

Alternative values:

```text
Value: 50  = two attacks against 100 health
Value: 100 = one attack against 100 health
```

---

# 3. 🦴 Harvest a Dead Animal

The hunter may harvest **any valid prey corpse**, not only their own kill.

```text
HarvestAnimal
├── Target Tags
│   └── Prey
├── Priority: Combat
├── Chance: 1
├── Range: 1
├── Cooldown: 1
├── Value: 0
├── Can Enter Target Building: False
│
└── Conditions
    ├── onDuty
    │   ├── Evaluation: Boolean
    │   └── Required State: True
    │
    └── carryingFullLoad
        ├── Evaluation: Boolean
        └── Required State: False
```

## Do not add these conditions

```text
hasTarget
targetDead
targetHasInventory
doneTrackingTarget
```

`FindHarvestTarget()` and `IsValidHarvestTarget()` already ensure that the selected animal:

- ✅ Is dead.
- ✅ Still has inventory.
- ✅ Has the `Prey` tag.

The harvest search order is:

```text
Hunter's remembered kill with loot
→ otherwise the nearest valid prey corpse with loot
```

Adding tracked-target conditions would unnecessarily restrict harvesting to the hunter's remembered target.

---

# 4. 📦 Deposit Harvested Resources

This is the intended future Inspector setup:

```text
DepositResources
├── Target Tags
│   └── None for the final building-targeted version
├── Priority: CompleteJob
├── Chance: 1
├── Range: 1
├── Cooldown: 1
├── Value: 0
├── Can Enter Target Building: True
│
└── Conditions
    ├── onDuty
    │   ├── Evaluation: Boolean
    │   └── Required State: True
    │
    └── carryingFullLoad
        ├── Evaluation: Boolean
        └── Required State: True
```

## ⚠️ Do not enable DepositResources yet

The current `DepositResourcesAction` is still a placeholder that damages its target:

```csharp
AgentDeath.HurtAgent(
    target,
    deathType.bleeding,
    9999f
);
```

The current `ActionManager` also stores only an `Agent` target:

```csharp
private Agent activeTarget;
```

Depositing should instead target the Hunter's Hut:

```csharp
hunter.myWorkplace
```

That destination is a `Building`, not an `Agent`.

Keep `DepositResources` out of the Hunter's actual interaction list until building-targeted depositing has been implemented.

---

# Priority Structure

```text
CompleteJob
└── DepositResources

Combat
├── HarvestAnimal
└── Combat

Job
└── Track
```

This results in the following precedence:

1. Return a full load.
2. Harvest an available corpse.
3. Kill fully tracked prey.
4. Track prey.

---

# Complete Hunting Cycle

```text
Hunter reaches the Hunter's Hut
└── onDuty becomes True

Hunter has carrying space
└── Track becomes available

Hunter selects living Prey
└── Adds tracking progress

Tracking reaches 100
├── Track becomes blocked
└── Combat becomes available

Hunter attacks tracked prey
└── Animal dies

A valid corpse has inventory
└── HarvestAnimal becomes available

Hunter transfers corpse inventory into loot
├── If capacity remains: harvest more or resume hunting
└── If inventory becomes full: Track, Combat, and Harvest are blocked

Hunter is carrying a full load
└── DepositResources becomes available

Hunter returns loot to the Hunter's Hut
├── Carrying capacity becomes available again
└── The hunting cycle restarts
```

---

# Final Condition Summary

```text
Track
├── onDuty = True
├── doneTrackingTarget = False
└── carryingFullLoad = False

Combat
├── onDuty = True
├── hasTarget = True
├── doneTrackingTarget = True
└── carryingFullLoad = False

HarvestAnimal
├── onDuty = True
└── carryingFullLoad = False

DepositResources
├── onDuty = True
└── carryingFullLoad = True
```

---

# Prey Setup

Animals that hunters can track, kill, and harvest need these tags:

```text
Tags
├── Animal
└── Prey
```

Their `startingInventory` should contain the items produced by harvesting their corpse, for example:

```text
Big Horn Sheep Drops
├── Meat: 3
└── Skin: 1
```

When an animal dies, the existing `FillCorpseInventory()` logic should copy these starting items into the corpse inventory.

---

# Recommended Next Implementation

Implement `DepositResources` as a special building-targeted plan that:

1. Uses `agent.myWorkplace` as the destination.
2. Returns the hunter to the Hunter's Hut.
3. Transfers `agent.TakeAllLoot()` into the building or town inventory.
4. Clears the hunter's carried loot.
5. Allows the hunting cycle to restart.
