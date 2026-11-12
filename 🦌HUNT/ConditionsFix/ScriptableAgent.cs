using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum AgentType
{
    none,
    jobSeeker,
    farmer,
    lawman,
    miner,
    prostitute,
    railwayMan,
    military,
    gangMember,
    entertainer,
    doctor,
    preacher,
    trader,
    vagrant,
    oilMan,
    hostileTroop,
    diseased,
    nativeAmerican,
    ghost,
    artisan,
    casinoHost,
    banker,
    gambler,
    furTrader,
    foodTrader,
    nightWatcher,
    pig,
    cayote,
    hunter,
    bigHornSheep
}

public enum TeamType
{
    none,
    townDefenders,
    gang,
    hostileTroops
}

#region TAG SYSTEM

public enum AgentTag
{
    None,

    Prey,
    Predator,

    Civilian,
    Enemy,
    Ally,

    Animal,
    Human,
    Undead
}

#endregion

#region STATS & SPENDING

[System.Serializable]
public class PointStat
{
    public scoreType typeOfScore;
    public float amount;
}

[System.Serializable]
public class BuyStat
{
    public AgentType who;
    public float amount;
}

[System.Serializable]
public class BuyBehaviour
{
    public BuildingType where;
    public int chance;
    public float amount;
}

#endregion

// =====================================================
// INTERACTION CONDITION SYSTEM
// =====================================================

#region INTERACTION SYSTEM

/// <summary>
/// Determines how a condition should be evaluated.
///
/// Boolean:
///     The condition must equal Required State.
///
/// ValueComparison:
///     The condition's numeric value is compared against Required Value.
/// </summary>
public enum ConditionEvaluationMode
{
    Boolean,
    ValueComparison
}

/// <summary>
/// Numeric comparison operators available to value-based conditions.
/// </summary>
public enum ConditionComparison
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

[System.Serializable]
public class Condition
{
    [Tooltip("The state or value this condition reads.")]
    public ConditionType condition;

    [Tooltip(
        "Boolean checks whether the condition is True or False. " +
        "Value Comparison compares its numeric value with another number."
    )]
    public ConditionEvaluationMode evaluationMode =
        ConditionEvaluationMode.Boolean;

    [Tooltip(
        "Used only in Boolean mode. " +
        "Enable when the condition must be True. " +
        "Disable when the condition must be False."
    )]
    public bool requiredState = true;

    [Tooltip(
        "Used only in Value Comparison mode."
    )]
    public ConditionComparison comparison =
        ConditionComparison.GreaterThanOrEqual;

    [Tooltip(
        "Used only in Value Comparison mode."
    )]
    public float requiredValue = 0f;
}

[System.Serializable]
public class AgentInteraction
{
    [Tooltip("Targets any agent that has ANY of these tags.")]
    public List<AgentTag> targetTags =
        new List<AgentTag>();

    public InteractionType type;

    [Tooltip(
        "Every condition must pass for this interaction to be selected."
    )]
    public List<Condition> conditions =
        new List<Condition>();

    public ActionPriority priority =
        ActionPriority.none;

    [Range(0f, 1f)]
    public float chance = 1f;

    [Min(0f)]
    public float range = 1f;

    [Min(0f)]
    public float cooldown = 1f;

    public float value = 0f;

    [Header("Movement")]
    [Tooltip(
        "If true, this interaction may path onto the target's " +
        "building tile. For example, attacking an animal inside a pen."
    )]
    public bool canEnterTargetBuilding = false;

    [Header("Combat Positioning - for full fidelity")]
    public float alignmentTolerance = 0.20f;
    public float standOffDistance = 1.0f;
}

#endregion

// =====================================================
// SCRIPTABLE AGENT
// =====================================================

[CreateAssetMenu(
    fileName = "NewAgent",
    menuName = "NewAgent"
)]
public class ScriptableAgent : ScriptableObject
{
    [Header("Basic Agent Info")]
    public AgentType type;
    public Color debugColor;
    public TeamType team = TeamType.none;

    [Header("Agent Stats")]
    [SerializeField]
    public List<PointStat> stats =
        new List<PointStat>();

    [Header("Tags")]
    [SerializeField]
    public List<AgentTag> tags =
        new List<AgentTag>();

    [SerializeField]
    public List<AgentTag> scaredOfTags =
        new List<AgentTag>();

    [Header("Spending")]
    [SerializeField]
    public List<BuyBehaviour> spendsAt =
        new List<BuyBehaviour>();

    [Header("Hangout Preferences")]
    [SerializeField]
    public List<BuildingType> hangsAround =
        new List<BuildingType>();

    [Header("Interactions (Agent ↔ Agent)")]
    [SerializeField]
    public List<AgentInteraction> interactions =
        new List<AgentInteraction>();

    // On a living agent this is unused.
    //
    // When the agent dies, these items become the corpse's
    // harvestable inventory.
    [Header("Starting Inventory / Corpse Drops")]
    [SerializeField]
    public List<InventoryItem> startingInventory =
        new List<InventoryItem>();

    [Header("Carrying")]
    [SerializeField]
    public int carryCapacity = 10;

    [Header("Tracking")]
    [SerializeField]
    public float trackingDifficulty = 1f;
}
