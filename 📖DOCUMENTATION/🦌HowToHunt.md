You were right that the previous version made tracking much deeper than necessary. This version keeps your existing architecture intact and contains the actual code needed at each implementation step.

# 🦌 BoomTown Hunting System TODO

## 🎯 Goal

Create a hunting loop that emerges naturally through interactions and conditions.

```text
Hunter Hut
    ↓
Choose Best Animal
    ↓
Track Animal
    ↓
Release Track Action
    ↓
Reconsider Available Animals
    ↓
Continue Tracking Or Switch Target
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

# ⚡ AI Flow Reminder

```text
📜 ScriptableAgent
    Defines Interactions And Conditions
    ↓
🎯 ActionManager
    Selects Interaction
    Chooses Target
    Moves Into Range
    Proposes Action
    ↓
🎬 AgentActionState
    Accepts Or Rejects Proposal
    Plays Animation
    Handles Priority And Interruptions
    ↓
⚙️ SocialActions
    Executes The Interaction Effect
    Releases Completed Action
    ↓
✅ State And Progress Change
    Unlocks New Interactions
```

For hunting:

```text
ActionManager
    Chooses The Best Animal
    ↓
Existing Movement
    Moves Hunter Into Range
    ↓
TrackAction
    Adds Tracking Progress
    Releases AgentActionState
    ↓
ActionManager
    Thinks Again
```

---

# 1. 📦 Add Resource Types

## ☐ Add `ObjectType`

```csharp
public enum ObjectType
{
    Food,
    Fur
}
```

---

## ☐ Add `InventoryItem`

```csharp
[System.Serializable]
public class InventoryItem
{
    public ObjectType type;
    public int amount;

    public InventoryItem(
        ObjectType type,
        int amount)
    {
        this.type = type;
        this.amount = amount;
    }
}
```

---

# 2. 🎒 Add Inventory To `Agent`

```csharp
[SerializeField]
public List<InventoryItem> inventory =
    new List<InventoryItem>();
```

---

## ☐ Add Inventory Helper

This keeps the Inspector-friendly list while preventing duplicate entries for the same resource.

```csharp
public void AddToInventory(
    ObjectType type,
    int amount)
{
    if (amount <= 0)
        return;

    if (inventory == null)
    {
        inventory =
            new List<InventoryItem>();
    }

    foreach (InventoryItem item in inventory)
    {
        if (item == null)
            continue;

        if (item.type != type)
            continue;

        item.amount += amount;
        return;
    }

    inventory.Add(
        new InventoryItem(
            type,
            amount));
}
```

---

## ☐ Add Inventory Check

```csharp
public bool HasInventoryItems()
{
    if (inventory == null)
        return false;

    foreach (InventoryItem item in inventory)
    {
        if (item != null &&
            item.amount > 0)
        {
            return true;
        }
    }

    return false;
}
```

---

# 3. 🐃 Add Harvest Rewards To `ScriptableAgent`

Animals define what they produce when harvested.

```csharp
[Header("Hunting")]
[SerializeField]
public List<InventoryItem> harvestDrops =
    new List<InventoryItem>();

[Min(1f)]
[SerializeField]
public float trackingRequired = 100f;
```

Example configuration:

```text
Pig
    Tracking Required: 20
    Food x10

Coyote
    Tracking Required: 50
    Fur x5

Buffalo
    Tracking Required: 100
    Food x30
    Fur x20

Bear
    Tracking Required: 150
    Food x20
    Fur x40
```

---

# 4. 📈 Add Social Progress To `Agent`

Progress lives on the thing receiving the interaction.

```text
Buffalo
    Track Progress: 65

Villager
    Convert Progress: 30
```

Add:

```csharp
public Dictionary<InteractionType, float>
    socialProgress =
        new Dictionary<InteractionType, float>();
