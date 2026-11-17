#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AgentInteraction))]
public class ScriptableAgentInteractionDrawer : PropertyDrawer
{
    private const float OuterPadding = 6f;
    private const float SectionSpacing = 8f;
    private const float LeftBorderWidth = 3f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        float height =
            OuterPadding * 2f;

        SerializedProperty targetKindProperty =
            property.FindPropertyRelative("targetKind");

        SerializedProperty agentTargetSelectorProperty =
            property.FindPropertyRelative(
                "agentTargetSelector"
            );

        SerializedProperty buildingTargetSelectorProperty =
            property.FindPropertyRelative(
                "buildingTargetSelector"
            );

        SerializedProperty locationTargetSelectorProperty =
            property.FindPropertyRelative(
                "locationTargetSelector"
            );

        SerializedProperty targetTagsProperty =
            property.FindPropertyRelative("targetTags");

        SerializedProperty targetLocationTypesProperty =
            property.FindPropertyRelative(
                "targetLocationTypes"
            );

        SerializedProperty typeProperty =
            property.FindPropertyRelative("type");

        SerializedProperty conditionsProperty =
            property.FindPropertyRelative("conditions");

        SerializedProperty priorityProperty =
            property.FindPropertyRelative("priority");

        SerializedProperty chanceProperty =
            property.FindPropertyRelative("chance");

        SerializedProperty rangeProperty =
            property.FindPropertyRelative("range");

        SerializedProperty cooldownProperty =
            property.FindPropertyRelative("cooldown");

        SerializedProperty valueProperty =
            property.FindPropertyRelative("value");

        SerializedProperty canEnterTargetBuildingProperty =
            property.FindPropertyRelative(
                "canEnterTargetBuilding"
            );

        SerializedProperty alignmentToleranceProperty =
            property.FindPropertyRelative(
                "alignmentTolerance"
            );

        SerializedProperty standOffDistanceProperty =
            property.FindPropertyRelative(
                "standOffDistance"
            );

        // Target section header.
        height += GetHeaderHeight();
        height += spacing;

        // Target kind.
        height += GetPropertyHeight(
            targetKindProperty
        );
        height += spacing;

        if (targetKindProperty != null)
        {
            TargetKind targetKind =
                (TargetKind)
                targetKindProperty.enumValueIndex;

            switch (targetKind)
            {
                case TargetKind.Agent:
                    height += GetPropertyHeight(
                        agentTargetSelectorProperty
                    );
                    height += spacing;

                    height += GetPropertyHeight(
                        targetTagsProperty,
                        true
                    );
                    height += spacing;
                    break;

                case TargetKind.Building:
                    height += GetPropertyHeight(
                        buildingTargetSelectorProperty
                    );
                    height += spacing;

                    height += GetPropertyHeight(
                        targetTagsProperty,
                        true
                    );
                    height += spacing;
                    break;

                case TargetKind.Location:
                    height += GetPropertyHeight(
                        locationTargetSelectorProperty
                    );
                    height += spacing;

                    height += GetPropertyHeight(
                        targetLocationTypesProperty,
                        true
                    );
                    height += spacing;
                    break;
            }
        }

        height += SectionSpacing;

        // Interaction section header.
        height += GetHeaderHeight();
        height += spacing;

        height += GetPropertyHeight(typeProperty);
        height += spacing;

        height += GetPropertyHeight(
            conditionsProperty,
            true
        );
        height += spacing;

        height += GetPropertyHeight(priorityProperty);
        height += spacing;

        height += GetPropertyHeight(chanceProperty);
        height += spacing;

        height += GetPropertyHeight(rangeProperty);
        height += spacing;

        height += GetPropertyHeight(cooldownProperty);
        height += spacing;

        height += GetPropertyHeight(valueProperty);
        height += spacing;

        height += SectionSpacing;

        // Movement section header.
        height += GetHeaderHeight();
        height += spacing;

        height += GetPropertyHeight(
            canEnterTargetBuildingProperty
        );
        height += spacing;

        height += SectionSpacing;

        // Combat positioning section header.
        height += GetHeaderHeight();
        height += spacing;

        height += GetPropertyHeight(
            alignmentToleranceProperty
        );
        height += spacing;

        height += GetPropertyHeight(
            standOffDistanceProperty
        );

        return height;
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(
            position,
            label,
            property
        );

        DrawBackground(position);

        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        Rect contentRect =
            new Rect(
                position.x + OuterPadding,
                position.y + OuterPadding,
                position.width - OuterPadding * 2f,
                EditorGUIUtility.singleLineHeight
            );

        SerializedProperty targetKindProperty =
            property.FindPropertyRelative("targetKind");

        SerializedProperty agentTargetSelectorProperty =
            property.FindPropertyRelative(
                "agentTargetSelector"
            );

        SerializedProperty buildingTargetSelectorProperty =
            property.FindPropertyRelative(
                "buildingTargetSelector"
            );

