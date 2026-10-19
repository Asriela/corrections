# 🦌 BoomTown Hunting System Implementation

## 🎯 Goal

Create a hunting loop that naturally emerges through interactions and conditions.

```text
Hunter Hut
    ↓
Choose Best Animal
    ↓
Choose Tracking Waypoint Near Animal
    ↓
Walk To Tracking Waypoint
    ↓
Increase Tracking Progress
    ↓
Release Current Action
    ↓
Reconsider Best Animal
    ↓
Repeat Until Animal Is Tracked
    ↓
Combat
    ↓
Animal Dies
    ↓
Harvest Animal
    ↓
Carry Resources
    ↓
Return To Hunter Hut
    ↓
Deposit Resources
    ↓
Repeat
```

---

# ⚡ Architecture Summary

```text
📜 ScriptableAgent
    Defines available interactions and harvest data

🎯 ActionManager
    Chooses an interaction
    Chooses the best target
    Chooses the destination
    Moves the agent
    Proposes an AgentAction

🎬 AgentActionState
    Accepts or rejects the proposed action
    Handles priority, animation, locks, and interruption

⚙️ SocialActions
    Executes the effect after the destination is reached

✅ Conditions
    Determine which interactions are available

📈 SocialProgress
    Stores progress on the target receiving the interaction

🎯 CurrentTarget
    Stores who an actor is currently targeting for each interaction
```

The core hunting cycle is:

```text
Decision
    ↓
Target Selection
    ↓
Movement
    ↓
Action Proposal
    ↓
Effect
    ↓
Release
    ↓
Decision
```

---

# 1. 📦 Add Resource Data

## ☐ Add `ObjectType`

Place this somewhere shared and accessible by both agents and town-resource code.

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

    public InventoryItem(ObjectType type, int amount)
    {
        this.type = type;
        this.amount = amount;
    }
}
```

The constructor is important because harvested resources should be copied into the hunter’s inventory rather than referencing the asset’s original list entries.

---

# 2. 🎒 Add Inventory To `Agent`

Add:

```csharp
[SerializeField]
public List<InventoryItem> inventory = new List<InventoryItem>();
```

---

## ☐ Add Inventory Helper Methods To `Agent`

These helpers allow the Inspector-friendly list to behave like a dictionary.

```csharp
public bool HasInventoryItems()
{
    if (inventory == null)
        return false;

    for (int i = 0; i < inventory.Count; i++)
    {
        InventoryItem item = inventory[i];

        if (item != null && item.amount > 0)
            return true;
    }

    return false;
}
```

```csharp
public int GetInventoryAmount(ObjectType type)
{
    if (inventory == null)
        return 0;

    int total = 0;

    for (int i = 0; i < inventory.Count; i++)
    {
        InventoryItem item = inventory[i];

        if (item != null && item.type == type)
            total += item.amount;
    }

    return total;
}
```

```csharp
public void AddToInventory(ObjectType type, int amount)
{
    if (amount <= 0)
        return;

    if (inventory == null)
        inventory = new List<InventoryItem>();

    for (int i = 0; i < inventory.Count; i++)
    {
        InventoryItem existing = inventory[i];

        if (existing != null && existing.type == type)
        {
            existing.amount += amount;
            return;
        }
    }

    inventory.Add(new InventoryItem(type, amount));
}
```

```csharp
public void ClearInventory()
{
    inventory?.Clear();
}
```

---

# 3. 🐃 Add Harvest Data To `ScriptableAgent`

Add:

```csharp
[Header("Hunting")]
[SerializeField]
public List<InventoryItem> harvestDrops = new List<InventoryItem>();

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

Legendary Bear
    Tracking Required: 500
    Food x40
    Fur x100
```

---

# 4. 📈 Add Progress Storage To `Agent`

Progress lives on the target receiving the interaction.

For example:

```text
Buffalo
    Track Progress: 63

Villager
    Convert Progress: 25
```

Add to `Agent`:

```csharp
public readonly Dictionary<InteractionType, float> socialProgress =
    new Dictionary<InteractionType, float>();
```

---

## ☐ Add Progress Helpers To `Agent`

```csharp
public float GetSocialProgress(InteractionType interactionType)
{
    return socialProgress.TryGetValue(interactionType, out float value)
        ? value
        : 0f;
}
```

```csharp
public void AddSocialProgress(
    InteractionType interactionType,
    float amount)
{
    if (amount <= 0f)
        return;

    socialProgress[interactionType] =
        GetSocialProgress(interactionType) + amount;
}
```

```csharp
public void SetSocialProgress(
    InteractionType interactionType,
    float amount)
{
    socialProgress[interactionType] = Mathf.Max(0f, amount);
}
```

```csharp
public void ClearSocialProgress(
    InteractionType interactionType)
{
    socialProgress.Remove(interactionType);
}
```

---

# 5. 🎯 Add Current Targets To `Agent`

The actor stores a separate target for each interaction type.

Examples:

```text
Hunter
    CurrentTarget[Track] → Buffalo

Priest
    CurrentTarget[Convert] → Villager
