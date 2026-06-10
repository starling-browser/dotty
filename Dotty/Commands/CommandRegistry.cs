namespace Dotty.Commands;

public class CommandRegistry
{
    private readonly List<Command> _commands = new();

    public IReadOnlyList<Command> Commands => _commands;

    public void Register(Command command) => _commands.Add(command);

    public List<Command> Filter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Command>(_commands);

        return _commands
            .Where(c => c.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || c.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
