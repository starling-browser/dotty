namespace Dotty.Terminal.Input;

public static class TerminalKeyMapping
{
    public static TerminalKey MapAlphabeticKey(int platformKey, int platformA) =>
        MapContiguousKey(platformKey, platformA, TerminalKey.A, 26);

    public static TerminalKey MapDigitKey(int platformKey, int platform0) =>
        MapContiguousKey(platformKey, platform0, TerminalKey.D0, 10);

    public static TerminalKey MapNumberPadDigitKey(int platformKey, int platformNumberPad0) =>
        MapContiguousKey(platformKey, platformNumberPad0, TerminalKey.NumPad0, 10);

    public static TerminalKey MapContiguousKey(
        int platformKey,
        int firstPlatformKey,
        TerminalKey firstTerminalKey,
        int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var offset = platformKey - firstPlatformKey;
        return (uint)offset < (uint)count
            ? (TerminalKey)((int)firstTerminalKey + offset)
            : TerminalKey.None;
    }

    public static TerminalKey MapWindowsVirtualKey(int virtualKey)
    {
        var mapped = MapAlphabeticKey(virtualKey, WindowsVirtualKeys.A);
        if (mapped != TerminalKey.None)
            return mapped;

        mapped = MapDigitKey(virtualKey, WindowsVirtualKeys.Number0);
        if (mapped != TerminalKey.None)
            return mapped;

        mapped = MapNumberPadDigitKey(virtualKey, WindowsVirtualKeys.NumberPad0);
        if (mapped != TerminalKey.None)
            return mapped;

        return virtualKey switch
        {
            WindowsVirtualKeys.Enter => TerminalKey.Enter,
            WindowsVirtualKeys.Back => TerminalKey.Backspace,
            WindowsVirtualKeys.Tab => TerminalKey.Tab,
            WindowsVirtualKeys.Escape => TerminalKey.Escape,
            WindowsVirtualKeys.Up => TerminalKey.Up,
            WindowsVirtualKeys.Down => TerminalKey.Down,
            WindowsVirtualKeys.Right => TerminalKey.Right,
            WindowsVirtualKeys.Left => TerminalKey.Left,
            WindowsVirtualKeys.Home => TerminalKey.Home,
            WindowsVirtualKeys.End => TerminalKey.End,
            WindowsVirtualKeys.PageUp => TerminalKey.PageUp,
            WindowsVirtualKeys.PageDown => TerminalKey.PageDown,
            WindowsVirtualKeys.Insert => TerminalKey.Insert,
            WindowsVirtualKeys.Delete => TerminalKey.Delete,
            WindowsVirtualKeys.F1 => TerminalKey.F1,
            WindowsVirtualKeys.F2 => TerminalKey.F2,
            WindowsVirtualKeys.F3 => TerminalKey.F3,
            WindowsVirtualKeys.F4 => TerminalKey.F4,
            WindowsVirtualKeys.F5 => TerminalKey.F5,
            WindowsVirtualKeys.F6 => TerminalKey.F6,
            WindowsVirtualKeys.F7 => TerminalKey.F7,
            WindowsVirtualKeys.F8 => TerminalKey.F8,
            WindowsVirtualKeys.F9 => TerminalKey.F9,
            WindowsVirtualKeys.F10 => TerminalKey.F10,
            WindowsVirtualKeys.F11 => TerminalKey.F11,
            WindowsVirtualKeys.F12 => TerminalKey.F12,
            WindowsVirtualKeys.Space => TerminalKey.Space,
            WindowsVirtualKeys.OemMinus => TerminalKey.OemMinus,
            WindowsVirtualKeys.OemPlus => TerminalKey.OemPlus,
            WindowsVirtualKeys.OemOpenBrackets => TerminalKey.OemOpenBrackets,
            WindowsVirtualKeys.OemCloseBrackets => TerminalKey.OemCloseBrackets,
            WindowsVirtualKeys.OemPipe => TerminalKey.OemPipe,
            WindowsVirtualKeys.OemSemicolon => TerminalKey.OemSemicolon,
            WindowsVirtualKeys.OemQuotes => TerminalKey.OemQuotes,
            WindowsVirtualKeys.OemComma => TerminalKey.OemComma,
            WindowsVirtualKeys.OemPeriod => TerminalKey.OemPeriod,
            WindowsVirtualKeys.OemQuestion => TerminalKey.OemQuestion,
            WindowsVirtualKeys.OemTilde => TerminalKey.OemTilde,
            WindowsVirtualKeys.Multiply => TerminalKey.Multiply,
            WindowsVirtualKeys.Add => TerminalKey.Add,
            WindowsVirtualKeys.Subtract => TerminalKey.Subtract,
            WindowsVirtualKeys.Decimal => TerminalKey.Decimal,
            WindowsVirtualKeys.Divide => TerminalKey.Divide,
            _ => TerminalKey.None,
        };
    }

    public static bool IsPrintable(TerminalKey key) =>
        (key >= TerminalKey.A && key <= TerminalKey.Z)
        || (key >= TerminalKey.D0 && key <= TerminalKey.D9)
        || (key >= TerminalKey.NumPad0 && key <= TerminalKey.NumPad9)
        || key is TerminalKey.Space
            or TerminalKey.OemMinus
            or TerminalKey.OemPlus
            or TerminalKey.OemOpenBrackets
            or TerminalKey.OemCloseBrackets
            or TerminalKey.OemPipe
            or TerminalKey.OemSemicolon
            or TerminalKey.OemQuotes
            or TerminalKey.OemComma
            or TerminalKey.OemPeriod
            or TerminalKey.OemQuestion
            or TerminalKey.OemTilde
            or TerminalKey.Multiply
            or TerminalKey.Add
            or TerminalKey.Subtract
            or TerminalKey.Decimal
            or TerminalKey.Divide;

    private static class WindowsVirtualKeys
    {
        public const int Back = 0x08;
        public const int Tab = 0x09;
        public const int Enter = 0x0d;
        public const int Escape = 0x1b;
        public const int Space = 0x20;
        public const int PageUp = 0x21;
        public const int PageDown = 0x22;
        public const int End = 0x23;
        public const int Home = 0x24;
        public const int Left = 0x25;
        public const int Up = 0x26;
        public const int Right = 0x27;
        public const int Down = 0x28;
        public const int Insert = 0x2d;
        public const int Delete = 0x2e;
        public const int Number0 = 0x30;
        public const int A = 0x41;
        public const int NumberPad0 = 0x60;
        public const int Multiply = 0x6a;
        public const int Add = 0x6b;
        public const int Subtract = 0x6d;
        public const int Decimal = 0x6e;
        public const int Divide = 0x6f;
        public const int F1 = 0x70;
        public const int F2 = 0x71;
        public const int F3 = 0x72;
        public const int F4 = 0x73;
        public const int F5 = 0x74;
        public const int F6 = 0x75;
        public const int F7 = 0x76;
        public const int F8 = 0x77;
        public const int F9 = 0x78;
        public const int F10 = 0x79;
        public const int F11 = 0x7a;
        public const int F12 = 0x7b;
        public const int OemSemicolon = 0xba;
        public const int OemPlus = 0xbb;
        public const int OemComma = 0xbc;
        public const int OemMinus = 0xbd;
        public const int OemPeriod = 0xbe;
        public const int OemQuestion = 0xbf;
        public const int OemTilde = 0xc0;
        public const int OemOpenBrackets = 0xdb;
        public const int OemPipe = 0xdc;
        public const int OemCloseBrackets = 0xdd;
        public const int OemQuotes = 0xde;
    }
}
