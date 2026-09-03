using Game.Core;
using Game.UI;
using Godot;

namespace Game.Gameplay;

public partial class WildEncounterManager : Node
{
    [Signal]
    public delegate void FightSelectedEventHandler(PokemonInstance pokemon);

    [Signal]
    public delegate void FlightSelectedEventHandler(PokemonInstance pokemon);

    public static WildEncounterManager Instance { get; private set; }
    public PokemonInstance CurrentPokemon { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public static bool TryStart(EncounterTerrain terrain)
    {
        if (Instance == null || Instance.CurrentPokemon != null || MessageManager.IsReading())
            return false;

        Level level = SceneManager.GetCurrentLevel();
        PokemonInstance pokemon = level?.TryCreateWildPokemon(terrain);
        if (pokemon == null)
            return false;

        Instance.CurrentPokemon = pokemon;
        PokemonRegistry.MarkSeen(pokemon.Species.Id);
        Logger.Info($"Encountered wild {pokemon.DisplayName} at level {pokemon.Level}.");

        TrainerParty party = GameManager.GetPlayer()
            ?.GetNodeOrNull<TrainerPartyComponent>("TrainerParty")?.Party;
        if (party == null || party.Pokemon.Count == 0)
        {
            Instance.CurrentPokemon = null;
            DeathScreen.ShowAndRespawn();
            return true;
        }

        MessageManager.PlayChoice(
            $"You have encountered a wild {pokemon.DisplayName}. What will you do?",
            "Fight",
            "Flight",
            Instance.ResolveChoice);
        return true;
    }

    private void ResolveChoice(int choice)
    {
        PokemonInstance pokemon = CurrentPokemon;
        if (pokemon == null)
            return;

        if (choice == 0)
        {
            EmitSignal(SignalName.FightSelected, pokemon);
            MessageManager.CloseConversation();
            CallDeferred(MethodName.StartBattleMenu);
            return;
        }
        else
        {
            EmitSignal(SignalName.FlightSelected, pokemon);
            MessageManager.ContinueText("You got away safely.");
        }

        CurrentPokemon = null;
    }

    private void StartBattleMenu()
    {
        if (CurrentPokemon == null || BattleMenu.StartBattle(CurrentPokemon, FinishBattle))
            return;

        CurrentPokemon = null;
    }

    private void FinishBattle(string message)
    {
        CurrentPokemon = null;
        if (!string.IsNullOrWhiteSpace(message))
            MessageManager.PlayText(message);
    }
}
