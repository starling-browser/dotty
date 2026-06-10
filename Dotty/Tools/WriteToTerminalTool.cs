using System.Text;
using Dotty.AI.Tools;

namespace Dotty.Tools;

public class WriteToTerminalTool : IAppTool
{
    private readonly Func<byte[], Task> _writeToPty;

    /// <summary>
    /// The callback should dispatch WriteToPty on the UI thread.
    /// Example: bytes => Dispatcher.UIThread.InvokeAsync(() => terminal.WriteToPty(bytes))
    /// </summary>
    public WriteToTerminalTool(Func<byte[], Task> writeToPty)
    {
        _writeToPty = writeToPty;
    }

    public string Name => "write_to_terminal";

    public string Description =>
        "Send text input to the active terminal, as if typed by the user. Use this to run commands or type text into the terminal.";

    public string ParametersSchemaJson => """
        {
            "type": "object",
            "properties": {
                "text": {
                    "type": "string",
                    "description": "The text to send to the terminal. Use \\n for Enter key."
                }
            },
            "required": ["text"]
        }
        """;

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var text = ExtractJsonString(argumentsJson, "text");
            if (text == null)
                return new ToolResult("{\"error\": \"Missing required parameter: text\"}", IsError: true);

            var bytes = Encoding.UTF8.GetBytes(text);
            await _writeToPty(bytes);
            return new ToolResult("{\"status\": \"sent\", \"bytes\": " + bytes.Length + "}");
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
