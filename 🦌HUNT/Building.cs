using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Building : MonoBehaviour
{
    public BuildingType type = BuildingType.none;

    [Header("References")]
    public TextMeshProUGUI textMesh;
    public SpriteRenderer spriteRenderer;
    public SpriteRenderer shadow;
    public SpriteRenderer myLight;
    public BoxCollider2D collisionBox2D;

    public Dictionary<ResourceType, int> resourcesWeAreMadeOf =
        new Dictionary<ResourceType, int>();

    [Header("Data")]
    public ScriptableBuilding data;
    public bool isBroken;

    [Header("Footprint")]
    // GridPosition is the anchor tile: minimum X and minimum Y.
    public Vector2Int footprintSize = Vector2Int.one;

    [Header("Employees")]
    public Agent assignedAgent;

    // Assignment and physical presence are deliberately separate.
    private bool workerPresent = false;

    // Agents physically sleeping inside this building.
    // This works for normal houses and live-in workplaces.
    private readonly HashSet<Agent> residentsInside =
        new HashSet<Agent>();

    public Vector2Int GridPosition { get; private set; } =
        new Vector2Int(int.MinValue, int.MinValue);

    private Color baseColor = Color.white;
    private bool refundedResources;

    private int brokenAtTick = -1;
    private int repairAtTick = -1;

    private void Awake()
    {
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;

        if (collisionBox2D == null)
            collisionBox2D = GetComponent<BoxCollider2D>();

        // Buildings begin with their light disabled.
        // RefreshLightState decides whether it should be enabled.
        if (myLight != null)
            myLight.enabled = false;
    }

    private void Start()
    {
        Refresh();

        if (isBroken)
            InitializeBrokenState();

        if (!isBroken)
            DoBuildingEffect();

        SetColor();
        RefreshLightState();
    }

    private void SetColor()
    {
        /*
        if (spriteRenderer == null)
            return;

        if (data != null)
        {
            baseColor = data.debugColor;
            spriteRenderer.color = baseColor;
        }
        else
        {
            baseColor = Color.white;
            spriteRenderer.color = baseColor;
        }
        */
    }

    public void SetHovered(bool hovered)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color =
            hovered ? Color.white : baseColor;
    }

    public void SetGridPosition(Vector2Int gridPos)
    {
        GridPosition = gridPos;
    }

    /// <summary>
    /// Returns every grid tile occupied by this building.
    /// </summary>
    public IEnumerable<Vector2Int> GetOccupiedTiles()
    {
        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int y = 0; y < footprintSize.y; y++)
            {
                yield return GridPosition +
                             new Vector2Int(x, y);
            }
        }
    }

    /// <summary>
    /// Returns the offset from the anchor tile to the visual center
    /// of the complete building footprint.
    /// </summary>
    public static Vector2 GetFootprintCenterOffset(
        Vector2Int footprint)
    {
        return new Vector2(
            (footprint.x - 1) * 0.5f,
            (footprint.y - 1) * 0.5f
        );
    }

    public void SetResourcesWeAreMadeOf()
    {
        resourcesWeAreMadeOf =
            new Dictionary<ResourceType, int>();

        if (data == null && BuildingData.Instance != null)
            data = BuildingData.Instance.GetBuilding(type);

        if (data == null)
            return;

        if (data.resourceRequired != ResourceType.none)
        {
            if (!resourcesWeAreMadeOf.ContainsKey(
                    data.resourceRequired))
            {
                resourcesWeAreMadeOf[
                    data.resourceRequired
                ] = 0;
            }

            resourcesWeAreMadeOf[
                data.resourceRequired
            ] += 1;
        }

        int woodCount = Mathf.Max(0, data.woodToBuild);

        if (woodCount > 0)
        {
            if (!resourcesWeAreMadeOf.ContainsKey(
                    ResourceType.wood))
            {
                resourcesWeAreMadeOf[
                    ResourceType.wood
                ] = 0;
            }

            resourcesWeAreMadeOf[
                ResourceType.wood
            ] += woodCount;
        }
    }

    private void DoBuildingEffect()
    {
        // ───────── LIVE-IN WORKPLACES ─────────
        //
        // A live-in workplace spawns its own employee instead of
        // waiting for the ordinary employment system.
        //
        // SpawnLiveInEmployee:
        // 1. Spawns a job seeker.
        // 2. Immediately changes them to data.jobType.
        // 3. Assigns them to this workplace.
        // 4. AssignAgent also makes this building their home.
        //
        // This check happens before the BuildingType switch so new
        // live-in workplace types do not require hardcoded cases.
        if (data != null &&
            data.employeeLivesHere &&
            data.employeeCapacity > 0)
        {
            SpawnLiveInEmployee();
            return;
        }

        // ───────── ORDINARY BUILDINGS ─────────

        switch (type)
        {
            case BuildingType.house:
            {
                Agent agentToHouse = FindExistingVagrant();

                if (agentToHouse == null)
                {
                    Vector3 spawnLocation =
                        FindSpawnNear(transform.position);

                    agentToHouse =
                        GameManager.Instance.SpawnAgent(
                            AgentType.jobSeeker,
                            spawnLocation
                        );

                    if (agentToHouse == null)
                        return;

                    agentToHouse.outsider = false;
                }

                agentToHouse.myHouse = this;

                if (agentToHouse.type == AgentType.vagrant)
                {
                    agentToHouse.SetRole(
                        AgentType.jobSeeker
                    );
                }

                break;
            }

            case BuildingType.animalPen:
            {
                Vector3 spawnLocationAnimal =
                    FindSpawnNear(transform.position, 0);

                Agent animal =
                    GameManager.Instance.SpawnAgent(
                        AgentType.pig,
                        spawnLocationAnimal
                    );

                if (animal == null)
                    return;

                animal.outsider = false;

                Vector2Int penTile =
                    AgentPathing.WorldToGrid(
                        animal,
                        transform.position
                    );

                AgentPlacement.MoveAgentIntoBuilding(
                    animal,
                    penTile,
                    true
                );

                break;
            }
        }
    }

    /// <summary>
    /// Spawns and configures the resident employee for a workplace
    /// whose ScriptableBuilding has employeeLivesHere enabled.
    /// </summary>
    private void SpawnLiveInEmployee()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning(
                $"[Building] Could not spawn a live-in employee for " +
                $"{name} because GameManager.Instance is null."
            );

            return;
        }

        if (data == null)
        {
            Debug.LogWarning(
                $"[Building] Could not spawn a live-in employee for " +
                $"{name} because the building has no ScriptableBuilding data."
            );

            return;
        }

        if (!data.employeeLivesHere)
            return;

        if (data.employeeCapacity <= 0)
        {
            Debug.LogWarning(
                $"[Building] {name} is marked as a live-in workplace, " +
                $"but its employee capacity is zero."
            );

            return;
        }

        // A live-in workplace requires a specific job.
        if (data.jobType == AgentType.none)
        {
            Debug.LogWarning(
                $"[Building] {name} is marked as a live-in workplace, " +
                $"but its ScriptableBuilding jobType is AgentType.none."
            );

            return;
        }

        // Protect against duplicate spawning if DoBuildingEffect is
        // accidentally called more than once.
        if (assignedAgent != null)
            return;

        Vector3 spawnLocation =
            FindSpawnNear(transform.position);

        // Spawn through the standard civilian pathway first.
        Agent liveInEmployee =
            GameManager.Instance.SpawnAgent(
                AgentType.jobSeeker,
                spawnLocation
            );

        if (liveInEmployee == null)
        {
            Debug.LogWarning(
                $"[Building] Failed to spawn the live-in employee " +
                $"for {name}."
            );

            return;
        }

        liveInEmployee.outsider = false;

        // Immediately apply the job configured on this building asset.
        // This uses SetRole rather than directly changing agent.type so
        // any role-specific initialization inside SetRole still runs.
        liveInEmployee.SetRole(data.jobType);

        // Assigns the workplace relationship.
        //
        // Because employeeLivesHere is enabled, AssignAgent also sets:
        // liveInEmployee.myHouse = this;
        AssignAgent(liveInEmployee);
    }

    private Agent FindExistingVagrant()
    {
        var gm = GameManager.Instance;

        if (gm == null)
            return null;

        foreach (var list in gm.AllAgents.Values)
        {
            foreach (var agent in list)
            {
                if (agent == null)
                    continue;

                if (agent.outsider)
                    continue;

                if (agent.myHouse != null)
                    continue;

                if (agent.type == AgentType.vagrant)
                    return agent;
            }
        }

        return null;
    }

    public bool HasEmployee()
    {
        return assignedAgent != null;
    }

    public bool CanAcceptEmployee()
    {
        if (data == null)
            return false;

        if (assignedAgent != null)
            return false;

        return data.employeeCapacity > 0;
    }

    public void AssignAgent(Agent agent)
    {
        // Clean up an existing employee before replacing them.
        if (assignedAgent != null &&
            assignedAgent != agent)
        {
            Agent previousEmployee = assignedAgent;

            if (previousEmployee.myWorkplace == this)
                previousEmployee.myWorkplace = null;

            if (data != null &&
                data.employeeLivesHere &&
                previousEmployee.myHouse == this)
            {
                previousEmployee.myHouse = null;
            }

            residentsInside.Remove(previousEmployee);
        }

        assignedAgent = agent;
        workerPresent = false;

        if (agent != null)
        {
            agent.myWorkplace = this;

            // A live-in workplace is both the employee's workplace
            // and their home.
            if (data != null && data.employeeLivesHere)
                agent.myHouse = this;
        }

        RefreshLightState();
    }

    public void RemoveAgent()
    {
        if (assignedAgent != null)
        {
            Agent employeeBeingRemoved = assignedAgent;

            if (employeeBeingRemoved.myWorkplace == this)
                employeeBeingRemoved.myWorkplace = null;

            if (data != null &&
                data.employeeLivesHere &&
                employeeBeingRemoved.myHouse == this)
            {
                employeeBeingRemoved.myHouse = null;
            }

            residentsInside.Remove(employeeBeingRemoved);
        }

        assignedAgent = null;
        workerPresent = false;

        RefreshLightState();
    }

    /// <summary>
    /// True only when the assigned employee is physically present
    /// around the workplace.
    /// </summary>
    public bool IsStaffed =>
        assignedAgent != null &&
        workerPresent;

    /// <summary>
    /// True when at least one resident is physically sleeping inside.
    /// </summary>
    public bool HasResidentsInside
    {
        get
        {
            RemoveInvalidResidents();
            return residentsInside.Count > 0;
        }
    }

    /// <summary>
    /// True when somebody is working around the building or sleeping
    /// inside it.
    /// </summary>
    public bool IsOccupied =>
        IsStaffed || HasResidentsInside;

    public void NotifyWorkerArrived(Agent worker)
    {
        if (worker == null)
            return;

        if (worker != assignedAgent)
            return;

        workerPresent = true;
        RefreshLightState();
    }

    public void NotifyWorkerDeparted(Agent worker)
    {
        if (worker == null)
            return;

        if (worker != assignedAgent)
            return;

        workerPresent = false;
        RefreshLightState();
    }

    public void NotifyResidentEntered(Agent resident)
    {
        if (resident == null)
            return;

        // Only agents who consider this building their home count as
        // residents sleeping inside it.
        if (resident.myHouse != this)
            return;

        residentsInside.Add(resident);
        RefreshLightState();
    }

    public void NotifyResidentDeparted(Agent resident)
    {
        if (resident == null)
            return;

        residentsInside.Remove(resident);
        RefreshLightState();
    }

    private void RemoveInvalidResidents()
    {
        residentsInside.RemoveWhere(
            resident =>
                resident == null ||
                resident.myHouse != this ||
                resident.isDead ||
                resident.isLeavingTown
        );
    }

    public void RefreshLightState()
    {
        if (myLight == null)
            return;

        RemoveInvalidResidents();

        bool isNight =
            TimeManager.Instance != null &&
            !TimeManager.Instance.IsDaytime;

        bool someoneIsAroundBuilding =
            IsStaffed ||
            residentsInside.Count > 0;

        myLight.enabled =
            !isBroken &&
            isNight &&
            someoneIsAroundBuilding;
    }

    public bool CanBeShoppedAt()
    {
        if (data == null)
            return false;

        if (data.employeeCapacity <= 0)
            return true;

        return IsStaffed;
    }

    public static Vector3 FindSpawnNear(
        Vector3 origin,
        float maxRadius = 1f,
        float step = 0.5f)
    {
        for (float r = step; r <= maxRadius; r += step)
        {
            int samples = Mathf.CeilToInt(
                2f * Mathf.PI * r / step
            );

            for (int i = 0; i < samples; i++)
            {
                float angle =
                    (i / (float)samples) *
                    Mathf.PI *
                    2f;

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r,
                    0f
                );

                Vector3 pos = origin + offset;

                if (IsValidSpawn(pos))
                    return pos;
            }
        }

        return origin;
    }

    private static bool IsValidSpawn(Vector3 pos)
    {
        float radius = 0.3f;

        Collider2D hit = Physics2D.OverlapCircle(
            pos,
            radius,
            LayerMask.GetMask("Building")
        );

        return hit == null;
    }

    public void SetType(BuildingType newType)
    {
        type = newType;

        data = BuildingData.Instance != null
            ? BuildingData.Instance.GetBuilding(type)
            : null;

        footprintSize = data != null
            ? new Vector2Int(
                Mathf.Max(1, data.footprintSize.x),
                Mathf.Max(1, data.footprintSize.y)
            )
            : Vector2Int.one;

        Refresh();
        SetColor();
        RefreshLightState();
    }

    public void Refresh()
    {
        if (textMesh != null)
            textMesh.text = type.ToString();

        if (spriteRenderer != null &&
            type != BuildingType.none)
        {
            string spriteName = isBroken
                ? $"Sprites/Buildings/{type}_broken"
                : $"Sprites/Buildings/{type}";

            Sprite mainSprite =
                Help.GetSprite(spriteName);

            if (mainSprite == null && isBroken)
            {
                mainSprite = Help.GetSprite(
                    $"Sprites/Buildings/{type}"
                );
            }

            spriteRenderer.sprite = mainSprite;

            if (shadow != null)
            {
                string shadowName = isBroken
                    ? $"Sprites/Buildings/{type}_broken_shadow"
                    : $"Sprites/Buildings/{type}_shadow";

                Sprite shadowSprite =
                    Help.GetSprite(shadowName);

                if (shadowSprite == null)
                {
                    shadowSprite = Help.GetSprite(
                        $"Sprites/Buildings/{type}_shadow"
                    );
                }

                shadow.sprite = shadowSprite;
            }

            if (myLight != null)
            {
                Sprite lightSprite =
                    Help.GetSprite(
                        $"Sprites/Buildings/{type}_light"
                    );

                myLight.sprite = lightSprite;
            }
        }
    }

    private void LateUpdate()
    {
        UpdateSorting();
    }

    [SerializeField]
    private float sortingOffset = 0.5f;

    private void UpdateSorting()
    {
        if (spriteRenderer == null)
            return;

        float adjustedY =
            transform.position.y - sortingOffset;

        spriteRenderer.sortingOrder =
            Mathf.RoundToInt(-adjustedY * 100);

        if (myLight != null)
        {
            myLight.sortingOrder =
                Mathf.RoundToInt(
                    -adjustedY * 100 + 2
                );
        }
    }

    public void DestroyAndRefund()
    {
        if (isBroken)
        {
            Debug.Log(
                $"[Building] {name} is broken and cannot be destroyed."
            );

            return;
        }

        if (refundedResources)
        {
            Destroy(gameObject);
            return;
        }

        refundedResources = true;

        RefundConstructionResources();
        Destroy(gameObject);
    }

    private void RefundConstructionResources()
    {
        if (resourcesWeAreMadeOf == null ||
            resourcesWeAreMadeOf.Count == 0)
        {
            SetResourcesWeAreMadeOf();
        }

        if (resourcesWeAreMadeOf == null ||
            resourcesWeAreMadeOf.Count == 0)
        {
            return;
        }

        Vector3 origin = transform.position;

        Vector2Int originTile =
            LevelCreator.Instance != null
                ? LevelCreator.Instance.WorldToGrid(origin)
                : Vector2Int.zero;

        foreach (var res in resourcesWeAreMadeOf)
        {
            ResourceType resourceType = res.Key;
            int amount = Mathf.Max(0, res.Value);

            for (int i = 0; i < amount; i++)
            {
                if (resourceType == ResourceType.wood)
                {
                    CreateWoodRefund(
                        resourceType,
                        originTile,
                        origin
                    );
                }
                else
                {
                    CreateBelowRefund(
                        resourceType,
                        originTile,
                        origin
                    );
                }
            }
        }
    }

    private void CreateWoodRefund(
        ResourceType type,
        Vector2Int originTile,
        Vector3 originWorld)
    {
        Vector2Int tile =
            FindRandomOpenTileNear(originTile, 3);

        Vector3 worldPos = originWorld;

        if (LevelCreator.Instance != null)
        {
            worldPos =
                LevelCreator.Instance.GridToWorld(tile);
        }

        SpawnResource(type, worldPos, tile);
    }

    private void CreateBelowRefund(
        ResourceType type,
        Vector2Int originTile,
        Vector3 originWorld)
    {
        Vector2Int tile = originTile;
        Vector3 worldPos = originWorld;

        if (LevelCreator.Instance != null)
        {
            worldPos =
                LevelCreator.Instance.GridToWorld(tile);
        }

        SpawnResource(type, worldPos, tile);
    }

    private Vector2Int FindRandomOpenTileNear(
        Vector2Int originTile,
        int radius)
    {
        List<Vector2Int> openTiles =
            new List<Vector2Int>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int tile =
                    originTile +
                    new Vector2Int(x, y);

                if (x == 0 && y == 0)
                    continue;

                if (!IsTileOpen(tile))
                    continue;

                openTiles.Add(tile);
            }
        }

        if (openTiles.Count == 0)
        {
            return originTile +
                   new Vector2Int(
                       UnityEngine.Random.Range(-1, 2),
                       UnityEngine.Random.Range(-1, 2)
                   );
        }

        return openTiles[
            UnityEngine.Random.Range(
                0,
                openTiles.Count
            )
        ];
    }

    private bool IsTileOpen(Vector2Int tile)
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.TryGetBuildingAt(
                    tile,
                    out _))
            {
                return false;
            }

            if (GameManager.Instance.TryGetResourceAt(
                    tile,
                    out Resource resource) &&
                resource != null)
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnResource(
        ResourceType type,
        Vector3 worldPos,
        Vector2Int tile)
    {
        if (LevelCreator.Instance == null ||
            LevelCreator.Instance.resourcePrefab == null)
        {
            return;
        }

        Vector3 snappedWorldPos = worldPos;

        if (LevelCreator.Instance != null)
        {
            snappedWorldPos =
                LevelCreator.Instance.GridToWorld(tile);
        }

        GameObject obj = Instantiate(
            LevelCreator.Instance.resourcePrefab,
            snappedWorldPos,
            Quaternion.identity
        );

        Resource resource =
            obj.GetComponent<Resource>();

        if (resource != null)
        {
            resource.SetGridPosition(tile);
            resource.SetType(type);
            resource.usedBefore = true;
        }
    }

    public void BreakBuilding()
    {
        if (isBroken)
            return;

        isBroken = true;

        if (shadow != null)
            shadow.enabled = false;

        int currentTick = GetCurrentTick();
        int repairTicks = GetRepairTicks();

        brokenAtTick = currentTick;

        repairAtTick =
            currentTick +
            Mathf.Max(1, repairTicks);

        if (assignedAgent != null)
            RemoveAgent();

        residentsInside.Clear();

        Refresh();
        SetColor();
        RefreshLightState();
    }

    public void RepairBuilding()
    {
        if (!isBroken)
            return;

        if (shadow != null)
            shadow.enabled = true;

        isBroken = false;
        brokenAtTick = -1;
        repairAtTick = -1;

        Refresh();
        SetColor();
        RefreshLightState();
    }

    public void TickUpdate(int currentTick)
    {
        if (!isBroken)
            return;

        if (repairAtTick < 0)
        {
            repairAtTick =
                currentTick +
                Mathf.Max(1, GetRepairTicks());
        }

        if (currentTick >= repairAtTick)
            RepairBuilding();
    }

    private void InitializeBrokenState()
    {
        int currentTick = GetCurrentTick();

        if (brokenAtTick < 0)
            brokenAtTick = currentTick;

        if (repairAtTick < 0)
        {
            repairAtTick =
                currentTick +
                Mathf.Max(1, GetRepairTicks());
        }
    }

    private int GetCurrentTick()
    {
        return EventManager.Instance != null
            ? EventManager.Instance.CurrentTick
            : 0;
    }

    private int GetRepairTicks()
    {
        if (data == null &&
            BuildingData.Instance != null)
        {
            data =
                BuildingData.Instance.GetBuilding(type);
        }

        if (data == null)
            return 1;

        return data.ticksUntilRepaired;
    }

    private int ConvertToInt(object value)
    {
        if (value == null)
            return -1;

        try
        {
            if (value is int i)
                return i;

            if (value is float f)
                return Mathf.RoundToInt(f);

            if (value is double d)
                return Mathf.RoundToInt((float)d);

            return Convert.ToInt32(value);
        }
        catch
        {
            return -1;
        }
    }

    private void OnDestroy()
    {
        if (assignedAgent != null)
        {
            if (assignedAgent.myWorkplace == this)
                assignedAgent.myWorkplace = null;

            if (assignedAgent.myHouse == this)
                assignedAgent.myHouse = null;
        }

        residentsInside.Clear();

        if (GameManager.Instance != null &&
            GridPosition.x != int.MinValue)
        {
            GameManager.Instance.DeregisterBuilding(this);
        }
    }
}
