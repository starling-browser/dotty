using System.Text;
using Dotty.Terminal;
using Dotty.Terminal.Mcp;
using Dotty.Terminal.Mcp.Tools;
using Xunit;
using TerminalEngine = Dotty.Terminal.Terminal;

namespace Dotty.Terminal.Tests;

public class TerminalMcpToolTests
{
    private sealed class FakeTarget(TerminalEngine terminal) : ITerminalTarget
    {
        public byte[]? LastWrite { get; private set; }

        public string GetVisibleText() => TerminalText.GetVisibleText(terminal);

        public ValueTask WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            LastWrite = data;
            terminal.ProcessPtyOutput(data);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void TerminalText_GetVisibleText_ReturnsScreenContent()
    {
        var term = new TerminalEngine(new GridSize(80, 24));
        term.ProcessPtyOutput("Hello, World!"u8);

        var text = TerminalText.GetVisibleText(term);

        Assert.Contains("Hello, World!", text);
    }

    [Fact]
    public async Task ReadTerminalScreenTool_ReturnsScreenContent()
    {
        var term = new TerminalEngine(new GridSize(80, 24));
        term.ProcessPtyOutput("ready>"u8);
        var tool = new ReadTerminalScreenTool(new FakeTarget(term));

        var result = await tool.ExecuteAsync("{}", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("screen_content", result.ResultJson);
        Assert.Contains("ready>", result.ResultJson);
    }

    [Fact]
    public async Task WriteToTerminalTool_SendsTextToTarget()
    {
        var term = new TerminalEngine(new GridSize(80, 24));
        var target = new FakeTarget(term);
        var tool = new WriteToTerminalTool(target);

        var result = await tool.ExecuteAsync("{\"text\": \"ls\\n\"}", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("sent", result.ResultJson);
        Assert.Equal(Encoding.UTF8.GetBytes("ls\n"), target.LastWrite);
    }

    [Fact]
    public async Task WriteToTerminalTool_MissingText_ReturnsError()
    {
        var term = new TerminalEngine(new GridSize(80, 24));
        var tool = new WriteToTerminalTool(new FakeTarget(term));

        var result = await tool.ExecuteAsync("{}", CancellationToken.None);

        Assert.True(result.IsError);
    }
}
