namespace Dotty.Terminal.Parser;

/// <summary>
/// Handler interface for VT escape sequence dispatch.
/// Equivalent to the vte::Perform trait in the Rust reference.
/// </summary>
public interface IVtHandler
{
    /// <summary>A printable character was received.</summary>
    void Print(char c);

    /// <summary>A C0/C1 control character was received.</summary>
    void Execute(byte b);

    /// <summary>A CSI sequence was dispatched.</summary>
    void CsiDispatch(ReadOnlySpan<ushort> parameters, ReadOnlySpan<byte> intermediates, char action);

    /// <summary>An ESC sequence was dispatched.</summary>
    void EscDispatch(ReadOnlySpan<byte> intermediates, byte b);

    /// <summary>An OSC sequence was dispatched.</summary>
    void OscDispatch(ReadOnlySpan<byte> payload);
}
