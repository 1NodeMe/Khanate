using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;

public class CardSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform leftAnswer;
    public RectTransform rightAnswer;
    public float swipeThreshold = 250f;
    public float rotationFactor = 0.05f;
    public GameManager gameManager;

    [Header("Swipe sounds")]
    public AudioClip leftSwipeSound;
    public AudioClip rightSwipeSound;
    public AudioSource swipeAudioSource;
    public AudioMixerGroup swipeSoundGroup;

    private RectTransform rt;
    private Vector2 startPos;
    private CanvasGroup leftGroup, rightGroup;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        startPos = rt.anchoredPosition;
        leftGroup = GetOrAddCanvasGroup(leftAnswer);
        rightGroup = GetOrAddCanvasGroup(rightAnswer);

        if (swipeAudioSource == null) swipeAudioSource = GetComponent<AudioSource>();
        if (swipeAudioSource == null) swipeAudioSource = gameObject.AddComponent<AudioSource>();
        swipeAudioSource.playOnAwake = false;
        swipeAudioSource.loop = false;
        ConfigureSoundGroup();
    }

    void ConfigureSoundGroup()
    {
        if (swipeAudioSource == null || swipeSoundGroup != null)
        {
            if (swipeAudioSource != null && swipeSoundGroup != null)
                swipeAudioSource.outputAudioMixerGroup = swipeSoundGroup;
            return;
        }

        VolumeSettings settings = FindObjectOfType<VolumeSettings>();
        if (settings == null || settings.mixer == null) return;

        AudioMixerGroup[] groups = settings.mixer.FindMatchingGroups("Sound");
        if (groups.Length == 0) groups = settings.mixer.FindMatchingGroups("SFX");
        if (groups.Length > 0)
            swipeAudioSource.outputAudioMixerGroup = groups[0];
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

        if (gameManager != null)
        {
            if (Mathf.Abs(x) > 0.01f) gameManager.PreviewChoice(x > 0);
            else gameManager.ClearChoicePreview();
        }

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
            PlaySwipeSound(x > 0);
            gameManager.Choose(x > 0);
        }
        else
        {
            ResetCard();
        }
    }

    public void ResetCard()
    {
        if (gameManager != null) gameManager.ClearChoicePreview();
        rt.anchoredPosition = startPos;
        rt.localRotation = Quaternion.identity;
        leftGroup.alpha = 0;
        rightGroup.alpha = 0;
    }

    void PlaySwipeSound(bool right)
    {
        AudioClip clip = right ? rightSwipeSound : leftSwipeSound;
        if (clip != null && swipeAudioSource != null)
            swipeAudioSource.PlayOneShot(clip);
    }
}