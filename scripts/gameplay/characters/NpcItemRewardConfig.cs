using Game.Core;
using Godot;

namespace Game.Gameplay;

[GlobalClass]
public partial class NpcItemRewardConfig : Resource
{
    [Export]
    public NpcItemHandoverMode Mode = NpcItemHandoverMode.None;

    [Export]
    public string ItemId = string.Empty;

    [Export]
    public string ItemName = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string ConvincingGoal = string.Empty;

    [Export(PropertyHint.Range, "1,10,1")]
    public int RequiredConvincingTurns = 1;

    [Export]
    public int Quantity = 1;

    [Export(PropertyHint.MultilineText)]
    public string HandoverMessage = "Here, I want you to have this.";
}
