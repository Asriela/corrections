// TrackTargetDebugWindow.cs
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class TrackTargetDebugWindow : EditorWindow
{
    private Agent selectedAgent;

    private Vector2 reportScrollPosition;
    private Vector2 interactionScrollPosition;

    private string reportText =
        "Select an Agent, then click Generate Report.";

    private bool autoGenerateWhenPaused = true;
    private bool wasPaused;

    private readonly List<AgentInteraction>
        availableTrackInteractions =
            new List<AgentInteraction>();

    private readonly List<string>
        availableTrackInteractionNames =
            new List<string>();

    private int selectedTrackInteractionIndex;

    // =====================================================
    // WINDOW
    // =====================================================

    [MenuItem("Tools/Agents/Track Target Debugger")]
    public static void OpenWindow()
    {
        TrackTargetDebugWindow window =
            GetWindow<TrackTargetDebugWindow>();

        window.titleContent =
            new GUIContent("Track Target Debugger");

        window.minSize =
            new Vector2(650f, 450f);

        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        Selection.selectionChanged += OnUnitySelectionChanged;

        wasPaused =
            EditorApplication.isPaused;

        TryUseSelectedAgent();
        RefreshTrackInteractions();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Selection.selectionChanged -= OnUnitySelectionChanged;
    }

    // =====================================================
    // AUTOMATIC PAUSE SNAPSHOT
    // =====================================================

    private void OnEditorUpdate()
    {
        bool isPaused =
            EditorApplication.isPaused;

        bool justPaused =
            isPaused &&
            !wasPaused;

        wasPaused =
            isPaused;

        if (justPaused &&
            autoGenerateWhenPaused &&
            Application.isPlaying)
        {
            TryUseSelectedAgent();
            RefreshTrackInteractions();
            GenerateReport();
            Repaint();
        }
    }

    private void OnUnitySelectionChanged()
    {
        TryUseSelectedAgent();
        RefreshTrackInteractions();
        Repaint();
    }

    private void TryUseSelectedAgent()
    {
        if (Selection.activeGameObject == null)
            return;

        Agent selection =
            Selection.activeGameObject.GetComponent<Agent>();

        if (selection == null)
        {
            selection =
                Selection.activeGameObject
                    .GetComponentInParent<Agent>();
        }

        if (selection == null ||
            selection == selectedAgent)
        {
            return;
        }

        selectedAgent = selection;
        selectedTrackInteractionIndex = 0;

        reportText =
            $"Selected {FormatAgent(selectedAgent)}.\n\n" +
            "Click Generate Report, or pause Play Mode " +
            "with automatic pause snapshots enabled.";
    }

    // =====================================================
    // GUI
    // =====================================================

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(6f);

        DrawAgentSelection();

        EditorGUILayout.Space(6f);

        DrawInteractionSelection();

        EditorGUILayout.Space(8f);

        DrawReportControls();

        EditorGUILayout.Space(8f);

        DrawReport();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label(
            "Track Target Search Report",
            EditorStyles.boldLabel
        );

        GUILayout.FlexibleSpace();

        string playState =
            !Application.isPlaying
                ? "Edit Mode"
                : EditorApplication.isPaused
                    ? "Paused"
                    : "Playing";

        GUILayout.Label(
            playState,
            EditorStyles.miniLabel
        );

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAgentSelection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            "Agent Being Inspected",
            EditorStyles.boldLabel
        );

        EditorGUI.BeginChangeCheck();

        Agent newAgent =
            EditorGUILayout.ObjectField(
                "Agent",
                selectedAgent,
                typeof(Agent),
                true
            ) as Agent;

        if (EditorGUI.EndChangeCheck())
        {
            selectedAgent = newAgent;
            selectedTrackInteractionIndex = 0;

            RefreshTrackInteractions();

            reportText =
                selectedAgent == null
                    ? "Select an Agent."
                    : $"Selected {FormatAgent(selectedAgent)}.\n\n" +
                      "Click Generate Report.";
        }

        if (GUILayout.Button(
                "Use Currently Selected Agent",
                GUILayout.Height(24f)))
        {
            Agent previousAgent =
                selectedAgent;

            TryUseSelectedAgent();

            if (selectedAgent == previousAgent &&
                Selection.activeGameObject != null)
            {
                Agent selection =
                    Selection.activeGameObject
                        .GetComponentInParent<Agent>();

                if (selection != null)
                    selectedAgent = selection;
            }

            selectedTrackInteractionIndex = 0;
            RefreshTrackInteractions();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawInteractionSelection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            "Track Interaction",
            EditorStyles.boldLabel
        );

        if (selectedAgent == null)
        {
            EditorGUILayout.HelpBox(
                "Select an Agent first.",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
            return;
        }

        if (availableTrackInteractions.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"{selectedAgent.type} has no configured " +
                "Track interactions.",
                MessageType.Warning
            );

            EditorGUILayout.EndVertical();
            return;
        }

        selectedTrackInteractionIndex =
            Mathf.Clamp(
                selectedTrackInteractionIndex,
                0,
                availableTrackInteractions.Count - 1
            );

        selectedTrackInteractionIndex =
            EditorGUILayout.Popup(
                "Interaction",
                selectedTrackInteractionIndex,
                availableTrackInteractionNames.ToArray()
            );

        AgentInteraction interaction =
            GetSelectedTrackInteraction();

        if (interaction != null)
        {
            EditorGUILayout.LabelField(
                "Requested Tags",
                FormatTags(interaction.targetTags)
            );

            EditorGUILayout.LabelField(
                "Chance",
                $"{interaction.chance * 100f:0.#}%"
            );

            EditorGUILayout.LabelField(
                "Priority",
                interaction.priority.ToString()
            );
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawReportControls()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        autoGenerateWhenPaused =
            EditorGUILayout.ToggleLeft(
                "Automatically generate one snapshot when Play Mode is paused",
                autoGenerateWhenPaused
            );

        EditorGUILayout.BeginHorizontal();

        GUI.enabled =
            Application.isPlaying &&
            selectedAgent != null &&
            availableTrackInteractions.Count > 0;

        if (GUILayout.Button(
                "Generate Report",
                GUILayout.Height(30f)))
        {
            GenerateReport();
        }

        GUI.enabled = true;

        if (GUILayout.Button(
                "Copy Report",
                GUILayout.Height(30f),
                GUILayout.Width(110f)))
        {
            EditorGUIUtility.systemCopyBuffer =
                reportText;
        }

        if (GUILayout.Button(
                "Clear",
                GUILayout.Height(30f),
                GUILayout.Width(80f)))
        {
            reportText =
                "Report cleared.";
        }

        EditorGUILayout.EndHorizontal();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode before generating a report. " +
                "The report reads the live GameManager.AllAgents registry.",
                MessageType.Info
            );
        }
        else if (!EditorApplication.isPaused)
        {
            EditorGUILayout.HelpBox(
                "You can generate while the game is running, " +
                "but pausing first gives you a stable snapshot.",
                MessageType.Info
            );
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawReport()
    {
        EditorGUILayout.LabelField(
            "Report Output",
            EditorStyles.boldLabel
        );

        GUIStyle textStyle =
            new GUIStyle(EditorStyles.textArea);

        textStyle.wordWrap = false;
        textStyle.richText = false;

        reportScrollPosition =
            EditorGUILayout.BeginScrollView(
                reportScrollPosition,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

        EditorGUILayout.TextArea(
            reportText,
            textStyle,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true)
        );

        EditorGUILayout.EndScrollView();
    }

    // =====================================================
    // INTERACTION DISCOVERY
    // =====================================================

    private void RefreshTrackInteractions()
    {
        availableTrackInteractions.Clear();
        availableTrackInteractionNames.Clear();

        if (selectedAgent == null ||
            AgentData.Instance == null)
        {
            return;
        }

        ScriptableAgent data =
            AgentData.Instance.GetAgent(
                selectedAgent.type
            );

        if (data == null ||
            data.interactions == null)
        {
            return;
        }

        for (int i = 0;
             i < data.interactions.Count;
             i++)
        {
            AgentInteraction interaction =
                data.interactions[i];

            if (interaction == null ||
                interaction.type != InteractionType.Track)
            {
                continue;
            }

            availableTrackInteractions.Add(
                interaction
            );

            availableTrackInteractionNames.Add(
                $"#{i + 1} Track | " +
                $"Tags: {FormatTags(interaction.targetTags)} | " +
                $"Priority: {interaction.priority}"
            );
        }

        selectedTrackInteractionIndex =
            Mathf.Clamp(
                selectedTrackInteractionIndex,
                0,
                Mathf.Max(
                    0,
                    availableTrackInteractions.Count - 1
                )
            );
    }

    private AgentInteraction GetSelectedTrackInteraction()
    {
        if (availableTrackInteractions.Count == 0)
            return null;

        selectedTrackInteractionIndex =
            Mathf.Clamp(
                selectedTrackInteractionIndex,
                0,
                availableTrackInteractions.Count - 1
            );

        return availableTrackInteractions[
            selectedTrackInteractionIndex
        ];
    }

    // =====================================================
    // REPORT GENERATION
    // =====================================================

    private void GenerateReport()
    {
        if (!Application.isPlaying)
        {
            reportText =
                "Cannot generate the report outside Play Mode.";

            return;
        }

        if (selectedAgent == null)
        {
            reportText =
                "No Agent is selected.";

            return;
        }

        RefreshTrackInteractions();

        AgentInteraction interaction =
            GetSelectedTrackInteraction();

        if (interaction == null)
        {
            reportText =
                $"{selectedAgent.type} has no configured " +
                "Track interaction.";

            return;
        }

        StringBuilder report =
            new StringBuilder(4096);

        WriteReportHeader(
            report,
            interaction
        );

        if (GameManager.Instance == null)
        {
            report.AppendLine(
                "RESULT: GameManager.Instance is null."
            );

            reportText = report.ToString();
            return;
        }

        var allAgents =
            GameManager.Instance.AllAgents;

        if (allAgents == null)
        {
            report.AppendLine(
                "RESULT: GameManager.AllAgents is null."
            );

            reportText = report.ToString();
            return;
        }

        int totalEntries = 0;
        int validTargets = 0;
        int matchingTagCandidates = 0;

        Agent bestTarget = null;
        float bestScore = float.MinValue;

        Agent currentTarget =
            selectedAgent.GetCurrentTarget(
                InteractionType.Track
            );

        report.AppendLine("CURRENT TRACK TARGET");
        report.AppendLine("--------------------");
        report.AppendLine(
            FormatAgent(currentTarget)
        );
        report.AppendLine();

        report.AppendLine("CANDIDATE RESULTS");
        report.AppendLine("=================");

        foreach (var group in allAgents)
        {
            if (group.Value == null)
            {
                report.AppendLine(
                    $"GROUP {group.Key}: agent list is null."
                );

                report.AppendLine();
                continue;
            }

            foreach (Agent candidate in group.Value)
            {
                totalEntries++;

                if (candidate == null)
                {
                    report.AppendLine(
                        $"[{totalEntries}] NULL AGENT"
                    );

                    report.AppendLine(
                        $"Registry group: {group.Key}"
                    );

                    report.AppendLine(
                        "Result: REJECTED — candidate is null"
                    );

                    report.AppendLine();
                    continue;
                }

                List<AgentTag> matchingTags =
                    GetMatchingTags(
                        candidate,
                        interaction.targetTags
                    );

                if (matchingTags.Count > 0)
                    matchingTagCandidates++;

                List<string> rejectionReasons =
                    GetTrackRejectionReasons(
                        selectedAgent,
                        candidate,
                        interaction
                    );

                bool valid =
                    rejectionReasons.Count == 0;

                float score =
                    valid
                        ? CalculateTrackingScore(
                            selectedAgent,
                            candidate,
                            currentTarget
                        )
                        : float.MinValue;

                if (valid)
                {
                    validTargets++;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = candidate;
                    }
                }

                WriteCandidateResult(
                    report,
                    totalEntries,
                    group.Key.ToString(),
                    candidate,
                    interaction,
                    matchingTags,
                    rejectionReasons,
                    valid,
                    score
                );
            }
        }

        WriteReportSummary(
            report,
            interaction,
            totalEntries,
            matchingTagCandidates,
            validTargets,
            bestTarget,
            bestScore
        );

        reportText =
            report.ToString();

        reportScrollPosition =
            Vector2.zero;
    }

    private void WriteReportHeader(
        StringBuilder report,
        AgentInteraction interaction)
    {
        report.AppendLine(
            "TRACK TARGET SEARCH SNAPSHOT"
        );

        report.AppendLine(
            "============================"
        );

        report.AppendLine(
            $"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}"
        );

        report.AppendLine(
            $"Play state: " +
            $"{(EditorApplication.isPaused ? "Paused" : "Playing")}"
        );

        report.AppendLine(
            $"Tracking agent: {FormatAgent(selectedAgent)}"
        );

        report.AppendLine(
            $"Tracking agent tags: " +
            $"{FormatTags(selectedAgent.tags)}"
        );

        report.AppendLine(
            $"Requested target tags: " +
            $"{FormatTags(interaction.targetTags)}"
        );

        report.AppendLine(
            $"Target kind: {interaction.targetKind}"
        );

        report.AppendLine(
            $"Interaction priority: {interaction.priority}"
        );

        report.AppendLine(
            $"Interaction chance: " +
            $"{interaction.chance * 100f:0.#}%"
        );

        report.AppendLine();
    }

    private void WriteCandidateResult(
        StringBuilder report,
        int candidateNumber,
        string registryGroup,
        Agent candidate,
        AgentInteraction interaction,
        List<AgentTag> matchingTags,
        List<string> rejectionReasons,
        bool valid,
        float score)
    {
        report.AppendLine(
            $"[{candidateNumber}] " +
            $"{FormatAgent(candidate)}"
        );

        report.AppendLine(
            $"Registry group: {registryGroup}"
        );

        report.AppendLine(
            $"Owned tags: " +
            $"{FormatTags(candidate.tags)}"
        );

        report.AppendLine(
            $"Requested tags: " +
            $"{FormatTags(interaction.targetTags)}"
        );

        report.AppendLine(
            $"Matching tags: " +
            $"{FormatTags(matchingTags)}"
        );

        report.AppendLine(
            $"Dead: {candidate.isDead}"
        );

        report.AppendLine(
            $"Leaving town: {candidate.IsLeavingTown}"
        );

        float progress =
            candidate.GetSocialProgress(
                InteractionType.Track
            );

        report.AppendLine(
            $"Track progress: {progress:0.##}/100"
        );

        bool hasAgentData =
            AgentData.Instance != null &&
            AgentData.Instance.GetAgent(
                candidate.type
            ) != null;

        report.AppendLine(
            $"AgentData entry: " +
            $"{(hasAgentData ? "Found" : "Missing")}"
        );

        if (valid)
        {
            report.AppendLine(
                "Result: VALID TRACK TARGET"
            );

            report.AppendLine(
                $"Tracking score: {score:0.##}"
            );
        }
        else
        {
            report.AppendLine(
                "Result: REJECTED"
            );

            foreach (string reason in rejectionReasons)
            {
                report.AppendLine(
                    $"  - {reason}"
                );
            }
        }

        report.AppendLine();
    }

    private void WriteReportSummary(
        StringBuilder report,
        AgentInteraction interaction,
        int totalEntries,
        int matchingTagCandidates,
        int validTargets,
        Agent bestTarget,
        float bestScore)
    {
        report.AppendLine("SUMMARY");
        report.AppendLine("=======");

        report.AppendLine(
            $"Requested tags: " +
            $"{FormatTags(interaction.targetTags)}"
        );

        report.AppendLine(
            $"Registered entries checked: {totalEntries}"
        );

        report.AppendLine(
            $"Candidates with matching tags: " +
            $"{matchingTagCandidates}"
        );

        report.AppendLine(
            $"Valid Track targets: {validTargets}"
        );

        report.AppendLine(
            $"Expected selected target: " +
            $"{FormatAgent(bestTarget)}"
        );

        if (bestTarget != null)
        {
            report.AppendLine(
                $"Expected selected score: {bestScore:0.##}"
            );
        }

        report.AppendLine();

        if (interaction.targetTags == null)
        {
            report.AppendLine(
                "IMPORTANT: targetTags is NULL. " +
                "HasAnyTag will reject every candidate."
            );
        }
        else if (interaction.targetTags.Count == 0)
        {
            report.AppendLine(
                "IMPORTANT: targetTags is EMPTY. " +
                "The current HasAnyTag implementation treats " +
                "an empty list as matching nobody."
            );
        }
        else if (matchingTagCandidates == 0)
        {
            report.AppendLine(
                "IMPORTANT: no registered agent owns any " +
                "of the requested target tags."
            );
        }
        else if (validTargets == 0)
        {
            report.AppendLine(
                "Agents with matching tags were found, but " +
                "every one failed another Track requirement."
            );
        }
        else
        {
            report.AppendLine(
                "At least one valid Track target exists. " +
                "If Track is still not selected, inspect the " +
                "other ActionManager gates such as chance, " +
                "cooldown, priority and active-plan blocking."
            );
        }
    }

    // =====================================================
    // EXACT TRACK REQUIREMENTS
    // =====================================================

    private static List<string> GetTrackRejectionReasons(
        Agent trackingAgent,
        Agent candidate,
        AgentInteraction interaction)
    {
        List<string> reasons =
            new List<string>();

        if (candidate == null)
        {
            reasons.Add(
                "Candidate is null."
            );

            return reasons;
        }

        if (candidate == trackingAgent)
        {
            reasons.Add(
                "Candidate is the tracking agent itself."
            );
        }

        if (candidate.isDead)
        {
            reasons.Add(
                "Candidate is dead."
            );
        }

        if (candidate.IsLeavingTown)
        {
            reasons.Add(
                "Candidate is leaving town."
            );
        }

        if (interaction.targetTags == null)
        {
            reasons.Add(
                "Interaction targetTags is null."
            );
        }
        else if (interaction.targetTags.Count == 0)
        {
            reasons.Add(
                "Interaction targetTags is empty."
            );
        }
        else if (!candidate.HasAnyTag(
                     interaction.targetTags))
        {
            reasons.Add(
                "Candidate owns none of the requested tags."
            );
        }

        if (AgentData.Instance == null)
        {
            reasons.Add(
                "AgentData.Instance is null."
            );
        }
        else if (AgentData.Instance.GetAgent(
                     candidate.type) == null)
        {
            reasons.Add(
                $"AgentData has no entry for {candidate.type}."
            );
        }

        float progress =
            candidate.GetSocialProgress(
                InteractionType.Track
            );

        if (progress >= 100f)
        {
            reasons.Add(
                $"Track progress is already " +
                $"{progress:0.##}/100."
            );
        }

        return reasons;
    }

    private static float CalculateTrackingScore(
        Agent trackingAgent,
        Agent candidate,
        Agent currentTarget)
    {
        const float SwitchPenalty = 25f;

        if (trackingAgent == null ||
            candidate == null)
        {
            return float.MinValue;
        }

        float score = 0f;

        ScriptableAgent candidateData =
            AgentData.Instance != null
                ? AgentData.Instance.GetAgent(
                    candidate.type
                )
                : null;

        if (candidateData != null &&
            candidateData.startingInventory != null)
        {
            foreach (InventoryItem drop in
                     candidateData.startingInventory)
            {
                if (drop == null)
                    continue;

                score += drop.amount;
            }
        }

        score -= Vector3.Distance(
            trackingAgent.transform.position,
            candidate.transform.position
        );

        if (currentTarget != null &&
            candidate != currentTarget)
        {
            score -= SwitchPenalty;
        }

        return score;
    }

    // =====================================================
    // FORMATTING
    // =====================================================

    private static List<AgentTag> GetMatchingTags(
        Agent candidate,
        List<AgentTag> requestedTags)
    {
        List<AgentTag> matchingTags =
            new List<AgentTag>();

        if (candidate == null ||
            candidate.tags == null ||
            requestedTags == null)
        {
            return matchingTags;
        }

        foreach (AgentTag requestedTag in requestedTags)
        {
            if (candidate.tags.Contains(requestedTag))
            {
                matchingTags.Add(requestedTag);
            }
        }

        return matchingTags;
    }

    private static string FormatAgent(
        Agent agent)
    {
        if (agent == null)
            return "None";

        return
            $"{agent.name} ({agent.type})";
    }

    private static string FormatTags(
        List<AgentTag> tags)
    {
        if (tags == null)
            return "NULL";

        if (tags.Count == 0)
            return "EMPTY";

        return string.Join(", ", tags);
    }
}

#endif
