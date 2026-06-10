using Avalonia.Input;
using Dotty.Terminal.Input;
using TerminalModes = Dotty.Terminal.TerminalModes;

namespace Dotty.Input;

public static class KeyEncoder
{
    public static byte[]? Encode(Key key, KeyModifiers modifiers, string? text, TerminalModes modes) =>
        TerminalInputEncoder.Encode(MapKey(key), MapModifiers(modifiers), text, modes);

    public static TerminalKey MapKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (TerminalKey)((int)TerminalKey.A + (int)(key - Key.A)),
        >= Key.D0 and <= Key.D9 => (TerminalKey)((int)TerminalKey.D0 + (int)(key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (TerminalKey)((int)TerminalKey.NumPad0 + (int)(key - Key.NumPad0)),
        Key.Enter => TerminalKey.Enter,
        Key.Back => TerminalKey.Backspace,
        Key.Tab => TerminalKey.Tab,
        Key.Escape => TerminalKey.Escape,
        Key.Up => TerminalKey.Up,
        Key.Down => TerminalKey.Down,
        Key.Right => TerminalKey.Right,
        Key.Left => TerminalKey.Left,
        Key.Home => TerminalKey.Home,
        Key.End => TerminalKey.End,
        Key.PageUp => TerminalKey.PageUp,
        Key.PageDown => TerminalKey.PageDown,
        Key.Insert => TerminalKey.Insert,
        Key.Delete => TerminalKey.Delete,
        Key.F1 => TerminalKey.F1,
        Key.F2 => TerminalKey.F2,
        Key.F3 => TerminalKey.F3,
        Key.F4 => TerminalKey.F4,
        Key.F5 => TerminalKey.F5,
        Key.F6 => TerminalKey.F6,
        Key.F7 => TerminalKey.F7,
        Key.F8 => TerminalKey.F8,
        Key.F9 => TerminalKey.F9,
        Key.F10 => TerminalKey.F10,
        Key.F11 => TerminalKey.F11,
        Key.F12 => TerminalKey.F12,
        Key.Space => TerminalKey.Space,
        Key.OemMinus => TerminalKey.OemMinus,
        Key.OemPlus => TerminalKey.OemPlus,
        Key.OemOpenBrackets => TerminalKey.OemOpenBrackets,
        Key.OemCloseBrackets => TerminalKey.OemCloseBrackets,
        Key.OemPipe => TerminalKey.OemPipe,
        Key.OemSemicolon => TerminalKey.OemSemicolon,
        Key.OemQuotes => TerminalKey.OemQuotes,
        Key.OemComma => TerminalKey.OemComma,
        Key.OemPeriod => TerminalKey.OemPeriod,
        Key.OemQuestion => TerminalKey.OemQuestion,
        Key.OemTilde => TerminalKey.OemTilde,
        Key.Multiply => TerminalKey.Multiply,
        Key.Add => TerminalKey.Add,
        Key.Subtract => TerminalKey.Subtract,
        Key.Decimal => TerminalKey.Decimal,
        Key.Divide => TerminalKey.Divide,
        _ => TerminalKey.None,
    };

    public static TerminalKeyModifiers MapModifiers(KeyModifiers modifiers)
    {
        var mapped = TerminalKeyModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            mapped |= TerminalKeyModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Control))
            mapped |= TerminalKeyModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            mapped |= TerminalKeyModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Meta))
            mapped |= TerminalKeyModifiers.Meta;
        return mapped;
    }
}
