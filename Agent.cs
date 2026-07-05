// Agent.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ✅ NEW: if this is already declared elsewhere (e.g. your current ScriptableAgent.cs),
// delete this block to avoid a duplicate-definition compile error.
public enum MovementType
{
    AvoidAllBuildings,
    AllowTargetBuildingOnly
}

public class Agent : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public SpriteRenderer shadowRenderer;
    public BoxCollider2D clickCollider2D;
    public AgentType type;
    public TeamType team = TeamType.none;
    public Dictionary<scoreType, float> AgentStats = new Dictionary<scoreType, float>();
    public Dictionary<AgentType, float> BuysFrom = new Dictionary<AgentType, float>(); // legacy, keep for now
    public Building myHouse = null;
    public bool outsider = false;

    [Header("Visuals")]
    [SerializeField] public AnimationState animationState = AnimationState.idle;
    [SerializeField] public float attackVisualDuration = 0.22f;
    [SerializeField] public Color visualTint = Color.white;
    public float attackVisualTimer = 0f;

    [Header("Death")]
    public bool isDead = false;
    public deathType causeOfDeath = deathType.starve;
    public float health = 100f;

    [Header("Movement")]
    [SerializeField] public float moveSpeed = 1.5f;
    [SerializeField] public float arriveDistance = 0.05f;
    [SerializeField] public float pauseBetweenMoves = 0.35f;
    [SerializeField] public float repathDelay = 0.6f;
    [SerializeField] public int maxDestinationAttempts = 40;
    [SerializeField] public float tileEdgePadding = 0.08f;

    // ✅ NEW: movement rules used by AgentPathing / AgentActions for
    // combat approach + "wander near a hangout building" behavior
    [Header("Movement Rules")]
    public MovementType movementType = MovementType.AvoidAllBuildings;
    public Vector2Int targetTile = Vector2Int.zero;
    public bool roamInsideTarget = false;

    // ✅ NEW: set by AgentActions.MoveAgentIntoBuilding. Prevents Start()/SetRole()
    // from overwriting a deliberate placement (e.g. a pen animal) with normal wandering.
    public bool hasExplicitPlacement = false;

    [Header("Movement Variation")]
    [SerializeField] public float speedVariationStrength = 0.35f;
    [SerializeField] public float speedVariationSpeed = 1.75f;
    [SerializeField] public float acceleration = 6f;
    [SerializeField] public float turnResponsiveness = 8f;

    [Header("Facing")]
    [SerializeField] public float flipThreshold = 0.02f;

    [Header("Interactions")]
    [SerializeField] public float interactionDecisionInterval = 0.75f;



    [Header("Debug")]
    [SerializeField] public bool drawPathGizmos = true;
    [SerializeField] public float sortingOffset = 0.1f;

    [Header("Spending Schedule")]
    [SerializeField] public float spendTickAccumulator = 0f;
    internal float budget = 0f;

    [Header("Leaving Schedule")]
    [SerializeField] public float leaveTickAccumulator = 0f;

    [Header("Leaving")]
    [SerializeField] public float desireToLeave = 0f;
    [SerializeField] public float maxLeavePoints = 20f;
    [SerializeField] public float leaveWalkSpeedMultiplier = 1.05f;
    [SerializeField] public float leaveFadeSpeed = 0.75f;

    [Header("Stuck Recovery")]
    [SerializeField] public float stuckRepathTime = 1.25f;

    public readonly List<Vector3> currentPath = new List<Vector3>();
    public int pathIndex = 0;
    public float waitTimer = 0f;

    public float currentSpeed = 0f;
    public float noiseOffset = 0f;

    public Vector3 steeringDirection = Vector3.right;
    public Vector3 steeringVelocity = Vector3.zero;

    public readonly List<BuildingType> preferredBuildingTypes = new List<BuildingType>();
    public readonly List<BuyBehaviour> spendBehaviours = new List<BuyBehaviour>();

    public bool isOnSpendTrip = false;
    public int activeSpendBehaviourIndex = -1;
    public float activeSpendAmount = 0f;

    public bool hasForcedSpendDestination = false;
    public Vector2Int forcedSpendDestinationTile = Vector2Int.zero;

    public bool isLeavingTown = false;
    public bool leaveFadeStarted = false;
    public Vector3 leaveDestinationWorld = Vector3.zero;

    public Vector3 lastFramePosition;
    public float stuckTimer = 0f;
    public float lastDistanceToTarget = -1f;
    public bool hasDistanceSample = false;

    public float interactionTickAccumulator = 0f;
    public List<float> interactionCooldownTimers = new List<float>();
    public Agent interactionTarget = null;


    public AnimationState CurrentAnimationState => animationState;
    public Color VisualTint => visualTint;

    public bool IsOnSpendTrip => isOnSpendTrip;
    public int SpendBehaviourCount => spendBehaviours.Count;
    public bool HasForcedSpendDestination => hasForcedSpendDestination;
    public Vector2Int ForcedSpendDestinationTile => forcedSpendDestinationTile;
    public List<BuildingType> PreferredBuildingTypes => preferredBuildingTypes;
    public IReadOnlyList<BuyBehaviour> SpendBehaviours => spendBehaviours;
    public float RepathDelay => repathDelay;
    public int MaxDestinationAttempts => maxDestinationAttempts;

    public float DesireToLeave => desireToLeave;
    public float MaxLeavePoints => maxLeavePoints;

    public List<AgentTag> tags=new();
    public bool IsLeavingTown => isLeavingTown;
    public bool IsLeaveFadeStarted => leaveFadeStarted;
    public Vector3 LeaveDestinationWorld => leaveDestinationWorld;
    public float MoveSpeed => moveSpeed;
    public float LeaveWalkSpeedMultiplier => leaveWalkSpeedMultiplier;
    public float LeaveFadeSpeed => leaveFadeSpeed;



    //   public AgentMovement movement;
    //   public AgentCombat combat;
    public AgentVisuals Visuals;
    public AgentSpending Spending;
    public AgentSchedule Schedule;


    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Visuals = new AgentVisuals(this);
        Spending = new AgentSpending(this);
        Schedule = new AgentSchedule(this);

        Visuals.Init();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        noiseOffset = UnityEngine.Random.Range(0f, 1000f);

        if (spriteRenderer != null)
            spriteRenderer.flipX = false;
    }

    private void Start()
    {
        lastFramePosition = transform.position;
        InClearAllStatTypes();

        // ✅ FIX: if something already placed this agent (e.g. a pen animal spawned
        // and placed in the same frame, before Start() ran), don't let the normal
        // role-setup / wander-pick logic below undo it.
        SetRole(type, preservePlacement: hasExplicitPlacement);

        if (isDead)
        {
            Visuals.RefreshDeathVisuals();
            return;
        }

        if (!hasExplicitPlacement)
            AgentPathing.PickNewDestination(this);
    }

    private void Update()
    {
        if (isDead)
            return;

        Schedule.RunSchedules();
        Visuals.UpdateVisualState();

        if (isLeavingTown)
        {
            AgentLeaving.UpdateLeaving(this);
            return;
        }

        AgentMovement.UpdateMovement(this);
    }

    private void LateUpdate()
    {
        Visuals.UpdateSorting();
    }

    private void OnMouseDown()
    {
        if (isDead)
            return;

        AgentActions.HandleClick(this);
    }

    private void OnDestroy()
    {
        AgentActions.DeregisterTeamMember(this);

        if (GameManager.Instance != null)
            GameManager.Instance.DeregisterAgent(this);
    }

    private void InClearAllStatTypes()
    {
        foreach (scoreType score in Enum.GetValues(typeof(scoreType)))
        {
            if (!AgentStats.ContainsKey(score))
                AgentStats.Add(score, 0);
            else
                AgentStats[score] = 0;
        }

        foreach (AgentType agentType in Enum.GetValues(typeof(AgentType)))
        {
            if (!BuysFrom.ContainsKey(agentType))
                BuysFrom.Add(agentType, 0);
            else
                BuysFrom[agentType] = 0;
        }
    }


    public bool HasTag(AgentTag tag)
    {
        return tags.Contains(tag);
    }
    public void SetRole(AgentType newType, bool preservePlacement = false)
    {
        AgentActions.DeregisterTeamMember(this);

        if (GameManager.Instance != null)
            GameManager.Instance.DeregisterAgent(this);

        type = newType;

        var data = AgentData.Instance != null ? AgentData.Instance.GetAgent(type) : null;
        InClearAllStatTypes();

        preferredBuildingTypes.Clear();
        spendBehaviours.Clear();
        isOnSpendTrip = false;
        activeSpendBehaviourIndex = -1;
        activeSpendAmount = 0f;
        hasForcedSpendDestination = false;
        spendTickAccumulator = 0f;
        leaveTickAccumulator = 0f;
        desireToLeave = 0f;
        isLeavingTown = false;
        leaveFadeStarted = false;
        leaveDestinationWorld = Vector3.zero;
    interactionTickAccumulator = 0f;
    interactionCooldownTimers = new List<float>();
   interactionTarget = null;

    // ✅ don't let a stale movement/roam state survive a role swap —
    // unless the caller explicitly wants to keep a placement (e.g. pen animal spawn)
    if (!preservePlacement)
    {
        movementType = MovementType.AvoidAllBuildings;
        targetTile = Vector2Int.zero;
        roamInsideTarget = false;
    }

    team = data != null ? data.team : TeamType.none;
        visualTint = data != null ? data.debugColor : Color.white;
        animationState = AnimationState.idle;
        attackVisualTimer = 0f;
        tags = data.tags;

        if (data != null)
        {
            if (data.hangsAround != null)
                preferredBuildingTypes.AddRange(data.hangsAround);

            if (data.stats != null)
            {
                foreach (var agentStat in data.stats)
                {
                    Debug.Log($"⛏ updated stat {agentStat.typeOfScore} to {agentStat.amount}");
                    AgentStats[agentStat.typeOfScore] = agentStat.amount;
                }
            }

            if (data.spendsAt != null)
            {
                foreach (var spend in data.spendsAt)
                    spendBehaviours.Add(spend);
            }
        }

        Visuals.ApplyVisuals();
        Visuals.RefreshDeathVisuals();

        if (team != TeamType.none)
            AgentActions.RegisterTeamMember(this, team);

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterAgent(this);
    }

    public void SetDeadState(deathType newDeathType)
    {
        AgentActions.DeregisterTeamMember(this);

        isDead = true;
        causeOfDeath = newDeathType;
        health = 0f;

        animationState = AnimationState.none;
        attackVisualTimer = 0f;

        isOnSpendTrip = false;
        activeSpendBehaviourIndex = -1;
        activeSpendAmount = 0f;
        hasForcedSpendDestination = false;

        isLeavingTown = false;
        leaveFadeStarted = false;
        leaveDestinationWorld = Vector3.zero;

        spendTickAccumulator = 0f;
        leaveTickAccumulator = 0f;
        desireToLeave = 0f;

        interactionTickAccumulator = 0f;
        interactionCooldownTimers = new List<float>();
        interactionTarget = null;

        // ✅ same reason as SetRole — a dead agent shouldn't carry roam/combat state
        movementType = MovementType.AvoidAllBuildings;
        targetTile = Vector2Int.zero;
        roamInsideTarget = false;
        hasExplicitPlacement = false;

        AgentMovement.ClearPath(this);
        waitTimer = 0f;
        currentSpeed = 0f;
        steeringVelocity = Vector3.zero;
        stuckTimer = 0f;
        lastDistanceToTarget = -1f;
        hasDistanceSample = false;

        Visuals.RefreshDeathVisuals();
    }

    public void SetAliveState()
    {
        isDead = false;
        causeOfDeath = deathType.starve;
        health = 100f;

        animationState = AnimationState.idle;
        attackVisualTimer = 0f;

        if (team != TeamType.none)
            AgentActions.RegisterTeamMember(this, team);

        Visuals.ApplyVisuals();
        Visuals.RefreshDeathVisuals();
        Visuals.SetSpriteAlpha(1f);
    }

    public float GetGameTickLength()
    {
        if (Settings.Instance == null)
            return 1f;

        return Mathf.Max(0.01f, Settings.Instance.gameTickLength);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawPathGizmos)
            return;

        if (hasForcedSpendDestination)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(AgentPathing.GridToWorld(this, forcedSpendDestinationTile), 0.08f);
        }

        if (isLeavingTown)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(LevelCreator.Instance != null ? LevelCreator.Instance.transform.position : Vector3.zero,
                                  LevelCreator.Instance != null ? LevelCreator.Instance.LeaveRadius : 0f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(leaveDestinationWorld, 0.1f);
        }

        if (currentPath == null || currentPath.Count == 0)
            return;

        Gizmos.color = isOnSpendTrip ? Color.red : Color.cyan;
        for (int i = 0; i < currentPath.Count - 1; i++)
            Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);

        Gizmos.color = isOnSpendTrip ? new Color(1f, 0.35f, 0.35f, 1f) : Color.yellow;
        foreach (var point in currentPath)
            Gizmos.DrawSphere(point, 0.05f);

        if (pathIndex < currentPath.Count)
        {
            Gizmos.color = isOnSpendTrip ? Color.red : Color.green;
            Gizmos.DrawSphere(currentPath[pathIndex], 0.1f);
            Gizmos.DrawLine(transform.position, currentPath[pathIndex]);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(currentPath[currentPath.Count - 1], 0.12f);

        Handles.color = isOnSpendTrip ? Color.red : Color.white;

        string modeText = isOnSpendTrip
            ? "SPENDING"
            : isLeavingTown
                ? "LEAVING"
                : isDead
                    ? $"DEAD ({causeOfDeath})"
                    : "HANGING OUT";

        string tickText = isOnSpendTrip
            ? "Tick: paused while buying"
            : $"Tick left: {Spending.GetSpendTickRemaining():0.00}s";

        Handles.Label(
            transform.position + Vector3.up * 0.85f,
            $"{modeText}\n{tickText}\nLeave: {desireToLeave:0.00}/{maxLeavePoints:0.##}"
        );
    }
#endif
}
