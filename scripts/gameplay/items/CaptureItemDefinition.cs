using Godot;

namespace Game.Gameplay.Items;

[GlobalClass]
public partial class CaptureItemDefinition : ItemDefinition
{
    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float CatchRateMultiplier = 1f;
}
