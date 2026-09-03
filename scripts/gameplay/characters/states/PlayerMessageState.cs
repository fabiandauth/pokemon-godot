using Game.Core;
using Game.UI;
using Game.Utilities;
using Godot;
using System;

namespace Game.Gameplay;

public partial class PlayerMessageState : State
{
    public override void _Ready()
    {
        Logger.Info(new object[] { "PlayerMessageState: _Ready" });
        
        Signals.Instance.MessageBoxOpen += (value) =>
        {
            Logger.Info(new object[] { "PlayerMessageState: MessageBoxOpen signal received, value:", value });
            if (!value)
            {
                Logger.Info(new object[] { "PlayerMessageState: Changing to Roam state" });
                StateMachine.ChangeState("Roam");
            }
        };
    }

    public override void _Process(double delta)
    {
        // Only advance text with SPACE if we're NOT in AI conversation mode
        // In AI conversation mode, the InputField handles its own input
        if (!MessageManager.Scrolling() && Input.IsActionJustReleased("use"))
        {
            Logger.Info(new object[] { "PlayerMessageState: _Process - use action, scrolling:", MessageManager.Scrolling() });
            
            // Check if we're in AI conversation mode
            if (MessageManager.IsAIConversation())
            {
                Logger.Info(new object[] { "PlayerMessageState: In AI conversation, not advancing text with SPACE" });
                // Don't advance - let InputField handle the SPACE key for typing
                return;
            }

            if (MessageManager.IsAwaitingChoice())
                return;
            
            Logger.Info(new object[] { "PlayerMessageState: Advancing text with SPACE" });
            MessageManager.ScrollText();
        }
    }
}
