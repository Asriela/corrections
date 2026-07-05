// AgentMovement.cs
using System.Collections.Generic;
using UnityEngine;

public static class AgentMovement
{
    public static void UpdateMovement(Agent agent)
    {
        if (agent.isDead)
            return;

        if (agent.waitTimer > 0f)
        {
            agent.waitTimer -= Time.deltaTime;
            if (agent.waitTimer < 0f)
                agent.waitTimer = 0f;

            agent.lastFramePosition = agent.transform.position;
            agent.stuckTimer = 0f;
            agent.lastDistanceToTarget = -1f;
            agent.hasDistanceSample = false;
            return;
        }

        if (agent.currentPath.Count == 0 || agent.pathIndex >= agent.currentPath.Count)
        {
            // ✅ roaming near the hangout tile we already arrived at
            if (agent.roamInsideTarget)
            {
                HandleInsideTileRoaming(agent);
                return;
            }

            AgentPathing.PickNewDestination(agent);

            agent.lastFramePosition = agent.transform.position;
            agent.stuckTimer = 0f;
            agent.lastDistanceToTarget = -1f;
            agent.hasDistanceSample = false;
            return;
        }

        Vector3 target = agent.currentPath[agent.pathIndex];
        target.z = agent.transform.position.z;

        Vector3 toTarget = target - agent.transform.position;
        float distance = toTarget.magnitude;

        if (distance <= agent.arriveDistance)
        {
            agent.pathIndex++;

            if (agent.pathIndex >= agent.currentPath.Count)
            {
                agent.currentPath.Clear();
                agent.currentSpeed = 0f;

                // ✅ FIX: this is the missing trigger — arriving at a hangout tile
                // now actually starts the roam-in-place behavior instead of just idling
                if (agent.arriveShouldRoam)
                {
                    agent.roamInsideTarget = true;
                    agent.roamTimer = agent.roamDuration;
                    HandleInsideTileRoaming(agent);
                    return;
                }

                agent.waitTimer = agent.pauseBetweenMoves;
                agent.Spending.SpendTripFinishedIfNeeded();
            }

            agent.lastFramePosition = agent.transform.position;
            agent.stuckTimer = 0f;
            agent.lastDistanceToTarget = -1f;
            agent.hasDistanceSample = false;
            return;
        }

        Vector3 desiredDirection = toTarget.normalized;
        desiredDirection.z = 0f;

        Vector3 steerFrom = agent.steeringDirection.sqrMagnitude > 0.0001f
            ? agent.steeringDirection
            : desiredDirection;

        float turnAmount = Vector3.Angle(steerFrom, desiredDirection);

        agent.steeringDirection = turnAmount > 150f
            ? desiredDirection
            : Vector3.RotateTowards(
                steerFrom,
                desiredDirection,
                agent.turnResponsiveness * 2.25f * Mathf.Deg2Rad * Time.deltaTime,
                0f
            );

        agent.steeringDirection.Normalize();
        agent.Visuals.UpdateFacingFromDirection(agent.steeringDirection);

        float speedNoise = Mathf.PerlinNoise(Time.time * agent.speedVariationSpeed + agent.noiseOffset, 0f);
        float speedMultiplier = 1f + ((speedNoise - 0.5f) * 2f * agent.speedVariationStrength);
        float spendSpeedMultiplier = agent.isOnSpendTrip ? 2f : 1f;

        float turnPenalty = Mathf.InverseLerp(0f, 180f, turnAmount);
        float turnSpeedMultiplier = Mathf.Lerp(1f, 0.18f, turnPenalty);

        float targetSpeed = Mathf.Max(0.05f, agent.moveSpeed * speedMultiplier * spendSpeedMultiplier * turnSpeedMultiplier);

        agent.currentSpeed = Mathf.MoveTowards(agent.currentSpeed, targetSpeed, agent.acceleration * Time.deltaTime);

        agent.transform.position += agent.steeringDirection * agent.currentSpeed * Time.deltaTime;

        agent.lastFramePosition = agent.transform.position;
    }

    private static void HandleInsideTileRoaming(Agent agent)
    {
        // ✅ FIX: roaming now has a lifespan. Without this, an agent that starts
        // roaming never returns to AgentSchedule's spend/leave checks and gets
        // permanently parked at one building.
        agent.roamTimer -= Time.deltaTime;

        if (agent.roamTimer <= 0f)
        {
            agent.roamInsideTarget = false;
            agent.arriveShouldRoam = false;
            agent.waitTimer = agent.pauseBetweenMoves;
            agent.Spending.SpendTripFinishedIfNeeded();
            return;
        }

        Vector3 center = AgentPathing.GridToWorld(agent, agent.targetTile);

        Vector2 offset = Random.insideUnitCircle * 0.3f;
        Vector3 target = center + new Vector3(offset.x, offset.y, 0f);

        Vector3 dir = target - agent.transform.position;

        if (dir.sqrMagnitude > 0.001f)
        {
            dir.Normalize();
            agent.transform.position += dir * (agent.moveSpeed * 0.3f) * Time.deltaTime;
            agent.Visuals.UpdateFacingFromDirection(dir);
        }
    }

    public static void SetPath(Agent agent, List<Vector3> newPath)
    {
        if (agent.isDead)
            return;

        agent.currentPath.Clear();
        agent.pathIndex = 0;
        agent.currentSpeed = 0f;
        agent.steeringVelocity = Vector3.zero;

        // ✅ FIX: anything that forces a fresh path (combat approach, spend trip, etc.)
        // means we are no longer just idly wandering a hangout tile.
        agent.roamInsideTarget = false;
        agent.arriveShouldRoam = false;

        if (newPath != null)
            agent.currentPath.AddRange(newPath);
    }

    public static void ClearPath(Agent agent)
    {
        agent.currentPath.Clear();
        agent.pathIndex = 0;
        agent.currentSpeed = 0f;

        // ✅ FIX: same reasoning as SetPath — a forced clear shouldn't leave stale roam state behind
        agent.roamInsideTarget = false;
        agent.arriveShouldRoam = false;
    }

    public static void SetWaitTimer(Agent agent, float value)
    {
        agent.waitTimer = Mathf.Max(0f, value);
    }
}
