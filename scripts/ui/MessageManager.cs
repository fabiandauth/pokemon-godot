using Game.Core;
using Game.Gameplay;
using Game.Utilities;
using Godot;
using Godot.Collections;
using System.Threading.Tasks;

namespace Game.UI;

public partial class MessageManager : CanvasLayer
{
    public static MessageManager Instance { get; private set; }

    [ExportCategory("Components")]
    [Export]
    public NinePatchRect Box;

    [Export]
    public RichTextLabel Label;
    
    [Export]
    public LineEdit InputField;
    
    [Export]
    public Button SendButton;
    
    [Export]
    public Button CancelButton;

    [ExportCategory("Variables")]
    [Export]
    public bool IsScrolling = false;

    [Export]
    public int Delay = 15;

    [Export]
    public Array<string> Messages;
    
    // AI Conversation state
    private Npc currentNPC;
    private bool isAIConversation = false;
    private bool waitingForAI = false;

    public override void _Ready()
    {
        Instance = this;
        
        Logger.Info(new object[] { "MessageManager: _Ready called" });
        
        // Hide input field initially
        if (InputField != null)
        {
            InputField.Visible = false;
            Logger.Info(new object[] { "MessageManager: InputField found and hidden" });
        }
        else
        {
            Logger.Warning(new object[] { "MessageManager: InputField is NULL!" });
        }
        
        if (SendButton != null)
        {
            SendButton.Visible = false;
            Logger.Info(new object[] { "MessageManager: SendButton found and hidden" });
        }
        else
        {
            Logger.Warning(new object[] { "MessageManager: SendButton is NULL!" });
        }
        
        if (CancelButton != null)
        {
            CancelButton.Visible = false;
            Logger.Info(new object[] { "MessageManager: CancelButton found and hidden" });
        }
        else
        {
            Logger.Warning(new object[] { "MessageManager: CancelButton is NULL!" });
        }
        
        // Connect signals
        Logger.Info(new object[] { "MessageManager: Connecting input signals" });
        ConnectInputSignals();
    }

    public static void PlayText(params string[] payload)
    {
        if (IsReading()) return;
        if (payload.Length == 0) return;

        Signals.EmitGlobalSignal(Signals.SignalName.MessageBoxOpen, true);

        Instance.Messages = [.. payload];
        ScrollText();
    }

    public static async void ScrollText()
    {
        Logger.Info(new object[] { "MessageManager: ScrollText called. Messages count:", Instance.Messages.Count, "IsReading:", IsReading() });
        
        if (!IsReading())
        {
            Logger.Info(new object[] { "MessageManager: Box was not visible, making it visible" });
            Instance.Box.Visible = true;
        }

        if (Instance.Messages.Count == 0)
        {
            Logger.Warning(new object[] { "MessageManager: ScrollText - No messages to display" });
            Instance.Box.Visible = false;
            Signals.EmitGlobalSignal(Signals.SignalName.MessageBoxOpen, false);
            return;
        }

        Logger.Info(new object[] { "MessageManager: ScrollText - Displaying first message" });
        Instance.IsScrolling = true;
        Instance.Label.Text = "";

        string messageToDisplay = Instance.Messages[0];
        Logger.Info(new object[] { "MessageManager: ScrollText - Message content:", messageToDisplay });

        foreach (char letter in messageToDisplay)
        {
            Instance.Label.Text += letter;
            await Task.Delay(Instance.Delay);
        }

        Instance.Messages.RemoveAt(0);
        Logger.Info(new object[] { "MessageManager: ScrollText - Removed first message. Remaining:", Instance.Messages.Count });
        Instance.IsScrolling = false;
        
        // If there are more messages, recursively call ScrollText
        if (Instance.Messages.Count > 0)
        {
            Logger.Info(new object[] { "MessageManager: ScrollText - More messages to display, calling ScrollText again" });
            ScrollText();
        }
        else
        {
            // If we're in AI conversation and no more messages, re-enable input
            if (Instance.isAIConversation && !Instance.waitingForAI && Instance.InputField != null)
            {
                Logger.Info(new object[] { "MessageManager: ScrollText - All messages displayed, re-enabling input" });
                Instance.InputField.GrabFocus();
            }
        }
    }

    public static bool IsReading()
    {
        return Instance.Box.Visible;
    }

    public static bool Scrolling()
    {
        return Instance.IsScrolling;
    }
    
    /// <summary>
    /// Check if currently in AI conversation mode
    /// </summary>
    public static bool IsAIConversation()
    {
        return Instance.isAIConversation;
    }

    public static Array<string> GetMessages()
    {
        return Instance.Messages;
    }
    
