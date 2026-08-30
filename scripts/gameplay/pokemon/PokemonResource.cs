using Game.Core;
using Godot;
using Godot.Collections;

namespace Game.Gameplay;

[GlobalClass]
[Tool]
public partial class PokemonResource : Resource
{
    [ExportCategory("Basic Info")]
    [Export]
    public string Name;

    [Export]
    public int Id;

    [Export]
    public string Description;

    [Export]
    public string PokedexSource;

    [Export]
    public int DefaultLevel = 1;

    [Export]
    public PokemonType TypeOne = PokemonType.None;

    [Export]
    public PokemonType TypeTwo = PokemonType.None;

    [ExportCategory("Abilities")]
    [Export]
    public Array<string> Abilities = new();

    [Export]
    public string HiddenAbility = string.Empty;

    [ExportCategory("Stats")]
    [Export]
    public int Height;

    [Export]
    public int Weight;

    [Export]
    public int BaseExperience;

    [Export]
    public int BaseHp;

    [Export]
    public int BaseAttack;

    [Export]
    public int BaseDefense;

    [Export]
    public int BaseSpecialAttack;

    [Export]
    public int BaseSpecialDefense;

    [Export]
    public int BaseSpeed;

    [ExportCategory("Expected Stats")]
    [Export]
    public Dictionary<string, int> ExpectedStatsLevel50;

    [Export]
    public Dictionary<string, int> ExpectedStatsLevel100;

    [ExportCategory("Moves")]
    [Export]
    public Array<string> LearnableMoves;

    [Export]
    public Dictionary<string, int> LevelUpMoves;

    [Export]
    public Dictionary<string, int> LevelUpMovePp;

    [ExportCategory("Sprites")]
    [Export]
    public Texture2D FrontSprite;

    [Export]
    public Texture2D ShinyFrontSprite;

    [Export]
    public Texture2D BackSprite;

    [Export]
    public Texture2D ShinyBackSprite;

    [Export]
    public Texture2D MenuIconSprite;
}
