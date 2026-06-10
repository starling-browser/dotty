using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Xunit;

namespace Dotty.Avalonia.Tests;

public class ClipboardTests
{
    [AvaloniaFact]
    public async Task SetAndGetClipboardText()
    {
        var window = new Window();
        window.Show();

        var clipboard = window.Clipboard;
        Assert.NotNull(clipboard);

        await clipboard!.SetTextAsync("hello from test");
        var result = await clipboard.TryGetTextAsync();

        Assert.Equal("hello from test", result);
    }

    [AvaloniaFact]
    public async Task ClipboardHandlesEmptyString()
    {
        var window = new Window();
        window.Show();

        var clipboard = window.Clipboard!;

        await clipboard.SetTextAsync("");
        var result = await clipboard.TryGetTextAsync();

        Assert.True(string.IsNullOrEmpty(result));
    }

    [AvaloniaFact]
    public async Task ClipboardOverwritesPreviousContent()
    {
        var window = new Window();
        window.Show();

        var clipboard = window.Clipboard!;

        await clipboard.SetTextAsync("first");
        await clipboard.SetTextAsync("second");
        var result = await clipboard.TryGetTextAsync();

        Assert.Equal("second", result);
    }

    [AvaloniaFact]
    public async Task ClipboardHandlesMultilineText()
    {
        var window = new Window();
        window.Show();

        var clipboard = window.Clipboard!;

        var multiline = "line one\nline two\nline three";
        await clipboard.SetTextAsync(multiline);
        var result = await clipboard.TryGetTextAsync();

        Assert.Equal(multiline, result);
    }

    [AvaloniaFact]
    public async Task ClipboardHandlesUnicodeText()
    {
        var window = new Window();
        window.Show();

        var clipboard = window.Clipboard!;

        var unicode = "Hello \ud83c\udf0d \u2603 \u00e9\u00e8\u00ea";
        await clipboard.SetTextAsync(unicode);
        var result = await clipboard.TryGetTextAsync();

        Assert.Equal(unicode, result);
    }
}
