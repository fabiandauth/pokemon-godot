using System;
using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Godot;

namespace Game.Core;

public partial class MoveRegistry : Node
{
    public static MoveRegistry Instance { get; private set; }

    private readonly Dictionary<string, MoveResource> moves = new(StringComparer.InvariantCultureIgnoreCase);

    public override void _Ready()
    {
        Instance = this;
        foreach (string file in DirAccess.GetFilesAt("res://resources/moves").OrderBy(path => path))
        {
            if (!file.EndsWith(".tres")) continue;
            var resource = GD.Load<MoveResource>($"res://resources/moves/{file}");
            if (resource == null) continue;
            AddName(resource.Name, resource);
            foreach (string alias in resource.Aliases)
                AddName(alias, resource);
        }
    }

    public static MoveResource GetByName(string name) =>
        Instance != null && !string.IsNullOrWhiteSpace(name) && Instance.moves.TryGetValue(name, out var move)
            ? move
            : null;

    private void AddName(string name, MoveResource move)
    {
        if (!string.IsNullOrWhiteSpace(name))
            moves.TryAdd(name, move);
    }
}
