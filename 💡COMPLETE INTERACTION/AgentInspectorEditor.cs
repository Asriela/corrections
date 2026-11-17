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
            agent.WorkSchedule.