```

---

## ☐ Add Progress Helper

```csharp
public float GetSocialProgress(
    InteractionType interactionType)
{
    if (socialProgress.TryGetValue(
            interactionType,
            out float progress))
    {
        return progress;
    }

    return 0f;
}
```

---

## ☐ Add Progress Increase Helper

```csharp
public void AddSocialProgress(
    InteractionType interactionType,
    float amount)
{
    if (amount <= 0f)
        return;

    if (!socialProgress.ContainsKey(
            interactionType))
    {
        socialProgress[
            interactionType] = 0f;
    }

    socialProgress[
        interactionType] += amount;
}
```

---

# 5. 🎯 Add Current Targets To `Agent`

The actor remembers a separate target for each interaction.

```text
Hunter
    currentTarget[Track] → Buffalo

Priest
    currentTarget[Convert] → Villager
```

Add:

```csharp
public Dictionary<InteractionType, Agent>
    currentTarget =
        new Dictionary<InteractionType, Agent>();
```

---

## ☐ Add Target Helper

```csharp
public Agent GetCurrentTarget(
    InteractionType interactionType)
{
    if (currentTarget.TryGetValue(
            interactionType,
            out Agent target))
    {
        return target;
    }

    return null;
}
```

---

## ☐ Add Target Setter

```csharp
public void SetCurrentTarget(
    InteractionType interactionType,
    Agent target)
{
    if (target == null)
    {
        currentTarget.Remove(
            interactionType);

        return;
    }

    currentTarget[
        interactionType] = target;
}
```

---

## ☐ Add Target Clearer

```csharp
public void ClearCurrentTarget(
    InteractionType interactionType)
{
    currentTarget.Remove(
        interactionType);
}
```

---

# 6. 🦴 Add Harvest State To `Agent`

```csharp
public bool harvested;
```

Animal flow:

```text
Alive
    ↓
Being Tracked
    ↓
Fully Tracked
    ↓
Killed
    ↓
Harvestable
    ↓
Harvested
```

---

# 7. 🤝 Extend `InteractionType`

Add:

```csharp
Track,
DepositResources
```

Updated enum:

```csharp
public enum InteractionType
{
    Combat,
    VampireBite,
    BarFight,
    Swoon,
    Heal,
    Convert,
    FleeFrom,
    HarvestAnimal,
    Track,
    DepositResources
}
```

---

# 8. 🎬 Extend `AgentAction`

Add:

```csharp
track,
deposit
```

Updated enum:

```csharp
public enum AgentAction
{
    idle,
    walking,
    attack,
    bite,
    barFight,
    harvest,
    heal,
    swoon,
    convert,
    purchase,
    chat,
    laugh,
    debate,
    sleep,
    track,
    deposit
}
```

Remember to add the matching animation keys to `AnimationLookup`.

Temporary animation mappings are acceptable if dedicated tracking and depositing animations do not exist yet.

---

# 9. ✅ Extend `ConditionType`

Add:

```csharp
hasTrackableAnimal,
animalTracked,
animalDead,
carryingResources
```

Updated enum:

```csharp
public enum ConditionType
{
    noCondition,
    timeOfDay,
    haveAnimal,
    hasTrackableAnimal,
    animalTracked,
    animalDead,
    carryingResources
}
```

---

# 10. 🎯 Special-Case Tracking Target Selection

## Current Flow

```text
Select Interaction
    ↓
FindNearestValidTarget()
    ↓
Use Nearest Target
```

The problem:

```text
Hunter Tracks Buffalo To 70%
    ↓
Decision Tick
    ↓
Pig Happens To Be Closer
    ↓
Hunter Switches To Pig
```

Tracking therefore needs its own target-selection method.

---

## ☐ Change Target Selection In `SelectAction()`

Find:

```csharp
Agent target =
    FindNearestValidTarget(chosen);
```

Replace with:

```csharp
Agent target =
    chosen.type == InteractionType.Track
        ? FindBestTrackingTarget(chosen)
        : FindNearestValidTarget(chosen);
