# Dotty

Dotty is a terminal emulator for .NET. The core is an embeddable, framework-agnostic
engine, so you can drop a Dotty terminal into any .NET host.

## Projects

- **Dotty.Terminal** — the terminal engine. Pure .NET, no UI dependencies.
- **Dotty.Terminal.Avalonia** — an Avalonia control that hosts the engine.
- **Dotty.Terminal.Mcp** — a Model Context Protocol (MCP) server that exposes
  terminal tools to MCP clients. It depends only on the engine, so any host that
  embeds the terminal gets MCP support.

## Samples

- **samples/Dotty** — a sample Avalonia app that embeds the terminal.

## References & Inspirations

Projects that we reviewed as inspiration or references.

- [XtermSharp](https://github.com/migueldeicaza/XtermSharp) — a VT100/xterm terminal
  engine for .NET.
