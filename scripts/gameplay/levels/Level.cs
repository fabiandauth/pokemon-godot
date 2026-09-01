using System.Collections.Generic;
using Game.Core;
using Godot;
using Godot.Collections;

namespace Game.Gameplay;

public partial class Level : Node2D
{
	[ExportCategory("Level Basics")]
	[Export]
	public LevelName LevelName;

	[ExportCategory("Wild Pokemon")]
	[Export(PropertyHint.Range, "0,100,0.1")]
	public float TallGrassEncounterRate = 10f;

	[Export(PropertyHint.Range, "0,100,0.1")]
	public float CaveEncounterRate;

	[Export]
	public Array<WildPokemonEncounter> WildPokemon = [];

	[ExportCategory("Camera Limits")]
	[Export]
	public int Top;

	[Export]
	public int Bottom;

	[Export]
	public int Left;

	[Export]
	public int Right;

	[ExportCategory("Debugging")]
	[Export]
	public bool DebugLayerOn = false;

	private readonly HashSet<Vector2> reserverdTiles = [];

	public AStarGrid2D Grid;
	public Vector2 TargetPosition = Vector2.Zero;
	public Array<Vector2> CurrentPatrolPoints = [];

	public override void _Ready()
	{
		Logger.Info($"Loading level {LevelName} ...");

		GetNode<LevelDebugger>("DebugLayer").DebugOn = DebugLayerOn;
	}

	public override void _Process(double delta)
	{
		if (Grid == null && GameManager.GetPlayer() != null)
		{
			SetupGrid();
		}
	}

	public void SetupGrid()
	{
		Logger.Info("Setting up A* Grid ...");

		Grid = new()
		{
			Region = new Rect2I(0, 0, Right, Bottom),
			CellSize = new Vector2(Globals.GRID_SIZE, Globals.GRID_SIZE),
			DefaultComputeHeuristic = AStarGrid2D.Heuristic.Manhattan,
			DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Manhattan,
			DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never
		};

		Grid.Update();

		var mapHeight = Bottom / Globals.GRID_SIZE;
		var mapWidth = Right / Globals.GRID_SIZE;

		for (int y = 0; y < mapHeight; y++)
		{
			for (int x = 0; x < mapWidth; x++)
			{
				Vector2I cell = new(x, y);
				Vector2 worldPosition = new(x * Globals.GRID_SIZE, y * Globals.GRID_SIZE);

				var (_, collisions) = GameManager.GetPlayer().GetNode<CharacterMovement>("Movement").GetTargetColliders(worldPosition);

				foreach (var collision in collisions)
				{
					var collider = (Node)(GodotObject)collision["collider"];
					var colliderType = collider.GetType().Name;

					if (colliderType == "TallGrass" || colliderType == "Player")
					{
						continue;
					}

					if (colliderType == "Npc")
					{
						switch (((Npc)collider).NpcInputConfig.NpcMovementType)
						{
							case NpcMovementType.Patrol:
								continue;
							case NpcMovementType.Wander:
								continue;
						}
					}

					Grid.SetPointSolid(cell, true);
				}
			}
		}
	}

	public bool ReserveTile(Vector2 position)
	{
		if (reserverdTiles.Contains(position))
			return false;

		reserverdTiles.Add(position);
		return true;
	}

	public bool IsTileFree(Vector2 position)
	{
		return !reserverdTiles.Contains(position);
	}

	public void ReleaseTile(Vector2 position)
	{
		reserverdTiles.Remove(position);
	}

	public PokemonInstance TryCreateWildPokemon(EncounterTerrain terrain)
	{
		float encounterRate = terrain == EncounterTerrain.TallGrass
			? TallGrassEncounterRate
			: CaveEncounterRate;
		RandomNumberGenerator random = Globals.GetRandomNumberGenerator();

		if (encounterRate <= 0f || WildPokemon.Count == 0 || random.RandfRange(0f, 100f) >= encounterRate)
			return null;

		float totalWeight = 0f;
		foreach (WildPokemonEncounter encounter in WildPokemon)
		{
			if (encounter?.Species != null && encounter.SpawnWeight > 0f)
				totalWeight += encounter.SpawnWeight;
		}

		if (totalWeight <= 0f)
			return null;

		float roll = random.RandfRange(0f, totalWeight);
		foreach (WildPokemonEncounter encounter in WildPokemon)
		{
			if (encounter?.Species == null || encounter.SpawnWeight <= 0f)
				continue;

			roll -= encounter.SpawnWeight;
			if (roll <= 0f)
				return encounter.Create(random);
		}

		return null;
	}
}
