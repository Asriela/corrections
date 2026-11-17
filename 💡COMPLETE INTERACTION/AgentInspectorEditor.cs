// AgentInspectorEditor.cs — class AgentInspectorEditor
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Agent))]
public class AgentInspectorEditor : Editor
{
    // =====================================================
    // ACTIONMANAGER REFLECTION
    // =====================================================

    private static readonly FieldInfo ActiveInteractionField =
        typeof(ActionManager).GetField(
            "activeInteraction",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly FieldInfo ActiveInteractionIndexField =
        typeof(ActionManager).GetField(
            "activeInteractionIndex",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly FieldInfo ActiveTargetField =
        typeof(ActionManager).GetField(
            "activeTarget",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly FieldInfo CooldownTimersField =
        typeof(ActionManager).GetField(
            "cooldownTimers",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly FieldInfo DecisionTickAccumulatorField =
        typeof(ActionManager).GetField(
            "decisionTickAccumulator",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly MethodInfo ResolveTargetMethod =
        typeof(ActionManager).GetMethod(
            "ResolveTarget",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static readonly MethodInfo ValidateActiveTargetMethod =
        typeof(ActionManager).GetMethod(
            "ValidateActiveTarget",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    // =====================================================
    // COLOURS
    // =====================================================

    private static readonly Color MetColor =
        new Color(0.45f, 1f, 0.45f);

    private static readonly Color FailedColor =
        new Color(1f, 0.45f, 0.45f);

    private static readonly Color ActiveColor =
        new Color(0.35f, 0.85f, 1f);

    private static readonly Color WaitingColor =
        new Color(1f, 0.8f, 0.3f);

    private static readonly Color MutedColor =
        new Color(0.72f, 0.72f, 0.72f);

    private static readonly Color PanelColor =
        new Color(0.17f, 0.17f, 0.17f);

    // =====================================================
    // EDITOR STATE
    // =====================================================

    private readonly Dictionary<int, bool> interactionFoldouts =
        new Dictionary<int, bool>();

    private bool showSocialActionDebugger = true;

    // =====================================================
    // PER-INTERACTION DEBUG RESULT
    // =====================================================

    private class InteractionGateResult
    {
        public bool managerAvailable;
        public bool agentAvailable;
        public bool agentAlive;
        public bool agentNotLeaving;
        public bool noActivePlanBlocking;
        public bool decisionReady;

        public bool cooldownReady;
        public float cooldownRemaining;

        public bool priorityCanCompete;
        public int interactionPriority;
        public int strongestDeterministicPriority;

        public bool targetKindSupported;
        public bool conditionsMet;

        public bool chanceConfigured;
        public float chance;
        public string chanceDescription;

        public bool targetResolutionAvailable;
        public IInteractionTarget resolvedTarget;
        public bool targetFound;
        public bool targetExists;
        public bool targetKindMatches;

        public bool actionMapped;
        public AgentAction mappedAction;

        public bool deterministicSelectionReady;
        public bool fullySelectableExceptChance;
    }

    // =====================================================
    // UNITY EDITOR
    // =====================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(10f);

        Agent agent = target as Agent;

        if (agent == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawSocialActionDebugger(agent);

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    // =====================================================
    // MAIN DEBUGGER
    // =====================================================

    private void DrawSocialActionDebugger(Agent agent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        showSocialActionDebugger = EditorGUILayout.Foldout(
            showSocialActionDebugger,
            "🧠 Social Action Debugger",
            true,
            EditorStyles.foldoutHeader
        );

        if (!showSocialActionDebugger)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space(4f);

        if (!Application.isPlaying)
        {
            DrawStatusBox(
                "▶ Enter Play Mode to see live action, target, chance, " +
                "priority, cooldown and condition gates.",
                MessageType.Info
            );

            DrawConfiguredInteractions(agent, false);

            EditorGUILayout.EndVertical();
            return;
        }

        if (agent.Actions == null)
        {
            DrawStatusBox(
                "ActionManager has not been initialized yet.",
                MessageType.Warning
            );

            EditorGUILayout.EndVertical();
            return;
        }

        DrawAgentSummary(agent);

        EditorGUILayout.Space(6f);

        DrawSelectionSummary(agent);

        EditorGUILayout.Space(6f);

        DrawCurrentAction(agent);

        EditorGUILayout.Space(8f);

        DrawConfiguredInteractions(agent, true);

        EditorGUILayout.EndVertical();
    }

    // =====================================================
    // AGENT SUMMARY
    // =====================================================

    private void DrawAgentSummary(Agent agent)
    {
        DrawSectionHeading("👤 Agent State");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawValueRow(
            "Agent Type",
            agent.type.ToString()
        );

        DrawValueRow(
            "Alive",
            agent.isDead ? "❌ No" : "✅ Yes",
            agent.isDead ? FailedColor : MetColor
        );

        DrawValueRow(
            "Leaving Town",
            agent.isLeavingTown ? "❌ Yes" : "✅ No",
            agent.isLeavingTown ? FailedColor : MetColor
        );

        bool isOnDuty =
            agent.WorkSchedule != null &&
            agent.WorkSchedule.IsAtWork;

        DrawValueRow(
            "On Duty",
            isOnDuty ? "✅ Yes" : "❌ No",
            isOnDuty ? MetColor : FailedColor
        );

        DrawValueRow(
            "At Home",
            agent.WorkSchedule != null &&
            agent.WorkSchedule.IsHome
                ? "✅ Yes"
                : "❌ No"
        );

        DrawValueRow(
            "Commuting",
            agent.WorkSchedule != null &&
            agent.WorkSchedule.IsCommuting
                ? "✅ Yes"
                : "❌ No"
        );

        DrawValueRow(
            "Carried Loot",
            $"{agent.CarriedLoot}/{agent.maxCarryCapacity}"
        );

        DrawValueRow(
            "Free Capacity",
            agent.FreeCarryCapacity.ToString()
        );

        Agent trackedTarget =
            agent.GetCurrentTarget(
                InteractionType.Track
            );

        DrawObjectRow(
            "Tracked Target",
            trackedTarget
        );

        if (trackedTarget != null)
        {
            DrawValueRow(
                "Target Type",
                trackedTarget.type.ToString()
            );

            DrawValueRow(
                "Target Dead",
                trackedTarget.isDead
                    ? "✅ Yes"
                    : "❌ No",
                trackedTarget.isDead
                    ? MetColor
                    : MutedColor
            );

            DrawValueRow(
                "Tracking Progress",
                $"{trackedTarget.GetSocialProgress(InteractionType.Track):0.0}/100"
            );

            DrawValueRow(
                "Target Has Inventory",
                trackedTarget.HasInventoryItems()
                    ? "✅ Yes"
                    : "❌ No",
                trackedTarget.HasInventoryItems()
                    ? MetColor
                    : FailedColor
            );
        }

        EditorGUILayout.EndVertical();
    }

    // =====================================================
    // GLOBAL SELECTION SUMMARY
    // =====================================================

    private void DrawSelectionSummary(Agent agent)
    {
        DrawSectionHeading("🚦 Selection Cycle");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        AgentInteraction activeInteraction =
            GetActiveInteraction(agent.Actions);

        IInteractionTarget activeTarget =
            GetActiveTarget(agent.Actions);

        bool hasActiveTarget =
            activeTarget != null;

        bool activeTargetExists =
            activeTarget != null &&
            activeTarget.Exists;

        DrawValueRow(
            "Manager Available",
            agent.Actions != null
                ? "✅ Yes"
                : "❌ No",
            agent.Actions != null
                ? MetColor
                : FailedColor
        );

        DrawValueRow(
            "Agent Can Select",
            !agent.isDead && !agent.isLeavingTown
                ? "✅ Yes"
                : "❌ No",
            !agent.isDead && !agent.isLeavingTown
                ? MetColor
                : FailedColor
        );

        DrawValueRow(
            "Active Plan",
            activeInteraction == null
                ? "None"
                : activeInteraction.type.ToString(),
            activeInteraction == null
                ? MutedColor
                : ActiveColor
        );

        DrawValueRow(
            "Active Target Stored",
            hasActiveTarget
                ? "✅ Yes"
                : "No",
            hasActiveTarget
                ? ActiveColor
                : MutedColor
        );

        DrawValueRow(
            "Active Target Exists",
            !hasActiveTarget
                ? "Not applicable"
                : activeTargetExists
                    ? "✅ Yes"
                    : "❌ No",
            !hasActiveTarget
                ? MutedColor
                : activeTargetExists
                    ? MetColor
                    : FailedColor
        );

        bool selectionBlockedByActivePlan =
            activeTarget != null;

        DrawValueRow(
            "New Selection Blocked",
            selectionBlockedByActivePlan
                ? "⛔ Yes — pursuing active plan"
                : "✅ No",
            selectionBlockedByActivePlan
                ? WaitingColor
                : MetColor
        );

        float remaining =
            GetDecisionTimeRemaining(agent);

        DrawValueRow(
            "Next Decision",
            selectionBlockedByActivePlan
                ? "Paused by active target"
                : $"{remaining:0.00}s",
            selectionBlockedByActivePlan
                ? WaitingColor
                : remaining <= 0f
                    ? MetColor
                    : WaitingColor
        );

        DrawValueRow(
            "Decision Interval",
            $"{agent.interactionDecisionInterval:0.00}s"
        );

        if (activeInteraction == null &&
            activeTarget != null)
        {
            DrawStatusBox(
                "ActionManager has an active target but no active interaction. " +
                "UpdateActions will still enter PursueActivePlan and return, " +
                "which can prevent new selections until ClearPlan runs.",
                MessageType.Error
            );
        }

        if (activeInteraction != null &&
            activeTarget == null)
        {
            DrawStatusBox(
                "ActionManager has an active interaction but no active target. " +
                "A new selection is allowed because UpdateActions only blocks " +
                "when activeTarget is non-null.",
                MessageType.Warning
            );
        }

        EditorGUILayout.EndVertical();
    }

    // =====================================================
    // CURRENT ACTION
    // =====================================================

    private void DrawCurrentAction(Agent agent)
    {
        DrawSectionHeading("🎬 Current Social Action");

        AgentInteraction activeInteraction =
            GetActiveInteraction(agent.Actions);

        IInteractionTarget activeTarget =
            GetActiveTarget(agent.Actions);

        int activeIndex =
            GetActiveInteractionIndex(agent.Actions);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (activeInteraction == null)
        {
            DrawValueRow(
                "State",
                "💤 No active social action",
                MutedColor
            );

            DrawValueRow(
                "Next Decision",
                $"{GetDecisionTimeRemaining(agent):0.00}s",
                WaitingColor
            );

            EditorGUILayout.EndVertical();
            return;
        }

        DrawValueRow(
            "State",
            "▶ ACTIVE",
            ActiveColor
        );

        DrawValueRow(
            "Interaction",
            activeInteraction.type.ToString(),
            ActiveColor
        );

        DrawValueRow(
            "Interaction Index",
            activeIndex.ToString()
        );

        DrawValueRow(
            "Priority",
            activeInteraction.priority.ToString()
        );

        DrawValueRow(
            "Configured Target",
            activeInteraction.targetKind.ToString()
        );

        DrawInteractionTargetRow(
            "Current Target",
            activeTarget
        );

        bool activeTargetValid =
            ValidateActiveTarget(agent.Actions);

        DrawValueRow(
            "Active Target Valid",
            activeTargetValid
                ? "✅ Yes"
                : "❌ No",
            activeTargetValid
                ? MetColor
                : FailedColor
        );

        if (activeTarget != null)
        {
            DrawValueRow(
                "Target Exists",
                activeTarget.Exists
                    ? "✅ Yes"
                    : "❌ No",
                activeTarget.Exists
                    ? MetColor
                    : FailedColor
            );

            DrawValueRow(
                "Target Kind",
                activeTarget.Kind.ToString()
            );

            DrawValueRow(
                "Kind Matches",
                activeTarget.Kind ==
                activeInteraction.targetKind
                    ? "✅ Yes"
                    : "❌ No",
                activeTarget.Kind ==
                activeInteraction.targetKind
                    ? MetColor
                    : FailedColor
            );

            DrawTargetSpecificDetails(activeTarget);

            float distance =
                Vector3.Distance(
                    agent.transform.position,
                    activeTarget.WorldPosition
                );

            DrawValueRow(
                "Distance",
                distance.ToString("0.00")
            );

            DrawValueRow(
                "Required Range",
                activeInteraction.range.ToString("0.00")
            );

            bool inRange =
                distance <= activeInteraction.range;

            DrawValueRow(
                "In Range",
                inRange
                    ? "✅ Yes"
                    : "❌ No",
                inRange
                    ? MetColor
                    : WaitingColor
            );
        }

        bool actionMapped =
            SocialActions.TryGetAction(
                activeInteraction.type,
                out AgentAction mappedAction
            );

        DrawValueRow(
            "Action Mapping",
            actionMapped
                ? $"✅ {mappedAction}"
                : "❌ Missing",
            actionMapped
                ? MetColor
                : FailedColor
        );

        EditorGUILayout.EndVertical();
    }

    // =====================================================
    // CONFIGURED INTERACTIONS
    // =====================================================

    private void DrawConfiguredInteractions(
        Agent agent,
        bool showLiveState)
    {
        DrawSectionHeading("📋 Available Social Actions");

        ScriptableAgent data =
            AgentData.Instance != null
                ? AgentData.Instance.GetAgent(agent.type)
                : null;

        if (data == null)
        {
            DrawStatusBox(
                $"No ScriptableAgent data was found for {agent.type}.",
                MessageType.Warning
            );

            return;
        }

        if (data.interactions == null ||
            data.interactions.Count == 0)
        {
            DrawStatusBox(
                $"{agent.type} has no configured interactions.",
                MessageType.Info
            );

            return;
        }

        AgentInteraction activeInteraction =
            showLiveState &&
            agent.Actions != null
                ? GetActiveInteraction(agent.Actions)
                : null;

        List<float> cooldownTimers =
            showLiveState &&
            agent.Actions != null
                ? GetCooldownTimers(agent.Actions)
                : null;

        int strongestDeterministicPriority =
            CalculateStrongestDeterministicPriority(
                agent,
                data,
                cooldownTimers,
                showLiveState
            );

        for (int i = 0; i < data.interactions.Count; i++)
        {
            AgentInteraction interaction =
                data.interactions[i];

            if (interaction == null)
                continue;

            float cooldownRemaining =
                cooldownTimers != null &&
                i < cooldownTimers.Count
                    ? cooldownTimers[i]
                    : 0f;

            bool isActive =
                interaction == activeInteraction;

            InteractionGateResult gate =
                EvaluateInteractionGates(
                    agent,
                    interaction,
                    cooldownRemaining,
                    strongestDeterministicPriority,
                    showLiveState
                );

            DrawInteractionPanel(
                agent,
                interaction,
                i,
                isActive,
                gate,
                showLiveState
            );

            EditorGUILayout.Space(4f);
        }

        if (showLiveState)
        {
            EditorGUILayout.HelpBox(
                "SELECTABLE means every deterministic selection gate currently " +
                "passes. Chance is evaluated only inside ActionManager.SelectAction, " +
                "so this inspector shows whether the configured chance can pass, " +
                "but does not perform its own random roll.",
                MessageType.Info
            );
        }
    }

    // =====================================================
    // GATE EVALUATION
    // =====================================================

    private InteractionGateResult EvaluateInteractionGates(
        Agent agent,
        AgentInteraction interaction,
        float cooldownRemaining,
        int strongestDeterministicPriority,
        bool showLiveState)
    {
        InteractionGateResult result =
            new InteractionGateResult();

        result.managerAvailable =
            agent != null &&
            agent.Actions != null;

        result.agentAvailable =
            agent != null;

        result.agentAlive =
            agent != null &&
            !agent.isDead;

        result.agentNotLeaving =
            agent != null &&
            !agent.isLeavingTown;

        IInteractionTarget activeTarget =
            result.managerAvailable
                ? GetActiveTarget(agent.Actions)
                : null;

        result.noActivePlanBlocking =
            activeTarget == null;

        result.decisionReady =
            !showLiveState ||
            GetDecisionTimeRemaining(agent) <= 0f;

        result.cooldownRemaining =
            cooldownRemaining;

        result.cooldownReady =
            cooldownRemaining <= 0f;

        result.interactionPriority =
            (int)interaction.priority;

        result.strongestDeterministicPriority =
            strongestDeterministicPriority;

        result.priorityCanCompete =
            result.interactionPriority >=
            strongestDeterministicPriority;

        result.targetKindSupported =
            SocialActions.Supports(
                interaction.type,
                interaction.targetKind
            );

        result.conditionsMet =
            !showLiveState ||
            ActionConditions.AllMet(
                agent,
                interaction
            );

        result.chance =
            interaction.chance;

        result.chanceConfigured =
            interaction.chance > 0f;

        if (interaction.chance <= 0f)
        {
            result.chanceDescription =
                "❌ 0% — can never pass";
        }
        else if (interaction.chance >= 1f)
        {
            result.chanceDescription =
                "✅ 100% — always passes";
        }
        else
        {
            result.chanceDescription =
                $"🎲 {interaction.chance * 100f:0.#}% per decision";
        }

        result.targetResolutionAvailable =
            ResolveTargetMethod != null &&
            result.managerAvailable;

        result.resolvedTarget =
            result.targetResolutionAvailable
                ? ResolveTarget(
                    agent.Actions,
                    interaction
                )
                : null;

        result.targetFound =
            result.resolvedTarget != null;

        result.targetExists =
            result.resolvedTarget != null &&
            result.resolvedTarget.Exists;

        result.targetKindMatches =
            result.resolvedTarget != null &&
            result.resolvedTarget.Kind ==
            interaction.targetKind;

        result.actionMapped =
            SocialActions.TryGetAction(
                interaction.type,
                out AgentAction mappedAction
            );

        result.mappedAction =
            mappedAction;

        result.deterministicSelectionReady =
            result.managerAvailable &&
            result.agentAvailable &&
            result.agentAlive &&
            result.agentNotLeaving &&
            result.noActivePlanBlocking &&
            result.cooldownReady &&
            result.priorityCanCompete &&
            result.targetKindSupported &&
            result.conditionsMet &&
            result.targetResolutionAvailable &&
            result.targetFound &&
            result.targetExists &&
            result.targetKindMatches;

        result.fullySelectableExceptChance =
            result.deterministicSelectionReady &&
            result.chanceConfigured;

        return result;
    }

    private int CalculateStrongestDeterministicPriority(
        Agent agent,
        ScriptableAgent data,
        List<float> cooldownTimers,
        bool showLiveState)
    {
        if (!showLiveState ||
            agent == null ||
            agent.Actions == null ||
            data == null ||
            data.interactions == null)
        {
            return int.MinValue;
        }

        int strongestPriority =
            int.MinValue;

        for (int i = 0; i < data.interactions.Count; i++)
        {
            AgentInteraction interaction =
                data.interactions[i];

            if (interaction == null)
                continue;

            float cooldownRemaining =
                cooldownTimers != null &&
                i < cooldownTimers.Count
                    ? cooldownTimers[i]
                    : 0f;

            if (cooldownRemaining > 0f)
                continue;

            if (!SocialActions.Supports(
                    interaction.type,
                    interaction.targetKind))
            {
                continue;
            }

            if (!ActionConditions.AllMet(
                    agent,
                    interaction))
            {
                continue;
            }

            if (interaction.chance <= 0f)
                continue;

            IInteractionTarget resolvedTarget =
                ResolveTarget(
                    agent.Actions,
                    interaction
                );

            if (resolvedTarget == null ||
                !resolvedTarget.Exists ||
                resolvedTarget.Kind != interaction.targetKind)
            {
                continue;
            }

            strongestPriority =
                Mathf.Max(
                    strongestPriority,
                    (int)interaction.priority
                );
        }

        return strongestPriority;
    }

    // =====================================================
    // INTERACTION PANEL
    // =====================================================

    private void DrawInteractionPanel(
        Agent agent,
        AgentInteraction interaction,
        int index,
        bool isActive,
        InteractionGateResult gate,
        bool showLiveState)
    {
        if (!interactionFoldouts.ContainsKey(index))
            interactionFoldouts[index] = true;

        Color previousBackground =
            GUI.backgroundColor;

        if (isActive)
        {
            GUI.backgroundColor =
                new Color(0.25f, 0.65f, 0.9f);
        }
        else if (
            showLiveState &&
            gate.fullySelectableExceptChance)
        {
            GUI.backgroundColor =
                new Color(0.35f, 0.7f, 0.35f);
        }
        else
        {
            GUI.backgroundColor =
                PanelColor;
        }

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        GUI.backgroundColor =
            previousBackground;

        string heading =
            $"{index + 1}. {interaction.type}  " +
            GetInteractionStateText(
                isActive,
                gate,
                showLiveState
            );

        interactionFoldouts[index] =
            EditorGUILayout.Foldout(
                interactionFoldouts[index],
                heading,
                true,
                EditorStyles.foldoutHeader
            );

        if (!interactionFoldouts[index])
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space(3f);

        DrawValueRow(
            "Target Kind",
            interaction.targetKind.ToString()
        );

        DrawTargetConfiguration(interaction);

        DrawValueRow(
            "Priority",
            interaction.priority.ToString()
        );

        DrawValueRow(
            "Chance",
            $"{interaction.chance * 100f:0.#}%"
        );

        DrawValueRow(
            "Range",
            interaction.range.ToString("0.00")
        );

        DrawValueRow(
            "Configured Cooldown",
            $"{interaction.cooldown:0.00}s"
        );

        if (showLiveState)
        {
            EditorGUILayout.Space(5f);

            DrawGateHeading();

            DrawGateRow(
                "1. Agent Alive",
                gate.agentAlive,
                gate.agentAlive
                    ? "Agent is alive"
                    : "UpdateActions resets and returns"
            );

            DrawGateRow(
                "2. Not Leaving",
                gate.agentNotLeaving,
                gate.agentNotLeaving
                    ? "Agent is not leaving town"
                    : "UpdateActions resets and returns"
            );

            DrawGateRow(
                "3. No Active Plan",
                gate.noActivePlanBlocking,
                gate.noActivePlanBlocking
                    ? "New selection is allowed"
                    : "activeTarget blocks SelectAction"
            );

            DrawGateRow(
                "4. Decision Timer",
                gate.decisionReady,
                gate.decisionReady
                    ? "Decision interval reached"
                    : $"{GetDecisionTimeRemaining(agent):0.00}s remaining",
                gate.decisionReady
                    ? MetColor
                    : WaitingColor
            );

            DrawGateRow(
                "5. Cooldown",
                gate.cooldownReady,
                gate.cooldownReady
                    ? "Ready"
                    : $"{gate.cooldownRemaining:0.00}s remaining",
                gate.cooldownReady
                    ? MetColor
                    : WaitingColor
            );

            DrawGateRow(
                "6. Priority",
                gate.priorityCanCompete,
                GetPriorityGateDescription(gate)
            );

            DrawGateRow(
                "7. Supported",
                gate.targetKindSupported,
                gate.targetKindSupported
                    ? $"{interaction.type} supports {interaction.targetKind}"
                    : $"{interaction.type} has no handler for {interaction.targetKind}"
            );

            DrawGateRow(
                "8. Conditions",
                gate.conditionsMet,
                gate.conditionsMet
                    ? "All conditions met"
                    : "One or more conditions blocked"
            );

            DrawValueRow(
                "9. Chance Gate",
                gate.chanceDescription,
                gate.chance <= 0f
                    ? FailedColor
                    : gate.chance >= 1f
                        ? MetColor
                        : WaitingColor
            );

            DrawGateRow(
                "10. Resolver Access",
                gate.targetResolutionAvailable,
                gate.targetResolutionAvailable
                    ? "ResolveTarget available"
                    : "Could not access ResolveTarget"
            );

            DrawGateRow(
                "11. Target Found",
                gate.targetFound,
                gate.targetFound
                    ? "Target resolved"
                    : "ResolveTarget returned null"
            );

            DrawInteractionTargetRow(
                "Resolved Target",
                gate.resolvedTarget
            );

            DrawGateRow(
                "12. Target Exists",
                gate.targetExists,
                gate.targetFound
                    ? gate.targetExists
                        ? "Target exists"
                        : "Target no longer exists"
                    : "No target to validate"
            );

            DrawGateRow(
                "13. Kind Matches",
                gate.targetKindMatches,
                gate.targetFound
                    ? gate.targetKindMatches
                        ? $"{gate.resolvedTarget.Kind} matches configuration"
                        : $"{gate.resolvedTarget.Kind} does not match {interaction.targetKind}"
                    : "No target to compare"
            );

            DrawGateRow(
                "14. Action Mapping",
                gate.actionMapped,
                gate.actionMapped
                    ? $"Maps to AgentAction.{gate.mappedAction}"
                    : "TryGetAction will fail before execution"
            );

            EditorGUILayout.Space(5f);

            DrawFinalSelectionState(
                interaction,
                gate,
                isActive
            );
        }

        EditorGUILayout.Space(5f);

        DrawConditions(
            agent,
            interaction,
            showLiveState
        );

        EditorGUILayout.EndVertical();
    }

    private void DrawGateHeading()
    {
        EditorGUILayout.LabelField(
            "Selection Gates",
            EditorStyles.boldLabel
        );
    }

    private void DrawGateRow(
        string label,
        bool passed,
        string detail)
    {
        DrawGateRow(
            label,
            passed,
            detail,
            passed
                ? MetColor
                : FailedColor
        );
    }

    private void DrawGateRow(
        string label,
        bool passed,
        string detail,
        Color color)
    {
        DrawValueRow(
            label,
            passed
                ? $"✅ {detail}"
                : $"❌ {detail}",
            color
        );
    }

    private void DrawFinalSelectionState(
        AgentInteraction interaction,
        InteractionGateResult gate,
        bool isActive)
    {
        if (isActive)
        {
            DrawStatusBox(
                "ACTIVE: this interaction has already been selected and has an active plan.",
                MessageType.Info
            );

            return;
        }

        if (!gate.agentAlive)
        {
            DrawStatusBox(
                "BLOCKED: the agent is dead.",
                MessageType.Error
            );

            return;
        }

        if (!gate.agentNotLeaving)
        {
            DrawStatusBox(
                "BLOCKED: the agent is leaving town.",
                MessageType.Error
            );

            return;
        }

        if (!gate.noActivePlanBlocking)
        {
            DrawStatusBox(
                "WAITING: another active target is currently blocking all new selections.",
                MessageType.Warning
            );

            return;
        }

        if (!gate.cooldownReady)
        {
            DrawStatusBox(
                $"COOLDOWN: {gate.cooldownRemaining:0.00}s remaining.",
                MessageType.Warning
            );

            return;
        }

        if (!gate.priorityCanCompete)
        {
            DrawStatusBox(
                "PRIORITY RISK: another currently viable interaction has a higher priority. " +
                "The exact winner can still depend on list order and chance rolls.",
                MessageType.Warning
            );

            return;
        }

        if (!gate.targetKindSupported)
        {
            DrawStatusBox(
                $"UNSUPPORTED: SocialActions has no {interaction.type} handler " +
                $"that supports {interaction.targetKind}.",
                MessageType.Error
            );

            return;
        }

        if (!gate.conditionsMet)
        {
            DrawStatusBox(
                "BLOCKED: one or more configured conditions failed.",
                MessageType.Error
            );

            return;
        }

        if (gate.chance <= 0f)
        {
            DrawStatusBox(
                "IMPOSSIBLE: chance is 0%, so this interaction can never pass SelectAction.",
                MessageType.Error
            );

            return;
        }

        if (!gate.targetResolutionAvailable)
        {
            DrawStatusBox(
                "DEBUGGER ERROR: the editor could not access ActionManager.ResolveTarget.",
                MessageType.Error
            );

            return;
        }

        if (!gate.targetFound)
        {
            DrawStatusBox(
                "NO TARGET: conditions pass, but ResolveTarget returned null. " +
                "Check target tags, selector configuration, living/dead requirements, " +
                "tracked-target requirements and GameManager.AllAgents.",
                MessageType.Error
            );

            return;
        }

        if (!gate.targetExists)
        {
            DrawStatusBox(
                "INVALID TARGET: a target was resolved, but target.Exists is false.",
                MessageType.Error
            );

            return;
        }

        if (!gate.targetKindMatches)
        {
            DrawStatusBox(
                "INVALID TARGET: the resolved target kind does not match the configured target kind.",
                MessageType.Error
            );

            return;
        }

        if (!gate.actionMapped)
        {
            DrawStatusBox(
                "EXECUTION BLOCKED: the interaction can be selected, but " +
                "SocialActions.TryGetAction has no AgentAction mapping for it.",
                MessageType.Error
            );

            return;
        }

        if (gate.chance >= 1f)
        {
            DrawStatusBox(
                "SELECTABLE: every inspected gate passes and chance is 100%.",
                MessageType.Info
            );

            return;
        }

        DrawStatusBox(
            $"SELECTABLE: every deterministic gate passes. It still has a " +
            $"{gate.chance * 100f:0.#}% chance roll each decision.",
            MessageType.Info
        );
    }

    private string GetPriorityGateDescription(
        InteractionGateResult gate)
    {
        if (gate.strongestDeterministicPriority ==
            int.MinValue)
        {
            return "No deterministic competitor found";
        }

        if (gate.priorityCanCompete)
        {
            return
                $"Priority {gate.interactionPriority}; " +
                $"strongest viable priority " +
                $"{gate.strongestDeterministicPriority}";
        }

        return
            $"Priority {gate.interactionPriority} is below " +
            $"{gate.strongestDeterministicPriority}";
    }

    // =====================================================
    // TARGET CONFIGURATION
    // =====================================================

    private void DrawTargetConfiguration(
        AgentInteraction interaction)
    {
        switch (interaction.targetKind)
        {
            case TargetKind.Agent:
                DrawValueRow(
                    "Agent Selector",
                    interaction.agentTargetSelector.ToString()
                );

                DrawListRow(
                    "Target Tags",
                    interaction.targetTags
                );

                break;

            case TargetKind.Building:
                DrawValueRow(
                    "Building Selector",
                    interaction.buildingTargetSelector.ToString()
                );

                if (interaction.buildingTargetSelector ==
                    BuildingTargetSelector.Nearest)
                {
                    DrawListRow(
                        "Target Tags",
                        interaction.targetTags
                    );
                }
                else
                {
                    DrawValueRow(
                        "Target Tags",
                        "Not used by direct selector",
                        MutedColor
                    );
                }

                break;

            case TargetKind.Location:
                DrawValueRow(
                    "Location Selector",
                    interaction.locationTargetSelector.ToString()
                );

                DrawListRow(
                    "Location Types",
                    interaction.targetLocationTypes
                );

                break;
        }
    }

    // =====================================================
    // CONDITIONS
    // =====================================================

    private void DrawConditions(
        Agent agent,
        AgentInteraction interaction,
        bool showLiveState)
    {
        EditorGUILayout.LabelField(
            "Required Conditions",
            EditorStyles.boldLabel
        );

        if (interaction.conditions == null ||
            interaction.conditions.Count == 0)
        {
            EditorGUILayout.LabelField(
                "  ✅ No conditions",
                GetColoredStyle(MetColor)
            );

            return;
        }

        for (int i = 0;
             i < interaction.conditions.Count;
             i++)
        {
            Condition condition =
                interaction.conditions[i];

            if (condition == null)
            {
                EditorGUILayout.LabelField(
                    $"  {i + 1}. Missing condition",
                    GetColoredStyle(FailedColor)
                );

                continue;
            }

            if (!showLiveState)
            {
                DrawConfiguredCondition(
                    condition,
                    i
                );

                continue;
            }

            DrawLiveCondition(
                agent,
                interaction,
                condition,
                i
            );
        }
    }

    private static string DescribeRequirement(
        Condition condition)
    {
        if (condition.evaluationMode ==
            ConditionEvaluationMode.Boolean)
        {
            return
                $"must be " +
                $"{(condition.requiredState ? "True" : "False")}";
        }

        return
            $"value {ComparisonSymbol(condition.comparison)} " +
            $"{condition.requiredValue:0.##}";
    }

    private static string ComparisonSymbol(
        ConditionComparison comparison)
    {
        switch (comparison)
        {
            case ConditionComparison.Equal:
                return "==";

            case ConditionComparison.NotEqual:
                return "!=";

            case ConditionComparison.GreaterThan:
                return ">";

            case ConditionComparison.GreaterThanOrEqual:
                return ">=";

            case ConditionComparison.LessThan:
                return "<";

            case ConditionComparison.LessThanOrEqual:
                return "<=";

            default:
                return "?";
        }
    }

    private void DrawConfiguredCondition(
        Condition condition,
        int index)
    {
        EditorGUILayout.LabelField(
            $"  {index + 1}. {condition.condition}  —  " +
            $"{DescribeRequirement(condition)}"
        );
    }

    private void DrawLiveCondition(
        Agent agent,
        AgentInteraction interaction,
        Condition condition,
        int index)
    {
        bool skippedForHarvest =
            interaction.type ==
            InteractionType.HarvestAnimal &&
            (
                condition.condition ==
                ConditionType.hasTarget ||
                condition.condition ==
                ConditionType.targetDead ||
                condition.condition ==
                ConditionType.targetHasInventory ||
                condition.condition ==
                ConditionType.doneTrackingTarget
            );

        bool passed =
            skippedForHarvest ||
            ActionConditions.IsMet(
                agent,
                condition
            );

        string icon =
            skippedForHarvest
                ? "⏭"
                : passed
                    ? "✅"
                    : "❌";

        EditorGUILayout.LabelField(
            $"{icon} {index + 1}. {condition.condition}",
            GetColoredStyle(
                passed
                    ? MetColor
                    : FailedColor
            )
        );

        EditorGUI.indentLevel++;

        DrawValueRow(
            "Required",
            DescribeRequirement(condition)
        );

        if (condition.evaluationMode ==
            ConditionEvaluationMode.Boolean)
        {
            bool state =
                ActionConditions.GetCurrentBooleanState(
                    agent,
                    condition
                );

            DrawValueRow(
                "Current State",
                state
                    ? "True"
                    : "False"
            );
        }
        else
        {
            float value =
                ActionConditions.GetCurrentValue(
                    agent,
                    condition
                );

            DrawValueRow(
                "Current Value",
                value.ToString("0.##")
            );
        }

        if (skippedForHarvest)
        {
            DrawValueRow(
                "Note",
                "Ignored for Harvest (own target search)",
                MutedColor
            );
        }

        DrawConditionDetails(
            agent,
            condition
        );

        EditorGUI.indentLevel--;
    }

    private void DrawConditionDetails(
        Agent agent,
        Condition condition)
    {
        switch (condition.condition)
        {
            case ConditionType.noCondition:
                DrawValueRow(
                    "Details",
                    "Always true"
                );

                break;

            case ConditionType.timeOfDay:
            {
                string currentTime =
                    TimeManager.Instance == null
                        ? "No TimeManager"
                        : TimeManager.Instance.IsDaytime
                            ? "Day"
                            : "Night";

                DrawValueRow(
                    "Current",
                    currentTime
                );

                if (TimeManager.Instance != null)
                {
                    DrawValueRow(
                        "Current Hour",
                        TimeManager.Instance
                            .CurrentHour
                            .ToString()
                    );
                }

                break;
            }

            case ConditionType.onDuty:
            {
                bool onDuty =
                    agent.WorkSchedule != null &&
                    agent.WorkSchedule.IsAtWork;

                DrawValueRow(
                    "Current",
                    onDuty
                        ? "Agent is on duty"
                        : "Agent is not on duty"
                );

                break;
            }

            case ConditionType.hasTarget:
            case ConditionType.targetDead:
            case ConditionType.targetHasInventory:
            case ConditionType.doneTrackingTarget:
                DrawTrackedTargetDetails(
                    agent,
                    condition.condition
                );

                break;

            case ConditionType.carryingFullLoad:
                DrawValueRow(
                    "Carried Loot",
                    $"{agent.CarriedLoot}/" +
                    $"{agent.maxCarryCapacity}"
                );

                DrawValueRow(
                    "Free Capacity",
                    agent.FreeCarryCapacity.ToString()
                );

                break;
        }
    }

    private void DrawTrackedTargetDetails(
        Agent agent,
        ConditionType conditionType)
    {
        Agent tracked =
            agent.GetCurrentTarget(
                InteractionType.Track
            );

        DrawObjectRow(
            "Track Target",
            tracked
        );

        if (tracked == null)
            return;

        if (conditionType ==
            ConditionType.targetDead)
        {
            DrawValueRow(
                "Dead",
                tracked.isDead
                    ? "Yes"
                    : "No"
            );
        }

        if (conditionType ==
            ConditionType.targetHasInventory)
        {
            DrawValueRow(
                "Has Inventory",
                tracked.HasInventoryItems()
                    ? "Yes"
                    : "No"
            );
        }

        if (conditionType ==
            ConditionType.doneTrackingTarget)
        {
            DrawValueRow(
                "Progress",
                $"{tracked.GetSocialProgress(InteractionType.Track):0.0}/100"
            );

            DrawValueRow(
                "Target Alive",
                tracked.isDead
                    ? "No"
                    : "Yes"
            );
        }
    }

    // =====================================================
    // TARGET DETAILS
    // =====================================================

    private void DrawTargetSpecificDetails(
        IInteractionTarget interactionTarget)
    {
        if (interactionTarget is Agent agentTarget)
        {
            DrawValueRow(
                "Agent Type",
                agentTarget.type.ToString()
            );

            DrawValueRow(
                "Dead",
                agentTarget.isDead
                    ? "Yes"
                    : "No"
            );

            return;
        }

        if (interactionTarget is Building buildingTarget)
        {
            DrawValueRow(
                "Building Type",
                buildingTarget.type.ToString()
            );

            DrawValueRow(
                "Health",
                $"{buildingTarget.health:0.#}/" +
                $"{buildingTarget.MaxHealth:0.#}"
            );

            DrawValueRow(
                "Broken",
                buildingTarget.isBroken
                    ? "Yes"
                    : "No"
            );

            return;
        }

        if (interactionTarget is MapLocation locationTarget)
        {
            DrawValueRow(
                "Location Type",
                locationTarget.type.ToString()
            );
        }
    }

    // =====================================================
    // REFLECTION HELPERS
    // =====================================================

    private AgentInteraction GetActiveInteraction(
        ActionManager actions)
    {
        if (actions == null ||
            ActiveInteractionField == null)
        {
            return null;
        }

        return
            ActiveInteractionField.GetValue(actions)
            as AgentInteraction;
    }

    private int GetActiveInteractionIndex(
        ActionManager actions)
    {
        if (actions == null ||
            ActiveInteractionIndexField == null)
        {
            return -1;
        }

        object value =
            ActiveInteractionIndexField.GetValue(actions);

        return value is int index
            ? index
            : -1;
    }

    private IInteractionTarget GetActiveTarget(
        ActionManager actions)
    {
        if (actions == null ||
            ActiveTargetField == null)
        {
            return null;
        }

        return
            ActiveTargetField.GetValue(actions)
            as IInteractionTarget;
    }

    private List<float> GetCooldownTimers(
        ActionManager actions)
    {
        if (actions == null ||
            CooldownTimersField == null)
        {
            return null;
        }

        return
            CooldownTimersField.GetValue(actions)
            as List<float>;
    }

    private IInteractionTarget ResolveTarget(
        ActionManager actions,
        AgentInteraction interaction)
    {
        if (actions == null ||
            interaction == null ||
            ResolveTargetMethod == null)
        {
            return null;
        }

        try
        {
            return
                ResolveTargetMethod.Invoke(
                    actions,
                    new object[] { interaction }
                )
                as IInteractionTarget;
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(
                exception.InnerException ??
                exception
            );

            return null;
        }
    }

    private bool ValidateActiveTarget(
        ActionManager actions)
    {
        if (actions == null ||
            ValidateActiveTargetMethod == null)
        {
            return false;
        }

        try
        {
            object value =
                ValidateActiveTargetMethod.Invoke(
                    actions,
                    null
                );

            return value is bool valid &&
                   valid;
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(
                exception.InnerException ??
                exception
            );

            return false;
        }
    }

    private float GetDecisionTimeRemaining(
        Agent agent)
    {
        if (agent == null ||
            agent.Actions == null ||
            DecisionTickAccumulatorField == null)
        {
            return 0f;
        }

        object value =
            DecisionTickAccumulatorField.GetValue(
                agent.Actions
            );

        float accumulator =
            value is float floatValue
                ? floatValue
                : 0f;

        return Mathf.Max(
            0f,
            agent.interactionDecisionInterval -
            accumulator
        );
    }

    // =====================================================
    // STATE LABEL
    // =====================================================

    private string GetInteractionStateText(
        bool isActive,
        InteractionGateResult gate,
        bool showLiveState)
    {
        if (!showLiveState)
            return string.Empty;

        if (isActive)
            return "▶ ACTIVE";

        if (!gate.agentAlive ||
            !gate.agentNotLeaving)
        {
            return "❌ AGENT BLOCKED";
        }

        if (!gate.noActivePlanBlocking)
            return "⛔ PLAN BLOCKED";

        if (!gate.cooldownReady)
            return "⏳ COOLDOWN";

        if (!gate.priorityCanCompete)
            return "⬇ PRIORITY";

        if (!gate.targetKindSupported)
            return "❌ UNSUPPORTED";

        if (!gate.conditionsMet)
            return "❌ CONDITIONS";

        if (gate.chance <= 0f)
            return "❌ 0% CHANCE";

        if (!gate.targetFound)
            return "❌ NO TARGET";

        if (!gate.targetExists)
            return "❌ TARGET INVALID";

        if (!gate.targetKindMatches)
            return "❌ KIND MISMATCH";

        if (!gate.actionMapped)
            return "❌ NO ACTION MAP";

        if (gate.chance < 1f)
            return "🎲 SELECTABLE";

        return "✅ SELECTABLE";
    }

    // =====================================================
    // DRAWING HELPERS
    // =====================================================

    private void DrawSectionHeading(
        string heading)
    {
        EditorGUILayout.LabelField(
            heading,
            EditorStyles.boldLabel
        );
    }

    private void DrawValueRow(
        string label,
        string value)
    {
        DrawValueRow(
            label,
            value,
            EditorStyles.label.normal.textColor
        );
    }

    private void DrawValueRow(
        string label,
        string value,
        Color valueColor)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            label,
            GUILayout.Width(145f)
        );

        EditorGUILayout.LabelField(
            value,
            GetColoredStyle(valueColor)
        );

        EditorGUILayout.EndHorizontal();
    }

    private void DrawObjectRow(
        string label,
        UnityEngine.Object value)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            label,
            GUILayout.Width(145f)
        );

        EditorGUI.BeginDisabledGroup(true);

        EditorGUILayout.ObjectField(
            value,
            typeof(UnityEngine.Object),
            true
        );

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawInteractionTargetRow(
        string label,
        IInteractionTarget value)
    {
        UnityEngine.Object unityObject =
            value as UnityEngine.Object;

        if (unityObject != null)
        {
            DrawObjectRow(
                label,
                unityObject
            );

            return;
        }

        DrawValueRow(
            label,
            value == null
                ? "None"
                : $"{value.Kind} at " +
                  $"{value.WorldPosition}"
        );
    }

    private void DrawListRow<T>(
        string label,
        List<T> values)
    {
        string text =
            values == null ||
            values.Count == 0
                ? "None"
                : string.Join(", ", values);

        DrawValueRow(
            label,
            text
        );
    }

    private void DrawStatusBox(
        string message,
        MessageType messageType)
    {
        EditorGUILayout.HelpBox(
            message,
            messageType
        );
    }

    private GUIStyle GetColoredStyle(
        Color color)
    {
        GUIStyle style =
            new GUIStyle(EditorStyles.label);

        style.normal.textColor =
            color;

        style.wordWrap =
            true;

        return style;
    }
}

#endif