```

The relevant part of `SelectAction()` becomes:

```csharp
private void SelectAction(
    ScriptableAgent data)
{
    AgentInteraction chosen = null;
    int chosenIndex = -1;
    int bestPriority = int.MinValue;

    for (int i = 0;
         i < data.interactions.Count;
         i++)
    {
        AgentInteraction interaction =
            data.interactions[i];

        if (cooldownTimers[i] > 0f)
            continue;

        /*
         * Keep the existing condition check here.
         * Example:
         *
         * if (!AreConditionsMet(interaction))
         *     continue;
         */

        if (Random.value >
            interaction.chance)
        {
            continue;
        }

        if (interaction.priority >
            bestPriority)
        {
            bestPriority =
                interaction.priority;

            chosen = interaction;
            chosenIndex = i;
        }
    }

    if (chosen == null)
        return;

    Agent target =
        chosen.type == InteractionType.Track
            ? FindBestTrackingTarget(chosen)
            : FindNearestValidTarget(chosen);

    if (target == null)
        return;

    activeInteraction = chosen;
    activeInteractionIndex = chosenIndex;
    activeTarget = target;

    PursueActivePlan();
}
```

---

# 11. 🦌 Add `FindBestTrackingTarget()`

This method:

```text
Checks Current Tracking Target
    ↓
Scores Current Target Without Penalty
    ↓
Scores Other Animals With Switch Penalty
    ↓
Chooses Best Animal
    ↓
Stores Winner In currentTarget[Track]
```

Add to `ActionManager`:

```csharp
private Agent FindBestTrackingTarget(
    AgentInteraction interaction)
{
    Agent currentTrackTarget =
        agent.GetCurrentTarget(
            InteractionType.Track);

    if (currentTrackTarget != null &&
        (currentTrackTarget.isDead ||
         currentTrackTarget.IsLeavingTown ||
         !currentTrackTarget.HasAnyTag(
             interaction.targetTags)))
    {
        agent.ClearCurrentTarget(
            InteractionType.Track);

        currentTrackTarget = null;
    }

    Agent bestTarget =
        currentTrackTarget;

    float bestScore =
        currentTrackTarget != null
            ? CalculateTrackingScore(
                currentTrackTarget,
                applySwitchPenalty: false)
            : float.MinValue;

    var allAgents =
        GameManager.Instance?.AllAgents;

    if (allAgents == null)
        return bestTarget;

    foreach (var candidates in allAgents)
    {
        foreach (Agent candidate in
                 candidates.Value)
        {
            if (candidate == null ||
                candidate == agent ||
                candidate.isDead ||
                candidate.IsLeavingTown)
            {
                continue;
            }

            if (!candidate.HasAnyTag(
                    interaction.targetTags))
            {
                continue;
            }

            ScriptableAgent candidateData =
                AgentData.Instance?.GetAgent(
                    candidate.type);

            if (candidateData == null)
                continue;

            float trackingProgress =
                candidate.GetSocialProgress(
                    InteractionType.Track);

            if (trackingProgress >=
                candidateData.trackingRequired)
            {
                continue;
            }

            bool isDifferentTarget =
                currentTrackTarget != null &&
                candidate != currentTrackTarget;

            float score =
                CalculateTrackingScore(
                    candidate,
                    applySwitchPenalty:
                        isDifferentTarget);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }
    }

    if (bestTarget != null)
    {
        agent.SetCurrentTarget(
            InteractionType.Track,
            bestTarget);
    }

    return bestTarget;
}
```

---

# 12. 📊 Add `CalculateTrackingScore()`

The initial score formula is:

```text
Harvest Value
    -
Distance
    -
Switch Penalty
```

Add to `ActionManager`:

```csharp
private float CalculateTrackingScore(
    Agent target,
    bool applySwitchPenalty)
{
    const float SwitchPenalty = 25f;

    if (target == null)
        return float.MinValue;

    float score = 0f;

    ScriptableAgent data =
        AgentData.Instance?.GetAgent(
            target.type);

    if (data != null &&
        data.harvestDrops != null)
    {
        foreach (InventoryItem drop in
                 data.harvestDrops)
        {
            if (drop == null)
                continue;

            score += drop.amount;
        }
    }

    score -= Vector3.Distance(
        agent.transform.position,
        target.transform.position);

    if (applySwitchPenalty)
        score -= SwitchPenalty;

    return score;
}
```

The current animal receives no switch penalty.

Alternative animals receive:

```csharp
-25f
```

The hunter therefore stays committed unless another animal is significantly more valuable or more convenient.

---

# 13. ⚙️ Add `TrackAction`

`ActionManager` has already:

```text
Chosen The Interaction
Chosen The Animal
Moved Into Range
Proposed AgentAction.track
```

`TrackAction` only needs to:

```text
Increase Tracking Progress
    ↓
