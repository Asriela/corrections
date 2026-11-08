using UnityEngine;

// Most conditions here are self-only.
//
// Conditions concerning the current Track target normally inspect:
// agent.GetCurrentTarget(InteractionType.Track)
//
// HarvestAnimal is the exception. Its target is found by
// ActionManager.FindHarvestTarget(), so its target-related conditions
// are deferred to ActionManager's corpse validation.
public static class ActionConditions
{
    // =====================================================
    // ENTRY POINT
    // All relevant conditions on the interaction must pass.
    // =====================================================

    public static bool AllMet(
        Agent self,
        AgentInteraction interaction)
    {
        if (self == null || interaction == null)
            return false;

        if (interaction.conditions == null)
            return true;

        foreach (Condition condition in interaction.conditions)
        {
            if (condition == null)
                continue;

            // HarvestAnimal can target any valid corpse.
            //
            // These conditions normally inspect only the agent's current
            // Track target. Evaluating them here would prevent the harvest
            // resolver from searching for unrelated kills.
            //
            // ActionManager.IsValidHarvestTarget() already guarantees that
            // the chosen target:
            // - is dead
            // - has inventory remaining
            // - has one of the required target tags
            if (interaction.type == InteractionType.HarvestAnimal &&
                IsHarvestTargetCondition(condition.condition))
            {
                continue;
            }

            bool result = Evaluate(self, condition);

            if (condition.invert)
                result = !result;

            if (!result)
                return false;
        }

        return true;
    }

    private static bool IsHarvestTargetCondition(
        ConditionType conditionType)
    {
        return conditionType == ConditionType.hasTarget ||
               conditionType == ConditionType.targetDead ||
               conditionType == ConditionType.targetHasInventory ||
               conditionType == ConditionType.doneTrackingTarget;
    }

    private static bool Evaluate(
        Agent self,
        Condition condition)
    {
        switch (condition.condition)
        {
            case ConditionType.noCondition:
                return true;

            // conditionValueToBeTrue:
            // 1 = must be daytime
            // 0 = must be nighttime
            case ConditionType.timeOfDay:
                return TimeManager.Instance != null &&
                       TimeManager.Instance.IsDaytime ==
                       (condition.conditionValueToBeTrue > 0.5f);

            // On duty means the agent has reached work and has not
            // transitioned back to their nighttime home schedule yet.
            case ConditionType.onDuty:
                return self.WorkSchedule != null &&
                       self.WorkSchedule.IsAtWork;

            // Does this agent currently remember a Track target?
            case ConditionType.hasTarget:
                return self.GetCurrentTarget(
                    InteractionType.Track
                ) != null;

            // Is the agent's current Track target dead?
            case ConditionType.targetDead:
            {
                Agent target = self.GetCurrentTarget(
                    InteractionType.Track
                );

                return target != null &&
                       target.isDead;
            }

            // Does the agent's current Track target still contain
            // harvestable inventory?
            case ConditionType.targetHasInventory:
            {
                Agent target = self.GetCurrentTarget(
                    InteractionType.Track
                );

                return target != null &&
                       target.HasInventoryItems();
            }

            // Is the current living Track target fully tracked?
            case ConditionType.doneTrackingTarget:
                return IsCurrentAnimalTracked(self);

            // Is the agent carrying loot with no remaining capacity?
            case ConditionType.carryingFullLoad:
                return IsLoadFull(self);

            default:
                return true;
        }
    }

    // =====================================================
    // HELPERS USED BY ACTIONMANAGER
    // =====================================================

    // A non-inverted targetDead condition requires this interaction
    // to act on a dead target.
    public static bool RequiresDeadTarget(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in interaction.conditions)
        {
            if (condition == null)
                continue;

            if (condition.condition ==
                    ConditionType.targetDead &&
                !condition.invert)
            {
                return true;
            }
        }

        return false;
    }

    // Dead targets are acceptable when the interaction explicitly
    // checks that the target is dead or has inventory.
    public static bool AllowsDeadTarget(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in interaction.conditions)
        {
            if (condition == null ||
                condition.invert)
            {
                continue;
            }

            if (condition.condition ==
                    ConditionType.targetDead ||
                condition.condition ==
                    ConditionType.targetHasInventory)
            {
                return true;
            }
        }

        return false;
    }

    // A non-inverted targetHasInventory condition requires the
    // selected target to still contain items.
    public static bool RequiresTargetInventory(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in interaction.conditions)
        {
            if (condition == null)
                continue;

            if (condition.condition ==
                    ConditionType.targetHasInventory &&
                !condition.invert)
            {
                return true;
            }
        }

        return false;
    }

    // These conditions normally indicate that the interaction should
    // use the agent's current Track target.
    //
    // HarvestAnimal does not use this fallback because ActionManager
    // gives it its own FindHarvestTarget() resolver.
    public static bool UsesTrackedTarget(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in interaction.conditions)
        {
            if (condition == null ||
                condition.invert)
            {
                continue;
            }

            if (condition.condition ==
                    ConditionType.hasTarget ||
                condition.condition ==
                    ConditionType.doneTrackingTarget ||
                condition.condition ==
                    ConditionType.targetDead ||
                condition.condition ==
                    ConditionType.targetHasInventory)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsCurrentAnimalTracked(
        Agent agent)
    {
        if (agent == null)
            return false;

        Agent target = agent.GetCurrentTarget(
            InteractionType.Track
        );

        if (target == null ||
            target.isDead)
        {
            return false;
        }

        return target.GetSocialProgress(
                   InteractionType.Track
               ) >= 100f;
    }

    // =====================================================
    // CARRY CAPACITY
    // =====================================================

    // Uses Agent's real loot and capacity values. This now matches
    // HarvestAnimalAction instead of using a separate placeholder.
    private static bool IsLoadFull(Agent self)
    {
        if (self == null)
            return false;

        return self.CarriedLoot > 0 &&
               self.FreeCarryCapacity <= 0;
    }
}
