using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TimeManager : Singleton<TimeManager>
{
    [Header("Time Settings")]
    public float singleDayDuration = 120f * 1.5f;

    public int DayStartHour = 6;
    internal int NightStartHour = 16;

    public int CurrentHour = 0;

    private int LastHour = -1;

    public float dayCounter = 0f;

    private int totalDays = 1;

    [Header("Lighting")]
    private Light2D globalLight;

    private Color dayColor = Color.white;

    public Color nightColor = Help.HexToColor("#091998");

    public float transitionDuration = 1f;

    private float transitionTimer = 0f;

    public enum CyclePhase
    {
        Day,
        Night,
        TransitionToDay,
        TransitionToNight
    }

    public CyclePhase CurrentPhase = CyclePhase.Day;

    public bool IsDaytime
    {
        get
        {
            if (DayStartHour == NightStartHour)
                return true;

            return DayStartHour < NightStartHour
                ? CurrentHour >= DayStartHour &&
                  CurrentHour < NightStartHour
                : CurrentHour >= DayStartHour ||
                  CurrentHour < NightStartHour;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        globalLight = GetComponent<Light2D>();

        if (globalLight == null ||
            globalLight.lightType != Light2D.LightType.Global)
        {
            Debug.LogError(
                "No Global Light2D component found on this GameObject."
            );

            enabled = false;
            return;
        }

        transitionDuration = Math.Max(
            0.1f,
            singleDayDuration * 0.04f
        );

        CurrentPhase = CyclePhase.Day;
        globalLight.color = dayColor;

        dayCounter = (singleDayDuration / 24f) * 7f;
    }

    private void GameSpeed()
    {
        /*
        if (MainMenu.Instance.MainMenuPanel.visible)
            Time.timeScale = 0f;
        else if (UI.Instance.EventPanel.visible)
            Time.timeScale = 0.1f;
        else if (Input.GetKey(KeyCode.D))
            Time.timeScale = 4f;
        else if (Input.GetKey(KeyCode.F))
            Time.timeScale = 8f;
        else if (Input.GetKey(KeyCode.G))
            Time.timeScale = 30f;
        else
            Time.timeScale = 1f;
        */
    }

    private void Update()
    {
        GameSpeed();

        dayCounter += Time.deltaTime;

        CurrentHour =
            (int)Math.Floor(
                (dayCounter / singleDayDuration) * 24
            ) % 24;

        if (CurrentHour != LastHour)
        {
            // CurrentHour has already changed, so IsDaytime now contains
            // the correct new lighting condition.
            HourChangedEvent();

            LastHour = CurrentHour;

            if (CurrentHour == DayStartHour &&
                CurrentPhase != CyclePhase.Day)
            {
                StartTransition(
                    CyclePhase.TransitionToDay
                );
            }
            else if (CurrentHour == NightStartHour &&
                     CurrentPhase != CyclePhase.Night)
            {
                StartTransition(
                    CyclePhase.TransitionToNight
                );
            }
        }

        if (dayCounter >= singleDayDuration)
        {
            PassDay();
            dayCounter = 0f;
        }

        UpdateLighting();

        var phaseText =
            CurrentPhase == CyclePhase.Day
                ? "Day"
                : CurrentPhase == CyclePhase.Night
                    ? "Night"
                    : CurrentPhase == CyclePhase.TransitionToDay
                        ? "Dawn"
                        : "Dusk";
    }

    private void StartTransition(CyclePhase transitionPhase)
    {
        CurrentPhase = transitionPhase;
        transitionTimer = 0f;
    }

    private void UpdateLighting()
    {
        if (CurrentPhase == CyclePhase.TransitionToDay)
        {
            transitionTimer += Time.deltaTime;

            float t = Mathf.Clamp01(
                transitionTimer / transitionDuration
            );

            globalLight.color = Color.Lerp(
                nightColor,
                dayColor,
                t
            );

            if (t >= 1f)
                CurrentPhase = CyclePhase.Day;
        }
        else if (CurrentPhase == CyclePhase.TransitionToNight)
        {
            transitionTimer += Time.deltaTime;

            float t = Mathf.Clamp01(
                transitionTimer / transitionDuration
            );

            globalLight.color = Color.Lerp(
                dayColor,
                nightColor,
                t
            );

            if (t >= 1f)
                CurrentPhase = CyclePhase.Night;
        }
    }

    private void HourChangedEvent()
    {
        if (GameManager.Instance == null)
            return;

        foreach (var group in GameManager.Instance.AllBuildings)
        {
            foreach (var building in group.Value)
            {
                if (building == null)
                    continue;

                building.RefreshLightState();
            }
        }
    }

    public void PassDay()
    {
        totalDays++;
    }

    public void SwitchDayPhase(CyclePhase phase)
    {
        CurrentPhase = phase;
        transitionTimer = 0f;

        RefreshAllBuildingLights();
    }

    private void RefreshAllBuildingLights()
    {
        if (GameManager.Instance == null)
            return;

        foreach (var group in GameManager.Instance.AllBuildings)
        {
            foreach (var building in group.Value)
            {
                if (building == null)
                    continue;

                building.RefreshLightState();
            }
        }
    }

    public void AddLightToBuilding(Building building)
    {
        if (building == null || building.myLight == null)
            return;

        building.myLight.enabled = true;
    }

    public void RemoveLightFromBuilding(Building building)
    {
        if (building == null || building.myLight == null)
            return;

        building.myLight.enabled = false;
    }
}
