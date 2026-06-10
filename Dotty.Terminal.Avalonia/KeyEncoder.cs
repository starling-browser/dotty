using Avalonia.Input;
using TerminalModes = Dotty.Terminal.TerminalModes;

namespace Dotty.Input;

/// <summary>
/// Maps Avalonia Key + modifiers to VT escape sequences.
/// Ported from reference input.rs.
/// </summary>
public static class KeyEncoder
{
    public static byte[]? Encode(Key key, KeyModifiers modifiers, string? text, TerminalModes modes)
    {
        bool appCursor = modes.HasFlag(TerminalModes.CursorKeys);
        bool ctrl = modifiers.HasFlag(KeyModifiers.Control);
        bool alt = modifiers.HasFlag(KeyModifiers.Alt);
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);

        // Ctrl+letter -> control code
        if (ctrl)
        {
            if (key >= Key.A && key <= Key.Z)
            {
                byte code = (byte)(key - Key.A + 1);
                return [code];
            }
            if (key == Key.Space)
                return [0];
        }

        // Named keys
        switch (key)
        {
            case Key.Enter:
                return "\r"u8.ToArray();
            case Key.Back:
                return [0x7f];
            case Key.Tab:
                return shift ? "\x1b[Z"u8.ToArray() : "\t"u8.ToArray();
            case Key.Escape:
                return [0x1b];
            case Key.Up:
                return appCursor ? "\x1bOA"u8.ToArray() : "\x1b[A"u8.ToArray();
            case Key.Down:
                return appCursor ? "\x1bOB"u8.ToArray() : "\x1b[B"u8.ToArray();
            case Key.Right:
                return appCursor ? "\x1bOC"u8.ToArray() : "\x1b[C"u8.ToArray();
            case Key.Left:
                return appCursor ? "\x1bOD"u8.ToArray() : "\x1b[D"u8.ToArray();
            case Key.Home:
                return "\x1b[H"u8.ToArray();
            case Key.End:
                return "\x1b[F"u8.ToArray();
            case Key.PageUp:
                return "\x1b[5~"u8.ToArray();
            case Key.PageDown:
                return "\x1b[6~"u8.ToArray();
            case Key.Insert:
                return "\x1b[2~"u8.ToArray();
            case Key.Delete:
                return "\x1b[3~"u8.ToArray();
            case Key.F1:
                return "\x1bOP"u8.ToArray();
            case Key.F2:
                return "\x1bOQ"u8.ToArray();
            case Key.F3:
                return "\x1bOR"u8.ToArray();
            case Key.F4:
                return "\x1bOS"u8.ToArray();
            case Key.F5:
                return "\x1b[15~"u8.ToArray();
            case Key.F6:
                return "\x1b[17~"u8.ToArray();
            case Key.F7:
                return "\x1b[18~"u8.ToArray();
            case Key.F8:
                return "\x1b[19~"u8.ToArray();
            case Key.F9:
                return "\x1b[20~"u8.ToArray();
            case Key.F10:
                return "\x1b[21~"u8.ToArray();
            case Key.F11:
                return "\x1b[23~"u8.ToArray();
            case Key.F12:
                return "\x1b[24~"u8.ToArray();
        }

        // Alt+key: ESC prefix
        if (alt && text is { Length: > 0 })
        {
            var bytes = new byte[1 + System.Text.Encoding.UTF8.GetByteCount(text)];
            bytes[0] = 0x1b;
            System.Text.Encoding.UTF8.GetBytes(text, bytes.AsSpan(1));
            return bytes;
        }

        // Printable characters (from KeySymbol or OnTextInput)
        if (text is { Length: > 0 })
            return System.Text.Encoding.UTF8.GetBytes(text);

        // Fallback: map Key enum to ASCII character directly.
        // Covers cases where neither KeySymbol nor OnTextInput provides text.
        if (!ctrl && !alt)
        {
            char? c = KeyToChar(key, shift);
            if (c.HasValue)
                return System.Text.Encoding.UTF8.GetBytes(c.Value.ToString());
        }

        return null;
    }

    private static char? KeyToChar(Key key, bool shift)
    {
        if (key >= Key.A && key <= Key.Z)
            return shift ? (char)('A' + key - Key.A) : (char)('a' + key - Key.A);

        if (key >= Key.D0 && key <= Key.D9)
        {
            if (!shift)
                return (char)('0' + key - Key.D0);
            return (key - Key.D0) switch
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

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return (char)('0' + key - Key.NumPad0);

        return key switch
        {
            Key.Space => ' ',
            Key.OemMinus => shift ? '_' : '-',
            Key.OemPlus => shift ? '+' : '=',
            Key.OemOpenBrackets => shift ? '{' : '[',
            Key.OemCloseBrackets => shift ? '}' : ']',
            Key.OemPipe => shift ? '|' : '\\',
            Key.OemSemicolon => shift ? ':' : ';',
            Key.OemQuotes => shift ? '"' : '\'',
            Key.OemComma => shift ? '<' : ',',
            Key.OemPeriod => shift ? '>' : '.',
            Key.OemQuestion => shift ? '?' : '/',
            Key.OemTilde => shift ? '~' : '`',
            Key.Multiply => '*',
            Key.Add => '+',
            Key.Subtract => '-',
            Key.Decimal => '.',
            Key.Divide => '/',
            _ => null,
        };
    }
}
