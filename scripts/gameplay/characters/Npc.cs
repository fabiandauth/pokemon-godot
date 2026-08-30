using Game.Core;
using Game.Core.AI;
using Game.UI;
using Game.Utilities;
using Godot;
using Godot.Collections;

namespace Game.Gameplay;

[Tool]
public partial class Npc : CharacterBody2D
{
    private NpcAppearance npcAppearance = NpcAppearance.Worker;
    
    // AI Conversation
    private OllamaAI.ConversationContext conversationContext;
    private int goalSatisfiedTurns;
    private bool rewardGiven;

    [ExportCategory("Traits")]
    [Export]
    public NpcAppearance NpcAppearance
    {
        get => npcAppearance;
        set
        {
            if (npcAppearance != value)
            {
                npcAppearance = value;
                UpdateAppearance();
            }
        }
    }

    private AnimatedSprite2D animatedSprite2D;
    private NpcInput npcInput;
    private StateMachine stateMachine;
    private CharacterMovement characterMovement;

    private readonly Dictionary<NpcAppearance, SpriteFrames> appearanceFrames = new()
    {
        { NpcAppearance.BugCatcher, GD.Load<SpriteFrames>("res://resources/spriteframes/bug_catcher.tres") },
        { NpcAppearance.Gardener, GD.Load<SpriteFrames>("res://resources/spriteframes/gardener.tres") },
        { NpcAppearance.Worker, GD.Load<SpriteFrames>("res://resources/spriteframes/worker.tres") }
    };

    [Export]
    public NpcInputConfig NpcInputConfig;

