#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Agent))]
public class AgentInspectorEditor : Editor
{
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

    private static readonly MethodInfo EvaluateConditionMethod =
        typeof(ActionConditions).GetMethod(
            "Evaluate",
            BindingFlags.Static | BindingFlags.NonPublic
        );

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

    private readonly Dictionary<int, bool> interactionFoldouts =
        new Dictionary<int, bool>();

    private bool showSocialActionDebugger = true;

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

        // Keep the debug display live while the game is running.
        if (Application.isPlaying)
            Repaint();
    }

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
                "▶ Enter Play Mode to see live action and condition states.",
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

        DrawCurrentAction(agent);

        EditorGUILayout.Space(8f);

        DrawConfiguredInteractions(agent, true);

        EditorGUILayout.EndVertical();
    }

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
            agent.GetCurrentTarget(InteractionType.Track);

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

    private void DrawCurrentAction(Agent agent)
    {
        DrawSectionHeading("🎬 Current Social Action");

        AgentInteraction activeInteraction =
            GetActiveInteraction(agent.Actions);

        Agent activeTarget =
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

            float decisionTimeRemaining =
                GetDecisionTimeRemaining(agent);

            DrawValueRow(
                "Next Decision",
                $"{decisionTimeRemaining:0.00}s",
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

        DrawObjectRow(
            "Current Target",
            activeTarget
        );

        if (activeTarget != null)
        {
            DrawValueRow(
                "Target Type",
                activeTarget.type.ToString()
            );

            float distance = Vector3.Distance(
                agent.transform.position,
                activeTarget.transform.position
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
                inRange ? "✅ Yes" : "❌ No",
                inRange ? MetColor : WaitingColor
            );
        }

        EditorGUILayout.EndVertical();
    }

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
            showLiveState && agent.Actions != null
                ? GetActiveInteraction(agent.Actions)
                : null;

        List<float> cooldownTimers =
            showLiveState && agent.Actions != null
                ? GetCooldownTimers(agent.Actions)
                : null;

        for (int i = 0; i < data.interactions.Count; i++)
        {
            AgentInteraction interaction =
                data.interactions[i];

            if (interaction == null)
                continue;

            float cooldownRemaining =
                cooldownTimers != null &&
                i >= 0 &&
                i < cooldownTimers.Count
                    ? cooldownTimers[i]
                    : 0f;

            bool isActive =
                interaction == activeInteraction;

            bool conditionsMet =
                !showLiveState ||
                ActionConditions.AllMet(
                    agent,
                    interaction
                );

            bool cooldownReady =
                cooldownRemaining <= 0f;

            DrawInteractionPanel(
                agent,
                interaction,
                i,
                isActive,
                conditionsMet,
                cooldownReady,
                cooldownRemaining,
                showLiveState
            );

            EditorGUILayout.Space(4f);
        }

        if (showLiveState)
        {
            EditorGUILayout.Space(3f);

            EditorGUILayout.HelpBox(
                "READY means the conditions and cooldown currently pass. " +
                "The interaction still needs to pass its Chance roll and " +
                "find a valid target before it can become active.",
                MessageType.Info
            );
        }
    }

    private void DrawInteractionPanel(
        Agent agent,
        AgentInteraction interaction,
        int index,
        bool isActive,
        bool conditionsMet,
        bool cooldownReady,
        float cooldownRemaining,
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
        else if (showLiveState &&
                 conditionsMet &&
                 cooldownReady)
        {
            GUI.backgroundColor =
                new Color(0.35f, 0.7f, 0.35f);
        }
        else
        {
            GUI.backgroundColor =
                PanelColor;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUI.backgroundColor = previousBackground;

        string stateText =
            GetInteractionStateText(
                isActive,
                conditionsMet,
                cooldownReady,
                showLiveState
            );

        string heading =
            $"{index + 1}. {interaction.type}  {stateText}";

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
            DrawValueRow(
                "Cooldown Remaining",
                $"{cooldownRemaining:0.00}s",
                cooldownReady
                    ? MetColor
                    : WaitingColor
            );

            DrawValueRow(
                "All Conditions",
                conditionsMet
                    ? "✅ MET"
                    : "❌ BLOCKED",
                conditionsMet
                    ? MetColor
                    : FailedColor
            );
        }

        DrawTargetTags(interaction);

        EditorGUILayout.Space(5f);

        DrawConditions(
            agent,
            interaction,
            showLiveState
        );

        EditorGUILayout.EndVertical();
    }

    private void DrawTargetTags(
        AgentInteraction interaction)
    {
        string targetTags = "None";

        if (interaction.targetTags != null &&
            interaction.targetTags.Count > 0)
        {
            targetTags = string.Join(
                ", ",
                interaction.targetTags
            );
        }

        DrawValueRow(
            "Target Tags",
            targetTags
        );
    }

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

    private void DrawConfiguredCondition(
        Condition condition,
        int index)
    {
        string invertText =
            condition.invert
                ? " [INVERTED]"
                : string.Empty;

        EditorGUILayout.LabelField(
            $"  {index + 1}. {condition.condition}{invertText}"
        );
    }

    private void DrawLiveCondition(
        Agent agent,
        AgentInteraction interaction,
        Condition condition,
        int index)
    {
        bool rawResult =
            EvaluateRawCondition(
                agent,
                condition
            );

        bool finalResult =
            condition.invert
                ? !rawResult
                : rawResult;

        string icon =
            finalResult ? "✅" : "❌";

        string invertText =
            condition.invert
                ? " [INVERTED]"
                : string.Empty;

        string label =
            $"{icon} {index + 1}. " +
            $"{condition.condition}" +
            invertText;

        EditorGUILayout.LabelField(
            label,
            GetColoredStyle(
                finalResult
                    ? MetColor
                    : FailedColor
            )
        );

        EditorGUI.indentLevel++;

        DrawValueRow(
            "Raw State",
            rawResult ? "True" : "False",
            rawResult
                ? MetColor
                : FailedColor
        );

        if (condition.invert)
        {
            DrawValueRow(
                "After Invert",
                finalResult ? "True" : "False",
                finalResult
                    ? MetColor
                    : FailedColor
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
                bool requiresDay =
                    condition.conditionValueToBeTrue > 0.5f;

                string requiredTime =
                    requiresDay
                        ? "Day"
                        : "Night";

                string currentTime =
                    TimeManager.Instance == null
                        ? "No TimeManager"
                        : TimeManager.Instance.IsDaytime
                            ? "Day"
                            : "Night";

                DrawValueRow(
                    "Required",
                    requiredTime
                );

                DrawValueRow(
                    "Current",
                    currentTime
                );

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
            {
                Agent tracked =
                    agent.GetCurrentTarget(
                        InteractionType.Track
                    );

                DrawObjectRow(
                    "Track Target",
                    tracked
                );

                break;
            }

            case ConditionType.targetDead:
            {
                Agent tracked =
                    agent.GetCurrentTarget(
                        InteractionType.Track
                    );

                DrawObjectRow(
                    "Track Target",
                    tracked
                );

                if (tracked != null)
                {
                    DrawValueRow(
                        "Dead",
                        tracked.isDead
                            ? "Yes"
                            : "No"
                    );
                }

                break;
            }

            case ConditionType.targetHasInventory:
            {
                Agent tracked =
                    agent.GetCurrentTarget(
                        InteractionType.Track
                    );

                DrawObjectRow(
                    "Track Target",
                    tracked
                );

                if (tracked != null)
                {
                    DrawValueRow(
                        "Has Inventory",
                        tracked.HasInventoryItems()
                            ? "Yes"
                            : "No"
                    );
                }

                break;
            }

            case ConditionType.doneTrackingTarget:
            {
                Agent tracked =
                    agent.GetCurrentTarget(
                        InteractionType.Track
                    );

                DrawObjectRow(
                    "Track Target",
                    tracked
                );

                if (tracked != null)
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

                break;
            }

            case ConditionType.carryingFullLoad:
                DrawValueRow(
                    "Carried Loot",
                    $"{agent.CarriedLoot}/{agent.maxCarryCapacity}"
                );

                DrawValueRow(
                    "Free Capacity",
                    agent.FreeCarryCapacity.ToString()
                );
                break;
        }
    }

    private bool EvaluateRawCondition(
        Agent agent,
        Condition condition)
    {
        if (agent == null ||
            condition == null ||
            EvaluateConditionMethod == null)
        {
            return false;
        }

        try
        {
            object result =
                EvaluateConditionMethod.Invoke(
                    null,
                    new object[]
                    {
                        agent,
                        condition
                    }
                );

            return result is bool boolResult &&
                   boolResult;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private AgentInteraction GetActiveInteraction(
        ActionManager actions)
    {
        if (actions == null ||
            ActiveInteractionField == null)
        {
            return null;
        }

        return ActiveInteractionField.GetValue(actions)
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

    private Agent GetActiveTarget(
        ActionManager actions)
    {
        if (actions == null ||
            ActiveTargetField == null)
        {
            return null;
        }

        return ActiveTargetField.GetValue(actions)
            as Agent;
    }

    private List<float> GetCooldownTimers(
        ActionManager actions)
    {
        if (actions == null ||
            CooldownTimersField == null)
        {
            return null;
        }

        return CooldownTimersField.GetValue(actions)
            as List<float>;
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

    private string GetInteractionStateText(
        bool isActive,
        bool conditionsMet,
        bool cooldownReady,
        bool showLiveState)
    {
        if (!showLiveState)
            return string.Empty;

        if (isActive)
            return "▶ ACTIVE";

        if (!cooldownReady)
            return "⏳ COOLDOWN";

        if (!conditionsMet)
            return "❌ BLOCKED";

        return "✅ READY";
    }

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

        GUIStyle style =
            GetColoredStyle(valueColor);

        EditorGUILayout.LabelField(
            value,
            style
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

        style.normal.textColor = color;
        style.wordWrap = true;

        return style;
    }
}

#endif
