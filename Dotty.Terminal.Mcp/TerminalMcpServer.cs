using Dotty.Terminal.Mcp.Tools;

namespace Dotty.Terminal.Mcp;

/// <summary>
/// Convenience entry point that wires the embeddable terminal to an MCP endpoint.
/// It builds a tool registry pre-populated with the built-in terminal tools
/// (read_terminal_screen, write_to_terminal) bound to the given terminal, and
/// hosts them over the embedded <see cref="McpHttpServer"/>.
///
/// A host can register additional tools on <see cref="Tools"/> before calling
/// <see cref="Start"/>, and can bind UI to <see cref="Server"/> for status.
/// </summary>
public sealed class TerminalMcpServer : IDisposable
{
    /// <summary>Tool registry serving this MCP endpoint. Add host tools here before starting.</summary>
    public AppToolRegistry Tools { get; }

    /// <summary>The underlying HTTP/SSE MCP server.</summary>
    public McpHttpServer Server { get; }

    public int Port => Server.Port;
    public bool IsRunning => Server.IsRunning;

    /// <summary>
    /// Creates a terminal-backed MCP server. The built-in terminal tools are registered
    /// against <paramref name="terminal"/>.
    /// </summary>
    public TerminalMcpServer(ITerminalTarget terminal, int port = 8257)
    {
        Tools = new AppToolRegistry();
        Tools.Register(new ReadTerminalScreenTool(terminal));
        Tools.Register(new WriteToTerminalTool(terminal));
        Server = new McpHttpServer(Tools, port);
    }

    /// <summary>
    /// Creates a terminal-backed MCP server bound directly to a <see cref="Pty.TerminalDriver"/>.
    /// </summary>
    public TerminalMcpServer(Pty.TerminalDriver driver, int port = 8257)
        : this(new DriverTerminalTarget(driver), port)
    {
    }

    /// <summary>
    /// Creates a terminal-backed MCP server bound to a shared terminal session.
    /// </summary>
    public TerminalMcpServer(Dotty.Terminal.Hosting.TerminalSession session, int port = 8257)
        : this(new SessionTerminalTarget(session), port)
    {
    }

    public void Start() => Server.Start();

    public void Stop() => Server.Stop();

    public void Dispose() => Server.Dispose();
}
