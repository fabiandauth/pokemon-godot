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
        PokedexList.ItemActivated += OpenPokemonDetail;
        Signals.Instance.InventoryChanged += RefreshItems;
        ItemsButton.Pressed += ShowItems;
        PokedexButton.Pressed += ShowPokedex;
        SaveButton.Pressed += ShowSave;
        CloseButton.Pressed += Close;
        ShowPage(MenuPage.Main);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("menu"))
        {
            if (!Root.Visible && !MessageManager.IsReading()) Open();
            else if (Root.Visible) Back();
            GetViewport().SetInputAsHandled();
        }
        else if (Root.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            Back();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open()
    {
        wasPaused = GetTree().Paused;
        Root.Visible = true;
        GetTree().Paused = true;
        ShowPage(MenuPage.Main);
    }

    public void Close()
    {
        Root.Visible = false;
        GetTree().Paused = wasPaused;
    }

    public void ShowItems()
    {
        ShowPage(MenuPage.Items);
        RefreshItems();
        (ItemList.ItemCount > 0 ? (Control)ItemList : CategoryFilter).GrabFocus();
    }

    public void ShowPokedex()
    {
        ShowPage(MenuPage.Pokedex);
        RefreshPokedex();
        PokedexList.GrabFocus();
    }

    public void ShowSave() => ShowPage(MenuPage.Save);

    private void Back()
    {
        if (currentPage == MenuPage.PokemonDetail)
        {
            ShowPage(MenuPage.Pokedex);
            PokedexList.GrabFocus();
        }
        else if (currentPage != MenuPage.Main) ShowPage(MenuPage.Main);
        else Close();
    }

    private void ShowPage(MenuPage page)
    {
        currentPage = page;
        MainPanel.Visible = page == MenuPage.Main;
        ItemsPanel.Visible = page == MenuPage.Items;
        PokedexPanel.Visible = page == MenuPage.Pokedex;
        PokemonDetailPanel.Visible = page == MenuPage.PokemonDetail;
        SavePanel.Visible = page == MenuPage.Save;
        StatusLabel.Text = string.Empty;
        if (page == MenuPage.Main) ItemsButton.GrabFocus();
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
