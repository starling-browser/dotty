using System.Text;

namespace Dotty.Terminal.Input;

public static class TerminalMouseEncoder
{
    public static bool IsMouseTracking(TerminalModes modes) =>
        modes.HasFlag(TerminalModes.MouseX10)
        || modes.HasFlag(TerminalModes.MouseNormal)
        || modes.HasFlag(TerminalModes.MouseButtonEvent)
        || modes.HasFlag(TerminalModes.MouseAnyEvent);

    public static byte[]? EncodeButton(TerminalModes modes, int button, int col, int row, bool press)
    {
        if (modes.HasFlag(TerminalModes.MouseSgr))
        {
            char suffix = press ? 'M' : 'm';
            return Encoding.UTF8.GetBytes($"\x1b[<{button};{col + 1};{row + 1}{suffix}");
        }

        if (!press && modes.HasFlag(TerminalModes.MouseX10))
            return null;

        int cb = press ? button : 3;
        return
        [
            0x1b,
            (byte)'[',
            (byte)'M',
            (byte)(cb + 32),
            (byte)Math.Min(col + 33, 255),
            (byte)Math.Min(row + 33, 255)
        ];
    }

    public static byte[] EncodeMotion(TerminalModes modes, int button, int col, int row)
    {
        if (modes.HasFlag(TerminalModes.MouseSgr))
            return Encoding.UTF8.GetBytes($"\x1b[<{button + 32};{col + 1};{row + 1}M");

        return
        [
            0x1b,
            (byte)'[',
            (byte)'M',
            (byte)(button + 64),
            (byte)Math.Min(col + 33, 255),
            (byte)Math.Min(row + 33, 255)
        ];
    }
}
