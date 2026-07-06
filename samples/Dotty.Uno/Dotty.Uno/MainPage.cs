using Dotty.Terminal;
using Dotty.Terminal.Hosting;
using Dotty.Terminal.Input;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Core;
using Windows.System;

namespace Dotty.Uno;

public sealed partial class MainPage : Page
{
    private readonly TerminalSession _session;
    private readonly TextBox _terminalView;
    private bool _updatingTerminalView;
    private string _displayText = "";
    private string _pendingInput = "";

    public MainPage()
    {
        _session = new TerminalSession(new TerminalSessionOptions
        {
            Size = new GridSize(80, 24),
        });
        _session.ScreenChanged += OnScreenChanged;

        _terminalView = new TextBox
        {
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Colors.Black),
            AcceptsReturn = true,
            Height = 360,
            IsTabStop = true,
            TextWrapping = TextWrapping.NoWrap,
        };
        // handledEventsToo: the TextBox consumes navigation keys (Up/Down/Home/
        // End/…) for its own caret movement and marks them handled, so a plain
        // KeyDown handler never sees them and the shell never receives the arrow-
        // key escape sequence (e.g. no Up-arrow command-history recall). Opt in to
        // still receive them and forward them to the PTY.
        _terminalView.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler(OnTerminalKeyDown),
            handledEventsToo: true);
        _terminalView.TextChanged += OnTerminalTextChanged;
        _terminalView.Loaded += (_, _) => FocusTerminal();

        var content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
        };
        content.Children.Add(new TextBlock
        {
            Text = "Dotty Uno sample",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Click the terminal, type a command, and press Enter.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(Colors.Black),
            Padding = new Thickness(16),
            Child = _terminalView,
        });

        Background = new SolidColorBrush(Colors.White);
        Content = content;

        Unloaded += (_, _) => _session.Dispose();

        _session.Start();
        RefreshScreen();
    }

    private void OnScreenChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshScreen);
    }

    private void RefreshScreen()
    {
        // TextBox coerces all line breaks in Text to '\r' (WinUI behavior), so
        // store _displayText in the same form or comparisons against
        // _terminalView.Text fail once the screen has multiple lines.
        var screenText = _session.GetVisibleText().TrimEnd('\r', '\n')
            .Replace("\r\n", "\r").Replace('\n', '\r');
        _pendingInput = TrimEchoedInput(screenText, _pendingInput);
        SetTerminalText(screenText, _pendingInput);
    }

    private void OnTerminalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var key = TerminalKeyMapping.MapWindowsVirtualKey((int)e.Key);
        if (key == TerminalKey.None) return;

        var modifiers = MapModifiers();
        if (TerminalKeyMapping.IsPrintable(key)
            && !modifiers.HasFlag(TerminalKeyModifiers.Control)
            && !modifiers.HasFlag(TerminalKeyModifiers.Alt)
            && !modifiers.HasFlag(TerminalKeyModifiers.Meta))
            return;

        if (_session.SendKey(key, modifiers, text: null))
            e.Handled = true;
    }

    private void OnTerminalTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingTerminalView)
            return;

        if (!_terminalView.Text.StartsWith(_displayText, StringComparison.Ordinal))
        {
            SetTerminalText(_displayText, _pendingInput);
            return;
        }

        var input = _terminalView.Text[_displayText.Length..];
        if (!input.StartsWith(_pendingInput, StringComparison.Ordinal))
        {
            SetTerminalText(_displayText, _pendingInput);
            return;
        }

        var newInput = input[_pendingInput.Length..];
        if (newInput.Length == 0)
            return;

        _pendingInput = input;
        _session.SendText(newInput.Replace("\r\n", "\r").Replace("\n", "\r"));
        MoveCaretToEnd();
    }

    private static TerminalKeyModifiers MapModifiers()
    {
        var modifiers = TerminalKeyModifiers.None;
        if (IsKeyDown(VirtualKey.Shift))
            modifiers |= TerminalKeyModifiers.Shift;
        if (IsKeyDown(VirtualKey.Control))
            modifiers |= TerminalKeyModifiers.Control;
        if (IsKeyDown(VirtualKey.Menu))
            modifiers |= TerminalKeyModifiers.Alt;
        return modifiers;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private void FocusTerminal()
    {
        _terminalView.Focus(FocusState.Programmatic);
        MoveCaretToEnd();
    }

    private void MoveCaretToEnd()
    {
        _terminalView.SelectionStart = _terminalView.Text.Length;
        _terminalView.SelectionLength = 0;
    }

    private void SetTerminalText(string text, string pendingInput = "")
    {
        _displayText = text;
        _updatingTerminalView = true;
        _terminalView.Text = text + pendingInput;
        MoveCaretToEnd();
        _updatingTerminalView = false;
    }

    private static string TrimEchoedInput(string screenText, string pendingInput)
    {
        for (var length = pendingInput.Length; length > 0; length--)
        {
            if (screenText.EndsWith(pendingInput[..length], StringComparison.Ordinal))
                return pendingInput[length..];
        }

        return pendingInput;
    }
}
