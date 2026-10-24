using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum BuildingType
{
    none,
    house,
    farm,
    sheriffsOffice,
    saloon,
    tradepost,
    doctorsOffice,
    chapel,
    militaryOutpost,
    coffinMaker,
    generalStore,
    countyJail,
    townHall,
    newspaperOffice,
    bank,
    casino,
    nativeAmericanCamp,
    hotel,
    mine,
    nightWatchTower,
    animalPen,
    huntersHut
}

[System.Serializable]
public enum BuildingCategoryType
{
    growth,
    wealth,
    defense,
    star
}

[System.Serializable]
public class CostSaver
{
    public stat costType;
    public amounts amount;
    public float percentage;
}

[CreateAssetMenu(fileName = "NewBuilding", menuName = "NewBuilding")]
public class ScriptableBuilding : ScriptableObject
{
    [Header("Basic Building Info")]
    public BuildingType type;
    public BuildingCategoryType buildingCategory = BuildingCategoryType.growth;
    public Color debugColor = Color.white;

    // Footprint size in tiles: X = width, Y = height.
    public Vector2Int footprintSize = Vector2Int.one;

    public ResourceType resourceRequired = ResourceType.none;
    public List<CostSaver> costSavers = new();

    public float costToUnlock = 0;
    public int costToBuy = 0;
    public int woodToBuild = 0;
    public int ticksUntilRepaired = 0;

    [Header("Job Info")]
    public AgentType jobType = AgentType.none;
    public int employeeCapacity = 1;

    [Header("Housing Info")]
    [Tooltip(
        "If enabled, the employee assigned to this building also uses " +
        "the building as their home. For example, the hunter lives in " +
        "the hunter's hut."
    )]
    public bool employeeLivesHere = false;
}
