using System.Text;
using Dotty.Terminal.Mcp;
using Dotty.Commands;

namespace Dotty.Tools;

public class ListCommandsTool : IAppTool
{
    private readonly CommandRegistry _registry;

    public ListCommandsTool(CommandRegistry registry)
    {
        _registry = registry;
    }

    public string Name => "list_commands";

    public string Description =>
        "List all available Dotty commands. Optionally filter by a search query.";

    public string ParametersSchemaJson => """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Optional filter query to search commands by label or category"
                }
            }
        }
        """;

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var query = ExtractJsonString(argumentsJson, "query");
            var commands = string.IsNullOrEmpty(query)
                ? _registry.Commands.ToList()
                : _registry.Filter(query);

            var sb = new StringBuilder();
            sb.Append("{\"commands\": [");
            for (var i = 0; i < commands.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var cmd = commands[i];
                sb.Append($"{{\"id\": \"{cmd.Id}\", \"label\": \"{Escape(cmd.Label)}\", \"category\": \"{Escape(cmd.Category)}\"");
                if (cmd.ShortcutHint != null)
                    sb.Append($", \"shortcut\": \"{Escape(cmd.ShortcutHint)}\"");
                sb.Append('}');
            }
            sb.Append("]}");

            return Task.FromResult(new ToolResult(sb.ToString()));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(
                $"{{\"error\": \"{Escape(ex.Message)}\"}}", IsError: true));
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? ExtractJsonString(string json, string key)
    {
        var searchKey = $"\"{key}\"";
        var keyIndex = json.IndexOf(searchKey, StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
        if (colonIndex < 0) return null;

        var afterColon = json[(colonIndex + 1)..].TrimStart();
        if (afterColon.Length == 0 || afterColon[0] != '"') return null;

        var sb = new StringBuilder();
        var i = 1;
        while (i < afterColon.Length)
        {
            var c = afterColon[i];
            if (c == '\\' && i + 1 < afterColon.Length)
            {
                var next = afterColon[i + 1];
                switch (next)
                {
                    case '"': sb.Append('"'); i += 2; continue;
                    case '\\': sb.Append('\\'); i += 2; continue;
                    default: sb.Append(next); i += 2; continue;
                }
            }
            if (c == '"') break;
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
}
