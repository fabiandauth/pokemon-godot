using System.Linq;
using Game.Core;
using Game.Gameplay;
using Godot;

namespace Game.UI;

public partial class GameMenu : CanvasLayer
{
    private enum MenuPage { Main, Items, Pokedex, PokemonDetail, Save }

    [Export] public Control Root;
    [Export] public Control MainPanel;
    [Export] public Control ItemsPanel;
    [Export] public Control PokedexPanel;
    [Export] public Control PokemonDetailPanel;
    [Export] public Control SavePanel;
    [Export] public Button ItemsButton;
    [Export] public Button PokedexButton;
    [Export] public Button SaveButton;
    [Export] public Button CloseButton;
    [Export] public OptionButton CategoryFilter;
    [Export] public ItemList ItemList;
    [Export] public Label ItemDescription;
    [Export] public Label StatusLabel;
    [Export] public Button UseButton;
    [Export] public ItemList PokedexList;
    [Export] public Label PokedexHint;
    [Export] public TextureRect PokemonSprite;
    [Export] public Label PokemonName;
    [Export] public Label PokemonTypes;
    [Export] public Label PokemonStats;
    [Export] public Label PokemonMoves;
    [Export] public Label PokemonDescription;

    private MenuPage currentPage = MenuPage.Main;
    private bool wasPaused;
    private Button[] mainButtons;
    private string[] mainButtonLabels = { "ITEMS", "POKÉDEX", "SAVE", "CLOSE" };
    private int mainSelection;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Root.Visible = false;
        CategoryFilter.AddItem("All items");
        CategoryFilter.AddItem("General");
        CategoryFilter.AddItem("Status items");
        CategoryFilter.AddItem("Field actions");
        CategoryFilter.AddItem("TMs");
        CategoryFilter.ItemSelected += _ => RefreshItems();
        ItemList.ItemSelected += ShowSelectedItem;
        UseButton.Pressed += UseSelectedItem;
        PokedexList.ItemSelected += PreviewPokemon;
        PokedexList.ItemActivated += index =>
        {
            Logger.Info(new object[] { "GameMenu: Pokédex item activated by GUI:", index });
            OpenPokemonDetail(index);
        };
        Signals.Instance.InventoryChanged += RefreshItems;
        mainButtons = new[] { ItemsButton, PokedexButton, SaveButton, CloseButton };
        for (int index = 0; index < mainButtons.Length; index++)
        {
            int selectedIndex = index;
            mainButtons[index].FocusMode = Control.FocusModeEnum.None;
            mainButtons[index].MouseEntered += () => SelectMainEntry(selectedIndex);
            mainButtons[index].Pressed += () => ActivateMainEntry(selectedIndex, "mouse/button");
        }
        ItemList.FocusMode = Control.FocusModeEnum.None;
        PokedexList.FocusMode = Control.FocusModeEnum.None;
        ShowPage(MenuPage.Main);
        Logger.Info("GameMenu: ready; global keyboard polling enabled");
    }

    public override void _Process(double delta)
    {
        if (!Root.Visible)
        {
            if (Input.IsActionJustPressed("menu") && !MessageManager.IsReading())
            {
                Logger.Info("GameMenu input: menu/open");
                Open();
            }
            return;
        }

        if (Input.IsActionJustPressed("menu") || Input.IsActionJustPressed("ui_cancel"))
        {
            Logger.Info(new object[] { "GameMenu input: back from page", currentPage });
            Back();
            return;
        }

        if (Input.IsActionJustPressed("ui_up"))
        {
            Logger.Info(new object[] { "GameMenu input: up on page", currentPage });
            MoveSelection(-1);
        }
        else if (Input.IsActionJustPressed("ui_down"))
        {
            Logger.Info(new object[] { "GameMenu input: down on page", currentPage });
            MoveSelection(1);
        }
        else if (Input.IsActionJustPressed("ui_accept") || Input.IsActionJustPressed("use"))
        {
            Logger.Info(new object[] { "GameMenu input: accept on page", currentPage });
            ActivateSelection();
        }
    }

    private void MoveSelection(int direction)
    {
        if (currentPage == MenuPage.Main)
        {
            SelectMainEntry((mainSelection + direction + mainButtons.Length) % mainButtons.Length);
            return;
        }

        ItemList list = currentPage == MenuPage.Pokedex ? PokedexList
            : currentPage == MenuPage.Items ? ItemList : null;
        if (list == null || list.ItemCount == 0)
            return;

        int[] selected = list.GetSelectedItems();
        int current = selected.Length == 0 ? 0 : selected[0];
        int next = (current + direction + list.ItemCount) % list.ItemCount;
        list.Select(next);
        list.EnsureCurrentIsVisible();
        if (currentPage == MenuPage.Pokedex) PreviewPokemon(next);
        else ShowSelectedItem(next);
        Logger.Info(new object[] { "GameMenu selection:", currentPage, next });
    }

    private void ActivateSelection()
    {
        if (currentPage == MenuPage.Main)
        {
            ActivateMainEntry(mainSelection, "keyboard");
            return;
        }

        int[] selected = currentPage == MenuPage.Pokedex ? PokedexList.GetSelectedItems()
            : currentPage == MenuPage.Items ? ItemList.GetSelectedItems() : System.Array.Empty<int>();
        if (selected.Length == 0)
            return;
        if (currentPage == MenuPage.Pokedex) OpenPokemonDetail(selected[0]);
        else if (currentPage == MenuPage.Items) UseSelectedItem();
    }

    private void ActivateMainEntry(int index, string source)
    {
        SelectMainEntry(index);
        Logger.Info(new object[] { "GameMenu activate:", mainButtonLabels[index], "source:", source });
        switch (index)
        {
            case 0: ShowItems(); break;
            case 1: ShowPokedex(); break;
            case 2: ShowSave(); break;
            case 3: Close(); break;
        }
    }

    public void Open()
    {
        Logger.Info("GameMenu: opening");
        wasPaused = GetTree().Paused;
        Root.Visible = true;
        GetTree().Paused = true;
        ShowPage(MenuPage.Main);
    }

    public void Close()
    {
        Logger.Info("GameMenu: closing");
        Root.Visible = false;
        GetTree().Paused = wasPaused;
    }

    public void ShowItems()
    {
        ShowPage(MenuPage.Items);
        RefreshItems();
    }

    public void ShowPokedex()
    {
        ShowPage(MenuPage.Pokedex);
        RefreshPokedex();
    }

    public void ShowSave() => ShowPage(MenuPage.Save);

    private void Back()
    {
        if (currentPage == MenuPage.PokemonDetail)
        {
            ShowPage(MenuPage.Pokedex);
        }
        else if (currentPage != MenuPage.Main) ShowPage(MenuPage.Main);
        else Close();
    }

    private void ShowPage(MenuPage page)
    {
        Logger.Info(new object[] { "GameMenu page:", page });
        currentPage = page;
        MainPanel.Visible = page == MenuPage.Main;
        ItemsPanel.Visible = page == MenuPage.Items;
        PokedexPanel.Visible = page == MenuPage.Pokedex;
        PokemonDetailPanel.Visible = page == MenuPage.PokemonDetail;
        SavePanel.Visible = page == MenuPage.Save;
        StatusLabel.Text = string.Empty;
        if (page == MenuPage.Main) SelectMainEntry(0);
    }

    private void SelectMainEntry(int index)
    {
        if (mainButtons == null || mainButtons.Length == 0)
            return;

        mainSelection = Mathf.Clamp(index, 0, mainButtons.Length - 1);
        for (int buttonIndex = 0; buttonIndex < mainButtons.Length; buttonIndex++)
            mainButtons[buttonIndex].Text = buttonIndex == mainSelection
                ? $"▶  {mainButtonLabels[buttonIndex]}"
                : $"   {mainButtonLabels[buttonIndex]}";
        Logger.Info(new object[] { "GameMenu main selection:", mainSelection, mainButtonLabels[mainSelection] });
    }

    private void RefreshPokedex()
    {
        PokedexList.Clear();
        foreach (PokemonResource pokemon in PokemonRegistry.GetVisiblePokemon())
        {
            int index = PokedexList.AddItem($"#{pokemon.Id:000}  {pokemon.Name}", pokemon.FrontSprite);
            PokedexList.SetItemMetadata(index, pokemon.ResourcePath);
        }
        PokedexHint.Text = PokedexList.ItemCount == 0
            ? "No Pokémon have been seen yet."
            : $"Seen Pokémon: {PokedexList.ItemCount}   •   Enter: details   •   Esc: back";
        if (PokedexList.ItemCount > 0)
        {
            PokedexList.Select(0);
            PreviewPokemon(0);
        }
    }

    private void PreviewPokemon(long index)
    {
        PokemonResource pokemon = PokemonAt((int)index);
        if (pokemon != null) PokedexHint.Text = $"#{pokemon.Id:000} {pokemon.Name}   •   Enter: details   •   Esc: back";
    }

    private void OpenPokemonDetail(long index)
    {
        PokemonResource pokemon = PokemonAt((int)index);
        if (pokemon == null) return;
        PokemonSprite.Texture = pokemon.FrontSprite;
        PokemonName.Text = $"#{pokemon.Id:000}  {pokemon.Name}";
        PokemonTypes.Text = $"Type: {pokemon.TypeOne}" + (pokemon.TypeTwo == PokemonType.None ? "" : $" / {pokemon.TypeTwo}");
        PokemonStats.Text = $"HP {pokemon.BaseHp}   ATK {pokemon.BaseAttack}   DEF {pokemon.BaseDefense}\n" +
            $"SP.ATK {pokemon.BaseSpecialAttack}   SP.DEF {pokemon.BaseSpecialDefense}   SPD {pokemon.BaseSpeed}";
        PokemonMoves.Text = "Level-up moves\n" + string.Join("\n", pokemon.LevelUpMoves
            .OrderBy(move => move.Value).Select(move => $"Lv. {move.Value}: {move.Key}"));
        PokemonDescription.Text = pokemon.Description;
        ShowPage(MenuPage.PokemonDetail);
    }

    private PokemonResource PokemonAt(int index) => index < 0 || index >= PokedexList.ItemCount
        ? null
        : GD.Load<PokemonResource>(PokedexList.GetItemMetadata(index).AsString());

    private void RefreshItems()
    {
        ItemList.Clear();
        ItemDescription.Text = "Select an item to see its description.";
        UseButton.Disabled = true;
        ItemCategory? category = CategoryFilter.Selected switch
        {
            1 => ItemCategory.General, 2 => ItemCategory.Status,
            3 => ItemCategory.FieldAction, 4 => ItemCategory.TechnicalMachine, _ => null
        };
        foreach (Inventory.ItemStack stack in Inventory.GetItems(category))
        {
            int index = ItemList.AddItem($"{stack.Item.DisplayName}  x{stack.Quantity}", stack.Item.Icon);
            ItemList.SetItemMetadata(index, stack.Item.Id);
        }
        if (ItemList.ItemCount == 0) ItemDescription.Text = "There are no items in this category.";
    }

    private void ShowSelectedItem(long index)
    {
        string itemId = ItemList.GetItemMetadata((int)index).AsString();
        Inventory.ItemStack stack = Inventory.GetItems().FirstOrDefault(entry => entry.Item.Id == itemId);
        if (stack == null) return;
        ItemDescription.Text = string.IsNullOrWhiteSpace(stack.Item.Description) ? "No description available." : stack.Item.Description;
        UseButton.Disabled = false;
    }

    private void UseSelectedItem()
    {
        int[] selected = ItemList.GetSelectedItems();
        if (selected.Length == 0) return;
        Inventory.UseItem(ItemList.GetItemMetadata(selected[0]).AsString(), GameManager.GetPlayer());
        StatusLabel.Text = "Item effects are not implemented yet.";
    }
}
