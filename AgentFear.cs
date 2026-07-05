// AgentFear.cs
using System.Collections.Generic;
using UnityEngine;

public static class AgentFear
{
    // Called whenever a damage-dealing interaction actually lands, regardless of
    // the resulting damage number — an attack from a scary tag is scary even at
    // 0 damage (e.g. a ghost that only ever deals token damage but still haunts).
    public static void TryTriggerFright(Agent scaredAgent, Agent attacker)
    {
        if (scaredAgent == null || attacker == null || scaredAgent.isDead)
            return;

        if (scaredAgent.ScaredOfTags == null || scaredAgent.ScaredOfTags.Count == 0)
            return;

        if (!AgentActions.AgentHasAnyTag(attacker, scaredAgent.ScaredOfTags))
            return;

        RunFromDanger(scaredAgent);
    }

    private static void RunFromDanger(Agent agent)
    {
        if (agent == null || agent.isDead)
            return;

        // fear overrides whatever the agent was doing
        agent.interactionTarget = null;
        AgentMovement.ClearPath(agent);

        bool isResident = !agent.outsider;

        if (isResident && agent.myHouse != null)
        {
            RunHome(agent);
        }
        else
        {
            AgentLeaving.ForceImmediateLeave(agent);
        }
    }

    private static void RunHome(Agent agent)
    {
        Vector2Int houseTile = AgentPathing.WorldToGrid(agent, agent.myHouse.transform.position);
        Vector2Int currentTile = AgentPathing.WorldToGrid(agent, agent.transform.position);

        // house tiles are "blocked" from the pathfinder's perspective (it's a
        // building), so this has to be allowed as the one exception, same as
        // the fox-entering-the-pig's-pen case.
        agent.movementType = MovementType.AllowTargetBuildingOnly;
        agent.targetTile = houseTile;

        if (currentTile == houseTile)
        {
            // already home — just hunker down
            agent.roamInsideTarget = true;
            return;
        }

        if (AgentPathing.TryFindPathToTile(agent, houseTile, out List<Vector3> worldPath) && worldPath.Count > 0)
        {
            AgentMovement.SetPath(agent, worldPath);
        }
        else
        {
            // pathfinding failed for some reason — a straight line home beats standing still in danger
            AgentMovement.SetPath(agent, new List<Vector3> { agent.myHouse.transform.position });
        }

        // SetPath resets roamInsideTarget as a safety default — re-apply so the
        // agent shelters in place (like the pig's pen) once they arrive home,
        // instead of immediately wandering back out into danger.
        agent.roamInsideTarget = true;
    }
}
