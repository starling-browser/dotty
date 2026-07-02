using Dotty.Terminal.Hosting;
using Dotty.Terminal.Pty;
using Xunit;

namespace Dotty.Terminal.Tests;

public class ZshPromptShimTests
{
    [Fact]
    public void NoneHintDoesNothing()
    {
        var config = new PtyConfig { Shell = "/bin/zsh" };

        Assert.Null(ZshPromptShim.Apply(PromptHint.None, config));
        Assert.False(config.Env.ContainsKey("ZDOTDIR"));
    }

    [Fact]
    public void NonZshShellIgnoresHint()
    {
        var config = new PtyConfig { Shell = "/bin/bash" };

        Assert.Null(ZshPromptShim.Apply(PromptHint.DirectoryArrow, config));
        Assert.False(config.Env.ContainsKey("ZDOTDIR"));
    }

    [Fact]
    public void ZshHintWritesShimAndRedirectsZdotdir()
    {
        var config = new PtyConfig { Shell = "/bin/zsh" };

        string? shimDirectory = ZshPromptShim.Apply(PromptHint.DirectoryArrow, config);
        try
        {
            Assert.NotNull(shimDirectory);
            Assert.Equal(shimDirectory, config.Env["ZDOTDIR"]);

            foreach (var rc in new[] { ".zshenv", ".zprofile", ".zshrc", ".zlogin" })
                Assert.True(File.Exists(Path.Combine(shimDirectory!, rc)), $"{rc} missing");

            string zshrc = File.ReadAllText(Path.Combine(shimDirectory!, ".zshrc"));
            Assert.Contains("$_dotty_user_zdotdir/.zshrc", zshrc);
            Assert.Contains($"PROMPT={ZshPromptShim.GetZshPrompt(PromptHint.DirectoryArrow)}", zshrc);
        }
        finally
        {
            ZshPromptShim.Cleanup(shimDirectory);
        }
    }

    [Fact]
    public void HostSuppliedZdotdirIsForwardedToShim()
    {
        var config = new PtyConfig { Shell = "/bin/zsh" };
        config.Env["ZDOTDIR"] = "/custom/zdot";

        string? shimDirectory = ZshPromptShim.Apply(PromptHint.Directory, config);
        try
        {
            Assert.Equal("/custom/zdot", config.Env["DOTTY_USER_ZDOTDIR"]);
            Assert.Equal(shimDirectory, config.Env["ZDOTDIR"]);
        }
        finally
        {
            ZshPromptShim.Cleanup(shimDirectory);
        }
    }

    [Theory]
    [InlineData(PromptHint.Directory)]
    [InlineData(PromptHint.DirectoryArrow)]
    [InlineData(PromptHint.ParentAndDirectory)]
    public void EveryHintHasAZshPrompt(PromptHint hint)
    {
        string prompt = ZshPromptShim.GetZshPrompt(hint);

        // Must be a single-quoted zsh string so the generated .zshrc parses.
        Assert.StartsWith("'", prompt);
        Assert.EndsWith("'", prompt);
    }

    [Fact]
    public void CleanupRemovesShimDirectory()
    {
        var config = new PtyConfig { Shell = "/bin/zsh" };
        string? shimDirectory = ZshPromptShim.Apply(PromptHint.Directory, config);

        Assert.True(Directory.Exists(shimDirectory));

        ZshPromptShim.Cleanup(shimDirectory);

        Assert.False(Directory.Exists(shimDirectory));
    }
}
