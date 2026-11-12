using UnityEngine;

// Every condition can be evaluated in one of two ways:
//
// BOOLEAN
// --------
// The condition produces a natural True or False state.
//
// Example:
//     onDuty
//     Required State: True
//
// Example:
//     doneTrackingTarget
//     Required State: False
//
// VALUE COMPARISON
// ----------------
// The condition produces a numeric value that is compared against
// the configured Required Value.
//
// Example:
//     doneTrackingTarget
//     Comparison: GreaterThanOrEqual
//     Required Value: 100
//
// In Value Comparison mode, doneTrackingTarget represents the current
// Track target's tracking progress rather than only returning True/False.
public static class ActionConditions
{
    // =====================================================
    // ENTRY POINT
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

            // HarvestAnimal has its own target search.
            //
            // These conditions normally inspect only the hunter's current
            // Track target. Applying them here would prevent the hunter
            // from harvesting unrelated corpses.
            if (interaction.type ==
                    InteractionType.HarvestAnimal &&
                IsHarvestTargetCondition(
                    condition.condition))
            {
                continue;
            }

            if (!IsMet(self, condition))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluates one configured condition, including its selected
    /// Boolean or Value Comparison mode.
    ///
    /// This is public so Editor debugging tools can display the exact
    /// same result used by gameplay.
    /// </summary>
    public static bool IsMet(
        Agent self,
        Condition condition)
    {
        if (self == null || condition == null)
            return false;

        switch (condition.evaluationMode)
        {
            case ConditionEvaluationMode.Boolean:
            {
                bool currentState =
                    EvaluateBoolean(
                        self,
                        condition.condition
                    );

                return currentState ==
                       condition.requiredState;
            }

            case ConditionEvaluationMode.ValueComparison:
            {
                float currentValue =
                    EvaluateValue(
                        self,
                        condition.condition
                    );

                return Compare(
                    currentValue,
                    condition.comparison,
                    condition.requiredValue
                );
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Returns the natural Boolean state before Required State is applied.
    /// Useful for live Editor debugging.
    /// </summary>
    public static bool GetCurrentBooleanState(
        Agent self,
        Condition condition)
    {
        if (self == null || condition == null)
            return false;

        return EvaluateBoolean(
            self,
            condition.condition
        );
    }

    /// <summary>
    /// Returns the condition's current numeric value.
    /// Useful for live Editor debugging.
    /// </summary>
    public static float GetCurrentValue(
        Agent self,
        Condition condition)
    {
        if (self == null || condition == null)
            return 0f;

        return EvaluateValue(
            self,
            condition.condition
        );
    }

    // =====================================================
    // BOOLEAN EVALUATION
    // =====================================================

    private static bool EvaluateBoolean(
        Agent self,
        ConditionType conditionType)
    {
        switch (conditionType)
        {
            case ConditionType.noCondition:
                return true;

            // In Boolean mode:
            //
            // True  = it is currently daytime
            // False = it is currently nighttime
            case ConditionType.timeOfDay:
                return TimeManager.Instance != null &&
                       TimeManager.Instance.IsDaytime;

            // True when the agent has begun its workday.
            //
            // For field workers such as hunters, this remains true while
            // they travel away from the workplace to perform their work.
            case ConditionType.onDuty:
                return self.WorkSchedule != null &&
                       self.WorkSchedule.IsAtWork;

            // True when the agent currently remembers a Track target.
            case ConditionType.hasTarget:
                return self.GetCurrentTarget(
                    InteractionType.Track
                ) != null;

            // True when the current Track target exists and is dead.
            case ConditionType.targetDead:
            {
                Agent target =
                    self.GetCurrentTarget(
                        InteractionType.Track
                    );

                return target != null &&
                       target.isDead;
            }

            // True when the current Track target exists and still
            // contains inventory.
            case ConditionType.targetHasInventory:
            {
                Agent target =
                    self.GetCurrentTarget(
                        InteractionType.Track
                    );

                return target != null &&
                       target.HasInventoryItems();
            }

            // True when the current Track target is:
            //
            // - present
            // - alive
            // - tracked to at least 100 progress
            case ConditionType.doneTrackingTarget:
                return IsCurrentAnimalTracked(self);

            // True when the agent carries loot and has no capacity left.
            case ConditionType.carryingFullLoad:
                return IsLoadFull(self);

            default:
                return false;
        }
    }

    // =====================================================
    // VALUE EVALUATION
    // =====================================================

    private static float EvaluateValue(
        Agent self,
        ConditionType conditionType)
    {
        switch (conditionType)
        {
            // Always returns 1.
            case ConditionType.noCondition:
                return 1f;

            // In Value Comparison mode, timeOfDay represents the
            // current in-game hour from 0 to 23.
            //
            // Example:
            //     timeOfDay >= 16
            //
            // means 16:00 or later.
            case ConditionType.timeOfDay:
                return TimeManager.Instance != null
                    ? TimeManager.Instance.CurrentHour
                    : -1f;

            // Boolean conditions can still be value-compared:
            //
            // True  = 1
            // False = 0
            case ConditionType.onDuty:
                return self.WorkSchedule != null &&
                       self.WorkSchedule.IsAtWork
                    ? 1f
                    : 0f;

            // In Value Comparison mode, hasTarget returns:
            //
            // 1 = has a target
            // 0 = no target
            case ConditionType.hasTarget:
                return self.GetCurrentTarget(
                    InteractionType.Track
                ) != null
                    ? 1f
                    : 0f;

            // In Value Comparison mode:
            //
            // 1 = current Track target is dead
            // 0 = no target or target is alive
            case ConditionType.targetDead:
            {
                Agent target =
                    self.GetCurrentTarget(
                        InteractionType.Track
                    );

                return target != null &&
                       target.isDead
                    ? 1f
                    : 0f;
            }

            // In Value Comparison mode, this returns the total number
            // of items remaining on the current Track target.
            case ConditionType.targetHasInventory:
            {
                Agent target =
                    self.GetCurrentTarget(
                        InteractionType.Track
                    );

                return GetInventoryAmount(target);
            }

            // In Value Comparison mode, this returns the actual shared
            // tracking progress stored on the current Track target.
            //
            // Example:
            //     doneTrackingTarget >= 100
            case ConditionType.doneTrackingTarget:
            {
                Agent target =
                    self.GetCurrentTarget(
                        InteractionType.Track
                    );

                if (target == null)
                    return 0f;

                return target.GetSocialProgress(
                    InteractionType.Track
                );
            }

            // In Value Comparison mode, this returns the actual amount
            // of loot currently being carried.
            //
            // Example:
            //     carryingFullLoad >= 5
            //
            // Note that Boolean mode still uses the agent's dynamic
            // maximum capacity rather than a hardcoded value.
            case ConditionType.carryingFullLoad:
                return self.CarriedLoot;

            default:
                return 0f;
        }
    }

    // =====================================================
    // COMPARISON
    // =====================================================

    private static bool Compare(
        float currentValue,
        ConditionComparison comparison,
        float requiredValue)
    {
        switch (comparison)
        {
            case ConditionComparison.Equal:
                return Mathf.Approximately(
                    currentValue,
                    requiredValue
                );

            case ConditionComparison.NotEqual:
                return !Mathf.Approximately(
                    currentValue,
                    requiredValue
                );

            case ConditionComparison.GreaterThan:
                return currentValue >
                       requiredValue;

            case ConditionComparison.GreaterThanOrEqual:
                return currentValue >=
                       requiredValue;

            case ConditionComparison.LessThan:
                return currentValue <
                       requiredValue;

            case ConditionComparison.LessThanOrEqual:
                return currentValue <=
                       requiredValue;

            default:
                return false;
        }
    }

    // =====================================================
    // ACTIONMANAGER HELPERS
    // =====================================================

    // Returns true when an interaction explicitly requires its
    // target to be dead.
    public static bool RequiresDeadTarget(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in
                 interaction.conditions)
        {
            if (condition == null ||
                condition.condition !=
                    ConditionType.targetDead)
            {
                continue;
            }

            if (RequiresPositiveCondition(condition))
                return true;
        }

        return false;
    }

    // Retained for other systems that may need to know whether an
    // interaction accepts dead agents.
    public static bool AllowsDeadTarget(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in
                 interaction.conditions)
        {
            if (condition == null)
                continue;

            bool isCorpseCondition =
                condition.condition ==
                    ConditionType.targetDead ||
                condition.condition ==
                    ConditionType.targetHasInventory;

            if (!isCorpseCondition)
                continue;

            if (RequiresPositiveCondition(condition))
                return true;
        }

        return false;
    }

    // Returns true when an interaction explicitly requires its
    // target to contain inventory.
    public static bool RequiresTargetInventory(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in
                 interaction.conditions)
        {
            if (condition == null ||
                condition.condition !=
                    ConditionType.targetHasInventory)
            {
                continue;
            }

            if (RequiresPositiveCondition(condition))
                return true;
        }

        return false;
    }

