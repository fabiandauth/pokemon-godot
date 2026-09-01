using Godot;

namespace Game.Gameplay;

[GlobalClass]
public partial class WildPokemonEncounter : Resource
{
    [Export]
    public PokemonResource Species;

    [Export(PropertyHint.Range, "0,100,0.1")]
    public float SpawnWeight = 1f;

    [Export(PropertyHint.Range, "1,100,1")]
    public int MinimumLevel = 1;

    [Export(PropertyHint.Range, "1,100,1")]
    public int MaximumLevel = 1;

    public PokemonInstance Create(RandomNumberGenerator random)
    {
        if (Species == null)
            return null;

        int minimum = Mathf.Clamp(Mathf.Min(MinimumLevel, MaximumLevel), 1, 100);
        int maximum = Mathf.Clamp(Mathf.Max(MinimumLevel, MaximumLevel), 1, 100);
        return PokemonInstance.Create(Species, random.RandiRange(minimum, maximum));
    }
}
