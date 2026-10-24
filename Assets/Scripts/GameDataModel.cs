using System;

// Mirrors the structure of Assets/Resources/cards.json (read with JsonUtility).

[Serializable]
public class StatsDef
{
    public int people = 50;
    public int army = 50;
    public int treasury = 50;
    public int faith = 50;
}

[Serializable]
public class FlagDef
{
    public string id;
    public string description;
}

[Serializable]
public class ChoiceDef
{
    public string label;       // full line the khan says
    public string action;      // short action name (fits on screen while swiping)
    public string outcome;     // short consequence text, may be empty
    public int people, army, treasury, faith;
    public string next;        // id of the card that follows immediately, may be empty
    public string setFlag;
    public string clearFlag;
}

[Serializable]
public class CardDef
{
    public string id;
    public string kind;        // start / follow / echo / small / debt
    public string character;
    public string text;
    public ChoiceDef left = new ChoiceDef();
    public ChoiceDef right = new ChoiceDef();
    public string[] requiresFlags = new string[0];
    public string altText;
    public string altIfFlag;       // if this flag is set, altText replaces text
    public string softenIfFlag;    // if this flag is set, negative effects of this card are reduced
    public float weight = 1f;
    public string autoSetFlag;     // set automatically when the card is shown
}

[Serializable]
public class EndingDef
{
    public string id;
    public string direction;   // "low" or "high"
    public string stat;        // people / army / treasury / faith
    public string title;
    public string text;
}

[Serializable]
public class WinDef
{
    public string id;
    public string title;
    public string text;
    public string[] requiresFlags = new string[0];
    public int minArmy;
}

[Serializable]
public class GameDataRoot
{
    public StatsDef startStats = new StatsDef();
    public int winAfterCards = 40;
    public FlagDef[] flags = new FlagDef[0];
    public CardDef[] cards = new CardDef[0];
    public EndingDef[] endings = new EndingDef[0];
    public WinDef[] wins = new WinDef[0];
}
