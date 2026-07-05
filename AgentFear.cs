// AgentFear.cs
using System.Collections.Generic;
using UnityEngine;

public static class AgentFear
{
    // Called whenever a damage-dealing interaction actually lands, regardless of
    // the resulting damage number — an attack from a scary tag is scary even at
    // 0 damage.
    public static void TryTriggerFright(Agent scaredAgent, Agent attacker)
    {
        if (scaredAgent == null || attacker == null || scaredAgent.isDead)
            return;

        if (scaredAgent.ScaredOfTags == null || scaredAgent.ScaredOfTags.Count == 0)
            return;

        if (!AgentActions.AgentHasAnyTag(attacker, scaredAgent.ScaredOfTags))
            return;

        RunFromDanger(scaredAgent, attacker);
    }

    // Called every Update() from Agent.cs.
    // Residents with homes hide until the thing that scared them is far enough away.
    // Outsiders / homeless agents still leave town permanently.
    public static void UpdateFear(Agent agent)
    {
        if (agent == null || agent.isDead)
            return;

        if (!agent.isFrightened)
            return;

        // Only agents with homes are allowed to "recover" from fear.
        // Anyone else should not be in this state, so force them out of town.
        if (agent.myHouse == null)
        {
            ClearFear(agent);
            AgentLeaving.ForceImmediateLeave(agent);
            return;
        }

        // If the fear source is gone/dead/leaving, it is safe enough to come out.
        if (agent.fearSource == null || agent.fearSource.isDead || agent.fearSource.IsLeavingTown)
        {
            StopHiding(agent);
            return;
        }

        float safeDistance = Mathf.Max(0.01f, agent.safeDistanceFromFear);
        float sqrSafeDistance = safeDistance * safeDistance;

        float sqrDistanceToThreat =
            (agent.transform.position - agent.fearSource.transform.position).sqrMagnitude;

        // Threat is far enough away — stop hiding and return to normal behavior.
        if (sqrDistanceToThreat >= sqrSafeDistance)
        {
            StopHiding(agent);
            return;
        }

        // Threat is still too close, so make sure the agent keeps hiding at home.
        EnsureStillHidingAtHome(agent);
    }

    private static void RunFromDanger(Agent agent, Agent attacker)
    {
        if (agent == null || agent.isDead)
            return;

        // Fear overrides whatever the agent was doing.
        agent.interactionTarget = null;
        AgentMovement.ClearPath(agent);

        bool canHideAtHome = !agent.outsider && agent.myHouse != null;

        if (canHideAtHome)
        {
            agent.isFrightened = true;
            agent.fearSource = attacker;

            // Stop current spend/trip state so schedule logic does not keep pulling them away.
            agent.isOnSpendTrip = false;
            agent.activeSpendBehaviourIndex = -1;
            agent.activeSpendAmount = 0f;
            agent.hasForcedSpendDestination = false;

            RunHome(agent);
        }
        else
        {
            // Outsiders and residents without homes do not recover by hiding.
            agent.isFrightened = false;
            agent.fearSource = null;

            AgentLeaving.ForceImmediateLeave(agent);
        }
    }

    private static void EnsureStillHidingAtHome(Agent agent)
    {
        if (agent == null || agent.myHouse == null)
            return;

        Vector2Int houseTile = AgentPathing.WorldToGrid(agent, agent.myHouse.transform.position);
        Vector2Int currentTile = AgentPathing.WorldToGrid(agent, agent.transform.position);

        // If already home, just keep hunkering down.
        if (currentTile == houseTile)
        {
            agent.interactionTarget = null;
            AgentMovement.ClearPath(agent);

            agent.movementType = MovementType.AllowTargetBuildingOnly;
            agent.targetTile = houseTile;
            agent.roamInsideTarget = true;
            return;
        }

        // If some schedule/spending/combat logic overwrote the destination while scared,
        // send the agent back home again.
        bool pathMissing = agent.currentPath == null || agent.currentPath.Count == 0;
        bool pathNotGoingHome = true;

        if (!pathMissing)
        {
            Vector3 finalPathPoint = agent.currentPath[agent.currentPath.Count - 1];
            Vector2Int finalPathTile = AgentPathing.WorldToGrid(agent, finalPathPoint);
            pathNotGoingHome = finalPathTile != houseTile;
        }

        if (pathMissing || pathNotGoingHome || agent.targetTile != houseTile)
        {
            agent.interactionTarget = null;

            agent.isOnSpendTrip = false;
            agent.activeSpendBehaviourIndex = -1;
            agent.activeSpendAmount = 0f;
            agent.hasForcedSpendDestination = false;

            AgentMovement.ClearPath(agent);
            RunHome(agent);
        }
    }

    private static void StopHiding(Agent agent)
    {
        if (agent == null)
            return;

        ClearFear(agent);

        AgentMovement.ClearPath(agent);

        // Return movement rules to normal town behavior.
        agent.movementType = MovementType.AvoidAllBuildings;
        agent.targetTile = Vector2Int.zero;
        agent.roamInsideTarget = false;

        agent.waitTimer = 0f;

        // Send them back into normal life.
        AgentPathing.PickNewDestination(agent);
    }

    private static void ClearFear(Agent agent)
    {
        if (agent == null)
            return;

        agent.isFrightened = false;
        agent.fearSource = null;
    }

    private static void RunHome(Agent agent)
    {
        if (agent == null || agent.myHouse == null)
            return;

        Vector2Int houseTile = AgentPathing.WorldToGrid(agent, agent.myHouse.transform.position);
        Vector2Int currentTile = AgentPathing.WorldToGrid(agent, agent.transform.position);

        // House tiles are "blocked" from the pathfinder's perspective, so this
        // has to be allowed as the one exception.
        agent.movementType = MovementType.AllowTargetBuildingOnly;
        agent.targetTile = houseTile;

        if (currentTile == houseTile)
        {
            // Already home — just hunker down.
            agent.roamInsideTarget = true;
            return;
        }

        if (AgentPathing.TryFindPathToTile(agent, houseTile, out List<Vector3> worldPath) && worldPath.Count > 0)
        {
            AgentMovement.SetPath(agent, worldPath);
        }
        else
        {
            // Pathfinding failed for some reason — a straight line home beats standing still in danger.
            AgentMovement.SetPath(agent, new List<Vector3> { agent.myHouse.transform.position });
        }

        // SetPath resets roamInsideTarget as a safety default — re-apply so the
        // agent shelters in place once they arrive home.
        agent.roamInsideTarget = true;
    }
}
