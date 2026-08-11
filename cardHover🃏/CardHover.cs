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

    private RectTransform rootRect;
    private Vector2 originalVisualPos;
    private Vector3 originalVisualScale;
    private int originalSibling;

    public bool IsHovered { get; private set; }

    private void Awake()
    {
        rootRect = GetComponent<RectTransform>();

        if (visualRoot == null)
        {
            Debug.LogError($"CardHover on {name}: 'visualRoot' is not assigned — hover will scale the collider again. Assign the 'Visual' child in the Inspector.");
            visualRoot = rootRect; // fallback, but this reintroduces the bug
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardDrag.IsAnyCardDragging) return;

        IsHovered = true;
        originalVisualPos = visualRoot.anchoredPosition;
        originalVisualScale = visualRoot.localScale;
        originalSibling = rootRect.GetSiblingIndex();

        // Bring whole card forward in sibling order so the bigger visual renders
        // above neighbors — root position/scale (and collider) untouched.
        rootRect.SetAsLastSibling();

        visualRoot.anchoredPosition = originalVisualPos + Vector2.up * hoverLift;
        visualRoot.localScale = originalVisualScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsHovered) return;

        IsHovered = false;
        visualRoot.anchoredPosition = originalVisualPos;
        visualRoot.localScale = originalVisualScale;
        rootRect.SetSiblingIndex(originalSibling);

        CardHand.Instance?.ArrangeCards();
    }
}
