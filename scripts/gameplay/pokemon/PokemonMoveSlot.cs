using Godot;

namespace Game.Gameplay;

[GlobalClass]
public partial class PokemonMoveSlot : Resource
{
    [Export] public string MoveName = string.Empty;
    [Export] public int LearnedAtLevel = 1;
    [Export] public int CurrentPp;
    [Export] public int MaxPp;
    [Export] public MoveResource Move;

    public void RestorePp() => CurrentPp = MaxPp;
}
