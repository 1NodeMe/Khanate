using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    const string YearKey = "year";

    enum CardMode { Normal, Death, Win, Rescue, Event }

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

    [Header("Stat value texts")]
    public TMP_Text peopleValueText;
    public TMP_Text armyValueText;
    public TMP_Text treasuryValueText;
    public TMP_Text faithValueText;

    [Header("Card UI")]
    public CardSwipe cardSwipe;
    public Image portraitImage;
    public TMP_Text nameText, cardText;
    public TMP_Text leftAnswerText, rightAnswerText;
    public PortraitLibrary portraits;   // manual portrait list (NPC name -> sprite)
    public Sprite defaultPortrait;      // used when no portrait is found for an NPC
    public TMP_Text outcomeText;        // optional: short consequence text after a swipe

    [Header("Death / win cards")]
    public Image cardBackground;        // optional: the Image on the Card object (tinted for endings)
    public Sprite deathPortrait;        // optional: used for every death card without its own portrait
    public Sprite winPortrait;          // optional: used for every win card without its own portrait
    public Color deathTint = new Color(0.55f, 0.2f, 0.2f, 1f);
    public Color winTint = new Color(0.9f, 0.78f, 0.35f, 1f);
    public Color rescueTint = new Color(0.35f, 0.65f, 0.45f, 1f);
    public Sprite threeBiyPortrait;
    public float deathRevealDelay = 0.7f;   // pause so the player sees the stat hit its limit
    public float endingLockSeconds = 0.8f;  // swipes are ignored for this long after an ending card appears
    public string endingAnswer = "Далее";

    [Header("Three biy rescue")]
    [Range(0f, 1f)] public float threeBiyChance = 0.25f;
    public string threeBiyTitle = "Три бия";
    public string threeBiyText = "Три бия заступились за тебя. Характеристика, приведшая к смерти, восстановлена до 50%.";

    [Header("Special events")]
    [Range(0f, 1f)] public float altynAdamChance = 0.2f;
    public Sprite altynAdamPortrait;
    public Color eventTint = new Color(0.35f, 0.55f, 0.75f, 1f);
    [FormerlySerializedAs("armyLockIcon")]
    public Image armyIcon;
    public Image armyLockOverlay;
    public Sprite armyLockSprite;
    public string altynAdamTitle = "Обнаружили Алтын Адам";
    public string altynAdamText = "В степи обнаружили Алтын Адам. Вера народа укрепилась.";
    public int silkRoadDurationTurns = 10;
    public float silkRoadIncomeInterval = 5f;
    public int silkRoadIncome = 1;

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
    string rescueStat;
    Color normalTint = Color.white;
    Coroutine outcomeRoutine;
    Coroutine endingRoutine;
    Coroutine silkRoadIncomeRoutine;
    string specialEvent;
    int silkRoadTurnsRemaining;
    int armyLockTurnsRemaining;
    bool silkRoadActive;
    bool altynAdamShown;

    // =====================================================================
    // Startup
    // =====================================================================
    void Start()
    {
        LoadData();
        if (data == null) return;

        if (cardBackground != null) normalTint = cardBackground.color;
        EnsureArmyLockIcon();

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

        byId["silk_start"] = MakeSpecialCard("silk_start", "Шёлковый путь",
            "Шёлковый путь открыт. Караваны двинулись через наши земли.");
        byId["silk_end"] = MakeSpecialCard("silk_end", "Шёлковый путь",
            "Десять лет Шёлкового пути завершились. Казна получила богатство, а караваны отправились дальше.");
    }

    void ResetReignState()
    {
        // Cancel a death reveal that may still be running and bring the card back
        if (endingRoutine != null) StopCoroutine(endingRoutine);
        endingRoutine = null;
        StopSilkRoad();
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
        rescueStat = null;
        specialEvent = null;
        silkRoadTurnsRemaining = 0;
        armyLockTurnsRemaining = 0;
        silkRoadActive = false;
        altynAdamShown = false;
        mode = CardMode.Normal;
        inputLockUntil = 0f;

        UpdateBars();
        UpdateArmyLockIcon();
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
                                 : newMode == CardMode.Rescue ? rescueTint
                                 : newMode == CardMode.Event ? eventTint
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
        if (s == null && m == CardMode.Rescue) s = threeBiyPortrait;
        if (s == null && m == CardMode.Event && specialEvent == "altyn_adam") s = altynAdamPortrait;
        if (s == null) s = defaultPortrait;

        portraitImage.sprite = s;
        portraitImage.enabled = s != null;
    }

    Sprite LoadPortrait(string key)
    {
        Sprite s;
        if (!portraitCache.TryGetValue(key, out s))
        {
            if (portraits != null) s = portraits.Get(key);
            if (s == null) s = Resources.Load<Sprite>("Portraits/" + key);   // optional file-name fallback
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

        ClearChoicePreview();

        // Ignore swipes right after an ending card appears, so it can't be skipped by accident
        if (Time.unscaledTime < inputLockUntil)
        {
            cardSwipe.ResetCard();
            return;
        }

        if (mode == CardMode.Rescue)
        {
            ApplyThreeBiyRescue();
            return;
        }

        if (mode == CardMode.Event)
        {
            ResolveSpecialEvent();
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

        bool startedSilkRoad = ch.setFlag == "silk_road_start";
        if (startedSilkRoad) StartSilkRoad();

        int armyChange = Soft(ch.army, soft);
        if (armyLockTurnsRemaining > 0 && armyChange < 0) armyChange = 0;
        bool activatedKokpar = current.id == "M3" && !right;

        people   = Mathf.Clamp(people   + Soft(ch.people,   soft), 0, 100);
        army     = Mathf.Clamp(army     + armyChange, 0, 100);
        treasury = Mathf.Clamp(treasury + Soft(ch.treasury, soft), 0, 100);
        faith    = Mathf.Clamp(faith    + Soft(ch.faith,    soft), 0, 100);

        if (activatedKokpar)
        {
            armyLockTurnsRemaining = Random.Range(5, 11);
            ShowOutcome("Кокпар: армия увеличена на 10. Снижение заблокировано на " + armyLockTurnsRemaining + " ходов.");
            UpdateArmyLockIcon();
        }

        if (!string.IsNullOrEmpty(ch.setFlag)) flags.Add(ch.setFlag);
        if (!string.IsNullOrEmpty(ch.clearFlag)) flags.Remove(ch.clearFlag);

        year++;
        cardsThisReign++;
        PlayerPrefs.SetInt(YearKey, year);
        if (achievements != null) achievements.OnYear(year);

        UpdateBars();
        UpdateYearUI();
        ShowOutcome(ch.outcome);

        if (armyLockTurnsRemaining > 0)
        {
            armyLockTurnsRemaining--;
            UpdateArmyLockIcon();
        }

        // 1) a stat hit 0 or 100: death card
        EndingDef death = CheckDeath();
        if (death != null)
        {
            StopSilkRoad();
            if (TryShowThreeBiy(death)) return;
            if (achievements != null) achievements.OnDeath();
            endingRoutine = StartCoroutine(RevealEnding(MakeEndingCard(death.id, death.title, death.text), CardMode.Death));
            return;
        }

        if (!startedSilkRoad && AdvanceSilkRoad()) return;

        if (TryTriggerRandomEvent()) return;

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

        if (next != null && IsSilkRoadOnlyCard(next) && !silkRoadActive)
            next = null;

        bool silkStartCard = next != null && next.id == "silk_start";
        ShowCard(next != null ? next : PickFromPool(), silkStartCard ? CardMode.Event : CardMode.Normal);
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

    void StartSilkRoad()
    {
        StopSilkRoad();
        silkRoadActive = true;
        silkRoadTurnsRemaining = Mathf.Max(1, silkRoadDurationTurns);
        silkRoadIncomeRoutine = StartCoroutine(SilkRoadIncome());
    }

    void StopSilkRoad()
    {
        if (silkRoadIncomeRoutine != null) StopCoroutine(silkRoadIncomeRoutine);
        silkRoadIncomeRoutine = null;
        silkRoadActive = false;
        silkRoadTurnsRemaining = 0;
    }

    IEnumerator SilkRoadIncome()
    {
        while (silkRoadActive)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, silkRoadIncomeInterval));
            if (!silkRoadActive) yield break;

            treasury = Mathf.Clamp(treasury + silkRoadIncome, 0, 100);
            UpdateBars();
        }
    }

    bool AdvanceSilkRoad()
    {
        if (!silkRoadActive) return false;

        silkRoadTurnsRemaining--;
        if (silkRoadTurnsRemaining > 0) return false;

        StopSilkRoad();
        ShowSpecialEvent("silk_end", "Шёлковый путь", 
            "Десять лет Шёлкового пути завершились. Караваны отправились дальше, оставив богатство в казне.");
        return true;
    }

    bool TryTriggerRandomEvent()
    {
        if (!altynAdamShown && faith < 20 && Random.value < altynAdamChance)
        {
            altynAdamShown = true;
            ShowSpecialEvent("altyn_adam", altynAdamTitle, altynAdamText);
            return true;
        }

        return false;
    }

    void ShowSpecialEvent(string eventId, string title, string text)
    {
        specialEvent = eventId;
        ShowCard(MakeSpecialCard(eventId, title, text), CardMode.Event);
    }

    void ResolveSpecialEvent()
    {
        string eventId = current.id;

        if (eventId == "altyn_adam")
        {
            faith = Mathf.Clamp(faith + 20, 0, 100);
            UpdateBars();
            ShowOutcome("Вера увеличена на 20.");
        }
        specialEvent = null;
        mode = CardMode.Normal;
        ShowCard(PickFromPool());
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
                if (IsSilkRoadOnlyCard(c) && !silkRoadActive) continue;

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

    bool IsSilkRoadOnlyCard(CardDef card)
    {
        if (card == null) return false;

        switch (card.id)
        {
            case "K1.0":
            case "K1.1":
            case "K1.2":
            case "E1a":
            case "E1b":
            case "E1c":
            case "E1d":
                return true;
            default:
                return false;
        }
    }

    // =====================================================================
    // Endings
    // =====================================================================
    EndingDef CheckDeath()
    {
        if (people <= 0)     return FindEnding("people", "low");
        if (people >= 100)   return FindEnding("people", "high");
        if (army <= 0)       return FindEnding("army", "low");
        if (army >= 100)     return FindEnding("army", "high");
        if (treasury <= 0)   return FindEnding("treasury", "low");
        if (treasury >= 100) return FindEnding("treasury", "high");
        if (faith <= 0)      return FindEnding("faith", "low");
        if (faith >= 100)    return FindEnding("faith", "high");
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

    CardDef MakeSpecialCard(string id, string title, string text)
    {
        return new CardDef
        {
            id = id,
            kind = "event",
            character = title,
            text = text,
            left = new ChoiceDef { label = endingAnswer },
            right = new ChoiceDef { label = endingAnswer }
        };
    }

    bool TryShowThreeBiy(EndingDef death)
    {
        if (threeBiyChance <= 0f || Random.value >= threeBiyChance) return false;

        rescueStat = death.stat;
        CardDef rescue = MakeEndingCard("three_biy", threeBiyTitle, threeBiyText);
        endingRoutine = StartCoroutine(RevealEnding(rescue, CardMode.Rescue));
        return true;
    }

    void ApplyThreeBiyRescue()
    {
        switch (rescueStat)
        {
            case "people": people = 50; break;
            case "army": army = 50; break;
            case "treasury": treasury = 50; break;
            case "faith": faith = 50; break;
            default:
                Debug.LogWarning("GameManager: unknown stat for Three biy rescue: " + rescueStat);
                return;
        }

        rescueStat = null;
        mode = CardMode.Normal;
        UpdateBars();
        ShowOutcome(threeBiyTitle + ": спасение состоялось.");
        ShowCard(PickFromPool());
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
        SetStatText(peopleValueText, people.ToString());
        SetStatText(armyValueText, army.ToString());
        SetStatText(treasuryValueText, treasury.ToString());
        SetStatText(faithValueText, faith.ToString());
    }

    public void PreviewChoice(bool right)
    {
        if (data == null || current == null || mode != CardMode.Normal) return;

        ChoiceDef choice = right ? current.right : current.left;
        bool soft = !string.IsNullOrEmpty(current.softenIfFlag) && flags.Contains(current.softenIfFlag);
        int armyChange = Soft(choice.army, soft);
        if (armyLockTurnsRemaining > 0 && armyChange < 0) armyChange = 0;

        int nextPeople = Mathf.Clamp(people + Soft(choice.people, soft), 0, 100);
        int nextArmy = Mathf.Clamp(army + armyChange, 0, 100);
        int nextTreasury = Mathf.Clamp(treasury + Soft(choice.treasury, soft), 0, 100);
        int nextFaith = Mathf.Clamp(faith + Soft(choice.faith, soft), 0, 100);

        SetStatPreview(peopleValueText, people, nextPeople);
        SetStatPreview(armyValueText, army, nextArmy);
        SetStatPreview(treasuryValueText, treasury, nextTreasury);
        SetStatPreview(faithValueText, faith, nextFaith);
    }

    public void ClearChoicePreview()
    {
        SetStatText(peopleValueText, people.ToString());
        SetStatText(armyValueText, army.ToString());
        SetStatText(treasuryValueText, treasury.ToString());
        SetStatText(faithValueText, faith.ToString());
    }

    static void SetStatText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    static void SetStatPreview(TMP_Text text, int currentValue, int nextValue)
    {
        if (text == null) return;
        text.text = currentValue == nextValue
            ? currentValue.ToString()
            : currentValue + " -> " + nextValue;
    }

    void UpdateArmyLockIcon()
    {
        bool locked = armyLockTurnsRemaining > 0;
        if (armyIcon != null) armyIcon.enabled = !locked;
        if (armyLockOverlay == null) return;
        if (armyLockSprite != null) armyLockOverlay.sprite = armyLockSprite;
        armyLockOverlay.enabled = locked;
    }

    void EnsureArmyLockIcon()
    {
        if (armyLockOverlay != null || armyLockSprite == null) return;

        GameObject iconObject = new GameObject("ArmyLockIcon", typeof(RectTransform), typeof(Image));
        Transform parent = armyIcon != null ? armyIcon.transform.parent : armyBar != null ? armyBar.transform : transform;
        iconObject.transform.SetParent(parent, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        if (armyIcon != null)
        {
            RectTransform source = armyIcon.rectTransform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.localRotation = source.localRotation;
            rect.localScale = source.localScale;
        }
        else
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(14f, 0f);
            rect.sizeDelta = new Vector2(24f, 24f);
        }

        armyLockOverlay = iconObject.GetComponent<Image>();
        armyLockOverlay.sprite = armyLockSprite;
        armyLockOverlay.preserveAspect = true;
        armyLockOverlay.raycastTarget = false;
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
