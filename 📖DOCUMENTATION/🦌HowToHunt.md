# 🦌 Hunting System TODO

## 🎯 Goal

Create a hunting loop that naturally emerges through interactions and conditions.

Desired flow:

```text
Hunter Hut
    ↓
Track Animal
    ↓
Hunt Animal
    ↓
Harvest Animal
    ↓
Return To Hunter Hut
    ↓
Deposit Resources
    ↓
Repeat
```

---

# 📦 Data

## ✅ Add ObjectType

```csharp
public enum ObjectType
{
    Food,
    Fur
}
```

---

## ✅ Add InventoryItem

```csharp
[System.Serializable]
public class InventoryItem
{
    public ObjectType type;
    public int amount;
}
```

---

## ✅ Add Inventory To Agent

```csharp
public List<InventoryItem> inventory;
```

---

## ✅ Add Harvest Rewards To ScriptableAgent

Animals define what they yield.

```csharp
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

# 📈 Progress System

## ✅ Add Progress Storage To Agent

Lives on the target.

```csharp
Dictionary<InteractionType, float> progress;
```

or

```csharp
float[] progress;
```

Examples:

```csharp
buffalo.progress[InteractionType.Track]
```

```csharp
villager.progress[InteractionType.Convert]
```

---

## ✅ Add Tracking Threshold

Animals decide how difficult they are to hunt.

```csharp
public float trackingRequired = 100;
```

Examples:

```text
Pig
20

Coyote
50

Buffalo
100

Legendary Bear
500
```

---

# 🤝 Interactions

## ✅ Add Track Interaction

```csharp
InteractionType.Track
```

---

## ✅ Add DepositResources Interaction

```csharp
InteractionType.DepositResources
```

---

# 🎬 Actions

## ✅ Add Track Animation Action

```csharp
AgentAction.track
```

---

## ✅ Add Deposit Animation Action

```csharp
AgentAction.deposit
```

---

# ⚙️ SocialActions

## ✅ Create TrackAction

Add to:

```csharp
registry
```

Behaviour:

```text
Move To Animal
    ↓
Increase Animal Track Progress
    ↓
When Progress Reaches Threshold
    ↓
Tracking Complete
```

Pseudo:

```csharp
target.progress[InteractionType.Track] += interaction.value;
```

---

## ✅ Fix HarvestAnimalAction

Current version kills the animal.

New version should:

```text
Animal Already Dead
    ↓
Give Harvest Drops
    ↓
Mark Harvested
```

Pseudo:

```csharp
self.inventory.AddRange(target.harvestDrops);

target.harvested = true;
```

---

## ✅ Create DepositResourcesAction

Behaviour:

```text
At Hunter Hut
    ↓
Convert Resources
    ↓
Clear Inventory
```

Pseudo:

```csharp
Food
    -> Town Food

Fur
    -> Town Wealth
```

---

# ✅ Conditions

## ✅ HasTrackableAnimal

True when:

```text
Nearby Animal
Alive
Not Harvested
```

---

## ✅ AnimalTracked

True when:

```text
Track Progress >= Required Track Amount
```

---

## ✅ AnimalDead

True when:

```text
Target Is Dead
```

---

## ✅ CarryingResources

True when:

```text
Inventory Contains Items
```

---

# 🦴 Animal States

## ✅ Add Harvested Flag

```csharp
public bool harvested;
```

Flow:

```text
Alive
    ↓
Tracked
    ↓
Dead
    ↓
Harvestable
    ↓
Harvested
```

---

# 🧠 Hunter Interactions

Hunter Scriptable should eventually contain:

```text
🦌 Track
Condition:
    HasTrackableAnimal

⚔️ Combat
Condition:
    AnimalTracked

🔪 HarvestAnimal
Condition:
    AnimalDead

🏕️ DepositResources
Condition:
    CarryingResources
```

The chain emerges from conditions becoming true.

---

# 🏕️ Hunter Hut

## ✅ Add Resource Deposit Logic

When DepositResources succeeds:

```text
Food
    -> Town Food

Fur
    -> Town Wealth
```

Then:

```csharp
inventory.Clear();
```

---

# ☠️ Death

## ✅ Drop Inventory On Death

When a hunter dies:

```text
Hunter Dies
    ↓
Inventory Drops
    ↓
Resources Remain In World
```

Future:

```text
Another Hunter Recovers It

Bandits Steal It

Loot Decays
```

---

# 🌟 First Success Test

When this can happen naturally:

```text
Hunter Leaves Hut
    ↓
Finds Buffalo
    ↓
Tracks Buffalo For Several Days
    ↓
Sleeps At Night
    ↓
Continues Tracking Next Day
    ↓
Buffalo Becomes Tracked
    ↓
Hunter Kills Buffalo
    ↓
Harvests Food And Fur
    ↓
Returns To Hunter Hut
    ↓
Deposits Resources
    ↓
Town Gains Food And Wealth
```

---

# 🌟 First Story Test

```text
Hunter Tracks Buffalo
    ↓
Hunter Kills Buffalo
    ↓
Hunter Harvests Fur
    ↓
Bear Kills Hunter
    ↓
Inventory Drops
    ↓
Town Never Receives The Wealth
```

If this story can happen, the hunting system is working the way BoomTown is intended to work.
