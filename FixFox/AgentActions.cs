using System.Collections.Generic;
using UnityEngine;

public static class AgentActions
{
    public static Dictionary<TeamType, List<Agent>> TeamMembers = new();

    private const float FoodTraderGoldCost = 5f;
    private const float FoodTraderFoodGain = 5f;

    // =====================================================
    // 🤝 TEAM REGISTRY
    // =====================================================

    public static void RegisterTeamMember(Agent agent, TeamType team)
    {
        if (agent == null || team == TeamType.none)
            return;

        DeregisterTeamMember(agent);

        if (!TeamMembers.TryGetValue(team, out List<Agent> members) || members == null)
        {
            members = new List<Agent>();
            TeamMembers[team] = members;
        }

        if (!members.Contains(agent))
            members.Add(agent);
    }

    public static void DeregisterTeamMember(Agent agent)
    {
        if (agent == null)
            return;

        foreach (var pair in TeamMembers)
        {
            if (pair.Value == null)
                continue;

            pair.Value.Remove(agent);
        }
    }

    // =====================================================
    // 🤝 INTERACTION SYSTEM
    // =====================================================

    public static bool AgentHasAnyTag(Agent agent, List<AgentTag> tags)
    {
        if (agent == null || tags == null || agent.tags == null)
            return false;

        foreach (var tag in tags)
        {
            if (agent.tags.Contains(tag))
                return true;
        }

        return false;
    }

