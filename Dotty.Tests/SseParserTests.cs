using Dotty.AI.Streaming;
using Xunit;

namespace Dotty.Tests;

public class SseParserTests
{
    private static MemoryStream ToStream(string text)
        => new(System.Text.Encoding.UTF8.GetBytes(text));

    private static async Task<List<string>> CollectEvents(string input)
    {
        using var stream = ToStream(input);
        var events = new List<string>();
        await foreach (var evt in SseParser.ParseAsync(stream))
            events.Add(evt);
        return events;
    }

    [Fact]
    public async Task SingleDataLineYieldsOneEvent()
    {
        var events = await CollectEvents("data: hello\n\n");
        Assert.Single(events);
        Assert.Equal("hello", events[0]);
    }

    [Fact]
    public async Task DoneTerminatesStreamEarly()
    {
        var events = await CollectEvents("data: first\n\ndata: [DONE]\ndata: ignored\n\n");
        Assert.Single(events);
        Assert.Equal("first", events[0]);
    }

    [Fact]
    public async Task MultiLineDataConcatenatedWithNewline()
    {
        var events = await CollectEvents("data: line1\ndata: line2\n\n");
        Assert.Single(events);
        Assert.Equal("line1\nline2", events[0]);
    }

    [Fact]
    public async Task EmptyStreamYieldsNoEvents()
    {
        var events = await CollectEvents("");
        Assert.Empty(events);
    }

    [Fact]
    public async Task TrailingBufferedDataYieldedWithoutTerminatingEmptyLine()
    {
        var events = await CollectEvents("data: trailing");
        Assert.Single(events);
        Assert.Equal("trailing", events[0]);
    }

    [Fact]
    public async Task NonDataLinesIgnored()
    {
        var events = await CollectEvents(": comment\nevent: update\nid: 123\ndata: actual\n\n");
        Assert.Single(events);
        Assert.Equal("actual", events[0]);
    }

    [Fact]
    public async Task MultipleEventsInSequence()
    {
        var events = await CollectEvents("data: first\n\ndata: second\n\ndata: third\n\n");
        Assert.Equal(3, events.Count);
        Assert.Equal("first", events[0]);
        Assert.Equal("second", events[1]);
        Assert.Equal("third", events[2]);
    }

    [Fact]
    public async Task JsonDataPassedThrough()
    {
        var json = "{\"choices\":[{\"delta\":{\"content\":\"hi\"}}]}";
        var events = await CollectEvents($"data: {json}\n\n");
        Assert.Single(events);
        Assert.Equal(json, events[0]);
    }

    [Fact]
    public async Task EmptyDataLine()
    {
        // "data: " with nothing after it → empty string event
        var events = await CollectEvents("data: \n\n");
        Assert.Single(events);
        Assert.Equal("", events[0]);
    }

    [Fact]
    public async Task CancellationStopsProcessing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = ToStream("data: hello\n\n");
        var events = new List<string>();
        await foreach (var evt in SseParser.ParseAsync(stream, cts.Token))
            events.Add(evt);

        Assert.Empty(events);
    }
}
