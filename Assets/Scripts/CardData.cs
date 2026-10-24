using UnityEngine;

[CreateAssetMenu(menuName = "Game/Card", fileName = "NewCard")]
public class CardData : ScriptableObject
{
    [Header("Content")]
    public bool isDeathCard;
    public string characterName;
    public Sprite portrait;
    [TextArea(2, 4)] public string text;
    public string leftAnswer = "No";
    public string rightAnswer = "Yes";

    [Header("Faith")]
    public int leftFaith, rightFaith;
    [Header("People")]
    public int leftPeople, rightPeople;
    [Header("Army")]
    public int leftArmy, rightArmy;
    [Header("Treasury")]
    public int leftTreasury, rightTreasury;
}