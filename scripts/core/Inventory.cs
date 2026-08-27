using System.Collections.Generic;
using Godot;

namespace Game.Core;

public partial class Inventory : Node
{
    public static Inventory Instance { get; private set; }

    private readonly Dictionary<string, int> items = new();

    public override void _Ready()
    {
        Instance = this;
    }

    public static void AddItem(string itemId, string itemName, int quantity = 1)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            return;

        Instance.items[itemId] = GetItemCount(itemId) + quantity;
        Signals.EmitGlobalSignal(
            Signals.SignalName.ItemReceived,
            itemId,
            string.IsNullOrWhiteSpace(itemName) ? itemId : itemName,
            quantity);
    }

    public static int GetItemCount(string itemId)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(itemId))
            return 0;

        return Instance.items.TryGetValue(itemId, out int count) ? count : 0;
    }
}
