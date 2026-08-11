using System.Collections.Generic;
using UnityEngine;

// =====================================================
// 🤝 ACTION MANAGER
// Owns the full lifecycle of an agent's social actions:
// selecting what to do, picking/validating a target, moving
// into position, checking range, executing (via SocialActions),
// managing cooldowns, and cancelling an in-progress plan.
//
// One instance lives per Agent (see Agent.Awake), same pattern
// as AgentVisuals / AgentSpending / AgentSchedule.
// =====================================================
public class ActionManager
{
    private readonly Agent agent;

    // ---- active plan state ----
    private AgentInteraction activeInteraction;
    private int activeInteractionIndex = -1;
    private Agent activeTarget;

    // ---- decision + cooldown bookkeeping ----
    private float decisionTickAccumulator = 0f;
    private readonly List<float> cooldownTimers = new List<float>();

    public Agent CurrentTarget => activeTarget;
    public bool HasActivePlan => activeTarget != null;

    public ActionManager(Agent agent)
    {
        this.agent = agent;

        // ✅ PERF: same desync reasoning as Agent.Awake's spend/leave jitter —
        // otherwise every agent's decision tick lands on the same frame.
        decisionTickAccumulator = Random.Range(0f, Mathf.Max(0.01f, agent.interactionDecisionInterval));
    }

    // =====================================================
    // 🔁 MAIN TICK — called once per frame from AgentSchedule
    // =====================================================
    public void UpdateActions()
    {
        if (agent.isDead || agent.isLeavingTown)
        {
            ResetAllState();
            return;
        }

        AdvanceCooldowns();

        var data = AgentData.Instance?.GetAgent(agent.type);
        if (data == null || data.interactions == null || data.interactions.Count == 0)
            return;

        SyncCooldownListLength(data.interactions.Count);

        // ✅ once a plan is committed, keep pursuing the same target/interaction
        // instead of re-rolling every decision tick — the agent no longer
        // changes its mind mid-approach.
        if (activeTarget != null)
        {
            PursueActivePlan();
            return;
        }

        decisionTickAccumulator += Time.deltaTime;
        if (decisionTickAccumulator < agent.interactionDecisionInterval)
            return;

        decisionTickAccumulator = 0f;
        SelectAction(data);
    }

