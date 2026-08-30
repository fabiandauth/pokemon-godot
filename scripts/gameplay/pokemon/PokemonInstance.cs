using System.Linq;
using Godot;
using Godot.Collections;

namespace Game.Gameplay;

[GlobalClass]
public partial class PokemonInstance : Resource
{
    public const int MaximumMoves = 4;

    [ExportCategory("Identity")]
    [Export] public PokemonResource Species;
    [Export] public string Nickname = string.Empty;
    [Export(PropertyHint.Range, "1,100,1")] public int Level = 1;
    [Export] public int Experience;
    [Export] public string Ability = string.Empty;

    [ExportCategory("Stats")]
    [Export] public PokemonStatBlock Stats = new();
    [Export] public PokemonStatBlock IndividualValues = CreateDefaultIvs();
    [Export] public PokemonStatBlock EffortValues = new();
    [Export] public int CurrentHp;

    [ExportCategory("Moves")]
    [Export] public Array<PokemonMoveSlot> Moves = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? Species?.Name ?? "Unknown" : Nickname;
    public bool IsFainted => CurrentHp <= 0;

    public static PokemonInstance Create(PokemonResource species, int level = 1)
    {
        var pokemon = new PokemonInstance { Species = species, Level = Mathf.Clamp(level, 1, 100) };
        if (species?.Abilities?.Count > 0)
            pokemon.Ability = species.Abilities[0];
        pokemon.LearnDefaultMoves();
        pokemon.RecalculateStats(heal: true);
        return pokemon;
    }

    public void InitializeIfNeeded()
    {
        if (Species == null) return;
        if (string.IsNullOrWhiteSpace(Ability) && Species.Abilities.Count > 0)
            Ability = Species.Abilities[0];
        if (Moves.Count == 0)
            LearnDefaultMoves();
        if (Stats == null || Stats.Hp <= 0)
            RecalculateStats(heal: true);
    }

    public void RecalculateStats(bool heal = false)
    {
        if (Species == null) return;
        int previousMaximumHp = Stats?.Hp ?? 0;
        Stats ??= new PokemonStatBlock();
        IndividualValues ??= CreateDefaultIvs();
        EffortValues ??= new PokemonStatBlock();
        Level = Mathf.Clamp(Level, 1, 100);
        Stats.Hp = CalculateHp(Species.BaseHp, IndividualValues.Hp, EffortValues.Hp);
        Stats.Attack = CalculateOther(Species.BaseAttack, IndividualValues.Attack, EffortValues.Attack);
        Stats.Defense = CalculateOther(Species.BaseDefense, IndividualValues.Defense, EffortValues.Defense);
        Stats.SpecialAttack = CalculateOther(Species.BaseSpecialAttack, IndividualValues.SpecialAttack, EffortValues.SpecialAttack);
        Stats.SpecialDefense = CalculateOther(Species.BaseSpecialDefense, IndividualValues.SpecialDefense, EffortValues.SpecialDefense);
        Stats.Speed = CalculateOther(Species.BaseSpeed, IndividualValues.Speed, EffortValues.Speed);
        CurrentHp = heal ? Stats.Hp : Mathf.Clamp(CurrentHp + Stats.Hp - previousMaximumHp, 0, Stats.Hp);
    }

    public bool TryLearnMove(PokemonMoveSlot move)
    {
        if (move == null || Moves.Count >= MaximumMoves || Moves.Any(slot => slot.MoveName == move.MoveName))
            return false;
        Moves.Add(move);
        return true;
    }

    public void LearnDefaultMoves()
    {
        Moves.Clear();
        if (Species?.LevelUpMoves == null) return;
        foreach (var move in Species.LevelUpMoves
            .Where(entry => entry.Value <= Level)
            .OrderByDescending(entry => entry.Value)
            .Take(MaximumMoves)
            .OrderBy(entry => entry.Value))
        {
            int maxPp = Species.LevelUpMovePp != null && Species.LevelUpMovePp.TryGetValue(move.Key, out int pp) ? pp : 0;
            Moves.Add(new PokemonMoveSlot
            {
                MoveName = move.Key,
                LearnedAtLevel = move.Value,
                MaxPp = maxPp,
                CurrentPp = maxPp
            });
        }
    }

    private int CalculateHp(int baseStat, int iv, int ev) =>
        ((2 * baseStat + Mathf.Clamp(iv, 0, 31) + Mathf.Clamp(ev, 0, 252) / 4) * Level) / 100 + Level + 10;

    private int CalculateOther(int baseStat, int iv, int ev) =>
        ((2 * baseStat + Mathf.Clamp(iv, 0, 31) + Mathf.Clamp(ev, 0, 252) / 4) * Level) / 100 + 5;

    private static PokemonStatBlock CreateDefaultIvs() => new()
    {
        Hp = 31, Attack = 31, Defense = 31, SpecialAttack = 31, SpecialDefense = 31, Speed = 31
    };
}
