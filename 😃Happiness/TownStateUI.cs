using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class TownStateUI : Singleton<TownStateUI>
{
    [Header("UI")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Town Message")]
    [SerializeField] private float townMessageHoldTime = 2.0f;
    [SerializeField] private float townMessageFadeTime = 1.25f;

    [Header("Cash In Button")]
    [SerializeField] private float cashInPulseSpeed = 2.5f;
    [SerializeField] private Color cashInGold =
        new Color(1f, 0.84f, 0f, 1f);

    [Header("Food Display")]
    [SerializeField] private Color foodLossColor =
        new Color(1f, 0.55f, 0f, 1f);

    [SerializeField] private Color outOfFoodDarkRed =
        new Color(0.7f, 0f, 0f, 1f);

    [SerializeField] private Color outOfFoodBrightRed =
        new Color(1f, 0.1f, 0.1f, 1f);

    [SerializeField] private float outOfFoodPulseSpeed = 4f;
    [SerializeField] private float outOfFoodFontSize = 22f;

    public VisualElement RootUI;

    public VisualElement EntertainmentCell;
    public VisualElement HappinessCell;
    public VisualElement GoldCell;
    public VisualElement FoodCell;
    public VisualElement ControlCell;

    public Label EntertainmentTitleLabel;
    public Label HappinessTitleLabel;
    public Label GoldTitleLabel;
    public Label FoodTitleLabel;
    public Label ControlTitleLabel;

    public Label EntertainmentLabel;
    public Label HappinessLabel;
    public Button GoldLabel;
    public Label FoodLabel;
    public Label ControlLabel;

    public Button LeaveButton;
    public Label TownMessageLabel;

    private readonly HashSet<stat> revealedStats = new();

    private Coroutine townMessageRoutine;
    private Coroutine cashInPulseRoutine;
    private Coroutine outOfFoodPulseRoutine;

    private void OnEnable()
    {
        if (RootUI == null && uiDocument != null)
            RootUI = uiDocument.rootVisualElement;

        if (RootUI == null)
        {
            Debug.LogError("TownStateUI: RootUI is missing.");
            return;
        }

        FindUIElements();
        ConfigureLeaveButton();
        ConfigureTownMessage();

        SetGoldVisible(true);

        SetStatVisible(
            true,
            HappinessCell,
            HappinessTitleLabel,
            HappinessLabel
        );

        SetStatVisible(
            true,
            FoodCell,
            FoodTitleLabel,
            FoodLabel
        );

        SetStatVisible(
            false,
            EntertainmentCell,
            EntertainmentTitleLabel,
            EntertainmentLabel
        );

        SetStatVisible(
            false,
            ControlCell,
            ControlTitleLabel,
            ControlLabel
        );

        PopulateTownStatePanel();
    }

    private void OnDisable()
    {
        if (LeaveButton != null)
            LeaveButton.clicked -= OnLeaveButtonClicked;

        StopTownMessageRoutine();
        StopCashInPulse();
        StopOutOfFoodPulse(FoodLabel);

        ResetLeaveButtonStyle();
    }

    private void FindUIElements()
    {
        EntertainmentCell =
            RootUI.Q<VisualElement>("EntertainmentCell");

        HappinessCell =
            RootUI.Q<VisualElement>("HappinessCell");

        GoldCell =
            RootUI.Q<VisualElement>("GoldCell");

        FoodCell =
            RootUI.Q<VisualElement>("FoodCell");

        ControlCell =
            RootUI.Q<VisualElement>("ControlCell");

        EntertainmentTitleLabel =
            RootUI.Q<Label>("EntertainmentTitleLabel");

        HappinessTitleLabel =
            RootUI.Q<Label>("HappinessTitleLabel");

        GoldTitleLabel =
            RootUI.Q<Label>("GoldTitleLabel");

        FoodTitleLabel =
            RootUI.Q<Label>("FoodTitleLabel");

        ControlTitleLabel =
            RootUI.Q<Label>("ControlTitleLabel");

        EntertainmentLabel =
            RootUI.Q<Label>("EntertainmentLabel");

        HappinessLabel =
            RootUI.Q<Label>("HappinessLabel");

        FoodLabel =
            RootUI.Q<Label>("FoodLabel");

        ControlLabel =
            RootUI.Q<Label>("ControlLabel");

        /*
         * The cash-out amount is displayed directly on LeaveButton.
         * GoldLabel therefore intentionally references LeaveButton.
         */
        LeaveButton = RootUI.Q<Button>("LeaveButton");
        GoldLabel = LeaveButton;

        TownMessageLabel =
            RootUI.Q<Label>("TownMessageLabel");
    }

    private void ConfigureLeaveButton()
    {
        if (LeaveButton == null)
        {
            Debug.LogError(
                "TownStateUI: LeaveButton not found in UXML."
            );

            return;
        }

        LeaveButton.clicked -= OnLeaveButtonClicked;
        LeaveButton.clicked += OnLeaveButtonClicked;
    }

    private void ConfigureTownMessage()
    {
        if (TownMessageLabel == null)
        {
            Debug.LogError(
                "TownStateUI: TownMessageLabel not found in UXML."
            );

            return;
        }

        TownMessageLabel.text = string.Empty;
        SetTownMessageAlpha(0f);
    }

    public void PopulateTownStatePanel()
    {
        if (StatsCounter.Instance == null ||
            StatsCounter.Instance.StatResults == null)
        {
            SetCashInButtonState(false);
            return;
        }

        Dictionary<stat, float> stats =
            StatsCounter.Instance.StatResults;

        UpdateHappinessStat(
            HappinessCell,
            HappinessTitleLabel,
            HappinessLabel,
            stat.tot_Happiness,
            stats
        );

        UpdateTownStateStat(
            GoldCell,
            GoldTitleLabel,
            null,
            GoldLabel,
            stat.tot_Wealth,
            stats
        );

        UpdateFoodStat(
            FoodCell,
            FoodTitleLabel,
            FoodLabel,
            stat.tot_food,
            stats
        );

        RefreshCashInButtonState(stats);
    }

    private void RefreshCashInButtonState(
        Dictionary<stat, float> stats)
    {
        LevelCreator.Level currentLevel =
            LevelCreator.Instance != null
                ? LevelCreator.Instance.CurrentLevel
                : null;

        bool cashInReady =
            currentLevel != null &&
            currentLevel.cashInConditions != null &&
            currentLevel.cashInConditions.AreSatisfied(stats);

        SetCashInButtonState(cashInReady);
    }

    private void SetCashInButtonState(bool ready)
    {
        if (LeaveButton == null)
            return;

        if (ready)
        {
            if (cashInPulseRoutine == null)
            {
                cashInPulseRoutine =
                    StartCoroutine(CashInPulseRoutine());
            }

            return;
        }

        StopCashInPulse();
        ResetLeaveButtonStyle();
    }

    private IEnumerator CashInPulseRoutine()
    {
        if (LeaveButton == null)
            yield break;

        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            float pulse01 =
                (Mathf.Sin(elapsed * cashInPulseSpeed) + 1f) * 0.5f;

            float opacity =
                Mathf.Lerp(0.55f, 1f, pulse01);

            float backgroundAlpha =
                Mathf.Lerp(0.45f, 0.9f, pulse01);

            LeaveButton.style.opacity = opacity;

            LeaveButton.style.backgroundColor = new Color(
                cashInGold.r,
                cashInGold.g,
                cashInGold.b,
                backgroundAlpha
            );

            LeaveButton.style.color =
                new Color(0.25f, 0.18f, 0f, 1f);

            LeaveButton.style.unityFontStyleAndWeight =
                FontStyle.Bold;

            yield return null;
        }
    }

    private void StopCashInPulse()
    {
        if (cashInPulseRoutine == null)
            return;

        StopCoroutine(cashInPulseRoutine);
        cashInPulseRoutine = null;
    }

    private void ResetLeaveButtonStyle()
    {
        if (LeaveButton == null)
            return;

        LeaveButton.style.opacity = 1f;
        LeaveButton.style.backgroundColor = StyleKeyword.Null;
        LeaveButton.style.color = StyleKeyword.Null;
        LeaveButton.style.unityFontStyleAndWeight =
            StyleKeyword.Null;
    }

    public void PushTownMessage(string message)
    {
        if (TownMessageLabel == null)
            return;

        StopTownMessageRoutine();

        TownMessageLabel.text = message ?? string.Empty;
        SetTownMessageAlpha(1f);

        townMessageRoutine =
            StartCoroutine(FadeTownMessageRoutine());
    }

    private IEnumerator FadeTownMessageRoutine()
    {
        if (townMessageHoldTime > 0f)
        {
            yield return new WaitForSecondsRealtime(
                townMessageHoldTime
            );
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, townMessageFadeTime);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(elapsed / duration);

            SetTownMessageAlpha(1f - progress);

            yield return null;
        }

        SetTownMessageAlpha(0f);
        townMessageRoutine = null;
    }

    private void StopTownMessageRoutine()
    {
        if (townMessageRoutine == null)
            return;

        StopCoroutine(townMessageRoutine);
        townMessageRoutine = null;
    }

    private void SetTownMessageAlpha(float alpha)
    {
        if (TownMessageLabel == null)
            return;

        TownMessageLabel.style.opacity = alpha;

        TownMessageLabel.pickingMode =
            alpha <= 0f
                ? PickingMode.Ignore
                : PickingMode.Position;
    }

    private void UpdateTownStateStat(
        VisualElement cell,
        Label titleLabel,
        Label valueLabel,
        Button button,
        stat statType,
        Dictionary<stat, float> stats)
    {
        if (LeaveInterpreter.Instance == null ||
            TownStateInterpreter.Instance == null)
        {
            return;
        }

        if (!stats.TryGetValue(statType, out float rawValue))
            rawValue = 0f;

        amounts amount =
            LeaveInterpreter.Instance.ValueInterpreter(
                statType,
                rawValue,
                stats
            );

        if (statType == stat.tot_Wealth)
        {
            if (button == null)
                return;

            int gold = Mathf.RoundToInt(rawValue);

            button.text = $"CASH OUT\n{gold}$";

            button.style.color =
                LeaveInterpreter.Instance.GetStatusColor(
                    statType,
                    amount
                );

            SetGoldVisible(true);
            return;
        }

        if (valueLabel == null)
            return;

        bool shouldReveal =
            amount != amounts.medium ||
            revealedStats.Contains(statType);

        if (shouldReveal)
            revealedStats.Add(statType);

        string text = shouldReveal
            ? TownStateInterpreter.Instance.Interpret(
                statType,
                amount,
                stats
            )
            : string.Empty;

        valueLabel.text = text;

        valueLabel.style.color =
            LeaveInterpreter.Instance.GetStatusColor(
                statType,
                amount
            );

        SetStatVisible(
            shouldReveal,
            cell,
            titleLabel,
            valueLabel
        );
    }

    private void UpdateHappinessStat(
        VisualElement cell,
        Label titleLabel,
        Label valueLabel,
        stat statType,
        Dictionary<stat, float> stats)
    {
        if (valueLabel == null)
            return;

        if (!stats.TryGetValue(statType, out float rawValue))
            rawValue = 0f;

        int happiness = Mathf.RoundToInt(rawValue);

        if (titleLabel != null)
        {
            titleLabel.text = "Happiness";
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            titleLabel.style.fontSize =
                StyleKeyword.Null;
        }

        valueLabel.text = happiness.ToString();
        valueLabel.style.color = Color.white;
        valueLabel.style.unityFontStyleAndWeight =
            FontStyle.Bold;
        valueLabel.style.fontSize =
            StyleKeyword.Null;
        valueLabel.transform.scale =
            Vector3.one;

        SetStatVisible(
            true,
            cell,
            titleLabel,
            valueLabel
        );
    }

    private void UpdateFoodStat(
        VisualElement cell,
        Label titleLabel,
        Label valueLabel,
        stat statType,
        Dictionary<stat, float> stats)
    {
        if (valueLabel == null)
            return;

        if (!stats.TryGetValue(statType, out float rawValue))
            rawValue = 0f;

        float rawFoodRate =
            StatsCounter.Instance != null
                ? StatsCounter.Instance.FoodImpact
                : 0f;

        int foodAmount =
            Mathf.Max(0, Mathf.RoundToInt(rawValue));

        int foodRate =
            Mathf.RoundToInt(rawFoodRate);

        if (titleLabel != null)
        {
            titleLabel.text = "Food";
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            titleLabel.style.fontSize =
                StyleKeyword.Null;
        }

        valueLabel.style.unityFontStyleAndWeight =
            FontStyle.Bold;

        valueLabel.transform.scale =
            Vector3.one;

        bool outOfFood = rawValue <= 0f;

        if (outOfFood)
        {
            valueLabel.text = "OUT OF FOOD";
            valueLabel.style.fontSize = outOfFoodFontSize;

            StartOutOfFoodPulse(valueLabel);
        }
        else
        {
            StopOutOfFoodPulse(valueLabel);

            valueLabel.text =
                $"{foodAmount} {FormatSignedValue(foodRate)}";

            valueLabel.style.fontSize =
                StyleKeyword.Null;

            valueLabel.style.color =
                foodRate < 0
                    ? foodLossColor
                    : Color.white;
        }

        SetStatVisible(
            true,
            cell,
            titleLabel,
            valueLabel
        );
    }

    private string FormatSignedValue(int value)
    {
        if (value > 0)
            return $"+{value}";

        return value.ToString();
    }

    private void StartOutOfFoodPulse(Label valueLabel)
    {
        if (outOfFoodPulseRoutine != null ||
            valueLabel == null)
        {
            return;
        }

        outOfFoodPulseRoutine =
            StartCoroutine(OutOfFoodPulseRoutine(valueLabel));
    }

    private void StopOutOfFoodPulse(Label valueLabel)
    {
        if (outOfFoodPulseRoutine != null)
        {
            StopCoroutine(outOfFoodPulseRoutine);
            outOfFoodPulseRoutine = null;
        }

        if (valueLabel != null)
            valueLabel.style.color = Color.white;
    }

    private IEnumerator OutOfFoodPulseRoutine(
        Label valueLabel)
    {
        float elapsed = 0f;

        while (true)
        {
            if (valueLabel == null)
            {
                outOfFoodPulseRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            float pulse01 =
                (
                    Mathf.Sin(
                        elapsed * outOfFoodPulseSpeed
                    ) + 1f
                ) * 0.5f;

            valueLabel.style.color = Color.Lerp(
                outOfFoodDarkRed,
                outOfFoodBrightRed,
                pulse01
            );

            yield return null;
        }
    }

    private void SetGoldVisible(bool visible)
    {
        SetElementOpacity(
            GoldCell,
            visible ? 1f : 0f
        );

        if (GoldTitleLabel != null)
            GoldTitleLabel.text = string.Empty;
    }

    private void SetStatVisible(
        bool visible,
        VisualElement cell,
        Label titleLabel,
        Label valueLabel)
    {
        float opacity = visible ? 1f : 0f;

        SetElementOpacity(cell, opacity);
        SetElementOpacity(titleLabel, opacity);
        SetElementOpacity(valueLabel, opacity);
    }

    private void SetElementOpacity(
        VisualElement element,
        float opacity)
    {
        if (element == null)
            return;

        element.style.opacity = opacity;

        element.pickingMode =
            opacity <= 0f
                ? PickingMode.Ignore
                : PickingMode.Position;
    }

    private void OnLeaveButtonClicked()
    {
        if (LeaveInterpreter.Instance == null)
        {
            Debug.LogWarning(
                "TownStateUI: LeaveInterpreter is missing."
            );

            return;
        }

        LeaveEnding leaveEnding =
            LeaveInterpreter.Instance.DetermineLeaveEnding();

        if (leaveEnding == null)
        {
            Debug.LogWarning(
                "TownStateUI: DetermineLeaveEnding returned null."
            );

            return;
        }

        if (MapUI.Instance == null)
        {
            Debug.LogWarning(
                "TownStateUI: MapUI is missing."
            );

            return;
        }

        MapUI.Instance.OpenMapWindow(leaveEnding);
    }
}