```

Add:

```csharp
public readonly Dictionary<InteractionType, Agent> currentTarget =
    new Dictionary<InteractionType, Agent>();
```

---

## ☐ Add Current-Target Helpers To `Agent`

```csharp
public Agent GetCurrentTarget(InteractionType interactionType)
{
    if (!currentTarget.TryGetValue(interactionType, out Agent target))
        return null;

    if (target == null)
    {
        currentTarget.Remove(interactionType);
        return null;
    }

    return target;
}
```

```csharp
public void SetCurrentTarget(
    InteractionType interactionType,
    Agent target)
{
    if (target == null)
    {
        currentTarget.Remove(interactionType);
        return;
    }

    currentTarget[interactionType] = target;
}
```

```csharp
public void ClearCurrentTarget(
    InteractionType interactionType)
{
    currentTarget.Remove(interactionType);
}
```

---

# 6. 🦴 Add Harvest State To `Agent`

Add:

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
Dead
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

The enum should resemble:

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

The enum should resemble:

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

Remember to add animation keys for `track` and `deposit` to `AnimationLookup`.

If no dedicated animation exists yet, map them temporarily to an existing animation rather than allowing a missing clip to produce a zero-duration lock.

---

# 9. ✅ Extend `ConditionType`

Add:

```csharp
hasTrackableAnimal,
animalTracked,
animalDead,
carryingResources
```

Example:

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

# 10. 🎯 Update `ActionManager` Target Selection

Tracking must reconsider all eligible animals every time a tracking interaction is selected.

The current target receives no switching penalty.

Alternative animals receive a switching penalty.

The score is:

```text
Harvest Value
    -
Distance
    -
Switch Penalty
```

---

## ☐ Add Tracking Constants To `ActionManager`

Place these near the other fields:

```csharp
private const float TrackingTargetSwitchPenalty = 25f;
private const int TrackingWaypointRadiusTiles = 3;
```

---

## ☐ Add Tracking-Destination State To `ActionManager`

Tracking needs a destination that is separate from the animal’s current position.

Add:

```csharp
private bool hasTrackingDestination;
private Vector2Int trackingDestinationTile;
private Agent trackingDestinationTarget;
```

This destination lasts for one tracking cycle.

Once the hunter reaches it and `TrackAction` executes, `ClearPlan()` removes it.

---

## ☐ Replace Target Selection Inside `SelectAction()`

Find:

```csharp
Agent target = FindNearestValidTarget(chosen);
```

Replace it with:

```csharp
Agent target;

