
# 🧠 BoomTown AI Architecture

## ⚡ Quick Overview

```text
📜 ScriptableAgent
    ↓
Defines Interactions

🎯 ActionManager
    ↓
Chooses Interaction
    ↓
Finds Target
    ↓
Moves Into Range
    ↓
Proposes Action

🎬 AgentActionState
    ↓
Accepts or Rejects Proposal
    ↓
Handles Priority
    ↓
Handles Interruptions
    ↓
Plays Animation

⚙️ SocialActions
    ↓
Executes Gameplay Logic
    ↓
Changes Progress / State

✅ Conditions + 📈 Progress
    ↓
Unlock New Interactions
    ↓
Create Behaviour Chains
```

---

# 📜 ScriptableAgent

Defines what an agent can do.

Contains:

```text
🏷️ Tags
📊 Stats
🤝 Interactions
💰 Spending
🏢 Hangouts
```

Interactions are data driven and contain:

```text
✅ Conditions
⭐ Priority
🎲 Chance
📏 Range
⏱️ Cooldown
```

---

# 🎯 ActionManager

Responsible for decision making.

Flow:

```text
Find Valid Interaction
    ↓
Find Target
    ↓
Move Into Range
    ↓
Propose Action To AgentActionState
```

ActionManager answers:

> 🤔 "What should I do?"

---

# 🎬 AgentActionState

The owner of the agent's current action.

All systems must propose actions to AgentActionState.

Examples:

```text
🎯 ActionManager
    proposes Attack

🚶 AgentMovement
    proposes Walking

😴 WorkSchedule
    proposes Sleep
```

AgentActionState then decides:

```text
Accept?
Reject?
Interrupt Current Action?
```

Based on:

```text
⭐ Priority
🔒 Locks
⏱️ Duration
```

Responsibilities:

```text
🎭 Current Action
🎞️ Animation Playback
⭐ Priority Handling
🔒 Locks
⛔ Interruptions
```

AgentActionState answers:

> 🤷 "What am I currently doing?"

---

# ⚙️ SocialActions

Responsible for gameplay execution.

Examples:

```text
⚔️ CombatAction
❤️ HealAction
✝️ ConvertAction
🦌 TrackAction
🔪 HarvestAnimalAction
```

SocialActions answers:

> 🔨 "What happens when this interaction executes?"

---

# ✅ Conditions

Conditions determine whether an interaction is available.

Examples:

```text
🌞 TimeOfDay

🐖 HaveAnimal

🦴 AnimalDead

🎒 CarryingResources

🦌 HasTrackableAnimal
```

Conditions drive behaviour.

---

# 📈 Progress System

Progress lives on the thing being acted upon.

Example:

```csharp
target.Progress[InteractionType.Track] += 1;
```

Examples:

```text
🦌 Tracking

✝️ Conversion

🤝 Friendship

👻 Possession

🔍 Investigation
```

Progress accumulates over time and unlocks future interactions.

---

# 🔗 Behaviour Chaining

```text
✅ Condition Becomes True
    ↓
🤝 Interaction Becomes Available
    ↓
🎯 ActionManager Selects It
    ↓
🎬 AgentActionState Accepts Proposal
    ↓
⚙️ SocialAction Executes
    ↓
📈 Progress / State Changes
    ↓
✅ New Conditions Become True
    ↓
🤝 Next Interaction Unlocks
```

Example:

```text
🦌 Track
    ↓
⚔️ Combat
    ↓
🔪 Harvest
    ↓
🏕️ Deposit Resources
```

Each interaction creates the conditions required for the next one.
