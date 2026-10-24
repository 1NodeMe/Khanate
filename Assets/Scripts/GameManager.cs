using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    const string YearKey = "year";

    enum CardMode { Normal, Death, Win }

    [Header("Data: Assets/Resources/cards.json")]
    public string dataFileName = "cards";

    [Header("Year")]
    public int startYear = 1465;
    public TMP_Text yearText;

    [Header("Stat bars: Sliders (value 0..1)")]
    public Slider peopleBar;
    public Slider armyBar;
    public Slider treasuryBar;
    [FormerlySerializedAs("diplomacyBar")] public Slider faithBar;   // the 4th stat is Faith

    [Header("Stat icons: Image fill (optional, used instead of or together with sliders)")]
    public Image peopleFill;
    public Image armyFill;
    public Image treasuryFill;
    public Image faithFill;

    [Header("Card UI")]
    public CardSwipe cardSwipe;
    public Image portraitImage;
    public TMP_Text nameText, cardText;
    public TMP_Text leftAnswerText, rightAnswerText;
    public Sprite defaultPortrait;      // used when Resources/Portraits/<name>.png is missing
    public TMP_Text outcomeText;        // optional: short consequence text after a swipe

    [Header("Death / win cards")]
    public Image cardBackground;        // optional: the Image on the Card object (tinted for endings)
    public Sprite deathPortrait;        // optional: used for every death card without its own portrait
    public Sprite winPortrait;          // optional: used for every win card without its own portrait
    public Color deathTint = new Color(0.55f, 0.2f, 0.2f, 1f);
    public Color winTint = new Color(0.9f, 0.78f, 0.35f, 1f);
    public float deathRevealDelay = 0.7f;   // pause so the player sees the stat hit its limit
    public float endingLockSeconds = 0.8f;  // swipes are ignored for this long after an ending card appears
    public string endingAnswer = "Далее";

    [Header("Other systems")]
    public AchievementManager achievements;
    public GameOverScreen gameOver;

    [Header("Tuning")]
    public bool showShortActions = true;     // true: short "action" text while swiping, false: full "label"
    public float flaggedCardBoost = 3f;      // cards that need flags (debts, E7) appear sooner
    public float softenFactor = 0.5f;        // softenIfFlag: negative effects are multiplied by this
    public float outcomeSeconds = 2.5f;

    // ----- runtime state -----
    GameDataRoot data;
    readonly Dictionary<string, CardDef> byId = new Dictionary<string, CardDef>();
    readonly List<CardDef> pool = new List<CardDef>();
    readonly HashSet<string> flags = new HashSet<string>();
    readonly HashSet<string> seen = new HashSet<string>();
    readonly Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();

    int people, army, treasury, faith;
    int year, reignStartYear, cardsThisReign;
    CardDef current, last;
    CardMode mode = CardMode.Normal;
    float inputLockUntil;
    Color normalTint = Color.white;
    Coroutine outcomeRoutine;
    Coroutine endingRoutine;

    // =====================================================================
    // Startup
    // =====================================================================
    void Start()
    {
        LoadData();
        if (data == null) return;

        if (cardBackground != null) normalTint = cardBackground.color;

        year = PlayerPrefs.GetInt(YearKey, startYear);
        reignStartYear = year;
        UpdateYearUI();
        ResetReignState();
        ShowCard(PickFromPool());
    }

    void LoadData()
    {
        var asset = Resources.Load<TextAsset>(dataFileName);
        if (asset == null)
        {
            Debug.LogError("GameManager: Assets/Resources/" + dataFileName + ".json not found.");
            return;
        }

        data = JsonUtility.FromJson<GameDataRoot>(asset.text.TrimStart('\uFEFF'));
        if (data == null || data.cards == null || data.cards.Length == 0)
        {
            Debug.LogError("GameManager: " + dataFileName + ".json has no cards.");
            data = null;
            return;
        }

        byId.Clear();
        pool.Clear();

        var linked = new HashSet<string>();
        foreach (var c in data.cards)
        {
            byId[c.id] = c;
            if (!string.IsNullOrEmpty(c.left.next)) linked.Add(c.left.next);
            if (!string.IsNullOrEmpty(c.right.next)) linked.Add(c.right.next);
        }

        // A card that nobody links to is a "pool" card: it comes up on its own
        foreach (var c in data.cards)
            if (!linked.Contains(c.id)) pool.Add(c);
    }

    void ResetReignState()
    {
        // Cancel a death reveal that may still be running and bring the card back
        if (endingRoutine != null) StopCoroutine(endingRoutine);
        endingRoutine = null;
        if (cardSwipe != null && !cardSwipe.gameObject.activeSelf) cardSwipe.gameObject.SetActive(true);

        var s = data.startStats;
        people = s.people;
        army = s.army;
        treasury = s.treasury;
        faith = s.faith;

        flags.Clear();
        seen.Clear();
        cardsThisReign = 0;
        current = null;
        last = null;
        mode = CardMode.Normal;
        inputLockUntil = 0f;

        UpdateBars();
        HideOutcome();
    }

    // =====================================================================
    // Showing cards
    // =====================================================================
    void ShowCard(CardDef c, CardMode newMode = CardMode.Normal)
    {
        current = c;
        last = c;
        mode = newMode;
        seen.Add(c.id);

        bool ending = newMode != CardMode.Normal;
        inputLockUntil = ending ? Time.unscaledTime + endingLockSeconds : 0f;

        if (!ending && !string.IsNullOrEmpty(c.autoSetFlag)) flags.Add(c.autoSetFlag);

        nameText.text = c.character;
        cardText.text = ending ? c.text : GetText(c);

        if (ending)
        {
            leftAnswerText.text = endingAnswer;
            rightAnswerText.text = endingAnswer;
            Debug.Log("GameManager: ending card shown -> " + c.id);
        }
        else
        {
            leftAnswerText.text = AnswerLabel(c.left);
            rightAnswerText.text = AnswerLabel(c.right);
        }

        if (ending) SetPortrait(newMode, c.id, c.character);
        else SetPortrait(newMode, c.character);

        if (cardBackground != null)
            cardBackground.color = newMode == CardMode.Death ? deathTint
                                 : newMode == CardMode.Win ? winTint
                                 : normalTint;

        cardSwipe.ResetCard();
    }

    string GetText(CardDef c)
    {
        if (!string.IsNullOrEmpty(c.altIfFlag) && !string.IsNullOrEmpty(c.altText) && flags.Contains(c.altIfFlag))
            return c.altText;
        return c.text;
    }

    string AnswerLabel(ChoiceDef ch)
    {
        if (showShortActions && !string.IsNullOrEmpty(ch.action)) return ch.action;
        return ch.label;
    }

    void SetPortrait(CardMode m, params string[] keys)
    {
        Sprite s = null;
        foreach (var k in keys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            s = LoadPortrait(k);
            if (s != null) break;
        }

        if (s == null && m == CardMode.Death) s = deathPortrait;
        if (s == null && m == CardMode.Win) s = winPortrait;
        if (s == null) s = defaultPortrait;

        portraitImage.sprite = s;
        portraitImage.enabled = s != null;
    }

    Sprite LoadPortrait(string key)
    {
        Sprite s;
        if (!portraitCache.TryGetValue(key, out s))
        {
            s = Resources.Load<Sprite>("Portraits/" + key);
            portraitCache[key] = s;
        }
        return s;
    }

    // =====================================================================
    // Choosing
    // =====================================================================
    public void Choose(bool right)
    {
        if (data == null || current == null) return;
        if (endingRoutine != null) return;   // death reveal in progress

        // Ignore swipes right after an ending card appears, so it can't be skipped by accident
        if (Time.unscaledTime < inputLockUntil)
        {
            cardSwipe.ResetCard();
            return;
        }

        // Swiping an ending card (death or win) opens the game over screen
        if (mode != CardMode.Normal)
        {
            if (gameOver != null) gameOver.Show(reignStartYear, year);
            else BeginNewReign();
            return;
        }

        ChoiceDef ch = right ? current.right : current.left;
        bool soft = !string.IsNullOrEmpty(current.softenIfFlag) && flags.Contains(current.softenIfFlag);

        people = Mathf.Clamp(people + Soft(ch.people, soft), 0, 100);
        army = Mathf.Clamp(army + Soft(ch.army, soft), 0, 100);
        treasury = Mathf.Clamp(treasury + Soft(ch.treasury, soft), 0, 100);
        faith = Mathf.Clamp(faith + Soft(ch.faith, soft), 0, 100);

        if (!string.IsNullOrEmpty(ch.setFlag)) flags.Add(ch.setFlag);
        if (!string.IsNullOrEmpty(ch.clearFlag)) flags.Remove(ch.clearFlag);

        year++;
        cardsThisReign++;
        PlayerPrefs.SetInt(YearKey, year);
        if (achievements != null) achievements.OnYear(year);

        UpdateBars();
        UpdateYearUI();
        ShowOutcome(ch.outcome);

        // 1) a stat hit 0 or 100: death card
        EndingDef death = CheckDeath();
        if (death != null)
        {
            if (achievements != null) achievements.OnDeath();
            endingRoutine = StartCoroutine(RevealEnding(MakeEndingCard(death.id, death.title, death.text), CardMode.Death));
            return;
        }

        // 2) survived long enough: win card
        if (cardsThisReign >= data.winAfterCards)
        {
            WinDef win = PickWin();
            if (win != null)
            {
                endingRoutine = StartCoroutine(RevealEnding(MakeEndingCard(win.id, win.title, win.text), CardMode.Win));
                return;
            }
        }

        // 3) the story continues: linked card, or a new card from the pool
        CardDef next = null;
        if (!string.IsNullOrEmpty(ch.next) && !byId.TryGetValue(ch.next, out next))
            Debug.LogWarning("GameManager: unknown next card '" + ch.next + "' (from " + current.id + ")");

        ShowCard(next != null ? next : PickFromPool());
    }

    // The card disappears for a moment so the player can see the stat reach its limit,
    // then the death / win card appears.
    IEnumerator RevealEnding(CardDef card, CardMode endingMode)
    {
        cardSwipe.ResetCard();
        cardSwipe.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(deathRevealDelay);

        cardSwipe.gameObject.SetActive(true);
        endingRoutine = null;
        ShowCard(card, endingMode);
    }

    int Soft(int value, bool soft)
    {
        return (soft && value < 0) ? Mathf.CeilToInt(value * softenFactor) : value;
    }

    // =====================================================================
    // Picking the next card
    // =====================================================================
    CardDef PickFromPool()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var candidates = new List<CardDef>();
            var weights = new List<float>();
            float total = 0f;

            foreach (var c in pool)
            {
                if (c == last || seen.Contains(c.id)) continue;
                if (!HasAll(c.requiresFlags)) continue;

                float w = Mathf.Max(0.01f, c.weight);
                if (c.requiresFlags != null && c.requiresFlags.Length > 0) w *= flaggedCardBoost;

                candidates.Add(c);
                weights.Add(w);
                total += w;
            }

            if (candidates.Count > 0)
            {
                float roll = Random.value * total;
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= weights[i];
                    if (roll <= 0f) return candidates[i];
                }
                return candidates[candidates.Count - 1];
            }

            // Everything has been shown in this reign: allow repeats
            seen.Clear();
        }

        return pool.Count > 0 ? pool[0] : data.cards[0];
    }

    bool HasAll(string[] required)
    {
        if (required == null) return true;
        foreach (var f in required)
            if (!string.IsNullOrEmpty(f) && !flags.Contains(f)) return false;
        return true;
    }

    // =====================================================================
    // Endings
    // =====================================================================
    EndingDef CheckDeath()
    {
        if (people <= 0) return FindEnding("people", "low");
        if (people >= 100) return FindEnding("people", "high");
        if (army <= 0) return FindEnding("army", "low");
        if (army >= 100) return FindEnding("army", "high");
        if (treasury <= 0) return FindEnding("treasury", "low");
        if (treasury >= 100) return FindEnding("treasury", "high");
        if (faith <= 0) return FindEnding("faith", "low");
        if (faith >= 100) return FindEnding("faith", "high");
        return null;
    }

    EndingDef FindEnding(string stat, string direction)
    {
        foreach (var e in data.endings)
            if (e.stat == stat && e.direction == direction) return e;

        // Safety net if an ending is missing in the JSON
        return new EndingDef
        {
            id = "end_" + stat + "_" + direction,
            stat = stat,
            direction = direction,
            title = "Конец правления",
            text = "Правление окончено."
        };
    }

    // Wins are checked in the order they appear in the JSON: the first one whose conditions are met
    WinDef PickWin()
    {
        foreach (var w in data.wins)
        {
            if (army < w.minArmy) continue;
            if (!HasAll(w.requiresFlags)) continue;
            return w;
        }
        return null;
    }

    CardDef MakeEndingCard(string id, string title, string text)
    {
        return new CardDef { id = id, character = title, text = text };
    }

    // =====================================================================
    // Reigns and progress
    // =====================================================================
    // Called by the "New reign begins" button
    public void BeginNewReign()
    {
        if (data == null) return;
        if (gameOver != null) gameOver.Hide();
        reignStartYear = year;
        ResetReignState();
        ShowCard(PickFromPool());
    }

    // Called by MenuController after the 3rd confirmation
    public void ResetAllProgress()
    {
        if (data == null) return;
        PlayerPrefs.DeleteKey(YearKey);
        if (achievements != null) achievements.ResetAll();
        PlayerPrefs.Save();

        year = startYear;
        reignStartYear = year;
        if (gameOver != null) gameOver.Hide();
        UpdateYearUI();
        ResetReignState();
        ShowCard(PickFromPool());
    }

    // =====================================================================
    // UI helpers
    // =====================================================================
    void UpdateBars()
    {
        SetBar(peopleBar, peopleFill, people);
        SetBar(armyBar, armyFill, army);
        SetBar(treasuryBar, treasuryFill, treasury);
        SetBar(faithBar, faithFill, faith);
    }

    static void SetBar(Slider slider, Image fill, int value)
    {
        float v = value / 100f;
        if (slider != null) slider.value = v;
        if (fill != null) fill.fillAmount = v;
    }

    void UpdateYearUI()
    {
        if (yearText != null) yearText.text = year.ToString();
    }

    void ShowOutcome(string text)
    {
        if (outcomeText == null) return;
        if (outcomeRoutine != null) StopCoroutine(outcomeRoutine);
        outcomeRoutine = null;

        if (string.IsNullOrEmpty(text)) { outcomeText.text = ""; return; }

        outcomeText.text = text;
        outcomeRoutine = StartCoroutine(HideOutcomeLater());
    }

    IEnumerator HideOutcomeLater()
    {
        yield return new WaitForSecondsRealtime(outcomeSeconds);
        outcomeText.text = "";
        outcomeRoutine = null;
    }

    void HideOutcome()
    {
        if (outcomeText == null) return;
        if (outcomeRoutine != null) StopCoroutine(outcomeRoutine);
        outcomeRoutine = null;
        outcomeText.text = "";
    }

    void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    void OnApplicationQuit() { PlayerPrefs.Save(); }
}