Release AgentActionState
```

Add to `SocialActions.cs`:

```csharp
public class TrackAction :
    ISocialAction
{
    public void Execute(
        Agent self,
        Agent target,
        AgentInteraction interaction)
    {
        if (self == null ||
            target == null ||
            target.isDead)
        {
            self?.Visuals.Release();
            return;
        }

        float progressAmount =
            interaction.value > 0f
                ? interaction.value
                : 1f;

        target.AddSocialProgress(
            InteractionType.Track,
            progressAmount);

        Debug.Log(
            $"{self.type} tracked " +
            $"{target.type}. Progress: " +
            $"{target.GetSocialProgress(InteractionType.Track)}"
        );

        // This tracking interaction is complete.
        self.Visuals.Release();
    }
}
```

---

# 14. 🧾 Register `TrackAction`

## ☐ Add To `actionForInteraction`

```csharp
{
    InteractionType.Track,
    AgentAction.track
},
```

---

## ☐ Add To `priorityForInteraction`

```csharp
{
    InteractionType.Track,
    (int)PriorityTier.Social
},
```

---

## ☐ Add To `registry`

```csharp
{
    InteractionType.Track,
    new TrackAction()
},
```

---

## Updated Registration Dictionaries

```csharp
private static readonly
Dictionary<InteractionType, AgentAction>
actionForInteraction = new()
{
    {
        InteractionType.Combat,
        AgentAction.attack
    },
    {
        InteractionType.VampireBite,
        AgentAction.bite
    },
    {
        InteractionType.BarFight,
        AgentAction.barFight
    },
    {
        InteractionType.HarvestAnimal,
        AgentAction.harvest
    },
    {
        InteractionType.Heal,
        AgentAction.heal
    },
    {
        InteractionType.Swoon,
        AgentAction.swoon
    },
    {
        InteractionType.Convert,
        AgentAction.convert
    },
    {
        InteractionType.Track,
        AgentAction.track
    },
    {
        InteractionType.DepositResources,
        AgentAction.deposit
    }
};
```

```csharp
private static readonly
Dictionary<InteractionType, int>
priorityForInteraction = new()
{
    {
        InteractionType.Combat,
        (int)PriorityTier.Combat
    },
    {
        InteractionType.VampireBite,
        (int)PriorityTier.Combat
    },
    {
        InteractionType.BarFight,
        (int)PriorityTier.Combat
    },
    {
        InteractionType.HarvestAnimal,
        (int)PriorityTier.Combat
    },
    {
        InteractionType.Heal,
        (int)PriorityTier.Social
    },
    {
        InteractionType.Swoon,
        (int)PriorityTier.Social
    },
    {
        InteractionType.Convert,
        (int)PriorityTier.Social
    },
    {
        InteractionType.Track,
        (int)PriorityTier.Social
    },
    {
        InteractionType.DepositResources,
        (int)PriorityTier.Social
    }
};
```

```csharp
private static readonly
Dictionary<InteractionType, ISocialAction>
registry = new()
{
    {
        InteractionType.Combat,
        new CombatAction()
    },
    {
        InteractionType.VampireBite,
        new VampireBiteAction()
    },
    {
        InteractionType.BarFight,
        new BarFightAction()
    },
    {
        InteractionType.Heal,
        new HealAction()
    },
    {
        InteractionType.Swoon,
        new SwoonAction()
    },
    {
        InteractionType.Convert,
        new ConvertAction()
    },
    {
        InteractionType.FleeFrom,
        new FleeAction()
    },
    {
        InteractionType.HarvestAnimal,
        new HarvestAnimalAction()
    },
    {
        InteractionType.Track,
        new TrackAction()
    },
    {
        InteractionType.DepositResources,
        new DepositResourcesAction()
    }
};
```

---

# 15. 🔁 Final Tracking Cycle

```text
Track Interaction Becomes Available
    ↓
