using UnityEngine;

public class AgentWorkSchedule
{
    private Agent agent;

    public bool IsCommuting { get; private set; }
    public bool IsAtWork { get; private set; }
    public bool IsHome { get; private set; }

    // Home only: this is the in-flight state between arriving at the
    // front of the house and actually reaching the building tile.
    private bool enteringHome;

    private bool homeVisualPending;

    private const float StationaryHoldSeconds = 0.5f;

    public AgentWorkSchedule(Agent agent)
    {
        this.agent = agent;
    }

    public void RunWorkSchedule()
    {
        if (agent.isDead || agent.isLeavingTown || agent.isOnSpendTrip)
            return;

        if (TimeManager.Instance == null)
            return;

        if (TimeManager.Instance.IsDaytime)
            PursueWork();
        else
            PursueHome();
    }

    // ───────── WORK ─────────

    private void PursueWork()
    {
        if (IsHome)
            LeaveHome();

        if (agent.myWorkplace == null)
        {
            IsAtWork = false;
            IsCommuting = false;
            return;
        }

        if (IsAtWork)
        {
            if (agent.currentPath.Count == 0)
                TryStartNewWanderHop();

            return;
        }

        Vector2Int workTile = AgentPathing.WorldToGrid(
            agent,
            agent.myWorkplace.transform.position
        );

        if (!AgentPathing.TryGetFrontAccessTile(
                agent,
                workTile,
                out Vector2Int frontTile,
                ignoreWalkability: true))
        {
            return;
        }

        if (HasArrivedAt(frontTile))
        {
            EnterWorkday();
            return;
        }

        BeginCommute(frontTile);
    }

    private void EnterWorkday()
    {
        IsCommuting = false;
        IsAtWork = true;

        if (agent.myWorkplace != null)
            agent.myWorkplace.NotifyWorkerArrived(agent);
    }

    private void LeaveWork()
    {
        if (!IsAtWork)
            return;

        IsAtWork = false;
        IsCommuting = false;

        AgentMovement.ClearPath(agent);
        AgentMovement.SetWaitTimer(agent, 0f);

        if (agent.myWorkplace != null)
            agent.myWorkplace.NotifyWorkerDeparted(agent);
    }

    private void TryStartNewWanderHop()
    {
        if (agent.myWorkplace == null)
            return;

        if (AgentPathing.TryPickFreeAdjacentTile(
                agent,
                agent.myWorkplace,
                out Vector2Int wanderTile) &&
            AgentPathing.TryFindPathToTile(
                agent,
                wanderTile,
                out var worldPath))
        {
            AgentMovement.SetPath(agent, worldPath);
        }
    }

    // ───────── HOME ─────────

    private void PursueHome()
    {
        if (IsAtWork)
            LeaveWork();

        if (agent.myHouse == null)
        {
            IsCommuting = false;
            return;
        }

        if (IsHome)
        {
            TrySyncHomeVisual();
            HoldStill();
            return;
        }

        Vector2Int houseTile = AgentPathing.WorldToGrid(
            agent,
            agent.myHouse.transform.position
        );

        if (enteringHome)
        {
            // Wait until the final one-tile hop has actually completed.
            if (HasArrivedAt(houseTile))
            {
                enteringHome = false;
                IsCommuting = false;
                IsHome = true;
                homeVisualPending = true;

                // The resident is now physically inside the building.
                agent.myHouse.NotifyResidentEntered(agent);

                TrySyncHomeVisual();
                HoldStill();
            }

            return;
        }

        if (!AgentPathing.TryGetFrontAccessTile(
                agent,
                houseTile,
                out Vector2Int frontTile,
                ignoreWalkability: true))
        {
            return;
        }

        if (HasArrivedAt(frontTile))
        {
            enteringHome = true;
            IsCommuting = false;

            AgentPlacement.MoveAgentIntoBuilding(
                agent,
                houseTile,
                false
            );

            // Do not HoldStill here. The final hop must be allowed to run.
            return;
        }

        BeginCommute(frontTile);
    }

    private void LeaveHome()
    {
        Building houseBeingLeft = agent.myHouse;

        // Set this before notifying the building so the schedule and
        // building occupancy state agree immediately.
        IsHome = false;
        enteringHome = false;
        homeVisualPending = false;
        IsCommuting = false;

        if (houseBeingLeft != null)
            houseBeingLeft.NotifyResidentDeparted(agent);

        agent.movementType = MovementType.AvoidAllBuildings;
        agent.hasExplicitPlacement = false;

        AgentMovement.SetWaitTimer(agent, 0f);

        if (agent.Visuals.CurrentAction == AgentAction.sleep)
            agent.Visuals.Release();
    }

    // ───────── SHARED ─────────

    private bool HasArrivedAt(Vector2Int destinationTile)
    {
        Vector2Int selfTile = AgentPathing.WorldToGrid(
            agent,
            agent.transform.position
        );

        return selfTile == destinationTile &&
               agent.currentPath.Count == 0;
    }

    private void BeginCommute(Vector2Int destinationTile)
    {
        IsCommuting = true;

        Vector2Int currentDestinationTile =
            agent.currentPath.Count > 0
                ? AgentPathing.WorldToGrid(
                    agent,
                    agent.currentPath[agent.currentPath.Count - 1]
                )
                : destinationTile;

        bool needsNewPath =
            agent.currentPath.Count == 0 ||
            currentDestinationTile != destinationTile;

        if (!needsNewPath)
            return;

        if (AgentPathing.TryFindPathToTile(
                agent,
                destinationTile,
                out var worldPath))
        {
            AgentMovement.SetPath(agent, worldPath);
        }
    }

    private void HoldStill()
    {
        AgentMovement.SetWaitTimer(
            agent,
            StationaryHoldSeconds
        );
    }

    private void TrySyncHomeVisual()
    {
        if (agent.Visuals.CurrentAction != AgentAction.sleep)
            homeVisualPending = true;

        if (!homeVisualPending)
            return;

        var proposition = new Proposition(
            AgentAction.sleep,
            (int)PriorityTier.Idle,
            PropositionType.Looping
        );

        if (agent.Visuals.EvaluateProposition(proposition))
            homeVisualPending = false;
    }
}
