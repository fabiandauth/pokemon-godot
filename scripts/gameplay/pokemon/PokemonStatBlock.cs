using Godot;

namespace Game.Gameplay;

[GlobalClass]
public partial class PokemonStatBlock : Resource
{
    [Export] public int Hp;
    [Export] public int Attack;
    [Export] public int Defense;
    [Export] public int SpecialAttack;
    [Export] public int SpecialDefense;
    [Export] public int Speed;

    public PokemonStatBlock DuplicateValues() => new()
    {
        Hp = Hp,
        Attack = Attack,
        Defense = Defense,
        SpecialAttack = SpecialAttack,
        SpecialDefense = SpecialDefense,
        Speed = Speed
    };
}