    /// <summary>
    /// Start an AI conversation with an NPC
    /// </summary>
    public static void StartAIConversation(Npc npc, string[] initialMessages)
    {
        Logger.Info(new object[] { "MessageManager: StartAIConversation called for NPC:", npc?.Name });
        
        if (IsReading())
        {
            Logger.Info(new object[] { "MessageManager: Already reading, returning" });
            return;
        }
        
        Instance.currentNPC = npc;
        Instance.isAIConversation = true;
        
        Logger.Info(new object[] { "MessageManager: Setting currentNPC to:", npc?.Name });
        
        Signals.EmitGlobalSignal(Signals.SignalName.MessageBoxOpen, true);
        
        // Format messages with NPC name prefix
        var formattedMessages = new Array<string>();
        string npcName = npc.Name.ToString();
        foreach (var msg in initialMessages)
        {
            // If message already has NPC name, just format it
            // Otherwise add NPC name prefix
            if (msg.StartsWith(npcName + ":"))
            {
                formattedMessages.Add($"{npcName}: {msg.Substring(npcName.Length + 2)}");
            }
            else
            {
                formattedMessages.Add($"{npcName}: {msg}");
            }
        }
        Instance.Messages = [.. formattedMessages];
        Logger.Info(new object[] { "MessageManager: Set formatted messages:", Instance.Messages });
        
        // Show input field for AI conversation
        if (Instance.InputField != null)
        {
            Instance.InputField.Visible = true;
            Logger.Info(new object[] { "MessageManager: InputField shown" });
        }
        
        if (Instance.SendButton != null)
        {
            Instance.SendButton.Visible = true;
            Logger.Info(new object[] { "MessageManager: SendButton shown" });
        }
        
        if (Instance.CancelButton != null)
        {
            Instance.CancelButton.Visible = true;
            Logger.Info(new object[] { "MessageManager: CancelButton shown" });
        }
        
        // Focus input field
        if (Instance.InputField != null)
        {
            Instance.InputField.GrabFocus();
            Logger.Info(new object[] { "MessageManager: InputField focused" });
        }
            
        Logger.Info(new object[] { "MessageManager: Calling ScrollText" });
        ScrollText();
    }
    
    /// <summary>
    /// Add AI response to conversation
    /// </summary>
    public static void AddAIResponse(Npc npc, string response)
    {
        Logger.Info(new object[] { "MessageManager: AddAIResponse called for NPC:", npc?.Name, "response:", response });
        
        if (!IsReading())
        {
            Logger.Info(new object[] { "MessageManager: Not reading, starting new conversation" });
            // Conversation ended, start new one
            StartAIConversation(npc, new[] { response });
            return;
        }
        
        // Format AI response with NPC name prefix
        string npcName = npc.Name.ToString();
        string formattedResponse;
        if (response.StartsWith(npcName + ":"))
        {
            formattedResponse = $"{npcName}: {response.Substring(npcName.Length + 2)}";
        }
        else
        {
            formattedResponse = $"{npcName}: {response}";
        }
        
        // Add response to messages
        Instance.Messages.Add(formattedResponse);
        Instance.waitingForAI = false;
        Instance.SetComposerEnabled(true);
        Logger.Info(new object[] { "MessageManager: Added formatted response to messages. Total messages:", Instance.Messages.Count });
        
        // A running typewriter loop will pick this up from the queue.
        if (!Instance.IsScrolling)
            ScrollText();
    }