ActionManager Calls FindBestTrackingTarget()
    ↓
Current Animal And Alternatives Are Scored
    ↓
Switch Penalty Is Applied To Alternatives
    ↓
Winner Is Stored In currentTarget[Track]
    ↓
Existing ActionManager Movement Runs
    ↓
Hunter Reaches Animal
    ↓
AgentAction.track Is Proposed
    ↓
AgentActionState Accepts Proposal
    ↓
TrackAction Executes
    ↓
Animal Track Progress Increases
    ↓
TrackAction Releases AgentActionState
    ↓
ActionManager Clears The Plan
    ↓
Next Decision Runs
    ↓
FindBestTrackingTarget() Runs Again
```

This allows the hunter to reconsider all animals after every completed tracking action.

---

# 16. ✅ Add Hunting Conditions

## ☐ `hasTrackableAnimal`

True when at least one valid living animal has the required target tag and has not reached its tracking threshold.

```text
Matching Animal Exists
    ↓
Animal Is Alive
    ↓
Animal Is Not Fully Tracked
```

---

## ☐ `animalTracked`

True when the hunter’s current tracking target has reached its required progress.

```csharp
private bool IsCurrentAnimalTracked()
{
    Agent target =
        agent.GetCurrentTarget(
            InteractionType.Track);

    if (target == null ||
        target.isDead)
    {
        return false;
    }

    ScriptableAgent data =
        AgentData.Instance?.GetAgent(
            target.type);

    if (data == null)
        return false;

    return target.GetSocialProgress(
               InteractionType.Track)
           >= data.trackingRequired;
}
```

---

## ☐ `animalDead`

True when a valid dead, unharvested animal exists.

```text
Animal Is Dead
    ↓
Animal Has Not Been Harvested
```

---

## ☐ `carryingResources`

```csharp
private bool IsCarryingResources()
{
    return agent.HasInventoryItems();
}
```

---

# 17. 🧠 Configure Hunter Interactions

The hunter’s `ScriptableAgent` should contain these interactions.

---

## 🏕️ DepositResources

```text
Condition:
    CarryingResources

Priority:
    Highest Hunting Priority
```

The hunter should return resources before beginning another hunt.

---

## 🔪 HarvestAnimal

```text
Target Tags:
    Prey

Condition:
    AnimalDead

Priority:
    Higher Than Combat And Track
```

---

## ⚔️ Combat

```text
Target Tags:
    Prey

Condition:
    AnimalTracked

Priority:
    Higher Than Track

Value:
    Damage Per Attack
```

Use normal damage rather than instant death so hunting takes time.

---

## 🦌 Track

```text
Target Tags:
    Prey

Condition:
    HasTrackableAnimal

Priority:
    Lowest Hunting Priority

Chance:
    1

Value:
    Tracking Progress Per Completed Action

Cooldown:
    Delay Before Another Tracking Action
```

Possible initial values:

```text
Track Value: 5

Pig Required Progress: 20

Coyote Required Progress: 50

Buffalo Required Progress: 100
```

---

# 18. 🔪 Replace `HarvestAnimalAction`

The current implementation:

```csharp
AgentDeath.HurtAgent(
    target,
    deathType.bleeding,
    9999f);
