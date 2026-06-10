using Dotty.AI.Tools;
using Xunit;

namespace Dotty.Tests;

public class TerminalToolHandlerTests
{
    [Fact]
    public void GetToolDefinitionsReturnsOneToolNamedReadTerminalScreen()
    {
        var handler = new TerminalToolHandler(() => "");
        var defs = handler.GetToolDefinitions();

        Assert.Single(defs);
        Assert.Equal("read_terminal_screen", defs[0].Name);
    }

    [Fact]
    public async Task ExecuteWithCorrectFunctionNameReturnsScreenContent()
    {
        var handler = new TerminalToolHandler(() => "hello world");
        var result = await handler.ExecuteToolAsync("call_1", "read_terminal_screen", "{}", CancellationToken.None);

        Assert.Equal("call_1", result.ToolCallId);
        Assert.False(result.IsError);
        Assert.Contains("hello world", result.ResultJson);
        Assert.Contains("screen_content", result.ResultJson);
    }

    [Fact]
    public async Task UnknownFunctionNameReturnsError()
    {
        var handler = new TerminalToolHandler(() => "");
        var result = await handler.ExecuteToolAsync("call_2", "unknown_function", "{}", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("call_2", result.ToolCallId);
        Assert.Contains("Unknown function", result.ResultJson);
    }

    [Fact]
    public async Task SpecialCharactersInScreenTextAreEscaped()
    {
        var handler = new TerminalToolHandler(() => "line1\nline2\ttab \"quoted\" back\\slash");
        var result = await handler.ExecuteToolAsync("call_3", "read_terminal_screen", "{}", CancellationToken.None);

        Assert.False(result.IsError);
        // Verify no raw newlines, tabs, quotes, or backslashes in the JSON value
        Assert.DoesNotContain("\n", result.ResultJson);
        Assert.DoesNotContain("\t", result.ResultJson);
        Assert.Contains("\\n", result.ResultJson);
        Assert.Contains("\\t", result.ResultJson);
        Assert.Contains("\\\"", result.ResultJson);
        Assert.Contains("\\\\", result.ResultJson);
    }

    [Fact]
    public async Task CallbackThrowingExceptionReturnsError()
    {
        var handler = new TerminalToolHandler(() => throw new InvalidOperationException("Something broke"));
        var result = await handler.ExecuteToolAsync("call_4", "read_terminal_screen", "{}", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("call_4", result.ToolCallId);
        Assert.Contains("Something broke", result.ResultJson);
    }

    [Fact]
    public async Task ToolCallIdPassedThroughCorrectly()
    {
        var handler = new TerminalToolHandler(() => "test");
        var result = await handler.ExecuteToolAsync("my-unique-id", "read_terminal_screen", "{}", CancellationToken.None);

        Assert.Equal("my-unique-id", result.ToolCallId);
    }

    [Fact]
    public async Task EmptyScreenContent()
    {
        var handler = new TerminalToolHandler(() => "");
        var result = await handler.ExecuteToolAsync("call_5", "read_terminal_screen", "{}", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("screen_content", result.ResultJson);
    }

    [Fact]
    public async Task CarriageReturnEscaped()
    {
        var handler = new TerminalToolHandler(() => "line1\r\nline2");
        var result = await handler.ExecuteToolAsync("call_6", "read_terminal_screen", "{}", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.DoesNotContain("\r", result.ResultJson);
        Assert.Contains("\\r", result.ResultJson);
    }
}
