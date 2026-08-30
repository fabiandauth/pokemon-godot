using Godot;
using Godot.Collections;

namespace Game.Gameplay;

[GlobalClass]
public partial class TrainerParty : Resource
{
    public const int MaximumSize = 6;

    [Signal] public delegate void PartyChangedEventHandler();
    private Array<PokemonInstance> pokemon = new();

    [Export]
    public Array<PokemonInstance> Pokemon
    {
        get => pokemon;
        set
        {
            pokemon = value ?? new Array<PokemonInstance>();
            TrimToMaximumSize();
        }
    }

    public bool IsFull => Pokemon.Count >= MaximumSize;

    public bool TryAdd(PokemonInstance pokemon)
    {
        if (pokemon == null || IsFull || Pokemon.Contains(pokemon)) return false;
        Pokemon.Add(pokemon);
        EmitSignal(SignalName.PartyChanged);
        return true;
    }

    public bool Remove(PokemonInstance pokemon)
    {
        bool removed = Pokemon.Remove(pokemon);
        if (removed) EmitSignal(SignalName.PartyChanged);
        return removed;
    }

    public bool Swap(int first, int second)
    {
        if (first < 0 || second < 0 || first >= Pokemon.Count || second >= Pokemon.Count || first == second)
            return false;
        (Pokemon[first], Pokemon[second]) = (Pokemon[second], Pokemon[first]);
        EmitSignal(SignalName.PartyChanged);
        return true;
    }

    public void TrimToMaximumSize()
    {
        while (pokemon.Count > MaximumSize)
            pokemon.RemoveAt(pokemon.Count - 1);
    }
}
