namespace Dotty.AI.Models;

public class ChatSession
{
    public List<ChatMessage> Messages { get; } = [];

    public void AddSystemMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatRole.System, Content = content });
    }

    public void AddUserMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatRole.User, Content = content });
    }

    public void AddAssistantMessage(string content, List<ChatToolCall>? toolCalls = null)
    {
        Messages.Add(new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = content,
            ToolCalls = toolCalls
        });
    }

    public void AddToolResult(string toolCallId, string result)
    {
        Messages.Add(new ChatMessage
        {
            Role = ChatRole.Tool,
            Content = result,
            ToolCallId = toolCallId
        });
    }

    public void Clear()
    {
        Messages.Clear();
    }
}