    public static Agent PickInteractionTarget(Agent self, AgentInteraction interaction)
    {
        if (self == null || interaction == null)
            return null;

        var allAgents = GameManager.Instance?.AllAgents;
        if (allAgents == null)
            return null;

        Agent bestTarget = null;
        float bestDist = float.MaxValue;
        Vector3 selfPos = self.transform.position;

        foreach (var candidates in allAgents)
        {
            foreach (var candidate in candidates.Value)
            {
                if (candidate == null || candidate == self || candidate.isDead || candidate.IsLeavingTown)
                    continue;

                if (!AgentHasAnyTag(candidate, interaction.targetTags))
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

    public static bool IsInInteractionRange(Agent self, Agent target, float range)
    {
        if (self == null || target == null)
            return false;

        return Vector3.Distance(self.transform.position, target.transform.position) <= range;
    }

    // =====================================================
    // 🎯 MOVEMENT + TARGETING
    // =====================================================

    public static void MoveTowardTarget(Agent self, Agent target, AgentInteraction interaction)
    {
        if (self == null || target == null || interaction == null)
            return;

        Vector3 selfPos = self.transform.position;
        Vector3 targetPos = target.transform.position;

        self.movementType = interaction.canEnterTargetBuilding
            ? MovementType.AllowTargetBuildingOnly
            : MovementType.AvoidAllBuildings;

        // ✅ combat targeting is mutually exclusive with hangout-roaming state
        self.roamInsideTarget = false;

        Vector2Int targetTileNow = AgentPathing.WorldToGrid(self, targetPos);
        self.targetTile = targetTileNow;

        if (interaction.canEnterTargetBuilding)
        {
            // ✅ FIX: the target lives inside a building tile (e.g. a pig in its pen) —
            // route around every OTHER building to reach it instead of walking in a
            // straight line through walls.
            ApproachTileByPathfinding(self, targetTileNow, targetPos, interaction);
        }
        else
        {
            // Open-field combat: already aligned?
            float yDiff = Mathf.Abs(selfPos.y - targetPos.y);
            float xDiff = Mathf.Abs(selfPos.x - targetPos.x);

            if (yDiff <= interaction.alignmentTolerance && xDiff <= interaction.range)
            {
                self.Visuals.UpdateFacingFromDirection(targetPos - selfPos);
                return;
            }

            // ✅ combat stand-off positioning
            float side = selfPos.x <= targetPos.x ? -1f : 1f;
            if (Mathf.Abs(selfPos.x - targetPos.x) < 0.001f)
                side = 1f;

            Vector3 desiredPosition = new Vector3(
                targetPos.x + (side * interaction.standOffDistance),
                targetPos.y,
                selfPos.z
            );

            if (!AgentPathing.TryGetCurrentPathTarget(self, out Vector3 currentPathTarget) ||
                (currentPathTarget - desiredPosition).sqrMagnitude > 0.05f)
            {
                AgentMovement.SetPath(self, new List<Vector3> { desiredPosition });
            }
        }

        self.Visuals.UpdateFacingFromDirection(targetPos - selfPos);
    }

    // ✅ NEW: used when an interaction allows entering the target's building tile.
    // Avoids re-pathing every tick by only recomputing when the goal tile actually changes.
    private static void ApproachTileByPathfinding(Agent self, Vector2Int goalTile, Vector3 targetWorldPos, AgentInteraction interaction)
    {
        float distanceToTarget = Vector3.Distance(self.transform.position, targetWorldPos);

        if (distanceToTarget <= interaction.range)
        {
            // ✅ already close enough to attack — stop here instead of continuing
            // to walk the rest of the path onto the target's exact tile center
            if (self.currentPath.Count > 0)
                AgentMovement.ClearPath(self);

            return;
        }

        Vector2Int selfTile = AgentPathing.WorldToGrid(self, self.transform.position);

        if (selfTile == goalTile)
        {
            // ✅ FIX: already in the same tile as the target — A* has nothing left
            // to route around (same start/goal tile = no path), which is why the
            // fox was stalling here. Close the last bit of distance by heading
            // straight at the target's current live position instead.
            if (!AgentPathing.TryGetCurrentPathTarget(self, out Vector3 currentPathTarget) ||
                (currentPathTarget - targetWorldPos).sqrMagnitude > 0.05f)
            {
                AgentMovement.SetPath(self, new List<Vector3> { targetWorldPos });
            }

            return;
        }

        Vector2Int currentDestinationTile = self.currentPath.Count > 0
            ? AgentPathing.WorldToGrid(self, self.currentPath[self.currentPath.Count - 1])
            : goalTile;

        bool needsNewPath = self.currentPath.Count == 0 || currentDestinationTile != goalTile;

        if (!needsNewPath)
            return;

        if (AgentPathing.TryFindPathToTile(self, goalTile, out List<Vector3> worldPath))
        {
            AgentMovement.SetPath(self, worldPath);
        }
    }

    // =====================================================
    // ⚔️ EXECUTION
    // =====================================================

    public static void ExecuteInteraction(Agent self, Agent target, AgentInteraction interaction)
    {
        if (self == null || target == null || target.isDead)
            return;

        self.Visuals.TriggerAttackVisual();

        switch (interaction.type)
        {
            case InteractionType.Combat:
                ExecuteCombat(self, target, interaction);
                break;

            case InteractionType.VampireBite:
                ExecuteVampireBite(self, target, interaction);
                break;

            case InteractionType.Swoon:
                ExecuteSwoon(target, interaction);
                break;

            case InteractionType.Heal:
                ExecuteHeal(target, interaction);
                break;

            case InteractionType.Convert:
                ExecuteConvert(target, interaction);
                break;

            case InteractionType.FleeFrom:
                ExecuteFlee(self, target);
                break;
        }
    }

    private static void ExecuteCombat(Agent self, Agent target, AgentInteraction interaction)
    {
        float damage = Mathf.Max(1f, interaction.value > 0f ? interaction.value : 10f);
        AgentDeath.HurtAgent(target, deathType.bleeding, damage);

        // ✅ fright check happens regardless of how much damage was actually dealt
        AgentFear.TryTriggerFright(target, self);
    }

    private static void ExecuteVampireBite(Agent self, Agent target, AgentInteraction interaction)
    {
        float damage = Mathf.Max(1f, interaction.value);
        AgentDeath.HurtAgent(target, deathType.bleeding, damage);
        self.health = Mathf.Min(100f, self.health + damage * 0.5f);

        // ✅ same fright check as combat — this also deals damage
        AgentFear.TryTriggerFright(target, self);
    }

    private static void ExecuteSwoon(Agent target, AgentInteraction interaction)
    {
        target.waitTimer = Mathf.Max(target.waitTimer, interaction.value);
    }

    private static void ExecuteHeal(Agent target, AgentInteraction interaction)
    {
        target.health = Mathf.Min(100f, target.health + interaction.value);
    }

    private static void ExecuteConvert(Agent target, AgentInteraction interaction)
    {
        Debug.Log($"Convert attempted on {target.type}");
    }

    private static void ExecuteFlee(Agent self, Agent threat)
    {
        Vector3 fleeDir = (self.transform.position - threat.transform.position).normalized;
        Vector3 fleeTarget = self.transform.position + fleeDir * 3f;

        AgentMovement.SetPath(self, new List<Vector3> { fleeTarget });
    }

    // =====================================================
    // 🖱️ CLICK / TRADE
    // =====================================================

    public static void HandleClick(Agent agent)
    {
        if (agent == null)
            return;

        if (agent.type == AgentType.foodTrader)
            TradeFoodWithFoodTrader(agent);
    }

    public static bool TradeFoodWithFoodTrader(Agent agent)
    {
        if (agent == null || agent.type != AgentType.foodTrader)
            return false;

        if (StatsCounter.Instance == null || StatsCounter.Instance.StatResults == null)
            return false;

        float currentGold = StatsCounter.Instance.StatResults[stat.tot_Wealth];

        if (currentGold < FoodTraderGoldCost)
        {
            TownStateUI.Instance?.PushTownMessage("Not enough gold.");
            return false;
        }

        StatsCounter.Instance.StatResults[stat.tot_Wealth] -= FoodTraderGoldCost;
        StatsCounter.Instance.StatResults[stat.tot_food] += FoodTraderFoodGain;

        agent.Spending.MakeMoneySpentBubble(-FoodTraderGoldCost, "$");

        return true;
    }

    // =====================================================
    // 🐷 HELPER (UNCHANGED)
    // =====================================================

    public static void MoveAgentIntoBuilding(Agent agent, Vector2Int tile, bool roamInside)
    {
        if (agent == null)
            return;

        agent.movementType = MovementType.AllowTargetBuildingOnly;
        agent.targetTile = tile;

        Vector3 world = AgentPathing.GridToWorld(agent, tile);
        AgentMovement.SetPath(agent, new List<Vector3> { world });

        // SetPath resets roamInsideTarget as a safety default — re-apply after.
        // This is a permanent placement (e.g. a pen animal): it roams here forever,
        // until something else (death, re-homing) explicitly moves it out.
        agent.roamInsideTarget = roamInside;

        // tells Agent.Start()/SetRole() not to overwrite this placement
        agent.hasExplicitPlacement = true;
    }
}
