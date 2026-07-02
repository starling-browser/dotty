namespace Dotty.Terminal.Hosting;

/// <summary>
/// A host-supplied hint for how the shell prompt should look inside the embedded
/// terminal. The hint is applied on top of the user's own shell configuration, so
/// aliases, PATH, and everything else from their rc files still load. Currently
/// honored for zsh only; other shells ignore the hint and keep their own prompt.
/// </summary>
public enum PromptHint
{
    /// <summary>Leave the user's prompt untouched.</summary>
    None = 0,

    /// <summary>Current directory name and the shell's prompt character, e.g. <c>net10.0-desktop %</c>.</summary>
    Directory,

    /// <summary>Current directory name and a <c>❯</c> arrow that turns red when the last command failed, e.g. <c>net10.0-desktop ❯</c>.</summary>
    DirectoryArrow,

    /// <summary>Last two path segments and the shell's prompt character, e.g. <c>akt/net10.0-desktop %</c>.</summary>
    ParentAndDirectory,
}