```

kills the animal.

The new harvesting action should only operate on an animal that is already dead.

Replace the class with:

```csharp
public class HarvestAnimalAction :
    ISocialAction
{
    public void Execute(
        Agent self,
        Agent target,
        AgentInteraction interaction)
    {
        if (self == null ||
            target == null)
        {
            self?.Visuals.Release();
            return;
        }

        if (!target.isDead)
        {
            Debug.LogWarning(
                $"{self.type} tried to harvest " +
                $"{target.type}, but it is alive."
            );

            self.Visuals.Release();
            return;
        }

        if (target.harvested)
        {
            self.Visuals.Release();
            return;
        }

        ScriptableAgent animalData =
            AgentData.Instance?.GetAgent(
                target.type);

        if (animalData == null)
        {
            self.Visuals.Release();
            return;
        }

        if (animalData.harvestDrops != null)
        {
            foreach (InventoryItem drop in
                     animalData.harvestDrops)
            {
                if (drop == null ||
                    drop.amount <= 0)
                {
                    continue;
                }

                self.AddToInventory(
                    drop.type,
                    drop.amount);
            }
        }

        target.harvested = true;

        if (self.GetCurrentTarget(
                InteractionType.Track)
            == target)
        {
            self.ClearCurrentTarget(
                InteractionType.Track);
        }

        Debug.Log(
            $"{self.type} harvested " +
            $"{target.type}."
        );

        self.Visuals.Release();
    }
}
```

---

# 19. 🦴 Allow `HarvestAnimal` To Target Corpses

The existing target search excludes dead agents.

Update `FindNearestValidTarget()` so harvesting can find corpses.

Replace:

```csharp
if (candidate == null ||
    candidate == agent ||
    candidate.isDead ||
    candidate.IsLeavingTown)
{
    continue;
}
```

With:

```csharp
if (candidate == null ||
    candidate == agent ||
    candidate.IsLeavingTown)
{
    continue;
}

bool needsDeadTarget =
    interaction.type ==
    InteractionType.HarvestAnimal;

if (needsDeadTarget)
{
    if (!candidate.isDead ||
        candidate.harvested)
    {
        continue;
    }
}
else if (candidate.isDead)
{
    continue;
}
```

The full method becomes:

```csharp
private Agent FindNearestValidTarget(
    AgentInteraction interaction)
{
    var allAgents =
        GameManager.Instance?.AllAgents;

    if (allAgents == null)
        return null;

    Agent bestTarget = null;
    float bestDist = float.MaxValue;

    Vector3 selfPos =
        agent.transform.position;

    foreach (var candidates in allAgents)
    {
        foreach (Agent candidate in
                 candidates.Value)
        {
            if (candidate == null ||
                candidate == agent ||
                candidate.IsLeavingTown)
            {
                continue;
            }

            bool needsDeadTarget =
                interaction.type ==
                InteractionType.HarvestAnimal;

            if (needsDeadTarget)
            {
                if (!candidate.isDead ||
                    candidate.harvested)
                {
                    continue;
                }
            }
            else if (candidate.isDead)
            {
                continue;
            }

            if (!candidate.HasAnyTag(
                    interaction.targetTags))
            {
                continue;
            }

            float dist =
                Vector3.Distance(
                    selfPos,
                    candidate.transform.position);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestTarget = candidate;
            }
        }
    }

    return bestTarget;
}
```

---

# 20. 🦴 Allow Active Harvest Plans To Keep Dead Targets

Update `ValidateActiveTarget()`:

```csharp
private bool ValidateActiveTarget()
{
    if (activeTarget == null ||
        activeInteraction == null)
    {
        return false;
    }

    if (activeTarget.IsLeavingTown)
        return false;

    if (activeInteraction.type ==
        InteractionType.HarvestAnimal)
    {
        if (!activeTarget.isDead)
            return false;

        if (activeTarget.harvested)
            return false;
    }
    else if (activeTarget.isDead)
    {
        return false;
    }

    if (!activeTarget.HasAnyTag(
            activeInteraction.targetTags))
    {
        return false;
    }

    return true;
}
```

---

# 21. ⚙️ Allow `SocialActions.Process()` To Harvest Corpses

The existing method rejects all dead targets.

Replace:

```csharp
public static void Process(
    Agent self,
    Agent target,
    AgentInteraction interaction)
{
    if (self == null ||
        target == null ||
        target.isDead)
    {
        return;
    }

    if (registry.TryGetValue(
            interaction.type,
            out var handler))
    {
        handler.Execute(
            self,
            target,
            interaction);
    }
    else
    {
        Debug.LogWarning(
            $"SocialActions: no handler " +
            $"registered for " +
            $"{interaction.type}"
        );
    }
}
```

With:

```csharp
public static void Process(
    Agent self,
    Agent target,
    AgentInteraction interaction)
{
    if (self == null ||
        target == null ||
        interaction == null)
    {
        return;
    }

    bool allowsDeadTarget =
        interaction.type ==
        InteractionType.HarvestAnimal;

    if (target.isDead &&
        !allowsDeadTarget)
    {
        return;
    }

    if (registry.TryGetValue(
            interaction.type,
            out ISocialAction handler))
    {
        handler.Execute(
            self,
            target,
            interaction);
    }
    else
    {
        Debug.LogWarning(
            $"SocialActions: no handler " +
            $"registered for " +
            $"{interaction.type}"
        );
    }
}
```

---

# 22. 🏕️ Add Resource Deposit Method

Depositing converts the hunter’s physical inventory into abstract town resources.

```text
Food
    → Town Food

