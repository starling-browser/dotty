using Dotty.AI.Models;
using Xunit;

namespace Dotty.Tests;

public class ChatSessionTests
{
    [Fact]
    public void AddSystemMessageSetsRoleAndContent()
    {
        var session = new ChatSession();
        session.AddSystemMessage("You are a helpful assistant.");

        Assert.Single(session.Messages);
        Assert.Equal(ChatRole.System, session.Messages[0].Role);
        Assert.Equal("You are a helpful assistant.", session.Messages[0].Content);
    }

    [Fact]
    public void AddUserMessageSetsRoleAndContent()
    {
        var session = new ChatSession();
        session.AddUserMessage("Hello");

        Assert.Single(session.Messages);
        Assert.Equal(ChatRole.User, session.Messages[0].Role);
        Assert.Equal("Hello", session.Messages[0].Content);
    }

    [Fact]
    public void AddAssistantMessageWithoutToolCalls()
    {
        var session = new ChatSession();
        session.AddAssistantMessage("Hi there!");

        Assert.Single(session.Messages);
        Assert.Equal(ChatRole.Assistant, session.Messages[0].Role);
        Assert.Equal("Hi there!", session.Messages[0].Content);
        Assert.Null(session.Messages[0].ToolCalls);
    }

    [Fact]
    public void AddAssistantMessageWithToolCalls()
    {
        var session = new ChatSession();
        var toolCalls = new List<ChatToolCall>
        {
            new() { Id = "call_1", FunctionName = "read_terminal_screen", ArgumentsJson = "{}" }
        };
        session.AddAssistantMessage("Let me check.", toolCalls);

        Assert.Single(session.Messages);
        Assert.Equal(ChatRole.Assistant, session.Messages[0].Role);
        Assert.NotNull(session.Messages[0].ToolCalls);
        Assert.Single(session.Messages[0].ToolCalls!);
        Assert.Equal("call_1", session.Messages[0].ToolCalls![0].Id);
    }

    [Fact]
    public void AddToolResultSetsRoleAndToolCallId()
    {
        var session = new ChatSession();
        session.AddToolResult("call_1", "{\"screen_content\": \"hello\"}");

        Assert.Single(session.Messages);
        Assert.Equal(ChatRole.Tool, session.Messages[0].Role);
        Assert.Equal("call_1", session.Messages[0].ToolCallId);
        Assert.Equal("{\"screen_content\": \"hello\"}", session.Messages[0].Content);
    }

    [Fact]
    public void MessageOrderingPreservedAcrossMixedAdds()
    {
        var session = new ChatSession();
        session.AddSystemMessage("system");
        session.AddUserMessage("user");
        session.AddAssistantMessage("assistant");
        session.AddToolResult("id", "result");

        Assert.Equal(4, session.Messages.Count);
        Assert.Equal(ChatRole.System, session.Messages[0].Role);
        Assert.Equal(ChatRole.User, session.Messages[1].Role);
        Assert.Equal(ChatRole.Assistant, session.Messages[2].Role);
        Assert.Equal(ChatRole.Tool, session.Messages[3].Role);
    }

    [Fact]
    public void ClearEmptiesMessagesList()
    {
        var session = new ChatSession();
        session.AddUserMessage("Hello");
        session.AddAssistantMessage("Hi");

        session.Clear();

        Assert.Empty(session.Messages);
    }

    [Fact]
    public void EmptyContentStringsHandled()
    {
        var session = new ChatSession();
        session.AddUserMessage("");
        session.AddAssistantMessage("");
        session.AddSystemMessage("");

        Assert.Equal(3, session.Messages.Count);
        Assert.All(session.Messages, m => Assert.Equal("", m.Content));
    }

    [Fact]
    public void AddMultipleUserMessages()
    {
        var session = new ChatSession();
        session.AddUserMessage("First");
        session.AddUserMessage("Second");

        Assert.Equal(2, session.Messages.Count);
        Assert.Equal("First", session.Messages[0].Content);
        Assert.Equal("Second", session.Messages[1].Content);
    }
}
