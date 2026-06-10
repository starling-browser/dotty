using System.Text.Json;
using Dotty.Terminal.Mcp;

namespace Dotty.Tools;

public class ResizeWindowTool : IAppTool
{
    private readonly Func<int, int, Task> _resizeWindow;

    public ResizeWindowTool(Func<int, int, Task> resizeWindow)
    {
        _resizeWindow = resizeWindow;
    }

    public string Name => "resize_window";
    public string Description => "Resize the Dotty application window to the specified width and height in pixels.";
    public string ParametersSchemaJson => """
        {
            "type": "object",
            "properties": {
                "width": {
                    "type": "integer",
                    "description": "The window width in pixels"
                },
                "height": {
                    "type": "integer",
                    "description": "The window height in pixels"
                }
            },
            "required": ["width", "height"]
        }
        """;

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("width", out var widthEl) || !root.TryGetProperty("height", out var heightEl))
                return new ToolResult("{\"error\": \"Missing required parameters: width and height\"}", IsError: true);

            var width = widthEl.GetInt32();
            var height = heightEl.GetInt32();

            if (width <= 0 || height <= 0)
                return new ToolResult("{\"error\": \"Width and height must be positive integers\"}", IsError: true);

            await _resizeWindow(width, height);
            return new ToolResult($"{{\"status\": \"resized\", \"width\": {width}, \"height\": {height}}}");
        }
        catch (Exception ex)
        {
            return new ToolResult($"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}",  IsError: true);
        }
    }
}
