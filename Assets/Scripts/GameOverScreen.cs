using System.Collections;
using UnityEngine;
using TMPro;

public class GameOverScreen : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleText;
    public GameObject buttonsGroup;   // "New reign begins" + "Settings", shown after the count ends

    [Header("Count-up speed")]
    public float secondsPerYear = 0.04f;
    public float minDuration = 0.6f;
    public float maxDuration = 2f;

    public string[] names =
    {
        "Arystan", "Bolat", "Dauren", "Yerlan", "Kairat", "Mamyr",
        "Nurlan", "Orken", "Sanzhar", "Temir", "Ulan", "Zhandos"
    };

    Coroutine routine;

    void Start()
    {
        panel.SetActive(false);
    }

    public void Show(int fromYear, int toYear)
    {
        panel.SetActive(true);
        string ruler = names[Random.Range(0, names.Length)];
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(CountUp(fromYear, toYear, ruler));
    }

    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        panel.SetActive(false);
    }

    IEnumerator CountUp(int from, int to, string ruler)
    {
        buttonsGroup.SetActive(false);

        int span = Mathf.Max(0, to - from);
        float duration = Mathf.Clamp(span * secondsPerYear, minDuration, maxDuration);
        int lastShown = int.MinValue;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            p = 1f - (1f - p) * (1f - p);              // ease-out: slows down near the end
            int y = from + Mathf.RoundToInt(span * p);
            if (y != lastShown) { SetTitle(y, ruler); lastShown = y; }
            yield return null;
        }

        SetTitle(to, ruler);
        buttonsGroup.SetActive(true);
        routine = null;
    }

    void SetTitle(int year, string ruler)
    {
        titleText.text = "Year " + year + " - " + ruler + "'s end of reign";
    }
}
