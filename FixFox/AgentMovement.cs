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
            if (agent.roamInsideTarget)
            {
                // picks ONE new point inside the tile, then falls through to the
                // normal steering/acceleration code below — same movement feel as
                // regular wandering, just confined to a small radius around the tile.
                // On arrival, the usual arrival branch further down handles the
                // pause + hands back here for the next hop, same as classic hangout hopping.
                BeginNewRoamHop(agent);
            }
            else
            {
                AgentPathing.PickNewDestination(agent);

                agent.lastFramePosition = agent.transform.position;
                agent.stuckTimer = 0f;
                agent.lastDistanceToTarget = -1f;
                agent.hasDistanceSample = false;
                return;
            }
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

    private static void BeginNewRoamHop(Agent agent)
    {
        Vector3 center = AgentPathing.GridToWorld(agent, agent.targetTile);
        center.y += agent.roamCenterYOffset;

        Vector2 offset = Random.insideUnitCircle * 0.3f;
        Vector3 point = center + new Vector3(offset.x, offset.y, 0f);
        point.z = agent.transform.position.z;

        agent.currentPath.Clear();
        agent.currentPath.Add(point);
        agent.pathIndex = 0;
        agent.currentSpeed = 0f;
        agent.steeringVelocity = Vector3.zero;

        // roamInsideTarget is deliberately left as-is here (not touched) —
        // that's what makes the arrival branch below loop back into another
        // hop instead of calling PickNewDestination once this point is reached.
    }

    public static void SetPath(Agent agent, List<Vector3> newPath)
    {
        if (agent.isDead)
            return;

        agent.currentPath.Clear();
        agent.pathIndex = 0;
        agent.currentSpeed = 0f;
        agent.steeringVelocity = Vector3.zero;

        // ✅ anything that forces a fresh path (combat approach, spend trip, etc.)
        // means we are no longer just idly wandering a hangout tile
        agent.roamInsideTarget = false;

        if (newPath != null)
            agent.currentPath.AddRange(newPath);
    }

    public static void ClearPath(Agent agent)
    {
        agent.currentPath.Clear();
        agent.pathIndex = 0;
        agent.currentSpeed = 0f;

        // ✅ a forced clear shouldn't leave stale roam state behind
        agent.roamInsideTarget = false;
    }

    public static void SetWaitTimer(Agent agent, float value)
    {
        agent.waitTimer = Mathf.Max(0f, value);
    }
}
