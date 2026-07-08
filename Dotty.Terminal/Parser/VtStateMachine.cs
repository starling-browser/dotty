namespace Dotty.Terminal.Parser;

/// <summary>
/// DEC ANSI VT parser state machine.
/// Based on https://vt100.net/emu/dec_ansi_parser
/// </summary>
public class VtStateMachine
{
    private enum State
    {
        Ground,
        Escape,
        EscapeIntermediate,
        CsiEntry,
        CsiParam,
        CsiIntermediate,
        CsiIgnore,
        OscString,
        DcsEntry,
        DcsParam,
        DcsIntermediate,
        DcsPassthrough,
        DcsIgnore,
        SosPmApcString,
    }

    private State _state = State.Ground;

    // CSI parameter collection
    private readonly ushort[] _params = new ushort[16];
    private int _paramCount;
    private bool _paramHasDigit;

    // Intermediate bytes
    private readonly byte[] _intermediates = new byte[4];
    private int _intermediateCount;

    // OSC payload. Sized for OSC 8 hyperlink URIs (the realistic long-OSC case);
    // bytes past the cap are dropped rather than growing the buffer.
    private readonly byte[] _oscPayload = new byte[4096];
    private int _oscPayloadLen;

    // UTF-8 decoding
    private int _utf8Remaining;
    private int _utf8Codepoint;

