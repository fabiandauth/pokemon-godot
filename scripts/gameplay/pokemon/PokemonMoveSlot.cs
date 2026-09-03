using Game.Core;
using Godot;

namespace Game.Gameplay;

[GlobalClass]
public partial class PokemonMoveSlot : Resource
{
    public const int DefaultAttackStrength = 40;

    [Export] public string MoveName = string.Empty;
    [Export] public int LearnedAtLevel = 1;
    [Export] public int CurrentPp;
    [Export] public int MaxPp;

    [ExportCategory("Battle")]
    [Export(PropertyHint.Range, "0,250,1")]
    public int AttackStrength = DefaultAttackStrength;

    [Export]
    public MoveCategory Category = MoveCategory.Physical;

    [Export] public MoveResource Move;

    public int EffectiveAttackStrength => Move?.Power > 0 ? Move.Power : AttackStrength;
    public MoveCategory EffectiveCategory => Move?.Category ?? Category;

    public void RestorePp() => CurrentPp = MaxPp;
}
