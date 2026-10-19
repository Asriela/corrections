# 🦌 BoomTown Hunting TODO List

## Phase 1: Data Structures

### ✅ Add ObjectType

```csharp
public enum ObjectType
{
    Food,
    Fur
}
```

---

### ✅ Add InventoryItem

```csharp
[System.Serializable]
public class InventoryItem
{
    public ObjectType type;
    public int amount;
}
```

---

### ✅ Add Agent Inventory

```csharp
public List<InventoryItem> inventory;
```

---

### ✅ Add Harvest Drops To ScriptableAgent

```csharp
[Header("Harvest")]
public List<InventoryItem> harvestDrops;
```

Examples:

```text
Pig
    Food x10

Coyote
    Fur x5

Buffalo
    Food x30
    Fur x20
```

---

## Phase 2: Progress System

### ✅ Add Generic Progress Storage To Agent

Stored on the target.

```csharp
Dictionary<InteractionType, float> progress;
```

Or:

```csharp
public float[] socialProgress;
```

Examples:

```csharp
buffalo.progress[InteractionType.Track]
```

```csharp
villager.progress[InteractionType.Convert]
```

---

### ✅ Add Track Interaction

```csharp
public enum InteractionType
{
    Combat,
    Track,
    HarvestAnimal,
    Convert,
    Heal,
    ...
}
```

---

## Phase 3: Animal States

### ✅ Add Harvest Flag

```csharp
public bool harvested;
```

Flow:

```text
Alive
 ↓
Dead
 ↓
Harvestable
 ↓
Harvested
 ↓
Removed
```

---

## Phase 4: Hunter Behaviour

### ✅ Create Hunter Behaviour

Decision order:

```text
Inventory not empty?
    Return To Hut

Dead Animal Exists?
    Harvest

Tracked Animal Exists?
    Continue Tracking

Animal Exists?
    Start Tracking

Nothing?
    Loiter Near Hut
```

---

## Phase 5: Tracking

### ✅ Find Animal

```csharp
Agent FindNearestTrackableAnimal();
```

Rules:

```text
Has Prey Tag
Alive
Not Harvested
```

---

### ✅ Track Action

```csharp
animal.progress[InteractionType.Track] += trackingRate;
```

Accumulated over time.

Not instantly.

---

### ✅ Animal Becomes Killable

```text
Track Progress >= Required Track Amount
```

↓

```text
Begin Hunt
```

---

## Phase 6: Hunting

### ✅ New Hunt Action

```csharp
AgentAction.hunt
```

(or reuse attack)

---

### ✅ Make Hunts Slow

Desired:

```text
Track
 ↓
Hunt
 ↓
Several Seconds
 ↓
Kill
```

Not:

```text
Touch Animal
 ↓
Animal Dies
```

This creates opportunities for:

```text
Nightfall

Bear Attack

Bandits

Interruptions

Multiple Hunters Collaborating
```

---

### ✅ Kill Animal

```csharp
animal.isDead = true;
```

---

## Phase 7: Harvesting

### ✅ Implement HarvestAnimalAction

Flow:

```text
Reach Corpse
 ↓
Play Harvest Animation
 ↓
Wait
 ↓
Transfer Harvest Drops
 ↓
Mark Harvested
```

Pseudo:

```csharp
hunter.inventory.AddRange(animal.harvestDrops);

animal.harvested = true;
```

---

## Phase 8: Returning Home

### ✅ Return To Hunter Hut

```text
Carrying Resources
 ↓
Return Home
```

---

### ✅ Deposit Resources

When hunter reaches hut:

```text
Food -> Town Food

Fur -> Town Wealth
```

Pseudo:

```csharp
foreach (var item in inventory)
{
    switch(item.type)
    {
        case ObjectType.Food:
            TownFood += item.amount;
            break;

        case ObjectType.Fur:
            TownWealth += item.amount;
            break;
    }
}

inventory.Clear();
```

---

## Phase 9: Death

### ✅ Drop Inventory On Death

```csharp
DropInventory();
```

Flow:

```text
Hunter Dies
 ↓
Loot Remains In World
 ↓
Can Be Recovered Later
```

---

## Future Improvement: Recovery

Not required for MVP.

Potential outcomes:

```text
Another Hunter Retrieves It

Trader Retrieves It

Bandits Steal It

Loot Expires
```

---

# First Milestone

When this can happen, hunting is working:

```text
Hunter Leaves Hut
 ↓
Finds Buffalo
 ↓
Tracks Buffalo For Several Days
 ↓
Goes Home At Night
 ↓
Continues Next Day
 ↓
Kills Buffalo
 ↓
Harvests Food And Fur
 ↓
Returns To Hut
 ↓
Deposits Resources
 ↓
Town Gains Food And Wealth
```

---

# First Emergent Story Test

```text
Hunter Kills Buffalo
 ↓
Harvests Fur
 ↓
Bear Kills Hunter
 ↓
Fur Drops On Ground
 ↓
Town Never Receives The Wealth
```

If that story can naturally happen, the hunting system is doing its job.
