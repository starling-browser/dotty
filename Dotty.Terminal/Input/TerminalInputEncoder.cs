using System.Text;

namespace Dotty.Terminal.Input;

public static class TerminalInputEncoder
{
    public static byte[]? Encode(TerminalKey key, TerminalKeyModifiers modifiers, string? text, TerminalModes modes)
    {
        bool appCursor = modes.HasFlag(TerminalModes.CursorKeys);
        bool ctrl = modifiers.HasFlag(TerminalKeyModifiers.Control);
        bool alt = modifiers.HasFlag(TerminalKeyModifiers.Alt);
        bool shift = modifiers.HasFlag(TerminalKeyModifiers.Shift);

        if (ctrl)
        {
            if (key >= TerminalKey.A && key <= TerminalKey.Z)
            {
                byte code = (byte)(key - TerminalKey.A + 1);
                return [code];
            }

            if (key == TerminalKey.Space)
                return [0];
        }

        switch (key)
        {
            case TerminalKey.Enter:
                return "\r"u8.ToArray();
            case TerminalKey.Backspace:
                return [0x7f];
            case TerminalKey.Tab:
                return shift ? "\x1b[Z"u8.ToArray() : "\t"u8.ToArray();
            case TerminalKey.Escape:
                return [0x1b];
            case TerminalKey.Up:
                return appCursor ? "\x1bOA"u8.ToArray() : "\x1b[A"u8.ToArray();
            case TerminalKey.Down:
                return appCursor ? "\x1bOB"u8.ToArray() : "\x1b[B"u8.ToArray();
            case TerminalKey.Right:
                return appCursor ? "\x1bOC"u8.ToArray() : "\x1b[C"u8.ToArray();
            case TerminalKey.Left:
                return appCursor ? "\x1bOD"u8.ToArray() : "\x1b[D"u8.ToArray();
            case TerminalKey.Home:
                return "\x1b[H"u8.ToArray();
            case TerminalKey.End:
                return "\x1b[F"u8.ToArray();
            case TerminalKey.PageUp:
                return "\x1b[5~"u8.ToArray();
            case TerminalKey.PageDown:
                return "\x1b[6~"u8.ToArray();
            case TerminalKey.Insert:
                return "\x1b[2~"u8.ToArray();
            case TerminalKey.Delete:
                return "\x1b[3~"u8.ToArray();
            case TerminalKey.F1:
                return "\x1bOP"u8.ToArray();
            case TerminalKey.F2:
                return "\x1bOQ"u8.ToArray();
            case TerminalKey.F3:
                return "\x1bOR"u8.ToArray();
            case TerminalKey.F4:
                return "\x1bOS"u8.ToArray();
            case TerminalKey.F5:
                return "\x1b[15~"u8.ToArray();
            case TerminalKey.F6:
                return "\x1b[17~"u8.ToArray();
            case TerminalKey.F7:
                return "\x1b[18~"u8.ToArray();
            case TerminalKey.F8:
                return "\x1b[19~"u8.ToArray();
            case TerminalKey.F9:
                return "\x1b[20~"u8.ToArray();
            case TerminalKey.F10:
                return "\x1b[21~"u8.ToArray();
            case TerminalKey.F11:
                return "\x1b[23~"u8.ToArray();
            case TerminalKey.F12:
                return "\x1b[24~"u8.ToArray();
        }

        if (alt && text is { Length: > 0 })
        {
            var bytes = new byte[1 + Encoding.UTF8.GetByteCount(text)];
            bytes[0] = 0x1b;
            Encoding.UTF8.GetBytes(text, bytes.AsSpan(1));
            return bytes;
        }

        if (text is { Length: > 0 })
            return Encoding.UTF8.GetBytes(text);

        if (!ctrl && !alt)
        {
            char? c = KeyToChar(key, shift);
            if (c.HasValue)
                return Encoding.UTF8.GetBytes(c.Value.ToString());
        }

        return null;
    }

    public static byte[] EncodePaste(string text, bool bracketed)
    {
        if (!bracketed)
            return Encoding.UTF8.GetBytes(text);

        return Encoding.UTF8.GetBytes($"\x1b[200~{text}\x1b[201~");
    }

    private static char? KeyToChar(TerminalKey key, bool shift)
    {
        if (key >= TerminalKey.A && key <= TerminalKey.Z)
            return shift ? (char)('A' + key - TerminalKey.A) : (char)('a' + key - TerminalKey.A);

        if (key >= TerminalKey.D0 && key <= TerminalKey.D9)
        {
            if (!shift)
                return (char)('0' + key - TerminalKey.D0);

            return (key - TerminalKey.D0) switch
            {
                0 => ')',
                1 => '!',
                2 => '@',
                3 => '#',
                4 => '$',
                5 => '%',
                6 => '^',
                7 => '&',
                8 => '*',
                9 => '(',
                _ => null,
            };
        }

        if (key >= TerminalKey.NumPad0 && key <= TerminalKey.NumPad9)
            return (char)('0' + key - TerminalKey.NumPad0);

        return key switch
        {
            TerminalKey.Space => ' ',
            TerminalKey.OemMinus => shift ? '_' : '-',
            TerminalKey.OemPlus => shift ? '+' : '=',
            TerminalKey.OemOpenBrackets => shift ? '{' : '[',
            TerminalKey.OemCloseBrackets => shift ? '}' : ']',
            TerminalKey.OemPipe => shift ? '|' : '\\',
            TerminalKey.OemSemicolon => shift ? ':' : ';',
            TerminalKey.OemQuotes => shift ? '"' : '\'',
            TerminalKey.OemComma => shift ? '<' : ',',
            TerminalKey.OemPeriod => shift ? '>' : '.',
            TerminalKey.OemQuestion => shift ? '?' : '/',
            TerminalKey.OemTilde => shift ? '~' : '`',
            TerminalKey.Multiply => '*',
            TerminalKey.Add => '+',
            TerminalKey.Subtract => '-',
            TerminalKey.Decimal => '.',
            TerminalKey.Divide => '/',
            _ => null,
        };
    }
}
