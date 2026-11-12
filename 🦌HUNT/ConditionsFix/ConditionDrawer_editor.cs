#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Condition))]
public class ConditionDrawer : PropertyDrawer
{
    private const float OuterPadding = 5f;
    private const float InnerPadding = 4f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        float line =
            EditorGUIUtility.singleLineHeight;

        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty modeProperty =
            property.FindPropertyRelative(
                "evaluationMode"
            );

        bool usesValueComparison =
            modeProperty != null &&
            modeProperty.enumValueIndex ==
            (int)ConditionEvaluationMode.ValueComparison;

        // Header/condition line.
        // Evaluation mode line.
        // Boolean state OR comparison line.
        int lineCount = 3;

        float height =
            OuterPadding * 2f +
            lineCount * line +
            (lineCount - 1) * spacing;

        // Value comparison displays a small description underneath.
        if (usesValueComparison)
        {
            height += spacing +
                      EditorGUIUtility.singleLineHeight;
        }

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

        float line =
            EditorGUIUtility.singleLineHeight;

        float spacing =
            EditorGUIUtility.standardVerticalSpacing;

        Rect contentRect =
            new Rect(
                position.x + OuterPadding,
                position.y + OuterPadding,
                position.width -
                    OuterPadding * 2f,
                line
            );

        SerializedProperty conditionProperty =
            property.FindPropertyRelative(
                "condition"
            );

        SerializedProperty modeProperty =
            property.FindPropertyRelative(
                "evaluationMode"
            );

        SerializedProperty requiredStateProperty =
            property.FindPropertyRelative(
                "requiredState"
            );

        SerializedProperty comparisonProperty =
            property.FindPropertyRelative(
                "comparison"
            );

        SerializedProperty requiredValueProperty =
            property.FindPropertyRelative(
                "requiredValue"
            );

        // ───────── CONDITION ─────────

        EditorGUI.PropertyField(
            contentRect,
            conditionProperty,
            new GUIContent(
                "Condition",
                "The state or numeric value to inspect."
            )
        );

        contentRect.y += line + spacing;

        // ───────── EVALUATION MODE ─────────

        EditorGUI.PropertyField(
            contentRect,
            modeProperty,
            new GUIContent(
                "Evaluation",
                "Boolean checks True/False. " +
                "Value Comparison uses an operator and number."
            )
        );

        contentRect.y += line + spacing;

        ConditionEvaluationMode mode =
            (ConditionEvaluationMode)
            modeProperty.enumValueIndex;

        if (mode ==
            ConditionEvaluationMode.Boolean)
        {
            DrawBooleanRequirement(
                contentRect,
                requiredStateProperty
            );
        }
        else
        {
            DrawValueRequirement(
                contentRect,
                comparisonProperty,
                requiredValueProperty
            );

            contentRect.y += line + spacing;

            DrawValueDescription(
                contentRect,
                conditionProperty,
                comparisonProperty,
                requiredValueProperty
            );
        }