        SerializedProperty locationTargetSelectorProperty =
            property.FindPropertyRelative(
                "locationTargetSelector"
            );

        SerializedProperty targetTagsProperty =
            property.FindPropertyRelative("targetTags");

        SerializedProperty targetLocationTypesProperty =
            property.FindPropertyRelative(
                "targetLocationTypes"
            );

        SerializedProperty typeProperty =
            property.FindPropertyRelative("type");

        SerializedProperty conditionsProperty =
            property.FindPropertyRelative("conditions");

        SerializedProperty priorityProperty =
            property.FindPropertyRelative("priority");

        SerializedProperty chanceProperty =
            property.FindPropertyRelative("chance");

        SerializedProperty rangeProperty =
            property.FindPropertyRelative("range");

        SerializedProperty cooldownProperty =
            property.FindPropertyRelative("cooldown");

        SerializedProperty valueProperty =
            property.FindPropertyRelative("value");

        SerializedProperty canEnterTargetBuildingProperty =
            property.FindPropertyRelative(
                "canEnterTargetBuilding"
            );

        SerializedProperty alignmentToleranceProperty =
            property.FindPropertyRelative(
                "alignmentTolerance"
            );

        SerializedProperty standOffDistanceProperty =
            property.FindPropertyRelative(
                "standOffDistance"
            );

        // =================================================
        // TARGET
        // =================================================

        DrawSectionHeader(
            ref contentRect,
            "Target"
        );

        MoveDown(
            ref contentRect,
            GetHeaderHeight() + spacing
        );

