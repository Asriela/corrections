using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visual Target")]
    [Tooltip("Child object holding the visible card art/text — NOT the root RectTransform. Scaling this instead of the root keeps the hoverable hitbox fixed-size.")]
    [SerializeField] private RectTransform visualRoot;

    [Header("Hover Settings")]
    [SerializeField] private float hoverLift = 150f;
    [SerializeField] private float hoverScale = 1.1f;

    private RectTransform rt;
    private Vector2 originalVisualPos;
    private Vector3 originalVisualScale;
    private int originalSibling;

    public bool IsHovered { get; private set; }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();

        if (visualRoot == null)
        {
            Debug.LogWarning($"CardHover on {name}: visualRoot not assigned, falling back to root transform (will cause hitbox overlap).");
            visualRoot = rt;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CardDrag.IsAnyCardDragging) return;

        IsHovered = true;
        originalVisualPos = visualRoot.anchoredPosition;
        originalVisualScale = visualRoot.localScale;
        originalSibling = rt.GetSiblingIndex();

        // Bring the whole card forward so the enlarged visual renders above neighbors,
        // but this does NOT change the root's size/hitbox.
        rt.SetAsLastSibling();

        visualRoot.anchoredPosition = originalVisualPos + Vector2.up * hoverLift;
        visualRoot.localScale = originalVisualScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsHovered) return;

        IsHovered = false;
        visualRoot.anchoredPosition = originalVisualPos;
        visualRoot.localScale = originalVisualScale;
        rt.SetSiblingIndex(originalSibling);

        CardHand.Instance?.ArrangeCards();
    }
}