switch (chosen.type)
{
    case InteractionType.Track:
        target = FindBestTrackingTarget(chosen);
        break;

    case InteractionType.DepositResources:
        target = CanDepositResources()
            ? agent
            : null;
        break;

    default:
        target = FindNearestValidTarget(chosen);
        break;
}
```

The full relevant portion becomes:

```csharp
private void SelectAction(ScriptableAgent data)
{
    AgentInteraction chosen = null;
    int chosenIndex = -1;
    int bestPriority = int.MinValue;

    for (int i = 0; i < data.interactions.Count; i++)
    {
        AgentInteraction interaction = data.interactions[i];

        if (cooldownTimers[i] > 0f)
            continue;

        if (!AreConditionsMet(interaction))
            continue;

        if (Random.value > interaction.chance)
            continue;

        if (interaction.priority > bestPriority)
        {
            bestPriority = interaction.priority;
            chosen = interaction;
            chosenIndex = i;
        }
    }

    if (chosen == null)
        return;

    Agent target;

    switch (chosen.type)
    {
        case InteractionType.Track:
            target = FindBestTrackingTarget(chosen);
            break;

        case InteractionType.DepositResources:
            target = CanDepositResources()
                ? agent
                : null;
            break;

        default:
            target = FindNearestValidTarget(chosen);
            break;
    }

    if (target == null)
        return;

    activeInteraction = chosen;
    activeInteractionIndex = chosenIndex;
    activeTarget = target;

    ResetInteractionDestination();

    PursueActivePlan();
}
```

`AreConditionsMet()` is included later in this document.

---

## ☐ Add `FindBestTrackingTarget()`

Add this method to `ActionManager`:

```csharp
private Agent FindBestTrackingTarget(
    AgentInteraction interaction)
{
    Agent currentTrackTarget =
        agent.GetCurrentTarget(InteractionType.Track);

    if (!IsValidTrackingTarget(currentTrackTarget, interaction))
    {
        agent.ClearCurrentTarget(InteractionType.Track);
        currentTrackTarget = null;
    }

    Agent bestTarget = currentTrackTarget;

    float bestScore = currentTrackTarget != null
        ? CalculateTrackingScore(
            currentTrackTarget,
            applySwitchPenalty: false)
        : float.MinValue;

    var allAgents = GameManager.Instance?.AllAgents;

    if (allAgents == null)
        return bestTarget;

    foreach (var candidates in allAgents)
    {
        foreach (Agent candidate in candidates.Value)
        {
            if (!IsValidTrackingTarget(candidate, interaction))
                continue;

            bool isSwitching =
                currentTrackTarget != null &&
                candidate != currentTrackTarget;

            float score = CalculateTrackingScore(
                candidate,
                applySwitchPenalty: isSwitching);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = candidate;
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

## ☐ Add `IsValidTrackingTarget()`

```csharp
private bool IsValidTrackingTarget(
    Agent candidate,
    AgentInteraction interaction)
{
    if (candidate == null)
        return false;

    if (candidate == agent)
        return false;

    if (candidate.isDead)
        return false;

    if (candidate.IsLeavingTown)
        return false;

    if (candidate.harvested)
        return false;

    if (!candidate.HasAnyTag(interaction.targetTags))
        return false;

    ScriptableAgent candidateData =
        AgentData.Instance?.GetAgent(candidate.type);

    if (candidateData == null)
        return false;

    float currentProgress =
        candidate.GetSocialProgress(InteractionType.Track);

    if (currentProgress >= candidateData.trackingRequired)
        return false;

    return true;
}
```

A fully tracked animal is excluded from another `Track` selection because the combat interaction should become available instead.

---

## ☐ Add `CalculateTrackingScore()`

```csharp
private float CalculateTrackingScore(
    Agent target,
    bool applySwitchPenalty)
{
    if (target == null)
        return float.MinValue;

    ScriptableAgent data =
        AgentData.Instance?.GetAgent(target.type);

    if (data == null)
        return float.MinValue;

    float harvestValue = 0f;

    if (data.harvestDrops != null)
    {
        foreach (InventoryItem drop in data.harvestDrops)
        {
            if (drop == null || drop.amount <= 0)
                continue;

            switch (drop.type)
            {
                case ObjectType.Food:
                    harvestValue += drop.amount;
                    break;

                case ObjectType.Fur:
                    harvestValue += drop.amount;
                    break;
            }
        }
    }

    float distance = Vector3.Distance(
        agent.transform.position,
        target.transform.position);

    float score = harvestValue - distance;

    if (applySwitchPenalty)
        score -= TrackingTargetSwitchPenalty;

    return score;
}
```

For now, one unit of food and one unit of fur have equal hunting score.

Change the weights later if town food and town wealth have different strategic values.

---

# 11. 🧭 Add Tracking-Waypoint Movement To `ActionManager`

Tracking should not execute when the hunter merely reaches the animal.

The hunter should:

```text
Choose a point near the animal
    ↓
Walk to that point
    ↓
Gain tracking progress
    ↓
Release the action
```

---

## ☐ Replace `PursueActivePlan()`

Use this version:

```csharp
private void PursueActivePlan()
{
    if (!ValidateActiveTarget())
    {
        ClearPlan();
        return;
    }

    MoveTowardTarget(
        activeTarget,
        activeInteraction);

    if (!HasReachedInteractionDestination(
            activeTarget,
            activeInteraction))
    {
        return;
    }

    if (!SocialActions.TryGetAction(
            activeInteraction.type,
            out AgentAction action))
    {
        ClearPlan();
        return;
    }

    int priority =
        SocialActions.GetPriority(activeInteraction.type);

    AgentInteraction interactionForCallback =
        activeInteraction;

    var proposition = new Proposition(
        action,
        priority,
        PropositionType.OneShot,
        onInterrupted: () =>
            SocialActions.Cancel(
                agent,
                interactionForCallback)
    );

    if (!agent.Visuals.EvaluateProposition(proposition))
        return;

    SocialActions.Process(
        agent,
        activeTarget,
        activeInteraction);

    cooldownTimers[activeInteractionIndex] =
        activeInteraction.cooldown;

    ClearPlan();
}
```

---

## ☐ Replace `MoveTowardTarget()`

Add the special Track and Deposit cases at the beginning.

```csharp
private void MoveTowardTarget(
    Agent target,
    AgentInteraction interaction)
{
    if (interaction.type == InteractionType.Track)
    {
        MoveTowardTrackingDestination(target);
        return;
    }

    if (interaction.type == InteractionType.DepositResources)
    {
        MoveTowardWorkplace();
        return;
    }

    Vector3 selfPos = agent.transform.position;
    Vector3 targetPos = target.transform.position;

    agent.movementType = interaction.canEnterTargetBuilding
        ? MovementType.AllowTargetBuildingOnly
        : MovementType.AvoidAllBuildings;

    agent.roamInsideTarget = false;

    Vector2Int targetTileNow =
        AgentPathing.WorldToGrid(agent, targetPos);

    agent.targetTile = targetTileNow;

    if (interaction.canEnterTargetBuilding)
    {
        ApproachTileByPathfinding(
            targetTileNow,
            targetPos,
            interaction);
    }
    else
    {
        float yDiff =
            Mathf.Abs(selfPos.y - targetPos.y);

        float xDiff =
            Mathf.Abs(selfPos.x - targetPos.x);

        if (yDiff <= interaction.alignmentTolerance &&
            xDiff <= interaction.range)
        {
            agent.Visuals.UpdateFacingFromDirection(
                targetPos - selfPos);

            return;
        }

        float side =
            selfPos.x <= targetPos.x ? -1f : 1f;

        if (Mathf.Abs(selfPos.x - targetPos.x) < 0.001f)
            side = 1f;

        Vector3 desiredPosition = new Vector3(
            targetPos.x +
                side * interaction.standOffDistance,
            targetPos.y,
            selfPos.z
        );

        if (!AgentPathing.TryGetCurrentPathTarget(
                agent,
                out Vector3 currentPathTarget) ||
            (currentPathTarget - desiredPosition).sqrMagnitude >
                0.05f)
        {
            AgentMovement.SetPath(
                agent,
                new List<Vector3>
                {
                    desiredPosition
                });
        }
    }

    agent.Visuals.UpdateFacingFromDirection(
        targetPos - selfPos);
}
```

---

## ☐ Add `MoveTowardTrackingDestination()`

```csharp
private void MoveTowardTrackingDestination(
    Agent target)
{
    if (target == null)
        return;

    if (!hasTrackingDestination ||
        trackingDestinationTarget != target)
    {
        if (!TryChooseTrackingDestination(
                target,
                out trackingDestinationTile))
        {
            return;
        }

        hasTrackingDestination = true;
        trackingDestinationTarget = target;
    }

    agent.movementType =
        MovementType.AvoidAllBuildings;

    agent.roamInsideTarget = false;
    agent.targetTile = trackingDestinationTile;

    Vector2Int currentDestinationTile =
        agent.currentPath.Count > 0
            ? AgentPathing.WorldToGrid(
                agent,
                agent.currentPath[
                    agent.currentPath.Count - 1])
            : trackingDestinationTile;

    bool needsNewPath =
        agent.currentPath.Count == 0 ||
        currentDestinationTile != trackingDestinationTile;

    if (!needsNewPath)
        return;

    if (AgentPathing.TryFindPathToTile(
            agent,
            trackingDestinationTile,
            out List<Vector3> worldPath))
    {
        AgentMovement.SetPath(agent, worldPath);
    }
}
```

---

## ☐ Add `TryChooseTrackingDestination()`

This searches for a reachable tile around the animal.

```csharp
private bool TryChooseTrackingDestination(
    Agent target,
    out Vector2Int chosenTile)
{
    chosenTile = Vector2Int.zero;

    if (target == null)
        return false;

    Vector2Int targetTile =
        AgentPathing.WorldToGrid(
            agent,
            target.transform.position);

    List<Vector2Int> candidates =
        new List<Vector2Int>();

    for (int x = -TrackingWaypointRadiusTiles;
         x <= TrackingWaypointRadiusTiles;
         x++)
    {
        for (int y = -TrackingWaypointRadiusTiles;
             y <= TrackingWaypointRadiusTiles;
             y++)
        {
            if (x == 0 && y == 0)
                continue;

            Vector2Int offset =
                new Vector2Int(x, y);

            if (offset.sqrMagnitude >
                TrackingWaypointRadiusTiles *
                TrackingWaypointRadiusTiles)
            {
                continue;
            }

            candidates.Add(targetTile + offset);
        }
    }

    while (candidates.Count > 0)
    {
        int randomIndex =
            Random.Range(0, candidates.Count);

        Vector2Int candidate =
            candidates[randomIndex];

        candidates.RemoveAt(randomIndex);

        if (!AgentPathing.TryFindPathToTile(
                agent,
                candidate,
                out List<Vector3> ignoredPath))
        {
            continue;
        }

        chosenTile = candidate;
        return true;
    }

    return false;
}
```

This version verifies that the selected tile has a valid path before accepting it.

---

## ☐ Add `HasReachedInteractionDestination()`

```csharp
private bool HasReachedInteractionDestination(
    Agent target,
    AgentInteraction interaction)
{
    switch (interaction.type)
    {
        case InteractionType.Track:
            return HasReachedTrackingDestination();

        case InteractionType.DepositResources:
            return HasReachedWorkplace();

        default:
            return IsInRange(
                target,
                interaction.range);
    }
}
```

---

## ☐ Add `HasReachedTrackingDestination()`

```csharp
private bool HasReachedTrackingDestination()
{
    if (!hasTrackingDestination)
        return false;

    Vector2Int selfTile =
        AgentPathing.WorldToGrid(
            agent,
            agent.transform.position);

    return selfTile == trackingDestinationTile &&
           agent.currentPath.Count == 0;
}
```

---

## ☐ Reset Interaction Destinations

Add:

```csharp
private void ResetInteractionDestination()
{
    hasTrackingDestination = false;
    trackingDestinationTile = Vector2Int.zero;
    trackingDestinationTarget = null;
}
```

Update `ClearPlan()`:

```csharp
private void ClearPlan()
{
    activeInteraction = null;
    activeInteractionIndex = -1;
    activeTarget = null;

    ResetInteractionDestination();
}
```

---

# 12. 🏕️ Add Workplace Movement For Depositing

The existing interaction system expects an `Agent` target.

For `DepositResources`, use the hunter itself as the interaction target while ActionManager sends the hunter to `myWorkplace`.

---

## ☐ Add `CanDepositResources()`

```csharp
private bool CanDepositResources()
{
    return agent.myWorkplace != null &&
           agent.HasInventoryItems();
}
```

---

## ☐ Add `MoveTowardWorkplace()`

```csharp
private void MoveTowardWorkplace()
{
    if (agent.myWorkplace == null)
        return;

    Vector2Int workplaceTile =
        AgentPathing.WorldToGrid(
            agent,
            agent.myWorkplace.transform.position);

    if (!AgentPathing.TryGetFrontAccessTile(
            agent,
            workplaceTile,
            out Vector2Int frontTile,
            ignoreWalkability: true))
    {
        return;
    }

    agent.movementType =
        MovementType.AvoidAllBuildings;

    agent.roamInsideTarget = false;
    agent.targetTile = frontTile;

    Vector2Int currentDestinationTile =
        agent.currentPath.Count > 0
            ? AgentPathing.WorldToGrid(
                agent,
                agent.currentPath[
                    agent.currentPath.Count - 1])
            : frontTile;

    bool needsNewPath =
        agent.currentPath.Count == 0 ||
        currentDestinationTile != frontTile;

    if (!needsNewPath)
        return;

    if (AgentPathing.TryFindPathToTile(
            agent,
            frontTile,
            out List<Vector3> worldPath))
    {
        AgentMovement.SetPath(agent, worldPath);
    }
}
```

---

## ☐ Add `HasReachedWorkplace()`

```csharp
private bool HasReachedWorkplace()
{
    if (agent.myWorkplace == null)
        return false;

    Vector2Int workplaceTile =
        AgentPathing.WorldToGrid(
            agent,
            agent.myWorkplace.transform.position);

    if (!AgentPathing.TryGetFrontAccessTile(
            agent,
            workplaceTile,
            out Vector2Int frontTile,
            ignoreWalkability: true))
    {
        return false;
    }

    Vector2Int selfTile =
        AgentPathing.WorldToGrid(
            agent,
            agent.transform.position);

    return selfTile == frontTile &&
           agent.currentPath.Count == 0;
}
```

---

# 13. 🦴 Allow Harvesting Dead Targets

The current code rejects dead targets in three places.

That prevents `HarvestAnimal` from ever executing on a corpse.

---

## ☐ Update `FindNearestValidTarget()`

Replace the dead-target check:

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

bool requiresDeadTarget =
    interaction.type ==
    InteractionType.HarvestAnimal;

if (requiresDeadTarget)
{
    if (!candidate.isDead ||
        candidate.harvested)
    {
        continue;
    }
}
else
{
    if (candidate.isDead)
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
        foreach (Agent candidate in candidates.Value)
        {
            if (candidate == null ||
                candidate == agent ||
                candidate.IsLeavingTown)
            {
                continue;
            }

            bool requiresDeadTarget =
                interaction.type ==
                InteractionType.HarvestAnimal;

            if (requiresDeadTarget)
            {
                if (!candidate.isDead ||
                    candidate.harvested)
                {
                    continue;
                }
            }
            else
            {
                if (candidate.isDead)
                    continue;
            }

            if (!candidate.HasAnyTag(
                    interaction.targetTags))
            {
                continue;
            }

            float dist = Vector3.Distance(
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

## ☐ Replace `ValidateActiveTarget()`

```csharp
private bool ValidateActiveTarget()
{
    if (activeTarget == null ||
        activeInteraction == null)
    {
        return false;
    }

    if (activeInteraction.type ==
        InteractionType.DepositResources)
    {
        return activeTarget == agent &&
               CanDepositResources();
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
    else
    {
        if (activeTarget.isDead)
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

# 14. ⚙️ Add `TrackAction` To `SocialActions.cs`

`TrackAction` applies one completed unit of tracking progress and then releases the accepted action.

It does not choose the target or destination.

```csharp
public class TrackAction : ISocialAction
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

        float currentProgress =
            target.GetSocialProgress(
                InteractionType.Track);

        ScriptableAgent targetData =
            AgentData.Instance?.GetAgent(
                target.type);

        float requiredProgress =
            targetData != null
                ? targetData.trackingRequired
                : 100f;

        Debug.Log(
            $"{self.type} tracked {target.type}: " +
            $"{currentProgress:0.##}/" +
            $"{requiredProgress:0.##}"
        );

        self.Visuals.Release();
    }
}
```

Each completed tracking waypoint now means:

```text
Tracking Progress Increased
    ↓
Track Action Released
    ↓
ActionManager Can Think Again
    ↓
Best Animal Re-evaluated
```

---

# 15. 🔪 Replace `HarvestAnimalAction`

The existing implementation kills the target:

```csharp
AgentDeath.HurtAgent(
    target,
    deathType.bleeding,
    9999f);
```

That behaviour belongs to hunting/combat, not harvesting.

Replace the entire class with:

```csharp
public class HarvestAnimalAction : ISocialAction
{
    public void Execute(
        Agent self,
        Agent target,
        AgentInteraction interaction)
    {
        if (self == null || target == null)
        {
            self?.Visuals.Release();
            return;
        }

        if (!target.isDead)
        {
            Debug.LogWarning(
                $"{self.type} tried to harvest " +
                $"{target.type}, but the target is alive."
            );

            self.Visuals.Release();
            return;
        }

        if (target.harvested)
        {
            self.Visuals.Release();
            return;
        }

        ScriptableAgent targetData =
            AgentData.Instance?.GetAgent(
                target.type);

        if (targetData == null)
        {
            Debug.LogWarning(
                $"No ScriptableAgent data found for " +
                $"{target.type}."
            );

            self.Visuals.Release();
            return;
        }

        if (targetData.harvestDrops != null)
        {
            foreach (InventoryItem drop in
                     targetData.harvestDrops)
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

        self.ClearCurrentTarget(
            InteractionType.Track);

        Debug.Log(
            $"{self.type} harvested {target.type}."
        );

        self.Visuals.Release();
    }
}
```

---

# 16. 🏕️ Add `DepositResourcesAction`

The exact global-stat method names must match the classes in the project.

The current UI references:

```text
stat.tot_food
stat.tot_Wealth
stat.tot_Goodness
```

Create one central helper for modifying those values.

---

## ☐ Create Deposit Action

```csharp
public class DepositResourcesAction : ISocialAction
{
    public void Execute(
        Agent self,
        Agent target,
        AgentInteraction interaction)
    {
        if (self == null)
            return;

        if (self.myWorkplace == null)
        {
            Debug.LogWarning(
                $"{self.type} cannot deposit resources " +
                $"because no workplace is assigned."
            );

            self.Visuals.Release();
            return;
        }

        if (!self.HasInventoryItems())
        {
            self.Visuals.Release();
            return;
        }

        int depositedFood = 0;
        int depositedFur = 0;

        foreach (InventoryItem item in self.inventory)
        {
            if (item == null || item.amount <= 0)
                continue;

            switch (item.type)
            {
                case ObjectType.Food:
                    depositedFood += item.amount;
                    break;

                case ObjectType.Fur:
                    depositedFur += item.amount;
                    break;
            }
        }

        /*
         * Replace these two calls with the actual
         * town-stat API used by the project.
         */
        TownResourceDeposit.AddFood(depositedFood);
        TownResourceDeposit.AddWealth(depositedFur);

        self.ClearInventory();

        Debug.Log(
            $"{self.type} deposited " +
            $"{depositedFood} food and " +
            $"{depositedFur} wealth."
        );

        self.Visuals.Release();
    }
}
```

---

## ☐ Add A Temporary Town Resource Adapter

Replace the bodies of these methods with the actual code that modifies:

```text
tot_food
tot_Wealth
```

```csharp
public static class TownResourceDeposit
{
    public static void AddFood(int amount)
    {
        if (amount <= 0)
            return;

        /*
         * Replace with the real project reference.
         *
         * Example:
         * GameStats.Instance.stat.tot_food += amount;
         */
    }

    public static void AddWealth(int amount)
    {
        if (amount <= 0)
            return;

        /*
         * Replace with the real project reference.
         *
         * Example:
         * GameStats.Instance.stat.tot_Wealth += amount;
         */
    }
}
```

This isolates the unknown town-stat API to one small adapter instead of coupling it directly to `DepositResourcesAction`.

---

# 17. 🧾 Register New Social Actions

In `actionForInteraction`, add:

```csharp
{ InteractionType.Track, AgentAction.track },
{ InteractionType.DepositResources, AgentAction.deposit },
```

The dictionary should resemble:

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

---

## ☐ Add Priorities

Add:

```csharp
{ InteractionType.Track, (int)PriorityTier.Social },
{ InteractionType.DepositResources, (int)PriorityTier.Social },
```

A sensible priority order is:

```text
DepositResources
    Highest hunting priority

HarvestAnimal
    Next

Combat against fully tracked prey
    Next

Track
    Lowest hunting priority
```

The exact enum tier can stay `Social`; use the interaction’s configured `priority` to determine which hunting interaction is selected first.

---

## ☐ Register Handlers

Add:

```csharp
{ InteractionType.Track, new TrackAction() },
{ InteractionType.DepositResources, new DepositResourcesAction() },
```

The registry should resemble:

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

# 18. 🦴 Update `SocialActions.Process()` For Corpses

The current method rejects every dead target:

```csharp
if (self == null ||
    target == null ||
    target.isDead)
{
    return;
}
```

That prevents `HarvestAnimalAction` from running.

Replace it with:

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

    if (target.isDead && !allowsDeadTarget)
        return;

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
            $"SocialActions: no handler registered " +
            $"for {interaction.type}"
        );
    }
}
```

`DepositResources` passes the hunter itself as `target`, so it works with the existing interface.

---

# 19. ✅ Implement Interaction Conditions

`ActionManager.SelectAction()` currently needs to check `interaction.conditions` before considering priority and chance.

---

## ☐ Add `AreConditionsMet()`

Add to `ActionManager`:

```csharp
private bool AreConditionsMet(
    AgentInteraction interaction)
{
    if (interaction.conditions == null ||
        interaction.conditions.Count == 0)
    {
        return true;
    }

    foreach (Condition condition in
             interaction.conditions)
    {
        if (condition == null)
            continue;

        if (!IsConditionMet(
                interaction,
                condition))
        {
            return false;
        }
    }

    return true;
}
```

---

## ☐ Add `IsConditionMet()`

```csharp
private bool IsConditionMet(
    AgentInteraction interaction,
    Condition condition)
{
    switch (condition.condition)
    {
        case ConditionType.noCondition:
            return true;

        case ConditionType.timeOfDay:
            return TimeManager.Instance != null &&
                   TimeManager.Instance.IsDaytime;

        case ConditionType.haveAnimal:
            return agent.GetCurrentTarget(
                       InteractionType.Track) != null;

        case ConditionType.hasTrackableAnimal:
            return HasAvailableTrackableAnimal(
                interaction);

        case ConditionType.animalTracked:
            return HasFullyTrackedAnimal();

        case ConditionType.animalDead:
            return HasHarvestableAnimal(
                interaction);

        case ConditionType.carryingResources:
            return agent.HasInventoryItems();

        default:
            Debug.LogWarning(
                $"Unhandled condition: " +
                $"{condition.condition}"
            );

            return false;
    }
}
```

---

## ☐ Add `HasAvailableTrackableAnimal()`

```csharp
private bool HasAvailableTrackableAnimal(
    AgentInteraction interaction)
{
    var allAgents =
        GameManager.Instance?.AllAgents;

    if (allAgents == null)
        return false;

    foreach (var candidates in allAgents)
    {
        foreach (Agent candidate in candidates.Value)
        {
            if (IsValidTrackingTarget(
                    candidate,
                    interaction))
            {
                return true;
            }
        }
    }

    return false;
}
```

---

## ☐ Add `HasFullyTrackedAnimal()`

```csharp
private bool HasFullyTrackedAnimal()
{
    Agent target =
        agent.GetCurrentTarget(
            InteractionType.Track);

    if (target == null ||
        target.isDead ||
        target.IsLeavingTown)
    {
        return false;
    }

    ScriptableAgent targetData =
        AgentData.Instance?.GetAgent(
            target.type);

    if (targetData == null)
        return false;

    return target.GetSocialProgress(
               InteractionType.Track) >=
           targetData.trackingRequired;
}
```

---

## ☐ Add `HasHarvestableAnimal()`

```csharp
private bool HasHarvestableAnimal(
    AgentInteraction interaction)
{
    var allAgents =
        GameManager.Instance?.AllAgents;

    if (allAgents == null)
        return false;

    foreach (var candidates in allAgents)
    {
        foreach (Agent candidate in candidates.Value)
        {
            if (candidate == null ||
                candidate == agent ||
                candidate.IsLeavingTown)
            {
                continue;
            }

            if (!candidate.isDead ||
                candidate.harvested)
            {
                continue;
            }

            if (!candidate.HasAnyTag(
                    interaction.targetTags))
            {
                continue;
            }

            return true;
        }
    }

    return false;
}
```

---

# 20. 🧠 Configure Hunter Interactions

Configure the hunter’s `ScriptableAgent` interactions in this order of priority.

---

## 🏕️ DepositResources

```text
Target Tags:
    None required because target is self

Conditions:
    CarryingResources

Priority:
    Highest hunting priority

Range:
    Ignored because ActionManager moves to workplace

Cooldown:
    Short
```

---

## 🔪 HarvestAnimal

```text
Target Tags:
    Prey

Conditions:
    AnimalDead

Priority:
    Higher than Combat and Track

Range:
    Approximately 1

canEnterTargetBuilding:
    True if penned animals occupy building tiles
```

---

## ⚔️ Combat

```text
Target Tags:
    Prey

Conditions:
    AnimalTracked

Priority:
    Higher than Track

Range:
    Normal combat range

Value:
    Hunting damage per attack
```

Do not use `9999` damage here if hunting should take time.

Use several combat executions so the kill develops slowly.

---

## 🦌 Track

```text
Target Tags:
    Prey

Conditions:
    HasTrackableAnimal

Priority:
    Lowest hunting priority

Chance:
    1

Range:
    Ignored for tracking-waypoint arrival

Cooldown:
    Controls pause between tracking cycles

Value:
    Tracking progress gained per waypoint
```

A reasonable first value:

```text
Track Value: 5
Tracking Required: 100
```

This requires roughly 20 completed tracking waypoints for a buffalo.

---

# 21. 🏕️ Keep Hunters From Loitering At Work While Hunting

The hunter should treat the hut as a base.

The interaction system should run before ordinary workplace wandering.

Desired priority:

```text
Active Social Plan
    ↓
Carrying Resources → Deposit
    ↓
Dead Animal → Harvest
    ↓
Tracked Animal → Combat
    ↓
Trackable Animal → Track
    ↓
Nothing Available → Rest Near Hunter Hut
```

If `AgentSchedule.RunSchedules()` currently sends the hunter into normal workplace roaming before conditions can select hunting, add a check before `RunWorkSchedule()`.

Example:

```csharp
if (agent.Actions.HasActivePlan)
    return;

if (agent.type == AgentType.furTrader &&
    agent.Actions.HasAvailableHuntingInteraction())
{
    return;
}

agent.WorkSchedule.RunWorkSchedule();
```

Only add this if testing shows the work schedule is stealing control before the interaction system can start hunting.

Do not add a separate `HunterBehaviour` class.

---

# 22. ☠️ Drop Inventory When A Hunter Dies

The complete loot-world-object system can come after the first hunting loop.

For the first pass, create a method that gathers what must be dropped.

```csharp
public List<InventoryItem> TakeInventoryForDrop()
{
    List<InventoryItem> droppedItems =
        new List<InventoryItem>();

    if (inventory == null)
        return droppedItems;

    foreach (InventoryItem item in inventory)
    {
        if (item == null || item.amount <= 0)
            continue;

        droppedItems.Add(
            new InventoryItem(
                item.type,
                item.amount));
    }

    inventory.Clear();

    return droppedItems;
}
```

Call this from the agent-death flow:

```csharp
List<InventoryItem> droppedItems =
    dyingAgent.TakeInventoryForDrop();
```

Then provide `droppedItems` to the future loot-object spawner.

Desired result:

```text
Hunter Dies
    ↓
Inventory Leaves Hunter
    ↓
Loot Exists In World
    ↓
Another Agent Can Recover It Later
```

---

# 23. 🧪 Testing Order

## ☐ Test 1: Inventory

Manually call:

```csharp
agent.AddToInventory(
    ObjectType.Food,
    10);
```

Verify in the Inspector:

```text
Food: 10
```

Call it again and verify:

```text
Food: 20
```

---

## ☐ Test 2: Tracking Target Selection

Place:

```text
One nearby low-value pig

One distant high-value buffalo
```

Verify that `CalculateTrackingScore()` selects the expected target.

---

## ☐ Test 3: Target Persistence

Allow the hunter to begin tracking an animal.

Verify:

```csharp
hunter.GetCurrentTarget(
    InteractionType.Track)
```

returns that animal.

Verify alternatives receive the switching penalty.

---

## ☐ Test 4: Tracking Movement

Verify one tracking cycle:

```text
Track Selected
    ↓
Tracking Waypoint Chosen
    ↓
Hunter Walks To Waypoint
    ↓
TrackAction Executes
    ↓
Progress Increases
    ↓
ActionState Releases
    ↓
Plan Clears
```

---

## ☐ Test 5: Reconsideration

After one completed waypoint, spawn a more valuable animal.

Verify the next Track selection reruns:

```csharp
FindBestTrackingTarget()
```

The hunter should either:

```text
Keep Current Target
```

or:

```text
Switch To Better Target
```

according to score.

---

## ☐ Test 6: Sleep Persistence

Allow partial tracking progress.

At night:

```text
Hunter Goes Home
```

The animal’s tracking progress should remain unchanged.

The next day, the hunter should be able to continue.

---

## ☐ Test 7: Fully Tracked Transition

When:

```csharp
target.GetSocialProgress(
    InteractionType.Track)
>=
targetData.trackingRequired
```

verify:

```text
Track Condition Stops Passing
Combat Condition Starts Passing
```

---

## ☐ Test 8: Slow Kill

Use normal damage rather than instant death.

Verify:

```text
Combat
    ↓
Cooldown
    ↓
Combat
    ↓
Cooldown
    ↓
Animal Dies
```

---

## ☐ Test 9: Harvest Corpse

Verify that dead animals are accepted through:

```text
FindNearestValidTarget
ValidateActiveTarget
SocialActions.Process
```

Verify:

```text
Harvest Drops Enter Inventory
Animal.harvested = true
```

---

## ☐ Test 10: Deposit

Verify:

```text
CarryingResources Condition Passes
    ↓
Hunter Returns To Workplace
    ↓
DepositResourcesAction Executes
    ↓
Food Becomes Town Food
    ↓
Fur Becomes Town Wealth
    ↓
Inventory Clears
```

---

# 🌟 First Complete Success Test

```text
Hunter Leaves Hut
    ↓
Hunter Evaluates Available Animals
    ↓
Hunter Selects Buffalo
    ↓
Hunter Walks To Tracking Waypoint
    ↓
Buffalo Tracking Progress Increases
    ↓
Track Action Releases
    ↓
Hunter Reconsiders Targets
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
Buffalo Becomes Fully Tracked
    ↓
Combat Unlocks
    ↓
Hunter Slowly Kills Buffalo
    ↓
Harvest Unlocks
    ↓
Food And Fur Enter Hunter Inventory
    ↓
Deposit Unlocks
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

# 🌟 First Target-Switching Story

```text
Hunter Tracks Buffalo To 60%
    ↓
A More Valuable Animal Appears
    ↓
Next Track Cycle Re-evaluates Targets
    ↓
New Animal Score Is Calculated
    ↓
Switch Penalty Is Applied
    ↓
Hunter Either Remains Committed
    OR
Hunter Switches To The Better Animal
```

---

# 🌟 First Death Story

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
Inventory Drops In Wilderness
    ↓
Town Receives Nothing
```

If these stories happen naturally through conditions, target selection, movement, actions, and progress, the hunting system is working the way BoomTown is intended to work.
