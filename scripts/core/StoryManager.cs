using System.Linq;
using Game.Gameplay;
using Game.UI;
using Godot;

namespace Game.Core;

public partial class StoryManager : Node
{
    public static StoryManager Instance { get; private set; }

    [Signal] public delegate void ProgressChangedEventHandler(StoryProgress previous, StoryProgress current);

    [Export] public StoryProgress Progress = StoryProgress.WokeUpLate;

    public override void _Ready()
    {
        Instance = this;
        Logger.Info($"Story started at {Progress}.");
    }

    public static bool HandleNpcInteraction(Npc npc)
    {
        if (Instance == null || npc == null || npc.StoryRole == NpcStoryRole.None)
            return false;

        return npc.StoryRole switch
        {
            NpcStoryRole.Mother => Instance.TalkToMother(npc),
            NpcStoryRole.ProfessorPokebart => Instance.TalkToProfessor(npc),
            _ => false
        };
    }

    public static async void TryStartOpening(Level level)
    {
        if (Instance == null || level == null || Instance.Progress != StoryProgress.WokeUpLate)
            return;

        await Instance.ToSignal(Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        Npc mother = level.FindChildren("*", "", true, false)
            .OfType<Npc>()
            .FirstOrDefault(npc => npc.StoryRole == NpcStoryRole.Mother);
        mother?.StartAutomaticStoryTalk();
    }

    public static void AdvanceTo(StoryProgress next)
    {
        if (Instance == null || next <= Instance.Progress)
            return;

        StoryProgress previous = Instance.Progress;
        Instance.Progress = next;
        Instance.EmitSignal(SignalName.ProgressChanged, (int)previous, (int)next);
        Logger.Info($"Story progress: {previous} -> {next}");
    }

    private bool TalkToMother(Npc mother)
    {
        string[] reminder =
        {
            "You finally woke up! You slept far too long.",
            "You missed your meeting with Professor Pokebart. You should go to the other house right away!"
        };

        if (Progress == StoryProgress.WokeUpLate)
        {
            MessageManager.PlayText(reminder);
            AdvanceTo(StoryProgress.MotherExplainedMissedMeeting);
        }
        else
        {
            mother.StartScriptedAIConversation(
                reminder,
                "the player's mother, who is caring but worried because the player overslept and missed a meeting with Professor Pokebart");
        }

        return true;
    }

    private bool TalkToProfessor(Npc professor)
    {
        if (Progress < StoryProgress.ProfessorSuggestedFlowers)
        {
            MessageManager.PlayText(
                "Oh dear, you are late. Your rival already took the last starter Pokemon.",
                "I do not have any Pokemon left to give you.",
                "But perhaps a bouquet of fragrant flowers could attract a young wild Pokemon. Bring one to me and talk to me again.");
            AdvanceTo(StoryProgress.ProfessorSuggestedFlowers);
            return true;
        }

        if (Progress == StoryProgress.ProfessorSuggestedFlowers)
        {
            if (Inventory.GetItemCount("flower_bouquet") <= 0)
            {
                MessageManager.PlayText("Bring me a flower bouquet, and we may be able to attract a young Pokemon.");
                return true;
            }

            GiveFirstPokemon(professor);
            return true;
        }

        MessageManager.PlayText("Take good care of your new partner. Every great Trainer starts with trust.");
        return true;
    }

    private void GiveFirstPokemon(Npc professor)
    {
        TrainerPartyComponent party = GameManager.GetPlayer()?.GetNodeOrNull<TrainerPartyComponent>("TrainerParty");
        if (party == null || party.Party.IsFull)
        {
            MessageManager.PlayText("You cannot carry another Pokemon right now. Make room in your party first.");
            return;
        }

        int pokedexNumber = Globals.GetRandomNumberGenerator().Randf() < 0.5f ? 10 : 13;
        PokemonResource species = PokemonRegistry.GetById(pokedexNumber);
        if (species == null || !party.TryAddPokemon(species, 5))
        {
            MessageManager.PlayText("Something interrupted the experiment. Please try again.");
            return;
        }

        PokemonRegistry.MarkSeen(pokedexNumber);
        AdvanceTo(StoryProgress.FirstPokemonReceived);
        MessageManager.PlayText(
            "Professor Pokebart places the bouquet near the open window...",
            $"A wild {species.Name} is drawn by the flowers! It seems to like you.",
            $"You received {species.Name} at level 5 as your first Pokemon!");
    }
}