        EditorGUI.EndProperty();
    }

    private void DrawBackground(
        Rect position)
    {
        Color backgroundColor =
            EditorGUIUtility.isProSkin
                ? new Color(
                    0.18f,
                    0.18f,
                    0.18f,
                    1f
                )
                : new Color(
                    0.88f,
                    0.88f,
                    0.88f,
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
                3f,
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

    private void DrawBooleanRequirement(
        Rect rect,
        SerializedProperty requiredStateProperty)
    {
        EditorGUI.PropertyField(
            rect,
            requiredStateProperty,
            new GUIContent(
                "Required State",
                "The condition's current Boolean state must equal this."
            )
        );

        Rect resultRect =
            new Rect(
                rect.x +
                    EditorGUIUtility.labelWidth +
                    22f,
                rect.y,
                rect.width -
                    EditorGUIUtility.labelWidth -
                    22f,
                rect.height
            );

        GUIStyle resultStyle =
            new GUIStyle(
                EditorStyles.miniBoldLabel
            );

        if (requiredStateProperty.boolValue)
        {
            resultStyle.normal.textColor =
                new Color(
                    0.35f,
                    0.9f,
                    0.35f
                );

            EditorGUI.LabelField(
                resultRect,
                "MUST BE TRUE",
                resultStyle
            );
        }
        else
        {
            resultStyle.normal.textColor =
                new Color(
                    1f,
                    0.45f,
                    0.45f
                );

            EditorGUI.LabelField(
                resultRect,
                "MUST BE FALSE",
                resultStyle
            );
        }
    }

    private void DrawValueRequirement(
        Rect rect,
        SerializedProperty comparisonProperty,
        SerializedProperty requiredValueProperty)
    {
        float labelWidth =
            EditorGUIUtility.labelWidth;

        Rect labelRect =
            new Rect(
                rect.x,
                rect.y,
                labelWidth,
                rect.height
            );

        float remainingWidth =
            rect.width -
            labelWidth;

        float operatorWidth =
            Mathf.Min(
                145f,
                remainingWidth * 0.55f
            );

        Rect comparisonRect =
            new Rect(
                rect.x + labelWidth,
                rect.y,
                operatorWidth,
                rect.height
            );

        Rect valueRect =
            new Rect(
                comparisonRect.xMax +
                    InnerPadding,
                rect.y,
                remainingWidth -
                    operatorWidth -
                    InnerPadding,
                rect.height
            );

        EditorGUI.LabelField(
            labelRect,
            new GUIContent(
                "Comparison",
                "Compare the condition's current numeric value."
            )
        );

        EditorGUI.PropertyField(
            comparisonRect,
            comparisonProperty,
            GUIContent.none
        );

        EditorGUI.PropertyField(
            valueRect,
            requiredValueProperty,
            GUIContent.none
        );
    }

    private void DrawValueDescription(
        Rect rect,
        SerializedProperty conditionProperty,
        SerializedProperty comparisonProperty,
        SerializedProperty requiredValueProperty)
    {
        ConditionType conditionType =
            (ConditionType)
            conditionProperty.enumValueIndex;

        ConditionComparison comparison =
            (ConditionComparison)
            comparisonProperty.enumValueIndex;

        string operatorText =
            GetOperatorText(comparison);

        string valueMeaning =
            GetValueMeaning(conditionType);

        GUIStyle descriptionStyle =
            new GUIStyle(
                EditorStyles.miniLabel
            );

        descriptionStyle.normal.textColor =
            EditorGUIUtility.isProSkin
                ? new Color(
                    0.72f,
                    0.82f,
                    0.92f
                )
                : new Color(
                    0.2f,
                    0.35f,
                    0.5f
                );

        descriptionStyle.wordWrap = true;

        EditorGUI.LabelField(
            rect,
            $"{valueMeaning} {operatorText} " +
            $"{requiredValueProperty.floatValue:0.##}",
            descriptionStyle
        );
    }

    private string GetOperatorText(
        ConditionComparison comparison)
    {
        switch (comparison)
        {
            case ConditionComparison.Equal:
                return "=";

            case ConditionComparison.NotEqual:
                return "!=";

            case ConditionComparison.GreaterThan:
                return ">";

            case ConditionComparison.GreaterThanOrEqual:
                return ">=";

            case ConditionComparison.LessThan:
                return "<";

            case ConditionComparison.LessThanOrEqual:
                return "<=";

            default:
                return "?";
        }
    }

    private string GetValueMeaning(
        ConditionType conditionType)
    {
        switch (conditionType)
        {
            case ConditionType.noCondition:
                return "Constant value";

            case ConditionType.timeOfDay:
                return "Current hour";

            case ConditionType.onDuty:
                return "On-duty state: 0 or 1";

            case ConditionType.hasTarget:
                return "Has-target state: 0 or 1";

            case ConditionType.targetDead:
                return "Target-dead state: 0 or 1";

            case ConditionType.targetHasInventory:
                return "Target inventory amount";

            case ConditionType.doneTrackingTarget:
                return "Current tracking progress";

            case ConditionType.carryingFullLoad:
                return "Carried loot amount";

            default:
                return "Current condition value";
        }
    }
}

#endif
