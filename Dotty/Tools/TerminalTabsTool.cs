using System.Text;
using Dotty.AI.Tools;
using Dotty.Controls;

namespace Dotty.Tools;

public class GetTabsTool : IAppTool
{
    private readonly Func<Task<string>> _getTabsJson;

    public GetTabsTool(Func<Task<string>> getTabsJson)
    {
        _getTabsJson = getTabsJson;
    }

    public string Name => "get_tabs";
    public string Description => "List all open terminal tabs with their IDs and titles.";
    public string ParametersSchemaJson => """{"type": "object", "properties": {}}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var json = await _getTabsJson();
            return new ToolResult(json);
        }
        catch (Exception ex)
        {
            return new ToolResult($"{{\"error\": \"{Escape(ex.Message)}\"}}", IsError: true);
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public class NewTabTool : IAppTool
{
    private readonly Func<Task> _createTab;

    public NewTabTool(Func<Task> createTab)
    {
        _createTab = createTab;
    }

    public string Name => "new_tab";
    public string Description => "Create a new terminal tab.";
    public string ParametersSchemaJson => """{"type": "object", "properties": {}}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            await _createTab();
            return new ToolResult("{\"status\": \"created\"}");
        }
        catch (Exception ex)
        {
            return new ToolResult($"{{\"error\": \"{Escape(ex.Message)}\"}}", IsError: true);
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public class CloseTabTool : IAppTool
{
    private readonly Func<Task> _closeTab;

    public CloseTabTool(Func<Task> closeTab)
    {
        _closeTab = closeTab;
    }

    public string Name => "close_tab";
    public string Description => "Close the currently active terminal tab.";
    public string ParametersSchemaJson => """{"type": "object", "properties": {}}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            await _closeTab();
            return new ToolResult("{\"status\": \"closed\"}");
        }
        catch (Exception ex)
        {
            return new ToolResult($"{{\"error\": \"{Escape(ex.Message)}\"}}", IsError: true);
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public class SwitchTabTool : IAppTool
{
    private readonly Func<string, Task<bool>> _switchTab;

    public SwitchTabTool(Func<string, Task<bool>> switchTab)
    {
        _switchTab = switchTab;
    }

    public string Name => "switch_tab";
    public string Description => "Switch to a terminal tab by direction ('next' or 'previous').";
    public string ParametersSchemaJson => """
        {
            "type": "object",
            "properties": {
                "direction": {
                    "type": "string",
                    "description": "Direction to switch: 'next' or 'previous'",
                    "enum": ["next", "previous"]
                }
            },
            "required": ["direction"]
        }
        """;

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var direction = ExtractJsonString(argumentsJson, "direction");
            if (direction == null)
                return new ToolResult("{\"error\": \"Missing required parameter: direction\"}", IsError: true);

            var success = await _switchTab(direction);
            return success
                ? new ToolResult($"{{\"status\": \"switched\", \"direction\": \"{direction}\"}}")
                : new ToolResult($"{{\"error\": \"Cannot switch {direction}\"}}", IsError: true);
        }
        catch (Exception ex)
        {
            return new ToolResult($"{{\"error\": \"{Escape(ex.Message)}\"}}", IsError: true);
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
        for (var i = 1; i < afterColon.Length; i++)
        {
            var c = afterColon[i];
            if (c == '\\' && i + 1 < afterColon.Length) { sb.Append(afterColon[++i]); continue; }
            if (c == '"') break;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
