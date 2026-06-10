# Dotty

Dotty is a terminal emulator for .NET. The core is an embeddable, framework-agnostic
engine, so you can drop a Dotty terminal into an Avalonia app, an Uno app, or any
other .NET host.

## Projects

- **Dotty.Terminal** — the terminal engine. Pure .NET, no UI dependencies.
- **Dotty.Terminal.Avalonia** — an Avalonia control that hosts the engine.
- **Dotty.Terminal.Mcp** — a Model Context Protocol (MCP) server that exposes
  terminal tools to MCP clients. It depends only on the engine, so any host that
  embeds the terminal gets MCP support.
- **Dotty** — a sample Avalonia app that embeds the terminal.

## References & Inspirations

Other projects that embed terminals in .NET. Use these as references.

- [XtermSharp](https://github.com/migueldeicaza/XtermSharp) — a VT100/xterm terminal
  engine for .NET, with front ends for several UI frameworks.
