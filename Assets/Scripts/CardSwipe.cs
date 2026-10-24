using UnityEngine;
using UnityEngine.EventSystems;

public class CardSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform leftAnswer;
    public RectTransform rightAnswer;
    public float swipeThreshold = 250f;
    public float rotationFactor = 0.05f;
    public GameManager gameManager;

    private RectTransform rt;
    private Vector2 startPos;
    private CanvasGroup leftGroup, rightGroup;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        startPos = rt.anchoredPosition;
        leftGroup = GetOrAddCanvasGroup(leftAnswer);
        rightGroup = GetOrAddCanvasGroup(rightAnswer);
    }

    CanvasGroup GetOrAddCanvasGroup(RectTransform target)
    {
        if (!target.TryGetComponent(out CanvasGroup group))
            group = target.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        rt.anchoredPosition += eventData.delta;
        float x = rt.anchoredPosition.x;
        rt.localRotation = Quaternion.Euler(0, 0, -x * rotationFactor);

        float t = Mathf.Clamp01(Mathf.Abs(x) / swipeThreshold);
        leftGroup.alpha = x < 0 ? t : 0;
        rightGroup.alpha = x > 0 ? t : 0;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float x = rt.anchoredPosition.x;
        if (Mathf.Abs(x) > swipeThreshold)
        {
            if (gameManager == null)
            {
                Debug.LogError("CardSwipe: Game Manager is not assigned in the Inspector!");
                ResetCard();
                return;
            }
            gameManager.Choose(x > 0);
        }
        else
        {
            ResetCard();
        }
    }

    public void ResetCard()
    {
        rt.anchoredPosition = startPos;
        rt.localRotation = Quaternion.identity;
        leftGroup.alpha = 0;
        rightGroup.alpha = 0;
    }
}