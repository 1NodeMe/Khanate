using System.Collections;
using UnityEngine;
using TMPro;

public class MenuController : MonoBehaviour
{
    public GameManager gameManager;
    public AchievementManager achievements;
    public GameObject menuPanel;
    public GameObject achievementsPanel;
    public TMP_Text menuTitleText;
    public TMP_Text resetButtonText;

    public string pausedTitle = "Game Paused";
    public string menuTitle = "Menu";

    int resetStage;
    Coroutine resetTimer;

    void Start()
    {
        menuPanel.SetActive(false);
        achievementsPanel.SetActive(false);
        ShowResetLabel();
    }

    // Menu button during gameplay
    public void OpenMenu()
    {
        menuTitleText.text = pausedTitle;
        menuPanel.SetActive(true);
        achievementsPanel.SetActive(false);
    }

    // "Settings" button on the game over screen
    public void OpenMenuFromGameOver()
    {
        menuTitleText.text = menuTitle;
        menuPanel.SetActive(true);
        achievementsPanel.SetActive(false);
    }

    public void CloseMenu()
    {
        DisarmReset();
        achievementsPanel.SetActive(false);
        menuPanel.SetActive(false);
    }

    public void OpenAchievements()
    {
        achievements.RefreshList();
        achievementsPanel.SetActive(true);
    }

    public void CloseAchievements()
    {
        achievementsPanel.SetActive(false);
    }

    public void SaveAndExit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----- Reset progress: 3 presses, with a prompt after each of the first two -----
    public void OnResetPressed()
    {
        resetStage++;
        if (resetStage >= 3)
        {
            DisarmReset();
            gameManager.ResetAllProgress();
            CloseMenu();
            return;
        }
        ShowResetLabel();
        if (resetTimer != null) StopCoroutine(resetTimer);
        resetTimer = StartCoroutine(DisarmAfterDelay());
    }

    IEnumerator DisarmAfterDelay()
    {
        yield return new WaitForSeconds(5f);
        DisarmReset();
    }

    void DisarmReset()
    {
        if (resetTimer != null) StopCoroutine(resetTimer);
        resetTimer = null;
        resetStage = 0;
        ShowResetLabel();
    }

    void ShowResetLabel()
    {
        switch (resetStage)
        {
            case 0: resetButtonText.text = "Сбросить прогресс"; break;
            case 1: resetButtonText.text = "Вы уверены? (1/3)"; break;
            case 2: resetButtonText.text = "Всё будет удалено! Точно? (2/3)"; break;
        }
    }
}