    // Returns true when an interaction's requirements indicate that
    // it should act on the agent's remembered Track target.
    public static bool UsesTrackedTarget(
        AgentInteraction interaction)
    {
        if (interaction == null ||
            interaction.conditions == null)
        {
            return false;
        }

        foreach (Condition condition in
                 interaction.conditions)
        {
            if (condition == null)
                continue;

            bool trackedTargetCondition =
                condition.condition ==
                    ConditionType.hasTarget ||
                condition.condition ==
                    ConditionType.doneTrackingTarget ||
                condition.condition ==
                    ConditionType.targetDead ||
                condition.condition ==
                    ConditionType.targetHasInventory;

            if (!trackedTargetCondition)
                continue;

            if (RequiresPositiveCondition(condition))
                return true;
        }

        return false;
    }

    // Determines whether a configured condition requires a positive
    // or present state.
    //
    // Boolean:
    //     Required State must be True.
    //
    // Value comparison:
    //     Any comparison is considered target-dependent because it
    //     reads a value from the tracked target.
    private static bool RequiresPositiveCondition(
        Condition condition)
    {
        if (condition == null)
            return false;

        if (condition.evaluationMode ==
            ConditionEvaluationMode.Boolean)
        {
            return condition.requiredState;
        }

        return true;
    }

    // =====================================================
    // SHARED HELPERS
    // =====================================================

    private static bool IsHarvestTargetCondition(
        ConditionType conditionType)
    {
        return conditionType ==
                   ConditionType.hasTarget ||
               conditionType ==
                   ConditionType.targetDead ||
               conditionType ==
                   ConditionType.targetHasInventory ||
               conditionType ==
                   ConditionType.doneTrackingTarget;
    }

    public static bool IsCurrentAnimalTracked(
        Agent agent)
    {
        if (agent == null)
            return false;

        Agent target =
            agent.GetCurrentTarget(
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

    private static bool IsLoadFull(
        Agent self)
    {
        if (self == null)
            return false;

        return self.CarriedLoot > 0 &&
               self.FreeCarryCapacity <= 0;
    }

    private static float GetInventoryAmount(
        Agent target)
    {
        if (target == null ||
            target.inventory == null)
        {
            return 0f;
        }

        float total = 0f;

        foreach (InventoryItem item in
                 target.inventory)
        {
            if (item == null ||
                item.amount <= 0)
            {
                continue;
            }

            total += item.amount;
        }

        return total;
    }
}
