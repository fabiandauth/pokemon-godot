using Game.Core;
using Godot;

namespace Game.Gameplay;

public static class BattleMechanics
{
    public static int CalculateDamage(PokemonInstance attacker, PokemonInstance defender, PokemonMoveSlot move)
    {
        if (attacker == null || defender == null)
            return 0;

        MoveCategory category = move?.EffectiveCategory ?? MoveCategory.Physical;
        int attackStrength = move?.EffectiveAttackStrength ?? PokemonMoveSlot.DefaultAttackStrength;
        return CalculateDamage(
            attackStrength,
            category,
            attacker.Stats.Attack,
            defender.Stats.Defense,
            attacker.Stats.SpecialAttack,
            defender.Stats.SpecialDefense);
    }

    public static int CalculateDamage(int attackStrength, int attack, int defense) =>
        Mathf.Max(0, attackStrength + attack - defense);

    public static int CalculateDamage(
        int attackStrength,
        MoveCategory category,
        int physicalAttack,
        int physicalDefense,
        int specialAttack,
        int specialDefense) => category switch
    {
        MoveCategory.Special => CalculateDamage(attackStrength, specialAttack, specialDefense),
        MoveCategory.Physical => CalculateDamage(attackStrength, physicalAttack, physicalDefense),
        _ => 0
    };

    public static bool ActsFirst(PokemonInstance first, PokemonInstance second, RandomNumberGenerator random)
    {
        bool firstWinsTie = first.Stats.Speed == second.Stats.Speed && random.Randf() < 0.5f;
        return ActsFirst(first.Stats.Speed, second.Stats.Speed, firstWinsTie);
    }

    public static bool ActsFirst(int firstSpeed, int secondSpeed, bool firstWinsTie) =>
        firstSpeed == secondSpeed ? firstWinsTie : firstSpeed > secondSpeed;
}
