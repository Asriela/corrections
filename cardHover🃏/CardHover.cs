using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Target — the child rect, NOT the root")]
    [Tooltip("Assign the 'Visual' child RectTransform here. Scaling this instead of the root keeps the BoxCollider2D on the root fixed-size, so hover growth can't steal neighbors' raycasts.")]
    [SerializeField] private RectTransform visualRoot;

    [Header("Hover Settings")]
    [SerializeField] private float hoverLift = 150f;
    [SerializeField] private float hoverScale = 1.1f;

    [Header("Tween")]
    [SerializeField] private float hoverFollowSpeed = 16f;

    private RectTransform rootRect;
    private Card card;

    private Vector2 restPosition;
    private Vector3 restScale;
    private Vector2 targetPosition;
    private Vector3 targetScale;
    private int originalSibling;

    public bool IsHovered { get; private set; }

    private void Awake()
    {
        rootRect = GetComponent<RectTransform>();
        card = GetComponent<Card>();

        if (visualRoot == null)
        {
            Debug.LogError($"CardHover on {name}: 'visualRoot' is not assigned — hover will scale the collider again. Assign the 'Visual' child in the Inspector.");
            visualRoot = rootRect;
        }

        // Any Graphic under the animated visual must never be a raycast target —
        // otherwise hover detection thrashes as the visual moves out from under the cursor.
        // The stable hit-target lives on the root instead (see prefab notes).
        foreach (var graphic in visualRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = false;

        restPosition = visualRoot.anchoredPosition;
        restScale = visualRoot.localScale;
        targetPosition = restPosition;
        targetScale = restScale;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float t = 1f - Mathf.Exp(-hoverFollowSpeed * dt);

        visualRoot.anchoredPosition = Vector2.Lerp(visualRoot.anchoredPosition, targetPosition, t);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, t);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardDrag.IsAnyCardDragging) return;

        IsHovered = true;
        originalSibling = rootRect.GetSiblingIndex();

        rootRect.SetAsLastSibling();

        targetPosition = restPosition + Vector2.up * hoverLift;
        targetScale = restScale * hoverScale;

        CardHand.Instance?.SetHoveredCard(card);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsHovered) return;

        IsHovered = false;
        targetPosition = restPosition;
        targetScale = restScale;
        rootRect.SetSiblingIndex(originalSibling);

        CardHand.Instance?.ClearHoveredCard(card);
    }
}
