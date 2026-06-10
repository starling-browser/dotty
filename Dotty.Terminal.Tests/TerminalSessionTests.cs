using Dotty.Terminal.Hosting;
using Xunit;

namespace Dotty.Terminal.Tests;

public class TerminalSessionTests
{
    [Fact]
    public void SessionCanReadVisibleTextWithoutUiFramework()
    {
        using var session = TerminalSession.CreateWithoutPty(new GridSize(10, 2));

        session.UpdateTerminal(terminal => terminal.ProcessPtyOutput("ready>"u8));

        Assert.Contains("ready>", session.GetVisibleText());
    }

    [Fact]
    public void SessionCreatesImmutableSnapshot()
    {
        using var session = TerminalSession.CreateWithoutPty(new GridSize(10, 2));
        session.UpdateTerminal(terminal => terminal.ProcessPtyOutput("ok"u8));

        var snapshot = session.CreateSnapshot();

        Assert.Equal('o', snapshot.CellAt(0, 0).Codepoint);
        Assert.Equal('k', snapshot.CellAt(1, 0).Codepoint);
    }

    [Fact]
    public void SessionReportsDamageUntilAcknowledged()
    {
        using var session = TerminalSession.CreateWithoutPty(new GridSize(10, 2));
        session.UpdateTerminal(terminal => terminal.ProcessPtyOutput("ok"u8));

        Assert.True(session.HasDamage());

        session.AcknowledgeDamage();

        Assert.False(session.HasDamage());
    }
}