Fur
    → Town Wealth
```

Add a method on the hunter or a resource service:

```csharp
public void DepositHuntingResources()
{
    if (inventory == null ||
        inventory.Count == 0)
    {
        return;
    }

    int foodToDeposit = 0;
    int wealthToDeposit = 0;

    foreach (InventoryItem item in inventory)
    {
        if (item == null ||
            item.amount <= 0)
        {
            continue;
        }

        switch (item.type)
        {
            case ObjectType.Food:
                foodToDeposit +=
                    item.amount;
                break;

            case ObjectType.Fur:
                wealthToDeposit +=
                    item.amount;
                break;
        }
    }

    /*
     * Replace these two lines with the
     * actual town-stat references.
     *
     * Example:
     *
     * stat.tot_food += foodToDeposit;
     * stat.tot_Wealth += wealthToDeposit;
     */

    inventory.Clear();

    Debug.Log(
        $"{type} deposited " +
        $"{foodToDeposit} food and " +
        $"{wealthToDeposit} wealth."
    );
}
```

---

# 23. 🏕️ Add `DepositResourcesAction`

```csharp
public class DepositResourcesAction :
    ISocialAction
{
    public void Execute(
        Agent self,
        Agent target,
        AgentInteraction interaction)
    {
        if (self == null)
            return;

        self.DepositHuntingResources();

        self.Visuals.Release();
    }
}
```

This action should only execute after the hunter returns to the hunter hut.

The existing workplace pathing in `AgentWorkSchedule` can handle returning to the workplace.

When:

```text
Hunter Has Inventory
    ↓
Hunter Reaches Workplace
```

execute:

```csharp
self.DepositHuntingResources();
```

A separate building-targeting system is not required for the first version.

---

# 24. ☠️ Drop Inventory When Hunter Dies

Add:

```csharp
public List<InventoryItem>
    TakeInventoryForDrop()
{
    List<InventoryItem> droppedItems =
        new List<InventoryItem>();

    if (inventory == null)
        return droppedItems;

    foreach (InventoryItem item in inventory)
    {
        if (item == null ||
            item.amount <= 0)
        {
            continue;
        }

        droppedItems.Add(
            new InventoryItem(
                item.type,
                item.amount));
    }

    inventory.Clear();

    return droppedItems;
}
```

Call this from the death system:

```csharp
List<InventoryItem> droppedItems =
    dyingAgent.TakeInventoryForDrop();
```

The future loot object stores `droppedItems`.

Desired result:

```text
Hunter Dies
    ↓
Food And Fur Drop
    ↓
Resources Remain In World
    ↓
Another Agent Can Recover Them Later
```

---

# 25. 🧪 Testing Order

## ☐ Test Inventory

```csharp
agent.AddToInventory(
    ObjectType.Food,
    10);

agent.AddToInventory(
    ObjectType.Food,
    5);
```

Expected Inspector result:

```text
Food: 15
```

---

## ☐ Test Progress

```csharp
animal.AddSocialProgress(
    InteractionType.Track,
    5f);
```

Expected:

```text
Track Progress: 5
```

---

## ☐ Test Current Target

```csharp
hunter.SetCurrentTarget(
    InteractionType.Track,
    buffalo);
```

Expected:

```text
currentTarget[Track] → Buffalo
```

---

## ☐ Test Target Selection

Place:

```text
One Nearby Pig