        DrawProperty(
            ref contentRect,
            targetKindProperty,
            new GUIContent(
                "Target Kind",
                "Select whether this interaction targets " +
                "an Agent, Building, or Location."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(targetKindProperty) +
            spacing
        );

        if (targetKindProperty != null)
        {
            TargetKind targetKind =
                (TargetKind)
                targetKindProperty.enumValueIndex;

            switch (targetKind)
            {
                case TargetKind.Agent:
                    DrawAgentTargetControls(
                        ref contentRect,
                        agentTargetSelectorProperty,
                        targetTagsProperty
                    );
                    break;

                case TargetKind.Building:
                    DrawBuildingTargetControls(
                        ref contentRect,
                        buildingTargetSelectorProperty,
                        targetTagsProperty
                    );
                    break;

                case TargetKind.Location:
                    DrawLocationTargetControls(
                        ref contentRect,
                        locationTargetSelectorProperty,
                        targetLocationTypesProperty
                    );
                    break;
            }
        }

        MoveDown(
            ref contentRect,
            SectionSpacing
        );

        // =================================================
        // INTERACTION
        // =================================================

        DrawSectionHeader(
            ref contentRect,
            "Interaction"
        );

        MoveDown(
            ref contentRect,
            GetHeaderHeight() + spacing
        );

        DrawProperty(
            ref contentRect,
            typeProperty,
            new GUIContent(
                "Type",
                "The interaction that will be performed."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(typeProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            conditionsProperty,
            new GUIContent(
                "Conditions",
                "Every configured condition must pass " +
                "before this interaction can be selected."
            ),
            true
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(
                conditionsProperty,
                true
            ) + spacing
        );

        DrawProperty(
            ref contentRect,
            priorityProperty,
            new GUIContent(
                "Priority",
                "The action priority assigned to this interaction."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(priorityProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            chanceProperty,
            new GUIContent(
                "Chance",
                "The probability that this interaction is selected."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(chanceProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            rangeProperty,
            new GUIContent(
                "Range",
                "The maximum range at which the interaction " +
                "can be performed."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(rangeProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            cooldownProperty,
            new GUIContent(
                "Cooldown",
                "The delay before this interaction can be " +
                "performed again."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(cooldownProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            valueProperty,
            new GUIContent(
                "Value",
                "The configurable numeric value used by " +
                "this interaction."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(valueProperty) +
            SectionSpacing
        );

        // =================================================
        // MOVEMENT
        // =================================================

        DrawSectionHeader(
            ref contentRect,
            "Movement"
        );

        MoveDown(
            ref contentRect,
            GetHeaderHeight() + spacing
        );

        DrawProperty(
            ref contentRect,
            canEnterTargetBuildingProperty,
            new GUIContent(
                "Can Enter Target Building",
                "Allows this interaction to path onto the " +
                "target's building tile."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(
                canEnterTargetBuildingProperty
            ) + SectionSpacing
        );

        // =================================================
        // COMBAT POSITIONING
        // =================================================

        DrawSectionHeader(
            ref contentRect,
            "Combat Positioning"
        );

        MoveDown(
            ref contentRect,
            GetHeaderHeight() + spacing
        );

        DrawProperty(
            ref contentRect,
            alignmentToleranceProperty,
            new GUIContent(
                "Alignment Tolerance",
                "How accurately the agent must align with " +
                "the target position."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(
                alignmentToleranceProperty
            ) + spacing
        );

        DrawProperty(
            ref contentRect,
            standOffDistanceProperty,
            new GUIContent(
                "Stand-Off Distance",
                "The desired distance the acting agent keeps " +
                "from the target."
            )
        );

        EditorGUI.EndProperty();
    }

    private void DrawAgentTargetControls(
        ref Rect contentRect,
        SerializedProperty selectorProperty,
        SerializedProperty tagsProperty)
    {
        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        DrawProperty(
            ref contentRect,
            selectorProperty,
            new GUIContent(
                "Agent Selector",
                "Determines how the target Agent is selected."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(selectorProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            tagsProperty,
            new GUIContent(
                "Agent Tags",
                "The Agent must have at least one of these tags."
            ),
            true
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(
                tagsProperty,
                true
            ) + spacing
        );
    }

    private void DrawBuildingTargetControls(
        ref Rect contentRect,
        SerializedProperty selectorProperty,
        SerializedProperty tagsProperty)
    {
        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        DrawProperty(
            ref contentRect,
            selectorProperty,
            new GUIContent(
                "Building Selector",
                "Determines how the target Building is selected."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(selectorProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            tagsProperty,
            new GUIContent(
                "Building Tags",
                "The Building must have at least one of these tags."
            ),
            true
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(
                tagsProperty,
                true
            ) + spacing
        );
    }

    private void DrawLocationTargetControls(
        ref Rect contentRect,
        SerializedProperty selectorProperty,
        SerializedProperty locationTypesProperty)
    {
        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        DrawProperty(
            ref contentRect,
            selectorProperty,
            new GUIContent(
                "Location Selector",
                "Determines how the target Location is selected."
            )
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(selectorProperty) +
            spacing
        );

        DrawProperty(
            ref contentRect,
            locationTypesProperty,
            new GUIContent(
                "Location Types",
                "The Location must match at least one of " +
                "these location types."
            ),
            true
        );

        MoveDown(
            ref contentRect,
            GetPropertyHeight(
                locationTypesProperty,
                true
            ) + spacing
        );
    }

    private void DrawProperty(
        ref Rect rect,
        SerializedProperty propertyToDraw,
        GUIContent propertyLabel,
        bool includeChildren = false)
    {
        if (propertyToDraw == null)
            return;

        float height =
            GetPropertyHeight(
                propertyToDraw,
                includeChildren
            );

        Rect propertyRect =
            new Rect(
                rect.x,
                rect.y,
                rect.width,
                height
            );

        EditorGUI.PropertyField(
            propertyRect,
            propertyToDraw,
            propertyLabel,
            includeChildren
        );
    }

    private void DrawSectionHeader(
        ref Rect rect,
        string title)
    {
        Rect headerRect =
            new Rect(
                rect.x,
                rect.y,
                rect.width,
                GetHeaderHeight()
            );

        Color headerColor =
            EditorGUIUtility.isProSkin
                ? new Color(
                    0.22f,
                    0.26f,
                    0.31f,
                    1f
                )
                : new Color(
                    0.72f,
                    0.78f,
                    0.84f,
                    1f
                );

        EditorGUI.DrawRect(
            headerRect,
            headerColor
        );

        GUIStyle headerStyle =
            new GUIStyle(
                EditorStyles.boldLabel
            );

        headerStyle.alignment =
            TextAnchor.MiddleLeft;

        headerStyle.padding =
            new RectOffset(
                6,
                0,
                0,
                0
            );

        headerStyle.normal.textColor =
            EditorGUIUtility.isProSkin
                ? new Color(
                    0.9f,
                    0.94f,
                    1f
                )
                : new Color(
                    0.12f,
                    0.18f,
                    0.25f
                );

        EditorGUI.LabelField(
            headerRect,
            title,
            headerStyle
        );
    }

    private void DrawBackground(
        Rect position)
    {
        Color backgroundColor =
            EditorGUIUtility.isProSkin
                ? new Color(
                    0.16f,
                    0.16f,
                    0.16f,
                    1f
                )
                : new Color(
                    0.9f,
                    0.9f,
                    0.9f,
                    1f
                );

        EditorGUI.DrawRect(
            position,
            backgroundColor
        );

        Rect leftBorder =
            new Rect(
                position.x,
                position.y,
                LeftBorderWidth,
                position.height
            );

        EditorGUI.DrawRect(
            leftBorder,
            new Color(
                0.25f,
                0.65f,
                0.95f,
                1f
            )
        );
    }

    private float GetPropertyHeight(
        SerializedProperty propertyToMeasure,
        bool includeChildren = false)
    {
        if (propertyToMeasure == null)
            return 0f;

        return EditorGUI.GetPropertyHeight(
            propertyToMeasure,
            includeChildren
        );
    }

    private float GetHeaderHeight()
    {
        return EditorGUIUtility.singleLineHeight + 4f;
    }

    private void MoveDown(
        ref Rect rect,
        float amount)
    {
        rect.y += amount;
    }
}

#endif
