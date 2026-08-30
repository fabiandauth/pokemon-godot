using Game.Core;
using Game.Gameplay.Items;
using Godot;
using Godot.Collections;

namespace Game.Gameplay;

[GlobalClass]
public partial class NpcItemRewardConfig : Resource
{
    [Export]
    public NpcItemHandoverMode Mode = NpcItemHandoverMode.None;

    [Export]
    public ItemDefinition Item;

    [Export(PropertyHint.MultilineText)]
    public string ConvincingGoal = string.Empty;

    [Export(PropertyHint.Range, "1,10,1")]
    public int RequiredConvincingTurns = 1;

    [ExportGroup("Deterministic fallback")]
    [Export]
    public Array<string> ConvincingSubjectTerms = new();

    [Export]
    public Array<string> ConvincingIntentTerms = new();

    [Export]
    public int Quantity = 1;

    [Export(PropertyHint.MultilineText)]
    public string HandoverMessage = "Here, I want you to have this.";

    public bool MatchesDeterministicConvincingRule(string message)
    {
        if (string.IsNullOrWhiteSpace(message)
            || ConvincingSubjectTerms.Count == 0
            || ConvincingIntentTerms.Count == 0)
            return false;

        string normalized = message.ToLowerInvariant();
        return ContainsAny(normalized, ConvincingSubjectTerms)
            && ContainsAny(normalized, ConvincingIntentTerms);
    }

    private static bool ContainsAny(string message, Array<string> terms)
    {
        foreach (string term in terms)
        {
            if (!string.IsNullOrWhiteSpace(term)
                && message.Contains(term.Trim().ToLowerInvariant()))
                return true;
        }

        return false;
    }
}
