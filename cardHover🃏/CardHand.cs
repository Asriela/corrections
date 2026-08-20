using System.Collections.Generic;
using UnityEngine;

public class CardHand : Singleton<CardHand>
{
    [Header("References")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform handAnchor;

    [Header("Layout Presets")]
    [SerializeField] private List<HandLayoutSettings> layouts = new();
    [SerializeField] private int activeLayoutIndex = 0;

    [Header("Hover Spread")]
    [SerializeField] private float hoverGapBoost = 40f;
    [SerializeField] private int hoverSpreadFalloff = 2;

    [Header("Tween")]
    [SerializeField] private float positionFollowSpeed = 12f; // higher = snappier
    [SerializeField] private float rotationFollowSpeed = 12f;

    private readonly List<Card> currentCards = new();
    private readonly Dictionary<Card, CardLayoutTarget> layoutTargets = new();
    public BuildingCategoryType currentCategory;

    private int hoveredIndex = -1;

    private HandLayoutSettings CurrentLayout => layouts.Count > 0 ? layouts[activeLayoutIndex] : null;

    private class CardLayoutTarget
    {
        public Vector2 position;
        public float rotation;
    }

    private void Update()
    {
        if (currentCards.Count == 0)
            return;

        float dt = Time.deltaTime;
        float posT = 1f - Mathf.Exp(-positionFollowSpeed * dt);
        float rotT = 1f - Mathf.Exp(-rotationFollowSpeed * dt);

        foreach (Card card in currentCards)
        {
            if (card == null)
                continue;

            CardHover hover = card.GetComponent<CardHover>();
            if (hover != null && hover.IsHovered)
                continue; // CardHover owns this card's root transform while hovered

            if (!layoutTargets.TryGetValue(card, out CardLayoutTarget target))
                continue;

            RectTransform rt = card.GetComponent<RectTransform>();
            if (rt == null)
                continue;

            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target.position, posT);

            float currentZ = rt.localRotation.eulerAngles.z;
            float newZ = Mathf.LerpAngle(currentZ, target.rotation, rotT);
            rt.localRotation = Quaternion.Euler(0f, 0f, newZ);
        }
    }

    public void ShowCategory(BuildingCategoryType category)
    {
        currentCategory = category;
        ClearHand();
        BuildHandForCategory(category);
        ArrangeCards();
    }

    public void RemoveCard(Card card)
    {
        if (!currentCards.Contains(card))
            return;

        currentCards.Remove(card);
        layoutTargets.Remove(card);
        Destroy(card.gameObject);

        if (hoveredIndex >= currentCards.Count)
            hoveredIndex = -1;

        ArrangeCards();
    }

    private void BuildHandForCategory(BuildingCategoryType category)
    {
        if (GameManager.CardsInDeck == null || BuildingData.Instance == null)
            return;

        foreach (var kvp in GameManager.CardsInDeck)
        {
            BuildingType type = kvp.Key;
            int count = kvp.Value;

            if (count <= 0)
                continue;

            ScriptableBuilding data = BuildingData.Instance.GetBuilding(type);

            if (data == null || data.buildingCategory != category)
                continue;

            SpawnCard(type, count, data.costToBuy);
        }
    }

    private void SpawnCard(BuildingType type, int stack, int price)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("CardHand: cardPrefab is not assigned.");
            return;
        }

        if (stack <= 0)
            return;

        GameObject go = Instantiate(cardPrefab, handAnchor);
        Card card = go.GetComponent<Card>();

        if (card == null)
        {
            Debug.LogError("CardHand: cardPrefab must have a Card component.");
            Destroy(go);
            return;
        }

        card.SetData(type, stack, price);
        currentCards.Add(card);
    }

    // Called by CardHover on pointer enter — this is what makes neighbors give the hovered card room.
    public void SetHoveredCard(Card card)
    {
        int index = currentCards.IndexOf(card);
        if (index < 0) return;

        hoveredIndex = index;
        ArrangeCards();
    }

    // Called by CardHover on pointer exit. Only clears if this card was the currently-hovered one,
    // so a stale exit event (fast mouse swipe) can't clobber a newer hover.
    public void ClearHoveredCard(Card card)
    {
        int index = currentCards.IndexOf(card);
        if (index != hoveredIndex) return;

        hoveredIndex = -1;
        ArrangeCards();
    }

    // Recomputes layout TARGETS — actual movement happens smoothly in Update().
    public void ArrangeCards()
    {
        int count = currentCards.Count;
        if (count == 0 || CurrentLayout == null)
            return;

        var layout = CurrentLayout;

        float totalWidth = layout.spacing * (count - 1);
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            Card card = currentCards[i];
            if (card == null)
                continue;

            RectTransform rt = card.GetComponent<RectTransform>();
            if (rt == null)
                continue;

            float t = count > 1 ? (i / (float)(count - 1)) * 2f - 1f : 0f;

            if (layout.invertFan)
                t *= -1f;

            float x = startX + i * layout.spacing + layout.anchorPosition.x;
            x += SpreadOffsetFor(i);

            float y = -layout.heightCurve * t * t * (count > 1 ? 100f : 0f) + layout.anchorPosition.y;
            float rotation = -layout.anglePerCard * t + layout.baseRotationOffset;

            CardHover hover = card.GetComponent<CardHover>();
            bool isHovered = hover != null && hover.IsHovered;

            if (!layoutTargets.TryGetValue(card, out CardLayoutTarget target))
            {
                target = new CardLayoutTarget();
                layoutTargets[card] = target;

                // First time we've seen this card — snap instantly instead of flying in from origin.
                if (!isHovered)
                {
                    rt.anchoredPosition = new Vector2(x, y);
                    rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }
            }

            target.position = new Vector2(x, y);
            target.rotation = rotation;

            rt.SetSiblingIndex(i);
        }
    }

    // Distance-based falloff so only the card(s) next to the hovered one shuffle aside,
    // instead of the whole hand shifting. Smoothstep so it eases to exactly zero at the
    // edge of the falloff range instead of hard-cutting (that hard cut was the "weird" kink).
    private float SpreadOffsetFor(int index)
    {
        if (hoveredIndex < 0 || index == hoveredIndex)
            return 0f;

        int distance = index - hoveredIndex;
        int absDistance = Mathf.Abs(distance);

        float normalized = Mathf.Clamp01((float)(hoverSpreadFalloff + 1 - absDistance) / hoverSpreadFalloff);
        float falloffT = normalized * normalized * (3f - 2f * normalized); // smoothstep, eases to 0 cleanly

        return Mathf.Sign(distance) * hoverGapBoost * falloffT;
    }

    private void ClearHand()
    {
        foreach (Card card in currentCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        currentCards.Clear();
        layoutTargets.Clear();
        hoveredIndex = -1;
    }

    public void OnCardPlaced(Card card)
    {
        if (card == null)
            return;

        card.DecrementStack();
    }

    public void SetLayout(int index)
    {
        if (index < 0 || index >= layouts.Count)
            return;

        activeLayoutIndex = index;
        ArrangeCards();
    }
}

[System.Serializable]
public class HandLayoutSettings
{
    public string name;

    [Header("Position")]
    public Vector2 anchorPosition;

    [Header("Rotation")]
    public float baseRotationOffset;

    [Header("Fan Direction")]
    public bool invertFan;

    [Header("Spacing & Shape")]
    public float spacing = 60f;
    public float anglePerCard = 5f;
    public float heightCurve = 0.4f;
}