One Distant Buffalo
```

Verify both animals receive a score.

Verify the animal with the highest final score is selected.

---

## ☐ Test Persistence

Allow the hunter to begin tracking a buffalo.

Verify:

```csharp
hunter.GetCurrentTarget(
    InteractionType.Track)
```

continues returning the buffalo.

Alternative animals should receive the switch penalty.

---

## ☐ Test Reconsideration

After one Track action:

```text
Progress Increases
    ↓
TrackAction Releases
    ↓
ActionManager Thinks Again
    ↓
FindBestTrackingTarget Runs Again
```

Verify the hunter can keep the current target or switch to a significantly better one.

---

## ☐ Test Tracking Threshold

When:

```csharp
animal.GetSocialProgress(
    InteractionType.Track)
>=
animalData.trackingRequired
```

expected result:

```text
Track Stops Being Available
    ↓
Combat Becomes Available
```

---

## ☐ Test Slow Combat

Use normal combat damage.

Expected:

```text
Attack
    ↓
Cooldown
    ↓
Attack
    ↓
Cooldown
    ↓
Animal Dies
```

---

## ☐ Test Harvesting

Expected:

```text
Animal Dies
    ↓
HarvestAnimal Becomes Available
    ↓
Hunter Reaches Corpse
    ↓
Harvest Drops Enter Inventory
    ↓
Animal.harvested = true
    ↓
Current Tracking Target Clears
```

---

## ☐ Test Deposit

Expected:

```text
Hunter Carries Food And Fur
    ↓
Hunter Returns To Hunter Hut
    ↓
Food Becomes Town Food
    ↓
Fur Becomes Town Wealth
    ↓
Inventory Clears
```

---

## ☐ Test Death

Expected:

```text
Hunter Carries Resources
    ↓
Bear Kills Hunter
    ↓
Inventory Leaves Hunter
    ↓
Resources Remain Recoverable
```

---

# 🌟 First Complete Success Test

```text
Hunter Leaves Hut
    ↓
FindBestTrackingTarget Selects Buffalo
    ↓
currentTarget[Track] Stores Buffalo
    ↓
Hunter Moves Into Range
    ↓
TrackAction Adds Progress
    ↓
TrackAction Releases AgentActionState
    ↓
ActionManager Thinks Again
    ↓
Buffalo Is Re-evaluated Without Switch Penalty
    ↓
Other Animals Are Evaluated With Switch Penalty
    ↓
Hunter Continues Tracking Buffalo
    ↓
Night Falls
    ↓
Hunter Sleeps
    ↓
Tracking Progress Remains On Buffalo
    ↓
Hunter Continues Next Day
    ↓
Buffalo Reaches Tracking Threshold
    ↓
Combat Unlocks
    ↓
Hunter Slowly Kills Buffalo
    ↓
HarvestAnimal Unlocks
    ↓
Food And Fur Enter Inventory
    ↓
Hunter Returns To Hut
    ↓
Food Becomes Town Food
    ↓
Fur Becomes Town Wealth
    ↓
Inventory Clears
```

---

# 🌟 Target-Switching Story Test

```text
Hunter Tracks Buffalo To 60%
    ↓
More Valuable Animal Appears
    ↓
Hunter Completes Current Track Action
    ↓
ActionState Releases
    ↓
FindBestTrackingTarget Runs Again
    ↓
New Animal Receives Switch Penalty
    ↓
New Animal Still Scores Higher
    ↓
Hunter Switches Targets
```

---

# 🌟 Death Story Test

```text
Hunter Tracks Buffalo
    ↓
Hunter Kills Buffalo
    ↓
Hunter Harvests Food And Fur
    ↓
Hunter Starts Returning Home
    ↓
Bear Kills Hunter
    ↓
Inventory Drops
    ↓
Town Receives Nothing
```

If these stories happen naturally through interactions, conditions, target selection, progress, and existing movement, the hunting system is working correctly.


The main changes are now intentionally minimal: tracking uses the existing target movement, ActionManager only receives smarter target selection, and TrackAction only adds progress and releases itself.
