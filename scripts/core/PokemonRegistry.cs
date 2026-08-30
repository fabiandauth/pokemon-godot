using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Godot;

namespace Game.Core;

public partial class PokemonRegistry : Node
{
    public static PokemonRegistry Instance { get; private set; }

    [Export] public bool ShowAllForDevelopment = true;

    private readonly List<PokemonResource> pokemon = new();
    private readonly HashSet<int> seenPokemon = new();

    public override void _Ready()
    {
        Instance = this;
        foreach (string file in DirAccess.GetFilesAt("res://resources/pokemon").OrderBy(path => path))
        {
            if (!file.EndsWith(".tres")) continue;
            var resource = GD.Load<PokemonResource>($"res://resources/pokemon/{file}");
            if (resource != null) pokemon.Add(resource);
        }
        pokemon.Sort((left, right) => left.Id.CompareTo(right.Id));
    }

    public static IReadOnlyList<PokemonResource> GetVisiblePokemon() => Instance == null
        ? System.Array.Empty<PokemonResource>()
        : Instance.pokemon.Where(entry => Instance.ShowAllForDevelopment || Instance.seenPokemon.Contains(entry.Id)).ToList();

    public static void MarkSeen(int pokedexNumber) => Instance?.seenPokemon.Add(pokedexNumber);

    public static bool HasBeenSeen(int pokedexNumber) =>
        Instance != null && (Instance.ShowAllForDevelopment || Instance.seenPokemon.Contains(pokedexNumber));
}
