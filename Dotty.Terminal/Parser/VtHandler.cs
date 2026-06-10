using System.Text;

namespace Dotty.Terminal.Parser;

/// <summary>
/// VT handler that dispatches escape sequences to the Terminal state machine.
/// Ported from reference handler.rs.
/// </summary>
public class VtHandler : IVtHandler
{
    private readonly Terminal _terminal;

    public VtHandler(Terminal terminal)
    {
        _terminal = terminal;
    }

    public void Print(char c)
    {
        _terminal.PutChar(c);
    }

    public void Execute(byte b)
    {
        switch (b)
        {
            case 0x07: // BEL — visual bell
                _terminal.Bell();
                break;
            case 0x08: // BS
                _terminal.Backspace();
                break;
            case 0x09: // HT
                _terminal.Tab();
                break;
            case 0x0A or 0x0B or 0x0C: // LF, VT, FF
                _terminal.Linefeed();
                break;
            case 0x0D: // CR
                _terminal.CarriageReturn();
                break;
            case 0x0E: // SO — Shift Out: activate G1
                _terminal.SetActiveCharset(1);
                break;
            case 0x0F: // SI — Shift In: activate G0
                _terminal.SetActiveCharset(0);
                break;
        }
    }

    public void CsiDispatch(ReadOnlySpan<ushort> parameters, ReadOnlySpan<byte> intermediates, char action)
    {
        ushort first = parameters.Length > 0 ? parameters[0] : (ushort)0;
        ushort second = parameters.Length > 1 ? parameters[1] : (ushort)0;

        switch (action)
        {
            case 'A': // CUU — Cursor Up
                _terminal.MoveCursorUp(Math.Max(first, (ushort)1));
                break;

            case 'B': // CUD — Cursor Down
                _terminal.MoveCursorDown(Math.Max(first, (ushort)1));
                break;

            case 'C': // CUF — Cursor Forward
                _terminal.MoveCursorForward(Math.Max(first, (ushort)1));
                break;

            case 'D': // CUB — Cursor Backward
                _terminal.MoveCursorBackward(Math.Max(first, (ushort)1));
                break;

            case 'E': // CNL — Cursor Next Line
            {
                _terminal.MoveCursorDown(Math.Max(first, (ushort)1));
                _terminal.CarriageReturn();
                break;
            }

            case 'F': // CPL — Cursor Previous Line
            {
                _terminal.MoveCursorUp(Math.Max(first, (ushort)1));
                _terminal.CarriageReturn();
                break;
            }

            case 'H' or 'f': // CUP — Cursor Position
            {
                ushort row = (ushort)(Math.Max(first, (ushort)1) - 1);
                ushort col = (ushort)(Math.Max(second, (ushort)1) - 1);
                if (_terminal.Modes.HasFlag(TerminalModes.OriginMode))
                {
                    _terminal.SetCursorPos(col, (ushort)(row + _terminal.ScrollTopRow));
                }
                else
                {
                    _terminal.SetCursorPos(col, row);
                }
                break;
            }

            case 'J': // ED — Erase in Display
                _terminal.EraseInDisplay(first);
                break;

            case 'K': // EL — Erase in Line
                _terminal.EraseInLine(first);
                break;

            case 'L': // IL — Insert Lines
            {
                ushort n = Math.Max(first, (ushort)1);
                ushort row = _terminal.CursorPos.Row;
                _terminal.GridMut.InsertLines(row, n);
                for (ushort r = row; r < _terminal.ScrollBottomRow; r++)
                    _terminal.DamageReportRef.MarkRow(r);
                break;
            }

            case 'M': // DL — Delete Lines
            {
                ushort n = Math.Max(first, (ushort)1);
                ushort row = _terminal.CursorPos.Row;
                _terminal.GridMut.DeleteLines(row, n);
                for (ushort r = row; r < _terminal.ScrollBottomRow; r++)
                    _terminal.DamageReportRef.MarkRow(r);
                break;
            }

            case 'P': // DCH — Delete Characters
            {
                ushort n = Math.Max(first, (ushort)1);
                var pos = _terminal.CursorPos;
                _terminal.GridMut.DeleteCells(pos.Col, pos.Row, n);
                _terminal.DamageReportRef.MarkRow(pos.Row);
                break;
            }

            case 'S': // SU — Scroll Up
            {
                ushort n = Math.Max(first, (ushort)1);
                _terminal.GridMut.ScrollUp(n);
                _terminal.DamageReportRef.MarkAllRows(_terminal.Size.Rows);
                break;
            }

            case 'T': // SD — Scroll Down
            {
                ushort n = Math.Max(first, (ushort)1);
                _terminal.GridMut.ScrollDown(n);
                _terminal.DamageReportRef.MarkAllRows(_terminal.Size.Rows);
                break;
            }

            case 'b': // REP — Repeat preceding graphic character
            {
                ushort n = Math.Max(first, (ushort)1);
                char c = _terminal.LastPrintedChar;
                for (int j = 0; j < n; j++)
                    _terminal.PutChar(c);
                break;
            }

            case '@': // ICH — Insert Characters
            {
                ushort n = Math.Max(first, (ushort)1);
                var pos = _terminal.CursorPos;
                _terminal.GridMut.InsertCells(pos.Col, pos.Row, n);
                _terminal.DamageReportRef.MarkRow(pos.Row);
                break;
            }

            case 'm': // SGR — Select Graphic Rendition
                ParseSgr(parameters);
                break;

            case 'n': // DSR — Device Status Report
                switch (first)
                {
                    case 5:
                        _terminal.PushResponse("\x1b[0n"u8);
                        break;
                    case 6:
                        var pos = _terminal.CursorPos;
                        _terminal.PushResponse(Encoding.ASCII.GetBytes(
                            $"\x1b[{pos.Row + 1};{pos.Col + 1}R"));
                        break;
                }
                break;

            case 's' when intermediates.Length == 0: // SCOSC — Save Cursor Position
                _terminal.SaveCursor();
                break;

            case 'u' when intermediates.Length == 0: // SCORC — Restore Cursor Position
                _terminal.RestoreCursor();
                break;

            case 'r': // DECSTBM — Set Top and Bottom Margins
            {
                ushort top = first == 0 ? (ushort)1 : first;
                ushort bottom = second == 0 ? _terminal.Size.Rows : second;
                _terminal.GridMut.SetScrollRegion((ushort)(top - 1), bottom);
                if (_terminal.Modes.HasFlag(TerminalModes.OriginMode))
                    _terminal.SetCursorPos(0, (ushort)(top - 1));
                else
                    _terminal.SetCursorPos(0, 0);
                break;
            }

            case 'h' or 'l': // SM/RM — Set/Reset Mode
            {
                bool enable = action == 'h';
                bool isPrivate = intermediates.Contains((byte)'?');
                if (isPrivate)
                {
                    for (int i = 0; i < parameters.Length; i++)
                        HandleDecMode(parameters[i], enable);
                }
                else
                {
                    switch (first)
                    {
                        case 4:
                            _terminal.SetMode(TerminalModes.InsertMode, enable);
                            break;
                        case 20:
                            _terminal.SetMode(TerminalModes.LinefeedNewline, enable);
                            break;
                    }
                }
                break;
            }

            case 'q' when intermediates is [(byte)' ']: // DECSCUSR — Set Cursor Style
            {
                var shape = first switch
                {
                    0 or 1 or 2 => CursorShape.Block,
                    3 or 4 => CursorShape.Underline,
                    5 or 6 => CursorShape.Bar,
                    _ => CursorShape.Block,
                };
                // Odd values = blinking, even values = steady (0 = default blinking)
                bool blinking = first == 0 || (first % 2) == 1;
                _terminal.SetCursorShape(shape);
                _terminal.SetCursorBlinking(blinking);
                break;
            }

            case 'G': // CHA — Cursor Character Absolute
            {
                ushort col = (ushort)(Math.Max(first, (ushort)1) - 1);
                _terminal.SetCursorCol(col);
                break;
            }

            case 'd': // VPA — Vertical Position Absolute
            {
                ushort row = (ushort)(Math.Max(first, (ushort)1) - 1);
                _terminal.SetCursorRow(row);
                break;
            }

            case 'X': // ECH — Erase Characters
            {
                int n = Math.Max(first, (ushort)1);
                var pos = _terminal.CursorPos;
                ushort cols = _terminal.Size.Cols;
                for (int i = 0; i < n; i++)
                {
                    ushort col = (ushort)(pos.Col + i);
                    if (col >= cols) break;
                    _terminal.GridMut.CellAt(col, pos.Row).Reset();
                }
                _terminal.DamageReportRef.MarkRow(pos.Row);
                break;
            }

            case 'g': // TBC — Tab Clear
                switch (first)
                {
                    case 0:
                        _terminal.ClearTabStop(_terminal.CursorPos.Col);
                        break;
                    case 3:
                        _terminal.ClearAllTabStops();
                        break;
                }
                break;

            case 'c': // DA — Device Attributes
                if (intermediates.Contains((byte)'>'))
                {
                    // DA2 — Secondary Device Attributes
                    // Response: VT220-compatible, firmware version 1, no ROM
                    _terminal.PushResponse("\x1b[>1;1;0c"u8);
                }
                else if (first == 0)
                {
                    _terminal.PushResponse("\x1b[?62;22c"u8);
                }
                break;
        }
    }

