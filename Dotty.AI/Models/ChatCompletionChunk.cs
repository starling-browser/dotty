namespace Dotty.AI.Models;

public class ChatCompletionChunk
{
    public string? ContentDelta { get; set; }
    public List<ToolCallDelta>? ToolCalls { get; set; }
    public bool IsFinished { get; set; }
}

public class ToolCallDelta
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public string? FunctionName { get; set; }
    public string? ArgumentsDelta { get; set; }
}