    public static void AddSystemMessage(string message)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(message))
            return;

        Instance.Messages.Add(message);
        if (!Instance.IsScrolling)
            ScrollText();
    }
    
    /// <summary>
    /// End AI conversation
    /// </summary>
    public static void EndAIConversation(Npc npc)
    {
        Logger.Info(new object[] { "MessageManager: EndAIConversation called for NPC:", npc?.Name });
        
        Instance.isAIConversation = false;
        Instance.waitingForAI = false;
        Instance.currentNPC = null;
        
        // Hide input field
        if (Instance.InputField != null)
            Instance.InputField.Visible = false;
        
        if (Instance.SendButton != null)
            Instance.SendButton.Visible = false;
        
        if (Instance.CancelButton != null)
            Instance.CancelButton.Visible = false;
        
        Logger.Info(new object[] { "MessageManager: Hiding message box" });
        Signals.EmitGlobalSignal(Signals.SignalName.MessageBoxOpen, false);
        Instance.Box.Visible = false;
    }
    
    /// <summary>
    /// End the current AI conversation
    /// </summary>
    public static void OnCancelConversation()
    {
        Logger.Info(new object[] { "MessageManager: OnCancelConversation called" });
        
        if (Instance.currentNPC != null)
        {
            Logger.Info(new object[] { "MessageManager: Ending AI conversation with NPC:", Instance.currentNPC.Name });
            EndAIConversation(Instance.currentNPC);
            
            // Return NPC to roaming state
            Instance.currentNPC.GetNode<StateMachine>("StateMachine").ChangeState("Roam");
        }
        else
        {
            Logger.Warning(new object[] { "MessageManager: OnCancelConversation - currentNPC is NULL" });
        }
    }
    
    /// <summary>
    /// Handle send button press or enter key
    /// </summary>
    public static void OnSendMessage()
    {
        Logger.Info(new object[] { "MessageManager: OnSendMessage called" });
        Logger.Info(new object[] { "MessageManager: IsReading:", IsReading(), "isAIConversation:", Instance.isAIConversation, "InputField null:", Instance.InputField == null });
        
        if (!IsReading())
        {
            Logger.Warning(new object[] { "MessageManager: OnSendMessage - Not reading, ignoring" });
            return;
        }
        
        if (!Instance.isAIConversation)
        {
            Logger.Warning(new object[] { "MessageManager: OnSendMessage - Not AI conversation, ignoring" });
            return;
        }
        
        if (Instance.InputField == null)
        {
            Logger.Error(new object[] { "MessageManager: OnSendMessage - InputField is NULL!" });
            return;
        }
        
        string userText = Instance.InputField.Text.Trim();
        Logger.Info(new object[] { "MessageManager: User text:", userText });
        
        if (string.IsNullOrEmpty(userText))
        {
            Logger.Warning(new object[] { "MessageManager: OnSendMessage - User text is empty" });
            return;
        }
        
        // Clear input
        Instance.InputField.Text = "";
        Logger.Info(new object[] { "MessageManager: Input field cleared" });
        
        // Add user message to display (formatted with the same style as NPC messages)
        // Format: "[b]You:[/b] message text"
        string formattedUserMessage = $"You: {userText}";
        Instance.Messages.Add(formattedUserMessage);
        Instance.waitingForAI = true;
        Instance.SetComposerEnabled(false);
        Logger.Info(new object[] { "MessageManager: Added user message to messages. Total:", Instance.Messages.Count });
        
        // Display the user message immediately
        if (!Instance.IsScrolling)
            ScrollText();
        
        // Send to NPC for AI processing after displaying user message
        if (Instance.currentNPC != null)
        {
            Logger.Info(new object[] { "MessageManager: Sending message to NPC:", Instance.currentNPC.Name });
            Instance.currentNPC.SendUserMessage(userText);
        }
        else
        {
            Logger.Error(new object[] { "MessageManager: OnSendMessage - currentNPC is NULL!" });
            Instance.IsScrolling = false;
        }
    }

    private void SetComposerEnabled(bool enabled)
    {
        InputField.Editable = enabled;
        SendButton.Disabled = !enabled;
        InputField.PlaceholderText = enabled ? "Talk to the NPC..." : "NPC is thinking...";
        if (enabled)
            InputField.GrabFocus();
    }
    
    /// <summary>
    /// Connect signals for input handling
    /// </summary>
    public void ConnectInputSignals()
    {
        Logger.Info(new object[] { "MessageManager: ConnectInputSignals called" });
        
        if (InputField != null)
        {
            Logger.Info(new object[] { "MessageManager: Connecting InputField.TextSubmitted signal" });
            InputField.TextSubmitted += (text) => 
            {
                Logger.Info(new object[] { "MessageManager: InputField.TextSubmitted triggered with text:", text });
                OnSendMessage();
            };
        }
        else
        {
            Logger.Error(new object[] { "MessageManager: ConnectInputSignals - InputField is NULL!" });
        }
        
        if (SendButton != null)
        {
            Logger.Info(new object[] { "MessageManager: Connecting SendButton.Pressed signal" });
            SendButton.Pressed += () => 
            {
                Logger.Info(new object[] { "MessageManager: SendButton.Pressed triggered" });
                OnSendMessage();
            };
        }
        else
        {
            Logger.Error(new object[] { "MessageManager: ConnectInputSignals - SendButton is NULL!" });
        }
        
        if (CancelButton != null)
        {
            Logger.Info(new object[] { "MessageManager: Connecting CancelButton.Pressed signal" });
            CancelButton.Pressed += () => 
            {
                Logger.Info(new object[] { "MessageManager: CancelButton.Pressed triggered" });
                OnCancelConversation();
            };
        }
        else
        {
            Logger.Error(new object[] { "MessageManager: ConnectInputSignals - CancelButton is NULL!" });
        }
    }
}
