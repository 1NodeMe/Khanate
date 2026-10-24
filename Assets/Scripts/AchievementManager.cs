using System.Text;
using UnityEngine;
using TMPro;

public class AchievementManager : MonoBehaviour
{
    public TMP_Text listText;

    struct Ach
    {
        public string id, title, desc;
        public Ach(string id, string title, string desc) { this.id = id; this.title = title; this.desc = desc; }
    }

    static readonly Ach[] All =
    {
        new Ach("first_death", "Конец правления", "Пережить первую смерть правителя"),
        new Ach("five_reigns", "Династия", "Пережить 5 правлений"),
        new Ach("year_1500", "Золотой век", "Дожить до 1500 года"),
    };

    const string DeathsKey = "deaths";

    public void OnDeath()
    {
        int d = PlayerPrefs.GetInt(DeathsKey, 0) + 1;
        PlayerPrefs.SetInt(DeathsKey, d);
        Unlock("first_death");
        if (d >= 5) Unlock("five_reigns");
    }

    public void OnYear(int year)
    {
        if (year >= 1500) Unlock("year_1500");
    }

    void Unlock(string id)
    {
        string key = "ach_" + id;
        if (PlayerPrefs.GetInt(key, 0) == 1) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        Debug.Log("Achievement unlocked: " + id);
    }

    public void RefreshList()
    {
        var sb = new StringBuilder();
        foreach (var a in All)
        {
            bool got = PlayerPrefs.GetInt("ach_" + a.id, 0) == 1;
            sb.AppendLine((got ? "[X] " : "[  ] ") + a.title);
            sb.AppendLine("      " + a.desc);
            sb.AppendLine();
        }
        listText.text = sb.ToString();
    }

    public void ResetAll()
    {
        foreach (var a in All) PlayerPrefs.DeleteKey("ach_" + a.id);
        PlayerPrefs.DeleteKey(DeathsKey);
    }
}