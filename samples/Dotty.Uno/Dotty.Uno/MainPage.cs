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
        _terminalView.KeyDown += OnTerminalKeyDown;
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
        var screenText = _session.GetVisibleText().TrimEnd('\r', '\n');
        _pendingInput = TrimEchoedInput(screenText, _pendingInput);
        SetTerminalText(screenText, _pendingInput);
    }

    private void OnTerminalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var key = MapKey(e.Key);
        if (key == TerminalKey.None) return;

        var modifiers = MapModifiers();
        if (IsPrintableKey(key)
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

    private static TerminalKey MapKey(VirtualKey key)
    {
        return key switch
        {
            >= VirtualKey.A and <= VirtualKey.Z => (TerminalKey)((int)TerminalKey.A + (int)(key - VirtualKey.A)),
            >= VirtualKey.Number0 and <= VirtualKey.Number9 => (TerminalKey)((int)TerminalKey.D0 + (int)(key - VirtualKey.Number0)),
            >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 => (TerminalKey)((int)TerminalKey.NumPad0 + (int)(key - VirtualKey.NumberPad0)),
            VirtualKey.Enter => TerminalKey.Enter,
            VirtualKey.Back => TerminalKey.Backspace,
            VirtualKey.Tab => TerminalKey.Tab,
            VirtualKey.Escape => TerminalKey.Escape,
            VirtualKey.Up => TerminalKey.Up,
            VirtualKey.Down => TerminalKey.Down,
            VirtualKey.Right => TerminalKey.Right,
            VirtualKey.Left => TerminalKey.Left,
            VirtualKey.Home => TerminalKey.Home,
            VirtualKey.End => TerminalKey.End,
            VirtualKey.PageUp => TerminalKey.PageUp,
            VirtualKey.PageDown => TerminalKey.PageDown,
            VirtualKey.Insert => TerminalKey.Insert,
            VirtualKey.Delete => TerminalKey.Delete,
            VirtualKey.F1 => TerminalKey.F1,
            VirtualKey.F2 => TerminalKey.F2,
            VirtualKey.F3 => TerminalKey.F3,
            VirtualKey.F4 => TerminalKey.F4,
            VirtualKey.F5 => TerminalKey.F5,
            VirtualKey.F6 => TerminalKey.F6,
            VirtualKey.F7 => TerminalKey.F7,
            VirtualKey.F8 => TerminalKey.F8,
            VirtualKey.F9 => TerminalKey.F9,
            VirtualKey.F10 => TerminalKey.F10,
            VirtualKey.F11 => TerminalKey.F11,
            VirtualKey.F12 => TerminalKey.F12,
            VirtualKey.Space => TerminalKey.Space,
            VirtualKey.Multiply => TerminalKey.Multiply,
            VirtualKey.Add => TerminalKey.Add,
            VirtualKey.Subtract => TerminalKey.Subtract,
            VirtualKey.Decimal => TerminalKey.Decimal,
            VirtualKey.Divide => TerminalKey.Divide,
            (VirtualKey)186 => TerminalKey.OemSemicolon,
            (VirtualKey)187 => TerminalKey.OemPlus,
            (VirtualKey)188 => TerminalKey.OemComma,
            (VirtualKey)189 => TerminalKey.OemMinus,
            (VirtualKey)190 => TerminalKey.OemPeriod,
            (VirtualKey)191 => TerminalKey.OemQuestion,
            (VirtualKey)192 => TerminalKey.OemTilde,
            (VirtualKey)219 => TerminalKey.OemOpenBrackets,
            (VirtualKey)220 => TerminalKey.OemPipe,
            (VirtualKey)221 => TerminalKey.OemCloseBrackets,
            (VirtualKey)222 => TerminalKey.OemQuotes,
            _ => TerminalKey.None,
        };
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

    private static bool IsPrintableKey(TerminalKey key) =>
        (key >= TerminalKey.A && key <= TerminalKey.Z)
        || (key >= TerminalKey.D0 && key <= TerminalKey.D9)
        || (key >= TerminalKey.NumPad0 && key <= TerminalKey.NumPad9)
        || key is TerminalKey.Space
            or TerminalKey.OemMinus
            or TerminalKey.OemPlus
            or TerminalKey.OemOpenBrackets
            or TerminalKey.OemCloseBrackets
            or TerminalKey.OemPipe
            or TerminalKey.OemSemicolon
            or TerminalKey.OemQuotes
            or TerminalKey.OemComma
            or TerminalKey.OemPeriod
            or TerminalKey.OemQuestion
            or TerminalKey.OemTilde
            or TerminalKey.Multiply
            or TerminalKey.Add
            or TerminalKey.Subtract
            or TerminalKey.Decimal
            or TerminalKey.Divide;

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
