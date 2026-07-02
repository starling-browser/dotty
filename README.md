# Dotty

Dotty is a terminal emulator for .NET. The core is an embeddable, framework-agnostic
engine, so you can drop a Dotty terminal into any .NET host.

## Projects

- **Dotty.Terminal** — the terminal engine and host-neutral embedding API. Pure .NET, no UI dependencies.
- **Dotty.Terminal.Avalonia** — an Avalonia control and renderer that bind to the engine.
- **Dotty.Terminal.Mcp** — a Model Context Protocol (MCP) server that exposes
  terminal tools to MCP clients. It depends only on the engine, so any host that
  embeds the terminal gets MCP support.

## Samples

- **samples/Dotty.Avalonia** — a sample Avalonia app that embeds the terminal.
- **samples/Dotty.Uno** — a small Uno app that hosts the framework-neutral terminal session.

## Embedding

Use `Dotty.Terminal` when you want to host a terminal in any .NET app. It has no UI framework dependency.

```csharp
using Dotty.Terminal;
using Dotty.Terminal.Hosting;

var session = new TerminalSession(new TerminalSessionOptions
{
    Size = new GridSize(80, 24),
});

session.ScreenChanged += (_, _) =>
{
    var snapshot = session.CreateSnapshot();
    // Draw snapshot cells and cursor with your UI framework.
};

session.Start();
```

A host binding is responsible for five small jobs:

- map framework keys and pointer events to Dotty input
- call `Resize` when the view size changes
- draw `TerminalScreenSnapshot` with the host drawing API
- bridge clipboard requests
- marshal events to the host UI thread

`Dotty.Terminal.Avalonia` is one example of that binding.

### Prompt hints

An embedded terminal often has little width, so the default `user@host dir %`
shell prompt wastes space. The host can ask for a shorter prompt with
`PromptHint`:

```csharp
var session = new TerminalSession(new TerminalSessionOptions
{
    Size = new GridSize(80, 24),
    PromptHint = PromptHint.DirectoryArrow, // "net10.0-desktop ❯"
});
```

Styles: `Directory` (`net10.0-desktop %`), `DirectoryArrow` (`net10.0-desktop ❯`,
arrow turns red when the last command failed), and `ParentAndDirectory`
(`akt/net10.0-desktop %`). The hint is applied after the user's own shell
config loads, so aliases and PATH are untouched. It works by pointing `ZDOTDIR`
at a generated shim that sources the user's real rc files and then sets
`PROMPT`. Only zsh honors the hint today. Other shells keep their own prompt.

## References & Inspirations

Projects that we reviewed as inspiration or references.

- [XtermSharp](https://github.com/migueldeicaza/XtermSharp) — a VT100/xterm terminal
  engine for .NET.