    // =====================================================
    // 🎯 ACTION SELECTION
    // =====================================================
    private void SelectAction(ScriptableAgent data)
    {
        AgentInteraction chosen = null;
        int chosenIndex = -1;
        int bestPriority = int.MinValue;

        for (int i = 0; i < data.interactions.Count; i++)
        {
            var interaction = data.interactions[i];

            if (cooldownTimers[i] > 0f)
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

        Agent target = FindNearestValidTarget(chosen);
        if (target == null)
            return;

        activeInteraction = chosen;
        activeInteractionIndex = chosenIndex;
        activeTarget = target;

        PursueActivePlan();
    }

    // =====================================================
    // 🔎 TARGET SELECTION + VALIDATION
    // =====================================================
    private Agent FindNearestValidTarget(AgentInteraction interaction)
    {
        var allAgents = GameManager.Instance?.AllAgents;
        if (allAgents == null)
            return null;

        Agent bestTarget = null;
        float bestDist = float.MaxValue;
        Vector3 selfPos = agent.transform.position;

        foreach (var candidates in allAgents)
        {
            foreach (var candidate in candidates.Value)
            {
                if (candidate == null || candidate == agent || candidate.isDead || candidate.IsLeavingTown)
                    continue;

                if (!candidate.HasAnyTag(interaction.targetTags))
                    continue;

                float dist = Vector3.Distance(selfPos, candidate.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = candidate;
                }
            }
        }

        return bestTarget;
    }

    private bool ValidateActiveTarget()
    {
        if (activeTarget == null)
            return false;

        if (activeTarget.isDead || activeTarget.IsLeavingTown)
            return false;

        if (!activeTarget.HasAnyTag(activeInteraction.targetTags))
            return false;

        return true;
    }

    // =====================================================
    // 🏃 PLAN PURSUIT — movement, alignment, range, execution
    // =====================================================
    private void PursueActivePlan()
    {
        if (!ValidateActiveTarget())
        {
            ClearPlan();
            return;
        }

        MoveTowardTarget(activeTarget, activeInteraction);

        if (!IsInRange(activeTarget, activeInteraction.range))
            return;

        SocialActions.Execute(agent, activeTarget, activeInteraction);

        if (activeInteractionIndex >= 0 && activeInteractionIndex < cooldownTimers.Count)
            cooldownTimers[activeInteractionIndex] = activeInteraction.cooldown;

        // ✅ plan resolves on execution — cooldown now gates this slot, and the
        // next decision tick re-evaluates fresh, same pacing as before.
        ClearPlan();
    }

    // =====================================================
    // 🎯 MOVEMENT + ALIGNMENT + RANGE
    // =====================================================
    private void MoveTowardTarget(Agent target, AgentInteraction interaction)
    {
        Vector3 selfPos = agent.transform.position;
        Vector3 targetPos = target.transform.position;

        agent.movementType = interaction.canEnterTargetBuilding
            ? MovementType.AllowTargetBuildingOnly
            : MovementType.AvoidAllBuildings;

        // ✅ combat targeting is mutually exclusive with hangout-roaming state
        agent.roamInsideTarget = false;

        Vector2Int targetTileNow = AgentPathing.WorldToGrid(agent, targetPos);
        agent.targetTile = targetTileNow;

        if (interaction.canEnterTargetBuilding)
        {
            // the target lives inside a building tile (e.g. a pig in its pen) —
            // route around every OTHER building to reach it instead of walking
            // in a straight line through walls.
            ApproachTileByPathfinding(targetTileNow, targetPos, interaction);
        }
        else
        {
            // Open-field: already aligned?
            float yDiff = Mathf.Abs(selfPos.y - targetPos.y);
            float xDiff = Mathf.Abs(selfPos.x - targetPos.x);

            if (yDiff <= interaction.alignmentTolerance && xDiff <= interaction.range)
            {
                agent.Visuals.UpdateFacingFromDirection(targetPos - selfPos);
                return;
            }

            // stand-off positioning
            float side = selfPos.x <= targetPos.x ? -1f : 1f;
            if (Mathf.Abs(selfPos.x - targetPos.x) < 0.001f)
                side = 1f;

            Vector3 desiredPosition = new Vector3(
                targetPos.x + (side * interaction.standOffDistance),
                targetPos.y,
                selfPos.z
            );

            if (!AgentPathing.TryGetCurrentPathTarget(agent, out Vector3 currentPathTarget) ||
                (currentPathTarget - desiredPosition).sqrMagnitude > 0.05f)
            {
                AgentMovement.SetPath(agent, new List<Vector3> { desiredPosition });
            }
        }

        agent.Visuals.UpdateFacingFromDirection(targetPos - selfPos);
    }

    private void ApproachTileByPathfinding(Vector2Int goalTile, Vector3 targetWorldPos, AgentInteraction interaction)
    {
        float distanceToTarget = Vector3.Distance(agent.transform.position, targetWorldPos);

        if (distanceToTarget <= interaction.range)
        {
            // already close enough to attack — stop here instead of continuing
            // to walk the rest of the path onto the target's exact tile center
            if (agent.currentPath.Count > 0)
                AgentMovement.ClearPath(agent);

            return;
        }

        Vector2Int selfTile = AgentPathing.WorldToGrid(agent, agent.transform.position);

        // ✅ nothing blocking a straight line to the target — skip tile-based
        // A* entirely and steer at its live position, at any distance. Tile
        // pathing quantizes to tile centers/borders, too coarse to track a
        // continuously-moving target: it can drift enough within a tile to
        // slip past without ever crossing a tile boundary, and re-pathing on
        // a tile-cross is free to choose a different route than last time,
        // which reads as backtracking. Direct pursuit just re-aims every
        // frame, so neither problem exists. Checked fresh every frame, so it
        // falls back to A* the instant something actually gets in the way.
        if (AgentPathing.HasClearLine(agent, selfTile, goalTile))
        {
            if (!AgentPathing.TryGetCurrentPathTarget(agent, out Vector3 currentPathTarget) ||
                (currentPathTarget - targetWorldPos).sqrMagnitude > 0.05f)
            {
                AgentMovement.SetPath(agent, new List<Vector3> { targetWorldPos });
            }

            return;
        }

        // obstructed — fall back to tile-based A* to route around whatever's in the way
        Vector2Int currentDestinationTile = agent.currentPath.Count > 0
            ? AgentPathing.WorldToGrid(agent, agent.currentPath[agent.currentPath.Count - 1])
            : goalTile;

        bool needsNewPath = agent.currentPath.Count == 0 || currentDestinationTile != goalTile;

        if (!needsNewPath)
            return;

        if (AgentPathing.TryFindPathToTile(agent, goalTile, out List<Vector3> worldPath))
        {
            AgentMovement.SetPath(agent, worldPath);
        }
    }

    private bool IsInRange(Agent target, float range)
    {
        if (target == null)
            return false;

        return Vector3.Distance(agent.transform.position, target.transform.position) <= range;
    }

    // =====================================================
    // 🧹 COOLDOWNS
    // =====================================================
    private void AdvanceCooldowns()
    {
        for (int i = 0; i < cooldownTimers.Count; i++)
        {
            if (cooldownTimers[i] > 0f)
                cooldownTimers[i] = Mathf.Max(0f, cooldownTimers[i] - Time.deltaTime);
        }
    }

    private void SyncCooldownListLength(int count)
    {
        while (cooldownTimers.Count < count)
            cooldownTimers.Add(0f);
    }

    // =====================================================
    // ❌ CANCELLATION / RESET
    // =====================================================

    // Called externally (e.g. AgentFear when a fright kicks in) to drop
    // whatever social action is in progress without touching cooldowns —
    // the plan is abandoned, not "used up."
    public void CancelCurrentAction()
    {
        ClearPlan();
    }

    private void ClearPlan()
    {
        activeInteraction = null;
        activeInteractionIndex = -1;
        activeTarget = null;
    }

    // Called while dead/leaving town — mirrors the old ResetAllInteractionState,
    // zeroes cooldowns in place rather than discarding the list.
    private void ResetAllState()
    {
        ClearPlan();
        decisionTickAccumulator = 0f;

        for (int i = 0; i < cooldownTimers.Count; i++)
            cooldownTimers[i] = 0f;
    }

    // Called from Agent.SetRole / Agent.SetDeadState — a full reset, since the
    // interactions list itself may change length with the new role.
    public void Reset()
    {
        ClearPlan();
        decisionTickAccumulator = 0f;
        cooldownTimers.Clear();
    }
}
