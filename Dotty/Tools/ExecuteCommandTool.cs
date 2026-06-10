using System.Text;
using Dotty.AI.Tools;
using Dotty.Commands;

namespace Dotty.Tools;

public class ExecuteCommandTool : IAppTool
{
    private readonly CommandRegistry _registry;
    private readonly Func<string, Task> _dispatchCommand;

    /// <summary>
    /// The dispatchCommand callback should execute the command on the UI thread.
    /// </summary>
    public ExecuteCommandTool(CommandRegistry registry, Func<string, Task> dispatchCommand)
    {
        _registry = registry;
        _dispatchCommand = dispatchCommand;
    }

    public string Name => "execute_command";

    public string Description =>
        "Execute a Dotty command by ID (e.g. 'terminal.new-tab', 'font.increase'). " +
        "Use list_commands tool to see available commands.";

    public string ParametersSchemaJson => """
        {
            "type": "object",
            "properties": {
                "command_id": {
                    "type": "string",
                    "description": "The command ID to execute (e.g. 'terminal.new-tab')"
                }
            },
            "required": ["command_id"]
        }
        """;

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var commandId = ExtractJsonString(argumentsJson, "command_id");
            if (commandId == null)
                return new ToolResult("{\"error\": \"Missing required parameter: command_id\"}", IsError: true);

            var command = _registry.Commands.FirstOrDefault(c => c.Id == commandId);
            if (command == null)
                return new ToolResult($"{{\"error\": \"Unknown command: {commandId}\"}}", IsError: true);

            await _dispatchCommand(commandId);
            return new ToolResult($"{{\"status\": \"executed\", \"command\": \"{commandId}\"}}");
        }
        catch (Exception ex)
        {
            return new ToolResult(
                $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}", IsError: true);
        }
    }

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
                    case 'n': sb.Append('\n'); i += 2; continue;
                    case 'r': sb.Append('\r'); i += 2; continue;
                    case 't': sb.Append('\t'); i += 2; continue;
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
