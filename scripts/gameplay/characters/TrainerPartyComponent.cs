using Godot;

namespace Game.Gameplay;

[GlobalClass]
public partial class TrainerPartyComponent : Node
{
    [Export] public TrainerParty Party;

    public override void _Ready()
    {
        Party ??= new TrainerParty();
        Party.TrimToMaximumSize();
        foreach (PokemonInstance pokemon in Party.Pokemon)
            pokemon?.InitializeIfNeeded();
    }

    public bool TryAddPokemon(PokemonResource species, int level = 1) =>
        Party.TryAdd(PokemonInstance.Create(species, level));
}
