using System;
using System.Linq;
using Game.Core;
using Game.Gameplay;
using Game.Gameplay.Items;
using Godot;

namespace Game.UI;

public partial class BattleMenu : CanvasLayer
{
    private enum BattlePage { Commands, Moves, Party, Items }

    public static BattleMenu Instance { get; private set; }
    public static bool IsOpen => Instance?.Root?.Visible == true;

    [Export] public Control Root;
    [Export] public Control CommandPanel;
    [Export] public Control SelectionPanel;
    [Export] public Button FightButton;
    [Export] public Button PokemonButton;
    [Export] public Button ItemButton;
    [Export] public Button RunButton;
    [Export] public Button BackButton;
    [Export] public ItemList SelectionList;
    [Export] public Label SelectionTitle;
    [Export] public Label BattleText;
    [Export] public Label PlayerName;
    [Export] public Label WildName;
    [Export] public ProgressBar PlayerHp;
    [Export] public ProgressBar WildHp;
    [Export] public TextureRect PlayerSprite;
    [Export] public TextureRect WildSprite;

    private BattlePage page;
    private TrainerParty party;
    private PokemonInstance activePokemon;
    private PokemonInstance wildPokemon;
    private Action<string> battleEnded;
    private bool wasPaused;
    private int commandSelection;
    private Button[] commandButtons;
    private readonly string[] commandLabels = ["FIGHT", "POKÉMON", "ITEM", "RUN"];

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Root.Visible = false;
        FightButton.Pressed += ShowMoves;
        PokemonButton.Pressed += ShowParty;
        ItemButton.Pressed += ShowItems;
        RunButton.Pressed += TryRun;
        BackButton.Pressed += ShowCommands;
        SelectionList.ItemActivated += ActivateSelection;
        commandButtons = [FightButton, PokemonButton, ItemButton, RunButton];
        foreach (Button button in commandButtons)
            button.FocusMode = Control.FocusModeEnum.None;
        SelectionList.FocusMode = Control.FocusModeEnum.None;
        BackButton.FocusMode = Control.FocusModeEnum.None;
    }

    public override void _Process(double delta)
    {
        if (!IsOpen)
            return;

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            TryRun();
            return;
        }

        if (page == BattlePage.Commands)
        {
            if (Input.IsActionJustPressed("ui_left") || Input.IsActionJustPressed("ui_right"))
                SelectCommand(commandSelection ^ 1);
            else if (Input.IsActionJustPressed("ui_up") || Input.IsActionJustPressed("ui_down"))
                SelectCommand((commandSelection + 2) % 4);
            else if (Input.IsActionJustPressed("ui_accept") || Input.IsActionJustPressed("use"))
                ActivateCommand();
            return;
        }

        if (Input.IsActionJustPressed("ui_up"))
            MoveListSelection(-1);
        else if (Input.IsActionJustPressed("ui_down"))
            MoveListSelection(1);
        else if (Input.IsActionJustPressed("ui_accept") || Input.IsActionJustPressed("use"))
        {
            int[] selected = SelectionList.GetSelectedItems();
            if (selected.Length > 0)
                ActivateSelection(selected[0]);
        }
    }

    public static bool StartBattle(PokemonInstance wild, Action<string> onEnded)
    {
        if (Instance == null || IsOpen || wild == null)
            return false;

        TrainerParty trainerParty = GameManager.GetPlayer()
            ?.GetNodeOrNull<TrainerPartyComponent>("TrainerParty")?.Party;
        PokemonInstance lead = trainerParty?.Pokemon.Count > 0 ? trainerParty.Pokemon[0] : null;
        if (lead?.IsFainted == true)
            lead = trainerParty.Pokemon.FirstOrDefault(pokemon => pokemon != null && !pokemon.IsFainted);
        if (lead == null)
        {
            onEnded?.Invoke(null);
            DeathScreen.ShowAndRespawn();
            return false;
        }

        Instance.party = trainerParty;
        Instance.activePokemon = lead;
        Instance.wildPokemon = wild;
        Instance.battleEnded = onEnded;
        Instance.wasPaused = Instance.GetTree().Paused;
        Instance.GetTree().Paused = true;
        Instance.Root.Visible = true;
        Instance.BattleText.Text = $"What will {lead.DisplayName} do?";
        Instance.RefreshCombatants();
        Instance.ShowCommands();
        return true;
    }

    private void ShowCommands()
    {
        page = BattlePage.Commands;
        CommandPanel.Visible = true;
        SelectionPanel.Visible = false;
        string prompt = $"What will {activePokemon.DisplayName} do?";
        if (!BattleText.Text.EndsWith(prompt, StringComparison.Ordinal))
            BattleText.Text += $"\n{prompt}";
        SelectCommand(0);
    }

    private void ShowMoves()
    {
        page = BattlePage.Moves;
        ShowSelection("Choose a move");
        foreach (PokemonMoveSlot move in activePokemon.Moves)
        {
            int index = SelectionList.AddItem($"{move.MoveName}    PP {move.CurrentPp}/{move.MaxPp}");
            SelectionList.SetItemMetadata(index, activePokemon.Moves.IndexOf(move));
            SelectionList.SetItemDisabled(index, move.CurrentPp <= 0);
        }
        SetFirstSelectionOrMessage("This Pokémon has no moves it can use.");
    }

    private void ShowParty()
    {
        page = BattlePage.Party;
        ShowSelection("Choose a Pokémon");
        for (int partyIndex = 0; partyIndex < party.Pokemon.Count; partyIndex++)
        {
            PokemonInstance pokemon = party.Pokemon[partyIndex];
            int index = SelectionList.AddItem(
                $"{pokemon.DisplayName}  Lv. {pokemon.Level}    HP {pokemon.CurrentHp}/{pokemon.Stats.Hp}",
                pokemon.Species?.MenuIconSprite ?? pokemon.Species?.FrontSprite);
            SelectionList.SetItemMetadata(index, partyIndex);
            SelectionList.SetItemDisabled(index, pokemon == activePokemon || pokemon.IsFainted);
        }
        SetFirstSelectionOrMessage("There is no other Pokémon available to switch to.");
    }

    private void ShowItems()
    {
        page = BattlePage.Items;
        ShowSelection("Choose an item");
        foreach (Inventory.ItemStack stack in Inventory.GetItems())
        {
            bool usable = stack.Item is CaptureItemDefinition or StatusItemDefinition;
            int index = SelectionList.AddItem($"{stack.Item.DisplayName}    x{stack.Quantity}", stack.Item.Icon);
            SelectionList.SetItemMetadata(index, stack.Item.Id);
            SelectionList.SetItemDisabled(index, !usable);
        }
        SetFirstSelectionOrMessage("There are no battle items in the bag.");
    }

    private void ShowSelection(string title)
    {
        CommandPanel.Visible = false;
        SelectionPanel.Visible = true;
        SelectionTitle.Text = title;
        SelectionList.Clear();
    }

    private void SetFirstSelectionOrMessage(string emptyMessage)
    {
        for (int index = 0; index < SelectionList.ItemCount; index++)
        {
            if (SelectionList.IsItemDisabled(index))
                continue;
            SelectionList.Select(index);
            return;
        }
        BattleText.Text = emptyMessage;
    }

    private void ActivateSelection(long listIndex)
    {
        if (listIndex < 0 || listIndex >= SelectionList.ItemCount || SelectionList.IsItemDisabled((int)listIndex))
            return;

        switch (page)
        {
            case BattlePage.Moves:
                UseMove(SelectionList.GetItemMetadata((int)listIndex).AsInt32());
                break;
            case BattlePage.Party:
                SwitchPokemon(SelectionList.GetItemMetadata((int)listIndex).AsInt32());
                break;
            case BattlePage.Items:
                UseItem(SelectionList.GetItemMetadata((int)listIndex).AsString());
                break;
        }
    }

    private void UseMove(int moveIndex)
    {
        if (moveIndex < 0 || moveIndex >= activePokemon.Moves.Count)
            return;
        PokemonMoveSlot move = activePokemon.Moves[moveIndex];
        if (move.CurrentPp <= 0)
            return;

        move.CurrentPp--;
        int damage = CalculateDamage(activePokemon, wildPokemon, move);
        wildPokemon.CurrentHp = Mathf.Max(0, wildPokemon.CurrentHp - damage);
        BattleText.Text = $"{activePokemon.DisplayName} used {move.MoveName}! It dealt {damage} damage.";
        RefreshCombatants();
        if (wildPokemon.IsFainted)
        {
            EndBattle($"The wild {wildPokemon.DisplayName} fainted!");
            return;
        }
        WildTurn();
    }

    private void SwitchPokemon(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= party.Pokemon.Count)
            return;
        PokemonInstance replacement = party.Pokemon[partyIndex];
        if (replacement == activePokemon || replacement.IsFainted)
            return;
        activePokemon = replacement;
        BattleText.Text = $"Go, {activePokemon.DisplayName}!";
        RefreshCombatants();
        WildTurn();
    }

    private void UseItem(string itemId)
    {
        Inventory.ItemStack stack = Inventory.GetItems().FirstOrDefault(entry => entry.Item.Id == itemId);
        if (stack?.Item is CaptureItemDefinition captureItem)
        {
            if (party.IsFull)
            {
                BattleText.Text = "Your party is full. You cannot catch another Pokémon.";
                ShowCommands();
                return;
            }
            if (!Inventory.TryConsumeItem(itemId, out _))
                return;
            float missingHp = 1f - (float)wildPokemon.CurrentHp / Mathf.Max(1, wildPokemon.Stats.Hp);
            float catchChance = Mathf.Clamp((0.35f + missingHp * 0.6f) * captureItem.CatchRateMultiplier, 0.05f, 0.95f);
            if (Globals.GetRandomNumberGenerator().Randf() <= catchChance)
            {
                party.TryAdd(wildPokemon);
                PokemonRegistry.MarkSeen(wildPokemon.Species.Id);
                EndBattle($"Gotcha! {wildPokemon.DisplayName} was caught!");
                return;
            }
            BattleText.Text = $"Oh no! The wild {wildPokemon.DisplayName} broke free!";
            WildTurn();
            return;
        }

        if (stack?.Item is StatusItemDefinition statusItem && statusItem.HpChange > 0)
        {
            if (activePokemon.CurrentHp >= activePokemon.Stats.Hp)
            {
                BattleText.Text = $"{activePokemon.DisplayName} already has full HP.";
                ShowCommands();
                return;
            }
            if (!Inventory.TryConsumeItem(itemId, out _))
                return;
            int healed = Mathf.Min(statusItem.HpChange, activePokemon.Stats.Hp - activePokemon.CurrentHp);
            activePokemon.CurrentHp += healed;
            BattleText.Text = $"{activePokemon.DisplayName} recovered {healed} HP.";
            RefreshCombatants();
            WildTurn();
            return;
        }

        BattleText.Text = "That item cannot be used in battle.";
        ShowCommands();
    }

    private void TryRun()
    {
        if (!IsOpen)
            return;
        float chance = Mathf.Clamp(0.5f + (activePokemon.Stats.Speed - wildPokemon.Stats.Speed) / 100f, 0.1f, 0.95f);
        if (Globals.GetRandomNumberGenerator().Randf() <= chance)
        {
            EndBattle("You got away safely.");
            return;
        }
        BattleText.Text = "You could not escape!";
        WildTurn();
    }

    private void WildTurn()
    {
        PokemonMoveSlot move = wildPokemon.Moves.FirstOrDefault(slot => slot.CurrentPp > 0);
        string moveName = move?.MoveName ?? "Struggle";
        if (move != null)
            move.CurrentPp--;
        int damage = CalculateDamage(wildPokemon, activePokemon, move);
        activePokemon.CurrentHp = Mathf.Max(0, activePokemon.CurrentHp - damage);
        BattleText.Text += $"\nThe wild {wildPokemon.DisplayName} used {moveName} and dealt {damage} damage.";
        RefreshCombatants();

        if (activePokemon.IsFainted)
        {
            PokemonInstance replacement = party.Pokemon.FirstOrDefault(pokemon => pokemon != null && !pokemon.IsFainted);
            if (replacement == null)
            {
                EndBattle(null);
                DeathScreen.ShowAndRespawn();
                return;
            }
            activePokemon = replacement;
            BattleText.Text += $"\nGo, {activePokemon.DisplayName}!";
            RefreshCombatants();
        }
        ShowCommands();
    }

    private static int CalculateDamage(PokemonInstance attacker, PokemonInstance defender, PokemonMoveSlot move)
    {
        int power = move?.Move?.Power > 0 ? move.Move.Power : 40;
        bool special = move?.Move?.Category == MoveCategory.Special;
        int attack = special ? attacker.Stats.SpecialAttack : attacker.Stats.Attack;
        int defense = Mathf.Max(1, special ? defender.Stats.SpecialDefense : defender.Stats.Defense);
        float randomFactor = Globals.GetRandomNumberGenerator().RandfRange(0.85f, 1f);
        return Mathf.Max(1, Mathf.RoundToInt((((2f * attacker.Level / 5f + 2f) * power * attack / defense) / 50f + 2f) * randomFactor));
    }

    private void RefreshCombatants()
    {
        PlayerName.Text = $"{activePokemon.DisplayName}    Lv. {activePokemon.Level}    HP {activePokemon.CurrentHp}/{activePokemon.Stats.Hp}";
        WildName.Text = $"Wild {wildPokemon.DisplayName}    Lv. {wildPokemon.Level}    HP {wildPokemon.CurrentHp}/{wildPokemon.Stats.Hp}";
        PlayerHp.MaxValue = activePokemon.Stats.Hp;
        PlayerHp.Value = activePokemon.CurrentHp;
        WildHp.MaxValue = wildPokemon.Stats.Hp;
        WildHp.Value = wildPokemon.CurrentHp;
        PlayerSprite.Texture = activePokemon.Species?.BackSprite ?? activePokemon.Species?.FrontSprite;
        WildSprite.Texture = wildPokemon.Species?.FrontSprite;
    }

    private void EndBattle(string message)
    {
        Root.Visible = false;
        GetTree().Paused = wasPaused;
        Action<string> callback = battleEnded;
        battleEnded = null;
        party = null;
        activePokemon = null;
        wildPokemon = null;
        callback?.Invoke(message);
    }

    private void SelectCommand(int index)
    {
        commandSelection = Mathf.Clamp(index, 0, commandButtons.Length - 1);
        for (int buttonIndex = 0; buttonIndex < commandButtons.Length; buttonIndex++)
            commandButtons[buttonIndex].Text = buttonIndex == commandSelection
                ? $"▶  {commandLabels[buttonIndex]}"
                : $"   {commandLabels[buttonIndex]}";
    }

    private void ActivateCommand()
    {
        switch (commandSelection)
        {
            case 0: ShowMoves(); break;
            case 1: ShowParty(); break;
            case 2: ShowItems(); break;
            case 3: TryRun(); break;
        }
    }

    private void MoveListSelection(int direction)
    {
        if (SelectionList.ItemCount == 0)
            return;
        int[] selected = SelectionList.GetSelectedItems();
        int current = selected.Length > 0 ? selected[0] : 0;
        for (int offset = 1; offset <= SelectionList.ItemCount; offset++)
        {
            int next = (current + direction * offset + SelectionList.ItemCount) % SelectionList.ItemCount;
            if (SelectionList.IsItemDisabled(next))
                continue;
            SelectionList.Select(next);
            SelectionList.EnsureCurrentIsVisible();
            return;
        }
    }
}
