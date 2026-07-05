// AgentPathing.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class AgentPathing
{
    private const int AnyBuildingHangoutOffsetTiles = 3;
    private const int BlockedTilePenalty = 1000;

    // ───────── GRID HELPERS ─────────

    public static void GetGridSettings(Agent agent, out Vector2 cellSize, out int halfW, out int halfH)
    {
        cellSize = new Vector2(1f, 1f);
        halfW = 10;
        halfH = 10;

        if (BuildingPlacer.Instance != null)
        {
            cellSize = BuildingPlacer.Instance.cellSize;
            halfW = BuildingPlacer.Instance.gridWidth / 2;
            halfH = BuildingPlacer.Instance.gridHeight / 2;
        }
    }

    public static Vector2Int WorldToGrid(Agent agent, Vector3 worldPos)
    {
        GetGridSettings(agent, out Vector2 cellSize, out int halfW, out int halfH);

        if (cellSize.x == 0f || cellSize.y == 0f)
            return Vector2Int.zero;

        int x = Mathf.RoundToInt(worldPos.x / cellSize.x);
        int y = Mathf.RoundToInt(worldPos.y / cellSize.y);

        return new Vector2Int(
            Mathf.Clamp(x, -halfW, halfW),
            Mathf.Clamp(y, -halfH, halfH)
        );
    }

    public static Vector3 GridToWorld(Agent agent, Vector2Int gridPos)
    {
        GetGridSettings(agent, out Vector2 cellSize, out _, out _);

        return new Vector3(
            gridPos.x * cellSize.x,
            gridPos.y * cellSize.y,
            agent.transform.position.z
        );
    }

    public static bool IsWithinBounds(Agent agent, Vector2Int tile)
    {
        GetGridSettings(agent, out _, out int halfW, out int halfH);

        return tile.x >= -halfW && tile.x <= halfW &&
               tile.y >= -halfH && tile.y <= halfH;
    }

    public static bool IsTileWalkable(Agent agent, Vector2Int tile)
    {
        if (GameManager.Instance == null)
            return false;

        if (!IsWithinBounds(agent, tile))
            return false;

        if (GameManager.Instance.TryGetBuildingAt(tile, out _))
            return false;

        return true;
    }

    // ✅ RESTORED — AgentActions.MoveTowardTarget depends on this to avoid re-pathing every frame
    public static bool TryGetCurrentPathTarget(Agent agent, out Vector3 target)
    {
        target = agent.transform.position;

        if (agent.pathIndex < 0 || agent.pathIndex >= agent.currentPath.Count)
            return false;

        target = agent.currentPath[agent.pathIndex];
        return true;
    }

    // ✅ NEW: lets AgentActions route a chasing agent (e.g. a fox) around every
    // building except the one at goalTile, respecting agent.movementType.
    public static bool TryFindPathToTile(Agent agent, Vector2Int goalTile, out List<Vector3> worldPath)
    {
        worldPath = new List<Vector3>();

        if (agent == null || GameManager.Instance == null)
            return false;

        Vector2Int startTile = WorldToGrid(agent, agent.transform.position);

        if (startTile == goalTile)
            return false;

        if (!TryFindPath(agent, startTile, goalTile, allowBlockedTiles: false, out List<Vector2Int> tilePath))
            return false;

        worldPath = ConvertToWorld(agent, tilePath);
        return worldPath.Count > 0;
    }

    // ───────── DESTINATION PICKING ─────────

    public static void PickNewDestination(Agent agent)
    {
        if (agent == null)
            return;

        if (GameManager.Instance == null)
        {
            AgentMovement.SetWaitTimer(agent, agent.RepathDelay);
            return;
        }

        // ✅ FIX: normal wandering should never inherit a leftover combat/interaction
        // movement rule — AllowTargetBuildingOnly is meant to be transient.
        agent.movementType = MovementType.AvoidAllBuildings;

        AgentMovement.ClearPath(agent);

        Vector2Int startTile = WorldToGrid(agent, agent.transform.position);
        bool foundPath = false;

        for (int attempt = 0; attempt < agent.MaxDestinationAttempts; attempt++)
        {
            Vector2Int destination = startTile;

            if (agent.IsOnSpendTrip && agent.HasForcedSpendDestination)
            {
                destination = agent.ForcedSpendDestinationTile;

                if (TryFindPath(agent, startTile, destination, allowBlockedTiles: false, out List<Vector2Int> spendTilePath) ||
                    TryFindPath(agent, startTile, destination, allowBlockedTiles: true, out spendTilePath))
                {
                    List<Vector3> spendWorldPath = ConvertToWorld(agent, spendTilePath);

                    if (spendWorldPath.Count > 0)
                    {
                        foundPath = true;
                        AgentMovement.SetPath(agent, spendWorldPath);
                        break;
                    }
                }

                continue;
            }

            bool foundPreferred = false;
            bool foundAnyHangout = false;

            if (agent.PreferredBuildingTypes != null && agent.PreferredBuildingTypes.Count > 0)
            {
                foundPreferred = TryPickPreferredTile(agent, agent.PreferredBuildingTypes, out destination);

                if (foundPreferred)
                    AgentLeaving.AddPreferredHangoutReduction(agent);
            }

            if (!foundPreferred)
            {
                foundAnyHangout = TryFindAnyBuildingHangoutTile(agent, startTile, out destination);

                if (!foundAnyHangout)
                    destination = PickRandomWalkableTile(agent);
            }

            if (destination == startTile)
                continue;

            if (TryFindPath(agent, startTile, destination, allowBlockedTiles: false, out List<Vector2Int> tilePath))
            {
                List<Vector3> worldPath = ConvertToWorld(agent, tilePath);

                if (worldPath.Count > 0)
                {
                    foundPath = true;
                    AgentMovement.SetPath(agent, worldPath);
                    agent.targetTile = destination;
                    break;
                }
            }
        }

        if (!foundPath)
            AgentMovement.SetWaitTimer(agent, agent.RepathDelay);
    }

    public static bool TryResolveSpendDestinationTile(Agent agent, BuyBehaviour behaviour, out Vector2Int destinationTile)
    {
        destinationTile = Vector2Int.zero;

        if (agent == null || GameManager.Instance == null)
            return false;

        List<BuildingType> singleType = new List<BuildingType> { behaviour.where };

        if (TryPickPreferredTile(agent, singleType, out Vector2Int guessedTile))
        {
            if (TryGetFrontAccessTile(agent, guessedTile, out destinationTile, ignoreWalkability: true))
                return true;
        }

        Vector2Int[] searchOffsets =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
            new Vector2Int(1, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1)
        };

        for (int i = 0; i < searchOffsets.Length; i++)
        {
            Vector2Int checkTile = guessedTile + searchOffsets[i];

            if (!IsWithinBounds(agent, checkTile))
                continue;

            if (!GameManager.Instance.TryGetBuildingAt(checkTile, out Building building))
                continue;

            if (building == null || building.type != behaviour.where)
                continue;

            if (TryGetFrontAccessTile(agent, checkTile, out destinationTile, ignoreWalkability: true))
                return true;
        }

        return false;
    }

    // ───────── PATH BUILD ─────────

    private static List<Vector3> ConvertToWorld(Agent agent, List<Vector2Int> tiles)
    {
        List<Vector3> path = new();

        if (tiles == null)
            return path;

        foreach (var t in tiles)
        {
            Vector3 point = GridToWorld(agent, t);
            point.z = agent.transform.position.z;
            path.Add(point);
        }

        return path;
    }

    // ───────── HANGOUT / PREFERRED-BUILDING TILE PICKING (RESTORED) ─────────

    private static bool TryGetFrontAccessTile(
        Agent agent,
        Vector2Int buildingTile,
        out Vector2Int frontTile,
        bool ignoreWalkability = false)
    {
        frontTile = buildingTile;

        if (agent == null || GameManager.Instance == null)
            return false;

        if (!GameManager.Instance.TryGetBuildingAt(buildingTile, out Building building) || building == null)
            return false;

        Vector2Int facingDir = GetCardinalFacingDirection(building.transform);

        if (facingDir == Vector2Int.zero)
            facingDir = Vector2Int.down;

        frontTile = buildingTile + facingDir;

        if (!IsWithinBounds(agent, frontTile))
            return false;

        if (!ignoreWalkability && !IsTileWalkable(agent, frontTile))
            return false;

        return true;
    }

    private static Vector2Int GetCardinalFacingDirection(Transform buildingTransform)
    {
        if (buildingTransform == null)
            return Vector2Int.down;

        Vector2 facing = buildingTransform.TransformDirection(Vector3.down);

        if (facing.sqrMagnitude < 0.0001f)
            facing = buildingTransform.right;

        facing.Normalize();

        if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y))
            return facing.x >= 0f ? Vector2Int.right : Vector2Int.left;

        return facing.y >= 0f ? Vector2Int.up : Vector2Int.down;
    }

    private static bool TryPickPreferredTile(Agent agent, List<BuildingType> buildingTypes, out Vector2Int chosenTile)
    {
        chosenTile = WorldToGrid(agent, agent.transform.position);

        if (GameManager.Instance == null)
            return false;

        if (buildingTypes == null || buildingTypes.Count == 0)
            return false;

        GetGridSettings(agent, out Vector2 cellSize, out int halfW, out int halfH);

        float maxCell = Mathf.Max(cellSize.x, cellSize.y);
        float worldSearchRadius = Mathf.Max(halfW, halfH) * maxCell * 2f;
        int tileSearchRadius = Mathf.Max(halfW, halfH);

        return GameManager.Instance.TryGetPreferredHangoutTile(
            buildingTypes,
            agent.transform.position,
            worldSearchRadius,
            tileSearchRadius,
            out chosenTile
        );
    }

    private static bool TryFindAnyBuildingHangoutTile(Agent agent, Vector2Int startTile, out Vector2Int destinationTile)
    {
        destinationTile = startTile;

        if (agent == null || GameManager.Instance == null)
            return false;

        GetGridSettings(agent, out _, out int halfW, out int halfH);

        HashSet<Vector2Int> candidates = new HashSet<Vector2Int>();

        for (int x = -halfW; x <= halfW; x++)
        {
            for (int y = -halfH; y <= halfH; y++)
            {
                Vector2Int buildingTile = new Vector2Int(x, y);

                if (!GameManager.Instance.TryGetBuildingAt(buildingTile, out _))
                    continue;

                CollectHangoutTilesNearTile(agent, buildingTile, candidates);
            }
        }

        if (candidates.Count == 0)
            return false;

        int pickIndex = UnityEngine.Random.Range(0, candidates.Count);
        int i = 0;

        foreach (Vector2Int tile in candidates)
        {
            if (i == pickIndex)
            {
                destinationTile = tile;
                return true;
            }

            i++;
        }

        return false;
    }

    private static void CollectHangoutTilesNearTile(
        Agent agent,
        Vector2Int buildingTile,
        HashSet<Vector2Int> candidates)
    {
        if (!IsWithinBounds(agent, buildingTile))
            return;

        for (int radius = 1; radius <= AnyBuildingHangoutOffsetTiles; radius++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        continue;

                    Vector2Int candidate = new Vector2Int(buildingTile.x + x, buildingTile.y + y);

                    if (!IsWithinBounds(agent, candidate))
                        continue;

                    if (!IsTileWalkable(agent, candidate))
                        continue;

                    candidates.Add(candidate);
                }
            }
        }
    }

    // ───────── A* ─────────

    private static bool TryFindPath(
        Agent agent,
        Vector2Int start,
        Vector2Int goal,
        bool allowBlockedTiles,
        out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();

        if (agent == null || GameManager.Instance == null)
            return false;

        if (!IsWithinBounds(agent, goal))
            return false;

        bool goalBlocked = !IsTileWalkable(agent, goal);
        bool goalAllowedByMovementType = agent.movementType == MovementType.AllowTargetBuildingOnly;

        if (goalBlocked && !allowBlockedTiles && !goalAllowedByMovementType)
            return false;

        var openSet = new List<Vector2Int> { start };
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet[0];
            int currentIndex = 0;
            int currentF = GetFScore(current, goal, gScore);

            for (int i = 1; i < openSet.Count; i++)
            {
                int f = GetFScore(openSet[i], goal, gScore);
                if (f < currentF)
                {
                    current = openSet[i];
                    currentIndex = i;
                    currentF = f;
                }
            }

            openSet.RemoveAt(currentIndex);

            if (current == goal)
            {
                path = ReconstructPath(cameFrom, current);
                return true;
            }

            closedSet.Add(current);

            foreach (Vector2Int neighbor in GetNeighbors(agent, current, allowBlockedTiles))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                if (!IsWithinBounds(agent, neighbor))
                    continue;

                bool neighborBlocked = !IsTileWalkable(agent, neighbor);
                bool neighborIsGoal = neighbor == goal;

                bool neighborAllowed;

                if (allowBlockedTiles)
                {
                    neighborAllowed = true;
                }
                else if (goalAllowedByMovementType)
                {
                    neighborAllowed = neighborIsGoal || !neighborBlocked;
                }
                else
                {
                    neighborAllowed = !neighborBlocked;
                }

                if (!neighborAllowed)
                    continue;

                int tentativeG = gScore[current] + GetMoveCost(current, neighbor, neighborBlocked);

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return false;
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var result = new List<Vector2Int> { current };

        while (cameFrom.TryGetValue(current, out Vector2Int parent))
        {
            current = parent;
            result.Add(current);
        }

        result.Reverse();
        return result;
    }

    private static IEnumerable<Vector2Int> GetNeighbors(Agent agent, Vector2Int tile, bool allowBlockedTiles)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                    continue;

                Vector2Int neighbor = new Vector2Int(tile.x + x, tile.y + y);

                // ✅ RESTORED: prevents agents cutting diagonally across a building corner
                if (!allowBlockedTiles && Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
                {
                    Vector2Int sideA = new Vector2Int(tile.x + x, tile.y);
                    Vector2Int sideB = new Vector2Int(tile.x, tile.y + y);

                    if (!IsWithinBounds(agent, sideA) || !IsWithinBounds(agent, sideB))
                        continue;

                    if (!IsTileWalkable(agent, sideA) || !IsTileWalkable(agent, sideB))
                        continue;
                }

                yield return neighbor;
            }
        }
    }

    private static int GetFScore(Vector2Int node, Vector2Int goal, Dictionary<Vector2Int, int> gScore)
    {
        int g = gScore.TryGetValue(node, out int value) ? value : int.MaxValue / 4;
        int h = GetHeuristic(node, goal);
        return g + h;
    }

    private static int GetMoveCost(Vector2Int from, Vector2Int to, bool blocked)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);
        int baseCost = (dx == 1 && dy == 1) ? 14 : 10;

        if (blocked)
            baseCost += BlockedTilePenalty;

        return baseCost;
    }

    private static int GetHeuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * (dx + dy);
    }

    private static Vector2Int PickRandomWalkableTile(Agent agent)
    {
        GetGridSettings(agent, out _, out int halfW, out int halfH);

        for (int i = 0; i < agent.MaxDestinationAttempts; i++)
        {
            Vector2Int tile = new Vector2Int(
                UnityEngine.Random.Range(-halfW, halfW + 1),
                UnityEngine.Random.Range(-halfH, halfH + 1)
            );

            if (IsTileWalkable(agent, tile))
                return tile;
        }

        return WorldToGrid(agent, agent.transform.position);
    }
}