    public void EscDispatch(ReadOnlySpan<byte> intermediates, byte b)
    {
        // Character set designation: ESC ( X or ESC ) X
        if (intermediates.Length == 1 && intermediates[0] is (byte)'(' or (byte)')')
        {
            int gSet = intermediates[0] == (byte)'(' ? 0 : 1;
            var cs = b switch
            {
                (byte)'0' => CharacterSet.LineDrawing,
                (byte)'B' or (byte)'A' => CharacterSet.Ascii,
                _ => CharacterSet.Ascii,
            };
            _terminal.DesignateCharset(gSet, cs);
            return;
        }

        switch (b)
        {
            case (byte)'c': // RIS — Full Reset
                _terminal.Reset();
                break;
            case (byte)'D': // IND — Index
                _terminal.Linefeed();
                break;
            case (byte)'M': // RI — Reverse Index
                _terminal.ReverseIndex();
                break;
            case (byte)'7': // DECSC — Save Cursor
                _terminal.SaveCursor();
                break;
            case (byte)'8' when intermediates.Length == 0: // DECRC — Restore Cursor
                _terminal.RestoreCursor();
                break;
            case (byte)'8' when intermediates is [(byte)'#']: // DECALN — Screen alignment
                FillScreenWithE();
                break;
            case (byte)'=': // DECKPAM
                _terminal.SetMode(TerminalModes.AppKeypad, true);
                break;
            case (byte)'>': // DECKPNM
                _terminal.SetMode(TerminalModes.AppKeypad, false);
                break;
            case (byte)'H': // HTS — Horizontal Tab Stop
                _terminal.SetTabStop(_terminal.CursorPos.Col);
                break;
        }
    }