    public void Advance(IVtHandler handler, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            ProcessByte(handler, b);
        }
    }

    private void ProcessByte(IVtHandler handler, byte b)
    {
        // Handle UTF-8 continuation bytes in Ground state
        if (_utf8Remaining > 0)
        {
            if ((b & 0xC0) == 0x80)
            {
                _utf8Codepoint = (_utf8Codepoint << 6) | (b & 0x3F);
                _utf8Remaining--;
                if (_utf8Remaining == 0)
                {
                    handler.Print((char)_utf8Codepoint);
                }
                return;
            }
            // Invalid continuation — reset
            _utf8Remaining = 0;
        }

        // Anywhere transitions (C0 controls that override current state)
        switch (b)
        {
            case 0x18 or 0x1A: // CAN, SUB
                _state = State.Ground;
                return;
            case 0x1B: // ESC
                // ESC inside an OSC string is the start of a String Terminator
                // (ST = ESC \): finalize the OSC now, then let the trailing '\'
                // be consumed in the Escape state. Without this, ST-terminated
                // OSC (titles, cwd, OSC 8 hyperlinks) would never dispatch — only
                // the BEL terminator would.
                if (_state == State.OscString)
                    handler.OscDispatch(OscPayload);
                TransitionTo(State.Escape);
                return;
        }

        switch (_state)
        {
            case State.Ground:
                GroundState(handler, b);
                break;
            case State.Escape:
                EscapeState(handler, b);
                break;
            case State.EscapeIntermediate:
                EscapeIntermediateState(handler, b);
                break;
            case State.CsiEntry:
                CsiEntryState(handler, b);
                break;
            case State.CsiParam:
                CsiParamState(handler, b);
                break;
            case State.CsiIntermediate:
                CsiIntermediateState(handler, b);
                break;
            case State.CsiIgnore:
                CsiIgnoreState(b);
                break;
            case State.OscString:
                OscStringState(handler, b);
                break;
            case State.DcsEntry:
            case State.DcsParam:
            case State.DcsIntermediate:
            case State.DcsPassthrough:
            case State.DcsIgnore:
                // DCS: minimal handling — just consume until ST
                DcsState(b);
                break;
            case State.SosPmApcString:
                // Consume until ST
                if (b == 0x9C) _state = State.Ground;
                break;
        }
    }

    private void GroundState(IVtHandler handler, byte b)
    {
        if (b < 0x20)
        {
            // C0 control
            handler.Execute(b);
        }
        else if (b < 0x80)
        {
            // Printable ASCII
            handler.Print((char)b);
        }
        else if (b < 0xC0)
        {
            // C1 control range or invalid — ignore
        }
        else if (b < 0xE0)
        {
            // 2-byte UTF-8
            _utf8Codepoint = b & 0x1F;
            _utf8Remaining = 1;
        }
        else if (b < 0xF0)
        {
            // 3-byte UTF-8
            _utf8Codepoint = b & 0x0F;
            _utf8Remaining = 2;
        }
        else if (b < 0xF8)
        {
            // 4-byte UTF-8
            _utf8Codepoint = b & 0x07;
            _utf8Remaining = 3;
        }
    }

    private void EscapeState(IVtHandler handler, byte b)
    {
        if (b < 0x20)
        {
            handler.Execute(b);
            return;
        }

        switch (b)
        {
            case (byte)'[': // CSI
                TransitionTo(State.CsiEntry);
                break;
            case (byte)']': // OSC
                TransitionTo(State.OscString);
                break;
            case (byte)'P': // DCS
                TransitionTo(State.DcsEntry);
                break;
            case (byte)'X' or (byte)'^' or (byte)'_': // SOS, PM, APC
                _state = State.SosPmApcString;
                break;
            case >= 0x20 and <= 0x2F: // Intermediate
                CollectIntermediate(b);
                _state = State.EscapeIntermediate;
                break;
            case >= 0x30 and <= 0x7E: // Final byte
                handler.EscDispatch(Intermediates, b);
                _state = State.Ground;
                break;
            default:
                _state = State.Ground;
                break;
        }
    }

    private void EscapeIntermediateState(IVtHandler handler, byte b)
    {
        if (b < 0x20)
        {
            handler.Execute(b);
            return;
        }

        if (b is >= 0x20 and <= 0x2F)
        {
            CollectIntermediate(b);
        }
        else if (b is >= 0x30 and <= 0x7E)
        {
            handler.EscDispatch(Intermediates, b);
            _state = State.Ground;
        }
        else
        {
            _state = State.Ground;
        }
    }

    private void CsiEntryState(IVtHandler handler, byte b)
    {
        if (b < 0x20)
        {
            handler.Execute(b);
            return;
        }

        if (b is >= 0x30 and <= 0x39) // Digit
        {
            _params[0] = (ushort)(b - 0x30);
            _paramCount = 1;
            _paramHasDigit = true;
            _state = State.CsiParam;
        }
        else if (b == 0x3B) // Semicolon
        {
            _paramCount = 2; // implicit 0 for first param
            _paramHasDigit = false;
            _state = State.CsiParam;
        }
        else if (b is >= 0x3C and <= 0x3F) // Private marker (?, >, =, <)
        {
            CollectIntermediate(b);
            _state = State.CsiParam;
        }
        else if (b is >= 0x20 and <= 0x2F) // Intermediate
        {
            CollectIntermediate(b);
            _state = State.CsiIntermediate;
        }
        else if (b is >= 0x40 and <= 0x7E) // Final
        {
            handler.CsiDispatch(Parameters, Intermediates, (char)b);
            _state = State.Ground;
        }
        else
        {
            _state = State.CsiIgnore;
        }
    }

    private void CsiParamState(IVtHandler handler, byte b)
    {
        if (b < 0x20)
        {
            handler.Execute(b);
            return;
        }

        if (b is >= 0x30 and <= 0x39) // Digit
        {
            if (_paramCount == 0)
            {
                _paramCount = 1;
            }
            int idx = _paramCount - 1;
            if (idx < _params.Length)
            {
                _params[idx] = (ushort)(_params[idx] * 10 + (b - 0x30));
            }
            _paramHasDigit = true;
        }
        else if (b == 0x3B) // Semicolon
        {
            if (_paramCount == 0) _paramCount = 1; // implicit first param = 0
            if (_paramCount < _params.Length)
            {
                _paramCount++;
                _params[_paramCount - 1] = 0;
            }
            _paramHasDigit = false;
        }
        else if (b is >= 0x3C and <= 0x3F) // Private marker within params — just collect
        {
            CollectIntermediate(b);
        }
        else if (b is >= 0x20 and <= 0x2F) // Intermediate
        {
            CollectIntermediate(b);
            _state = State.CsiIntermediate;
        }
        else if (b is >= 0x40 and <= 0x7E) // Final
        {
            handler.CsiDispatch(Parameters, Intermediates, (char)b);
            _state = State.Ground;
        }
        else
        {
            _state = State.CsiIgnore;
        }
    }

    private void CsiIntermediateState(IVtHandler handler, byte b)
    {
        if (b < 0x20)
        {
            handler.Execute(b);
            return;
        }

        if (b is >= 0x20 and <= 0x2F)
        {
            CollectIntermediate(b);
        }
        else if (b is >= 0x40 and <= 0x7E)
        {
            handler.CsiDispatch(Parameters, Intermediates, (char)b);
            _state = State.Ground;
        }
        else
        {
            _state = State.CsiIgnore;
        }
    }

    private void CsiIgnoreState(byte b)
    {
        if (b is >= 0x40 and <= 0x7E)
            _state = State.Ground;
    }

    private void OscStringState(IVtHandler handler, byte b)
    {
        switch (b)
        {
            case 0x07: // BEL terminates OSC
                handler.OscDispatch(OscPayload);
                _state = State.Ground;
                break;
            case 0x9C: // ST (8-bit)
                handler.OscDispatch(OscPayload);
                _state = State.Ground;
                break;
            // NB: ESC (start of a 7-bit ST) is intercepted by the "anywhere"
            // transition in ProcessByte, which dispatches the OSC before this
            // state ever sees it.
            default:
                if (_oscPayloadLen < _oscPayload.Length)
                    _oscPayload[_oscPayloadLen++] = b;
                break;
        }
    }

    private void DcsState(byte b)
    {
        // Minimal DCS handling: consume bytes until ST
        if (b == 0x9C) _state = State.Ground;
        else if (b == 0x1B) TransitionTo(State.Escape);
    }

    private void TransitionTo(State newState)
    {
        // On entry, clear state for the new context
        switch (newState)
        {
            case State.Escape:
                _intermediateCount = 0;
                break;
            case State.CsiEntry:
                _paramCount = 0;
                _paramHasDigit = false;
                _intermediateCount = 0;
                Array.Clear(_params);
                break;
            case State.OscString:
                _oscPayloadLen = 0;
                break;
        }
        _state = newState;
    }

    private void CollectIntermediate(byte b)
    {
        if (_intermediateCount < _intermediates.Length)
            _intermediates[_intermediateCount++] = b;
    }

    private ReadOnlySpan<ushort> Parameters => _params.AsSpan(0, _paramCount);
    private ReadOnlySpan<byte> Intermediates => _intermediates.AsSpan(0, _intermediateCount);
    private ReadOnlySpan<byte> OscPayload => _oscPayload.AsSpan(0, _oscPayloadLen);
}
