# BoomTown Multi-Target AI Changes

## Changed Classes

### `AgentEnums.cs`

- 🎯 Added target types: **Agent**, **Building**, and **Location**.
- 🔍 Added separate target selectors for each target type.
- 📍 Added location categories such as **Town Centre**.

### `ScriptableAgent.cs`

- 🎯 Each interaction can now choose its target type.
- ⚙️ Added agent, building, and location selector settings.
- 🏷️ Added location filters alongside the existing tag filters.
- ⚠️ Added warnings for unsupported action and target combinations.

### `ScriptableBuilding.cs`

- 🏷️ Added tags so actions can filter buildings.
- ❤️ Added a configurable maximum-health value.

### `Agent.cs`

- 🎯 Made agents implement the shared interaction-target system.
- 💥 Made agents implement the shared damage system.
- 🏷️ Agents can match interaction filters using their tags.
- 📍 Agents provide their position and approach information.

### `Building.cs`

- 🎯 Buildings can now be selected as interaction targets.
- ❤️ Added current health and maximum health.
- 💥 Buildings can receive damage and break.
- 🔧 Repairing restores the building’s health.
- 🏷️ Buildings can match interactions using building tags.
- 📍 Buildings provide an accessible approach tile.

### `SocialActions.cs`

- 🔀 Generalized actions to accept agents, buildings, or locations.
- ⚔️ Combat now supports both agents and buildings.
- 📦 Depositing resources now explicitly targets buildings.
- 👤 Agent-only actions reject building and location targets.
- ⚠️ Unsupported target combinations are safely ignored.

### `ActionManager.cs`

- 🔍 Finds targets based on whether they are agents, buildings, or locations.
- 🏠 Added **My Workplace**, **My Home**, and **Nearest Building** selection.
- 📍 Added nearest-location selection.
- ✅ Validates each target according to its target type.
- 🚶 Uses the correct approach and movement logic for each target type.
- 🎯 Tracks the active target through the shared target interface.

### `GameManager.cs`

- 🧹 Clears registered map locations when the game initializes.

---

## New Classes

### `IInteractionTarget.cs`

- 🤝 Provides one shared target contract for agents, buildings, and locations.
- 📍 Requires each target to provide its position and approach information.
- 🔍 Requires each target to support interaction-filter matching.

### `IDamageable.cs`

- 💥 Provides a shared damage contract.
- ⚔️ Allows combat to damage both agents and buildings consistently.

### `MapLocation.cs`

- 📍 Represents a targetable world location.
- 🗺️ Stores its location type and world position.
- 🚶 Provides a walkable approach position.

### `LocationRegistry.cs`

- 📋 Stores registered map locations.
- 🔍 Finds the nearest location matching an interaction.
- 🧹 Supports clearing locations when restarting or initializing the game.

---

## Updated Editor Classes

### `AgentInspectorEditor.cs`

- 🎯 Updated the debugger to support **Agent**, **Building**, and **Location** targets.
- 🤝 Reads the active target through `IInteractionTarget` instead of assuming it is always an agent.
- 🔍 Displays the configured target kind for every interaction.
- 👤 Displays the selected agent-target selector.
- 🏠 Displays the selected building-target selector.
- 📍 Displays the selected location-target selector.
- 🏷️ Displays target tags for agent and nearest-building interactions.
- 🗺️ Displays location-type filters for location interactions.
- 🚫 Explains when target tags are not used by direct selectors such as **My Workplace** or **My Home**.
- ✅ Shows whether the selected action supports the configured target type.
- ⚠️ Clearly marks unsupported action-and-target combinations.
- 👤 Displays agent-specific target information such as agent type and death state.
- 🏚️ Displays building-specific information such as building type, health, maximum health, and broken state.
- 🗺️ Displays the type of an active map-location target.
- 📏 Calculates the target distance using the shared target world position.
- 🎬 Continues to show the current action, priority, range, cooldown, and condition state.
- 🧠 Keeps the live social-action debugger updated during Play Mode.

### `ConditionDrawer_editor.cs`

- ✅ Confirmed that no multi-target-specific logic was required.
- 🧹 Removed copied HTML formatting such as `<br>` elements.
- 🔣 Replaced HTML entities such as `&gt;` and `&lt;` with valid C# operators.
- ☑️ Preserved Boolean **True/False** condition editing.
- 🔢 Preserved numeric value-comparison editing.
- ➕ Preserved comparison operators such as `=`, `!=`, `>`, `>=`, `<`, and `<=`.
- 📝 Preserved the readable condition-value description.
- 🎨 Preserved the custom background, border, and result colours.
- 📐 Preserved the dynamic property height for Boolean and comparison modes.