    private void FillScreenWithE()
    {
        var size = _terminal.Size;
        for (ushort row = 0; row < size.Rows; row++)
        {
            for (ushort col = 0; col < size.Cols; col++)
            {
                ref var cell = ref _terminal.GridMut.CellAt(col, row);
                cell.Codepoint = 'E';
            }
            _terminal.DamageReportRef.MarkRow(row);
        }
    }

    public void OscDispatch(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0) return;

        // Split on first ';' to get command number and payload
        int semiIdx = payload.IndexOf((byte)';');
        if (semiIdx < 0)
        {
            // Command number only, no payload
            return;
        }

        var cmdBytes = payload[..semiIdx];
        var data = payload[(semiIdx + 1)..];

        // Parse command number
        if (!TryParseAsciiNumber(cmdBytes, out int cmd)) return;

        switch (cmd)
        {
            case 0 or 2: // Set Window Title
                _terminal.SetTitle(Encoding.UTF8.GetString(data));
                break;
            case 7: // Current Working Directory
                _terminal.SetWorkingDirectory(Encoding.UTF8.GetString(data));
                break;
            case 52: // Clipboard access
                HandleOsc52(data);
                break;
        }
    }

    private void ParseSgr(ReadOnlySpan<ushort> parameters)
    {
        if (parameters.Length == 0)
        {
            _terminal.ResetPen();
            return;
        }

        int i = 0;
        while (i < parameters.Length)
        {
            ushort p = parameters[i];
            switch (p)
            {
                case 0:
                    _terminal.ResetPen();
                    break;
                case 1:
                    _terminal.SetPenAttr(CellAttributes.Bold, true);
                    break;
                case 2:
                    _terminal.SetPenAttr(CellAttributes.Dim, true);
                    break;
                case 3:
                    _terminal.SetPenAttr(CellAttributes.Italic, true);
                    break;
                case 4:
                    _terminal.SetPenAttr(CellAttributes.Underline, true);
                    break;
                case 5:
                    _terminal.SetPenAttr(CellAttributes.Blink, true);
                    break;
                case 7:
                    _terminal.SetPenAttr(CellAttributes.Inverse, true);
                    break;
                case 8:
                    _terminal.SetPenAttr(CellAttributes.Hidden, true);
                    break;
                case 9:
                    _terminal.SetPenAttr(CellAttributes.Strikethrough, true);
                    break;
                case 21:
                    _terminal.SetPenAttr(CellAttributes.Bold, false);
                    break;
                case 22:
                    _terminal.SetPenAttr(CellAttributes.Bold, false);
                    _terminal.SetPenAttr(CellAttributes.Dim, false);
                    break;
                case 23:
                    _terminal.SetPenAttr(CellAttributes.Italic, false);
                    break;
                case 24:
                    _terminal.SetPenAttr(CellAttributes.Underline, false);
                    break;
                case 25:
                    _terminal.SetPenAttr(CellAttributes.Blink, false);
                    break;
                case 27:
                    _terminal.SetPenAttr(CellAttributes.Inverse, false);
                    break;
                case 28:
                    _terminal.SetPenAttr(CellAttributes.Hidden, false);
                    break;
                case 29:
                    _terminal.SetPenAttr(CellAttributes.Strikethrough, false);
                    break;

                // Foreground colors
                case >= 30 and <= 37:
                    _terminal.SetPenFg(Color.Ansi((byte)(p - 30)));
                    break;
                case 38: // Extended foreground
                    if (TryParseExtendedColor(parameters, ref i, out var fgColor))
                        _terminal.SetPenFg(fgColor);
                    break;
                case 39:
                    _terminal.SetPenFg(Color.DefaultColor);
                    break;
                case >= 90 and <= 97:
                    _terminal.SetPenFg(Color.AnsiBright((byte)(p - 90)));
                    break;

                // Background colors
                case >= 40 and <= 47:
                    _terminal.SetPenBg(Color.Ansi((byte)(p - 40)));
                    break;
                case 48: // Extended background
                    if (TryParseExtendedColor(parameters, ref i, out var bgColor))
                        _terminal.SetPenBg(bgColor);
                    break;
                case 49:
                    _terminal.SetPenBg(Color.DefaultColor);
                    break;
                case >= 100 and <= 107:
                    _terminal.SetPenBg(Color.AnsiBright((byte)(p - 100)));
                    break;
            }

            i++;
        }
    }

    private static bool TryParseExtendedColor(ReadOnlySpan<ushort> parameters, ref int i, out Color color)
    {
        color = Color.DefaultColor;

        if (i + 1 >= parameters.Length) return false;

        switch (parameters[i + 1])
        {
            case 5: // 256-color
                if (i + 2 >= parameters.Length) { i += 1; return false; }
                color = Color.Indexed((byte)parameters[i + 2]);
                i += 2;
                return true;

            case 2: // True color
                if (i + 4 >= parameters.Length) { i += 1; return false; }
                color = Color.FromRgb(
                    (byte)parameters[i + 2],
                    (byte)parameters[i + 3],
                    (byte)parameters[i + 4]);
                i += 4;
                return true;

            default:
                return false;
        }
    }

    private void HandleDecMode(ushort mode, bool enable)
    {
        switch (mode)
        {
            case 1:
                _terminal.SetMode(TerminalModes.CursorKeys, enable);
                break;
            case 6:
                _terminal.SetMode(TerminalModes.OriginMode, enable);
                if (enable)
                    _terminal.SetCursorPos(0, _terminal.ScrollTopRow);
                else
                    _terminal.SetCursorPos(0, 0);
                break;
            case 7:
                _terminal.SetMode(TerminalModes.AutoWrap, enable);
                break;
            case 9: // X10 mouse tracking
                ClearMouseModes();
                if (enable) _terminal.SetMode(TerminalModes.MouseX10, true);
                break;
            case 25:
                _terminal.SetCursorVisible(enable);
                break;
            case 47: // Alt screen (no save cursor, no clear)
                _terminal.SwitchAltScreen(enable);
                break;
            case 1000: // Normal mouse tracking
                ClearMouseModes();
                if (enable) _terminal.SetMode(TerminalModes.MouseNormal, true);
                break;
            case 1002: // Button-event mouse tracking
                ClearMouseModes();
                if (enable) _terminal.SetMode(TerminalModes.MouseButtonEvent, true);
                break;
            case 1003: // Any-event mouse tracking
                ClearMouseModes();
                if (enable) _terminal.SetMode(TerminalModes.MouseAnyEvent, true);
                break;
            case 1004:
                _terminal.SetMode(TerminalModes.FocusTracking, enable);
                break;
            case 1006: // SGR extended mouse format
                _terminal.SetMode(TerminalModes.MouseSgr, enable);
                break;
            case 1047: // Alt screen buffer (no save cursor)
                _terminal.SwitchAltScreen(enable);
                break;
            case 1048: // Save/restore cursor only
                if (enable) _terminal.SaveCursor();
                else _terminal.RestoreCursor();
                break;
            case 1049:
                _terminal.SwitchAltScreen(enable);
                break;
            case 2004:
                _terminal.SetMode(TerminalModes.BracketedPaste, enable);
                break;
        }
    }

    private void ClearMouseModes()
    {
        _terminal.SetMode(TerminalModes.MouseX10, false);
        _terminal.SetMode(TerminalModes.MouseNormal, false);
        _terminal.SetMode(TerminalModes.MouseButtonEvent, false);
        _terminal.SetMode(TerminalModes.MouseAnyEvent, false);
    }

    private void HandleOsc52(ReadOnlySpan<byte> data)
    {
        // Format: selection;base64data
        // selection is typically "c" (clipboard) or "p" (primary)
        int semiIdx = data.IndexOf((byte)';');
        if (semiIdx < 0) return;

        var b64 = data[(semiIdx + 1)..];
        if (b64.Length == 0) return;

        // "?" means query — we don't support querying clipboard
        if (b64.Length == 1 && b64[0] == (byte)'?') return;

        try
        {
            var b64Str = Encoding.ASCII.GetString(b64);
            var decoded = Convert.FromBase64String(b64Str);
            var text = Encoding.UTF8.GetString(decoded);
            _terminal.SetClipboard(text);
        }
        catch
        {
            // Invalid base64 — ignore
        }
    }

    private static bool TryParseAsciiNumber(ReadOnlySpan<byte> bytes, out int result)
    {
        result = 0;
        if (bytes.Length == 0) return false;
        foreach (byte b in bytes)
        {
            if (b < (byte)'0' || b > (byte)'9') return false;
            result = result * 10 + (b - '0');
        }
        return true;
    }
}