    [ExportCategory("Story")]
    [Export]
    public NpcStoryRole StoryRole = NpcStoryRole.None;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            UpdateAppearance();
            return;
        }

        npcInput ??= GetNode<NpcInput>("Input");
        npcInput.Config = NpcInputConfig;

        stateMachine ??= GetNode<StateMachine>("StateMachine");
        stateMachine.ChangeState("Roam");

        animatedSprite2D ??= GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        characterMovement ??= GetNode<CharacterMovement>("Movement");
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        var player = GameManager.GetPlayer();

        if (player != null)
        {
            ZIndex = (player.Position.Y <= Position.Y) ? 6 : 4;
        }
    }

    private void UpdateAppearance()
    {
        if (animatedSprite2D == null)
        {
            animatedSprite2D = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

            if (animatedSprite2D == null)
            {
                return;
            }
        }

        if (appearanceFrames.TryGetValue(npcAppearance, out var spriteFrames))
        {
            if (animatedSprite2D.SpriteFrames != spriteFrames)
            {
                Logger.Info($"Updating appearance for {Name} to {spriteFrames.ResourcePath}");
                animatedSprite2D.SpriteFrames = spriteFrames;
            }
        }
        else
        {
            animatedSprite2D.SpriteFrames = null;
        }
    }

    public void PlayMessage(Vector2 Direction)
    {
        if (Engine.IsEditorHint())
            return;

        Logger.Info(new object[] { Name, ": PlayMessage called with direction:", Direction });

        if (characterMovement.IsMoving())
        {
            Logger.Info(new object[] { Name, ": Character is moving, ignoring PlayMessage" });
            return;
        }

        if (npcInput.Direction != Direction * -1)
        {
            npcInput.Direction = Direction * -1;
            npcInput.EmitSignal(CharacterInput.SignalName.Turn);
        }

        Logger.Info(new object[] { Name, ": Changing to Message state" });
        stateMachine.ChangeState("Message");

        if (StoryManager.HandleNpcInteraction(this))
            return;
        
        // Every interaction is a new conversation. This reloads the complete
        // system prompt and NPC role for Ollama.
        Logger.Info(new object[] { Name, ": Starting with a fresh AI conversation context" });
        conversationContext = CreateConversationContext();
        goalSatisfiedTurns = 0;
        
        // Start AI conversation
        Logger.Info(new object[] { Name, ": Starting AI talk" });
        StartAITalk();
    }

    public void StartAutomaticStoryTalk()
    {
        Player player = GameManager.GetPlayer();
        if (player == null)
            return;

        Vector2 difference = GlobalPosition - player.GlobalPosition;
        Vector2 direction = Mathf.Abs(difference.X) >= Mathf.Abs(difference.Y)
            ? new Vector2(Mathf.Sign(difference.X), 0)
            : new Vector2(0, Mathf.Sign(difference.Y));
        PlayMessage(direction == Vector2.Zero ? Vector2.Up : direction);
    }

    public void StartScriptedAIConversation(string[] initialMessages, string aiRole)
    {
        conversationContext = OllamaAI.InitializeConversation(Name, aiRole);
        MessageManager.StartAIConversation(this, initialMessages);
    }
    
    /// <summary>
    /// Start conversation with AI
    /// </summary>
    public async void StartAITalk()
    {
        Logger.Info(new object[] { Name, ": StartAITalk - checking Ollama availability" });
        
        // Check if Ollama is available
        if (!OllamaAI.IsAvailable())
        {
            // Fallback to static messages
            Logger.Warning(new object[] { Name, ": Ollama not available, using fallback messages" });
            MessageManager.StartAIConversation(this, [.. NpcInputConfig.Messages]);
            TryHandOverItem(talkCompleted: true, goalSatisfied: false);
            return;
        }
        
        Logger.Info(new object[] { Name, ": StartAITalk - Ollama available, sending initial message" });
        
        // Get AI response
        var response = await OllamaAI.SendMessageAsync(
            conversationContext,
            "Hello there!"  // Initial greeting
        );
        
        Logger.Info(new object[] { Name, ": StartAITalk - AI response received, success:", response.Success, "message:", response.Message });
        
        if (response.Success && !string.IsNullOrEmpty(response.Message))
        {
            // Display AI response
            Logger.Info(new object[] { Name, ": StartAITalk - Starting AI conversation with response" });
            MessageManager.StartAIConversation(this, new[] { response.Message });
            TryHandOverItem(talkCompleted: true, goalSatisfied: false);
        }
        else
        {
            // Fallback to static messages
            Logger.Warning(new object[] { Name, ": StartAITalk - AI response failed, using fallback. Error:", response.Error });
            MessageManager.StartAIConversation(this, [.. NpcInputConfig.Messages]);
        }
    }
    
    /// <summary>
    /// Send user message to AI and get response
    /// </summary>
    public async void SendUserMessage(string userMessage)
    {
        Logger.Info(new object[] { Name, ": SendUserMessage - user said:", userMessage });
        
        if (conversationContext == null)
        {
            Logger.Info(new object[] { Name, ": SendUserMessage - Creating new conversation context" });
            conversationContext = CreateConversationContext();
        }
        
        if (!OllamaAI.IsAvailable())
        {
            Logger.Warning(new object[] { Name, ": SendUserMessage - Ollama not available, using contextual fallback" });
            MessageManager.AddAIResponse(this, "Sorry, I lost my train of thought. Could you say that again in a moment?");
            return;
        }
        
        Logger.Info(new object[] { Name, ": SendUserMessage - Sending to AI" });
        
        NpcItemRewardConfig reward = NpcInputConfig?.ItemReward;
        var evaluationTask = reward?.Mode == NpcItemHandoverMode.AfterGoalSatisfied
            ? OllamaAI.EvaluateGoalAsync(reward.InteractionGoal, userMessage)
            : System.Threading.Tasks.Task.FromResult(new OllamaAI.GoalEvaluation { Success = true });

        // Dialogue and goal evaluation are independent and can run concurrently.
        var responseTask = OllamaAI.SendMessageAsync(conversationContext, userMessage);
        var response = await responseTask;
        var evaluation = await evaluationTask;

        Logger.Info(new object[] { Name, ": SendUserMessage - AI response:", response.Message });
        
        if (response.Success && !string.IsNullOrEmpty(response.Message))
        {
            Logger.Info(new object[]
            {
                Name,
                ": interaction goal evaluation - success:", evaluation.Success,
                "satisfied:", evaluation.GoalSatisfied,
                "reason:", evaluation.Reason,
                "error:", evaluation.Error
            });

            // Display AI response - this will add it to the messages and display it
            Logger.Info(new object[] { Name, ": SendUserMessage - Adding AI response to message manager" });
            MessageManager.AddAIResponse(this, response.Message);
            TryHandOverItem(talkCompleted: true, evaluation.Success && evaluation.GoalSatisfied);
        }
        else
        {
            Logger.Warning(new object[] { Name, ": SendUserMessage - AI error:", response.Error });
            // End conversation on error
            MessageManager.EndAIConversation(this);
            stateMachine.ChangeState("Roam");
        }
    }

    private OllamaAI.ConversationContext CreateConversationContext()
    {
        return OllamaAI.InitializeConversation(
            Name,
            NpcAppearance.ToString());
    }

    private void TryHandOverItem(bool talkCompleted, bool goalSatisfied)
    {
        NpcItemRewardConfig reward = NpcInputConfig?.ItemReward;
        if (reward == null || rewardGiven || reward.Mode == NpcItemHandoverMode.None)
            return;

        bool shouldGiveItem = reward.Mode switch
        {
            NpcItemHandoverMode.AfterTalking => talkCompleted,
            NpcItemHandoverMode.AfterGoalSatisfied => RegisterGoalSatisfiedTurn(goalSatisfied, reward.RequiredGoalSatisfiedTurns),
            _ => false
        };

        if (!shouldGiveItem || reward.Item == null || string.IsNullOrWhiteSpace(reward.Item.Id))
            return;

        rewardGiven = true;
        int quantity = Mathf.Max(1, reward.Quantity);
        string itemName = string.IsNullOrWhiteSpace(reward.Item.DisplayName) ? reward.Item.Id : reward.Item.DisplayName;
        Inventory.AddItem(reward.Item, quantity);

        if (!string.IsNullOrWhiteSpace(reward.HandoverMessage))
            MessageManager.AddAIResponse(this, reward.HandoverMessage);
        MessageManager.AddSystemMessage($"You received {quantity} x {itemName}!");
    }

    private bool RegisterGoalSatisfiedTurn(bool goalSatisfied, int requiredTurns)
    {
        if (goalSatisfied)
            goalSatisfiedTurns++;

        int required = Mathf.Max(1, requiredTurns);
        bool requirementMet = goalSatisfiedTurns >= required;
        Logger.Info(new object[]
        {
            Name,
            ": interaction goal debug - satisfied:", goalSatisfied,
            "progress:", goalSatisfiedTurns, "/", required,
            "reward condition met:", requirementMet
        });
        return requirementMet;
    }
}
