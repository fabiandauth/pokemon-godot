using Game.Core;
using Godot;

namespace Game.UI;

public partial class DeathScreen : CanvasLayer
{
    public static DeathScreen Instance { get; private set; }

    [Export]
    public Control Root;

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public double DisplaySeconds = 1.5;

    private bool isShowing;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Root.Visible = false;
    }

    public static async void ShowAndRespawn()
    {
        if (Instance == null || Instance.isShowing)
            return;

        Instance.isShowing = true;
        if (MessageManager.IsReading())
            MessageManager.CloseConversation();
        Instance.Root.Visible = true;
        Instance.GetTree().Paused = true;
        await Instance.ToSignal(
            Instance.GetTree().CreateTimer(Instance.DisplaySeconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
        Instance.GetTree().Paused = false;
        await SceneManager.RespawnAtLastSpawnPoint();
        Instance.Root.Visible = false;
        Instance.isShowing = false;
    }
}
