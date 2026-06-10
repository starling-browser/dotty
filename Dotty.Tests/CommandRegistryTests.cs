using Dotty.Commands;
using Xunit;

namespace Dotty.Tests;

public class CommandRegistryTests
{
    private static Command MakeCommand(string id, string label, string category)
        => new(id, label, category, null, () => { });

    [Fact]
    public void RegisterAddsCommandToCommandsList()
    {
        var registry = new CommandRegistry();
        var cmd = MakeCommand("test", "Test Command", "General");
        registry.Register(cmd);

        Assert.Single(registry.Commands);
        Assert.Equal("test", registry.Commands[0].Id);
    }

    [Fact]
    public void FilterWithEmptyQueryReturnsAllCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "Alpha", "Cat1"));
        registry.Register(MakeCommand("b", "Beta", "Cat2"));

        var result = registry.Filter("");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterWithNullQueryReturnsAllCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "Alpha", "Cat1"));

        var result = registry.Filter(null!);
        Assert.Single(result);
    }

    [Fact]
    public void FilterWithWhitespaceQueryReturnsAllCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "Alpha", "Cat1"));

        var result = registry.Filter("   ");
        Assert.Single(result);
    }

    [Fact]
    public void FilterMatchesLabelCaseInsensitive()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "New Terminal", "Terminals"));
        registry.Register(MakeCommand("b", "Settings", "Config"));

        var result = registry.Filter("new terminal");
        Assert.Single(result);
        Assert.Equal("a", result[0].Id);
    }

    [Fact]
    public void FilterMatchesCategoryCaseInsensitive()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "Open", "terminals"));
        registry.Register(MakeCommand("b", "Settings", "Config"));

        var result = registry.Filter("TERMINALS");
        Assert.Single(result);
        Assert.Equal("a", result[0].Id);
    }

    [Fact]
    public void FilterWithNoMatchReturnsEmptyList()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "Alpha", "Cat1"));

        var result = registry.Filter("zzzzz");
        Assert.Empty(result);
    }

    [Fact]
    public void FilterReturnsMultipleMatchingCommands()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "New Terminal", "Terminals"));
        registry.Register(MakeCommand("b", "Split Terminal", "Terminals"));
        registry.Register(MakeCommand("c", "Settings", "Config"));

        var result = registry.Filter("Terminal");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterDoesNotMutateInternalState()
    {
        var registry = new CommandRegistry();
        registry.Register(MakeCommand("a", "Alpha", "Cat1"));
        registry.Register(MakeCommand("b", "Beta", "Cat2"));

        registry.Filter("Alpha");
        Assert.Equal(2, registry.Commands.Count);

        var all = registry.Filter("");
        Assert.Equal(2, all.Count);
    }
}
