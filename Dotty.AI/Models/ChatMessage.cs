namespace Dotty.AI.Models;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

public class ChatToolCall
{
    public string Id { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public string ArgumentsJson { get; set; } = "";
}

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = "";
    public List<ChatToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
}
