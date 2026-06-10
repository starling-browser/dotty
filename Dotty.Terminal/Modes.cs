namespace Dotty.Terminal;

[Flags]
public enum TerminalModes : uint
{
    None = 0,
    /// DECCKM: Cursor key mode (application vs normal).
    CursorKeys = 0x0001,
    /// DECAWM: Auto-wrap mode.
    AutoWrap = 0x0002,
    /// DECTCEM: Cursor visible.
    CursorVisible = 0x0004,
    /// Alt screen buffer active.
    AltScreen = 0x0008,
    /// DECOM: Origin mode (cursor relative to scroll region).
    OriginMode = 0x0010,
    /// Application keypad mode (DECKPAM).
    AppKeypad = 0x0020,
    /// Bracketed paste mode.
    BracketedPaste = 0x0040,
    /// Focus tracking.
    FocusTracking = 0x0080,
    /// LNM: Line feed / new line mode.
    LinefeedNewline = 0x0100,
    /// Insert mode (IRM).
    InsertMode = 0x0200,
    /// Mouse tracking: X10 mode (button press only).
    MouseX10 = 0x0400,
    /// Mouse tracking: normal mode (press + release).
    MouseNormal = 0x0800,
    /// Mouse tracking: button-event tracking (press + release + drag).
    MouseButtonEvent = 0x1000,
    /// Mouse tracking: any-event tracking (all motion).
    MouseAnyEvent = 0x2000,
    /// SGR extended mouse mode (1006).
    MouseSgr = 0x4000,
}

public static class TerminalModesExtensions
{
    public static TerminalModes Initial => TerminalModes.AutoWrap | TerminalModes.CursorVisible;
}
