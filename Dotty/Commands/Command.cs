namespace Dotty.Commands;

public record Command(string Id, string Label, string Category, string? ShortcutHint, Action Execute);
