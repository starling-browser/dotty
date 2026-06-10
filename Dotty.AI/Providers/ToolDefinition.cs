namespace Dotty.AI.Providers;

public class ToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ParametersSchemaJson { get; set; } = "{}";
}
