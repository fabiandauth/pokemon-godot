using Game.Core.AI;
using Game.Gameplay;
using Game.UI;
using Godot;

namespace Game.Core;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[ExportCategory("Nodes")]
	[Export]
	public SubViewport GameViewPort;

	[ExportCategory("Vars")]
	[Export]
	public Player Player;
	
	[Export]
	public bool AI_TestMode = false;

	public override void _Ready()
	{
		Instance = this;

		// Enable AI test mode if configured
		OllamaAI.TestMode = AI_TestMode;
		
		Logger.Info("Loading game manager ...");

		SceneManager.ChangeLevel(spawn: true);
	}

	public static SubViewport GetGameViewPort()
	{
		return Instance.GameViewPort;
	}

	public static Player AddPlayer(Player player)
	{
		Instance.GameViewPort.AddChild(player);
		Instance.Player = player;
		return Instance.Player;
	}

	public static Player GetPlayer()
	{
		return Instance.Player;
	}
}
