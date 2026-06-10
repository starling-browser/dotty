using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dotty.Terminal.Mcp;

public static class McpConfigWriter
{
    public static async Task WriteGlobalAsync(int port)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");
        await WriteAsync(path, port);
    }

    public static async Task WriteProjectAsync(string folderPath, int port)
    {
        var path = Path.Combine(folderPath, ".mcp.json");
        await WriteAsync(path, port);
    }

    private static async Task WriteAsync(string path, int port)
    {
        var root = File.Exists(path)
            ? JsonNode.Parse(await File.ReadAllTextAsync(path)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        var servers = (root["mcpServers"] as JsonObject) ?? new JsonObject();
        servers["dotty"] = new JsonObject
        {
            ["type"] = "sse",
            ["url"] = JsonValue.Create($"http://localhost:{port}/sse"),
        };
        root["mcpServers"] = servers;

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
    }
}
