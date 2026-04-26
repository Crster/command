using System;
using Avalonia.Threading;
using CrsterCommand.ViewModels;
using SharpHook;
using SharpHook.Data;

namespace CrsterCommand.Services;

public sealed class DesktopRobotHotkeyService : IDisposable
{
    private readonly StorageService _storageService;
    private readonly MacroManagerViewModel _macroManagerViewModel;
    private readonly GlobalHookManager _hookManager;
    private bool _started;
    private bool _paused;
    private bool _captureMode;
    private Action<string>? _captureCallback;
    private Action? _onEscapePressed;
    private Action? _onBackspacePressed;
    private readonly object _sync = new();
    private bool _disposed;

    public DesktopRobotHotkeyService(StorageService storageService, MacroManagerViewModel macroManagerViewModel)
    {
        _storageService = storageService;
        _macroManagerViewModel = macroManagerViewModel;
        _hookManager = GlobalHookManager.Instance;
        _hookManager.KeyPressed += HookOnKeyPressed;
    }

    public void Start()
    {
        if (_started)
        {
            Console.WriteLine("[DesktopRobot] Service already started");
            return;
        }

        Console.WriteLine("[DesktopRobot] Starting Desktop Robot Hotkey Service");
        _started = true;
        _hookManager.Start();
        Console.WriteLine("[DesktopRobot] Hook manager started");
    }

    public void Pause()
    {
        lock (_sync)
        {
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            _paused = false;
        }
    }

    public void StartCaptureMode(Action<string> callback, Action? onEscapePressed = null, Action? onBackspacePressed = null)
    {
        lock (_sync)
        {
            Console.WriteLine("[DesktopRobot] StartCaptureMode called");
            _captureMode = true;
            _captureCallback = callback;
            _onEscapePressed = onEscapePressed;
            _onBackspacePressed = onBackspacePressed;
            Console.WriteLine("[DesktopRobot] Capture mode enabled, waiting for keys...");
        }
    }

    public void StopCaptureMode()
    {
        lock (_sync)
        {
            _captureMode = false;
            _captureCallback = null;
            _onEscapePressed = null;
            _onBackspacePressed = null;
        }
    }

    private void HookOnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        lock (_sync)
        {
            if (_captureMode && _captureCallback != null)
            {
                Console.WriteLine($"[DesktopRobot] Capture mode: Key pressed - KeyCode: {e.Data.KeyCode}");

                // Check for ESC key to restore last shortcut
                if (e.Data.KeyCode == KeyCode.VcEscape)
                {
                    Console.WriteLine("[DesktopRobot] ESC pressed - restoring last shortcut");
                    if (_onEscapePressed != null)
                    {
                        Dispatcher.UIThread.Post(() => _onEscapePressed());
                    }
                    return;
                }

                // Check for Backspace key to restore default
                if (e.Data.KeyCode == KeyCode.VcBackspace)
                {
                    Console.WriteLine("[DesktopRobot] Backspace pressed - restoring default shortcut");
                    if (_onBackspacePressed != null)
                    {
                        Dispatcher.UIThread.Post(() => _onBackspacePressed());
                    }
                    return;
                }

                var keyName = GetKeyNameFromKeyCode(e.Data.KeyCode);
                var modifiers = GetModifiersFromMask(e.RawEvent.Mask);

                Console.WriteLine($"[DesktopRobot] KeyName: {keyName}, Modifiers: {string.Join(", ", modifiers)}");

                if (!string.IsNullOrWhiteSpace(keyName))
                {
                    var shortcutStr = FormatShortcut(modifiers, keyName);
                    Console.WriteLine($"[DesktopRobot] Formatted shortcut: {shortcutStr}");
                    Dispatcher.UIThread.Post(() => _captureCallback(shortcutStr));
                }
                return;
            }

            if (_paused)
            {
                return;
            }
        }

        if (!TryParseShortcut(_storageService.GetDesktopRobotShortcut(), out var shortcut))
        {
            return;
        }

        if (e.Data.KeyCode != shortcut.KeyCode || !MatchesModifiers(e.RawEvent.Mask, shortcut))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _macroManagerViewModel.RunDesktopRobot(fromHotkey: true));
    }

    private static bool TryParseShortcut(string shortcut, out ShortcutDefinition definition)
    {
        definition = default;

        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return false;
        }

        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var modifiers = ShortcutModifiers.None;
        KeyCode? keyCode = null;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                case "cmd":
                case "command":
                case "meta":
                case "win":
                    modifiers |= ShortcutModifiers.Control;
                    continue;
                case "shift":
                    modifiers |= ShortcutModifiers.Shift;
                    continue;
                case "alt":
                    modifiers |= ShortcutModifiers.Alt;
                    continue;
            }

            if (TryParseKeyCode(part, out var parsedKeyCode))
            {
                keyCode = parsedKeyCode;
            }
            else
            {
                return false;
            }
        }

        if (keyCode == null)
        {
            return false;
        }

        if (!IsAllowedCombination(keyCode.Value, modifiers))
        {
            return false;
        }

        definition = new ShortcutDefinition(keyCode.Value, modifiers);
        return true;
    }

    private static bool IsAllowedCombination(KeyCode keyCode, ShortcutModifiers modifiers)
    {
        if (keyCode == KeyCode.VcPrintScreen)
        {
            return modifiers == ShortcutModifiers.None;
        }

        var hasControl = modifiers.HasFlag(ShortcutModifiers.Control);
        var hasAlt = modifiers.HasFlag(ShortcutModifiers.Alt);
        var hasShift = modifiers.HasFlag(ShortcutModifiers.Shift);
        var hasMeta = modifiers.HasFlag(ShortcutModifiers.Meta);

        return 
            (!hasShift && !hasControl && hasAlt && !hasMeta) ||
            (!hasShift && !hasControl && !hasAlt && hasMeta) ||
            (hasShift && !hasControl && hasAlt && !hasMeta) ||
            (hasShift && hasControl && !hasAlt && !hasMeta) ||
            (hasShift && hasControl && hasAlt && !hasMeta) ||
            (!hasShift && hasControl && hasAlt && !hasMeta);
    }

    private static bool TryParseKeyCode(string value, out KeyCode keyCode)
    {
        keyCode = KeyCode.VcUndefined;

        if (value.Length == 1)
        {
            var c = char.ToUpperInvariant(value[0]);
            if (c >= 'A' && c <= 'Z')
            {
                keyCode = Enum.Parse<KeyCode>($"Vc{c}");
                return true;
            }

            if (c >= '0' && c <= '9')
            {
                keyCode = Enum.Parse<KeyCode>($"Vc{c}");
                return true;
            }
        }

        var normalized = value.Replace(" ", string.Empty);
        if (normalized.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase))
        {
            keyCode = KeyCode.VcPrintScreen;
            return true;
        }

        if (TryParseSymbolKeyCode(normalized, out keyCode))
        {
            return true;
        }

        if (normalized.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
        {
            keyCode = KeyCode.VcBackspace;
            return true;
        }

        if (normalized.Equals("PageUp", StringComparison.OrdinalIgnoreCase))
        {
            keyCode = KeyCode.VcPageUp;
            return true;
        }

        if (normalized.Equals("PageDown", StringComparison.OrdinalIgnoreCase))
        {
            keyCode = KeyCode.VcPageDown;
            return true;
        }

        if (Enum.TryParse($"Vc{normalized}", true, out keyCode) && IsSupportedFunctionKey(keyCode))
        {
            return true;
        }

        return false;
    }

    private static bool IsSupportedFunctionKey(KeyCode keyCode)
    {
        return keyCode >= KeyCode.VcF1 && keyCode <= KeyCode.VcF12;
    }

    private static bool TryParseSymbolKeyCode(string value, out KeyCode keyCode)
    {
        keyCode = KeyCode.VcUndefined;

        switch (value)
        {
            case "/":
            case "?":
            case "OemQuestion":
            case "Oem2":
                keyCode = KeyCode.VcSlash;
                return true;
            case ",":
            case "OemComma":
                keyCode = KeyCode.VcComma;
                return true;
            case ".":
            case "OemPeriod":
                keyCode = KeyCode.VcPeriod;
                return true;
            case ";":
            case "OemSemicolon":
            case "Oem1":
                keyCode = KeyCode.VcSemicolon;
                return true;
            case "'":
            case "OemQuotes":
                keyCode = KeyCode.VcQuote;
                return true;
            case "\\":
            case "OemBackslash":
                keyCode = KeyCode.VcBackslash;
                return true;
            case "[":
            case "OemOpenBrackets":
                keyCode = KeyCode.VcOpenBracket;
                return true;
            case "]":
            case "OemCloseBrackets":
                keyCode = KeyCode.VcCloseBracket;
                return true;
            case "-":
            case "OemMinus":
                keyCode = KeyCode.VcMinus;
                return true;
            case "+":
            case "=":
            case "OemPlus":
                keyCode = KeyCode.VcEquals;
                return true;
            case "`":
            case "OemBackQuote":
            case "BackQuote":
                keyCode = KeyCode.VcBackQuote;
                return true;
            default:
                return false;
        }
    }

    private static bool MatchesModifiers(EventMask mask, ShortcutDefinition shortcut)
    {
        var controlPressed = mask.HasCtrl();
        var metaPressed = mask.HasMeta();

        return controlPressed == shortcut.Modifiers.HasFlag(ShortcutModifiers.Control)
            && metaPressed == shortcut.Modifiers.HasFlag(ShortcutModifiers.Meta)
            && mask.HasShift() == shortcut.Modifiers.HasFlag(ShortcutModifiers.Shift)
            && mask.HasAlt() == shortcut.Modifiers.HasFlag(ShortcutModifiers.Alt);
    }

    private string[] GetModifiersFromMask(EventMask mask)
    {
        var modifiers = new System.Collections.Generic.List<string>();

        if (mask.HasCtrl())
            modifiers.Add("Ctrl");

        if (mask.HasAlt())
            modifiers.Add("Alt");

        if (mask.HasShift())
            modifiers.Add("Shift");

        if (mask.HasMeta())
            modifiers.Add("Win");

        return modifiers.ToArray();
    }

    private string GetKeyNameFromKeyCode(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.VcPrintScreen => "PrintScreen",
            KeyCode.VcEscape => "Escape",
            KeyCode.VcBackspace => "Backspace",
            KeyCode.VcTab => "Tab",
            KeyCode.VcEnter => "Enter",
            KeyCode.VcSpace => "Space",
            KeyCode.VcF1 => "F1",
            KeyCode.VcF2 => "F2",
            KeyCode.VcF3 => "F3",
            KeyCode.VcF4 => "F4",
            KeyCode.VcF5 => "F5",
            KeyCode.VcF6 => "F6",
            KeyCode.VcF7 => "F7",
            KeyCode.VcF8 => "F8",
            KeyCode.VcF9 => "F9",
            KeyCode.VcF10 => "F10",
            KeyCode.VcF11 => "F11",
            KeyCode.VcF12 => "F12",
            KeyCode.Vc0 => "0",
            KeyCode.Vc1 => "1",
            KeyCode.Vc2 => "2",
            KeyCode.Vc3 => "3",
            KeyCode.Vc4 => "4",
            KeyCode.Vc5 => "5",
            KeyCode.Vc6 => "6",
            KeyCode.Vc7 => "7",
            KeyCode.Vc8 => "8",
            KeyCode.Vc9 => "9",
            KeyCode.VcA => "A",
            KeyCode.VcB => "B",
            KeyCode.VcC => "C",
            KeyCode.VcD => "D",
            KeyCode.VcE => "E",
            KeyCode.VcF => "F",
            KeyCode.VcG => "G",
            KeyCode.VcH => "H",
            KeyCode.VcI => "I",
            KeyCode.VcJ => "J",
            KeyCode.VcK => "K",
            KeyCode.VcL => "L",
            KeyCode.VcM => "M",
            KeyCode.VcN => "N",
            KeyCode.VcO => "O",
            KeyCode.VcP => "P",
            KeyCode.VcQ => "Q",
            KeyCode.VcR => "R",
            KeyCode.VcS => "S",
            KeyCode.VcT => "T",
            KeyCode.VcU => "U",
            KeyCode.VcV => "V",
            KeyCode.VcW => "W",
            KeyCode.VcX => "X",
            KeyCode.VcY => "Y",
            KeyCode.VcZ => "Z",
            KeyCode.VcSlash => "/",
            KeyCode.VcComma => ",",
            KeyCode.VcPeriod => ".",
            KeyCode.VcSemicolon => ";",
            KeyCode.VcQuote => "'",
            KeyCode.VcBackslash => "\\",
            KeyCode.VcOpenBracket => "[",
            KeyCode.VcCloseBracket => "]",
            KeyCode.VcMinus => "-",
            KeyCode.VcEquals => "=",
            KeyCode.VcBackQuote => "`",
            _ => ""
        };
    }

    private string FormatShortcut(string[] modifiers, string keyName)
    {
        var parts = new System.Collections.Generic.List<string>(modifiers);
        if (!string.IsNullOrEmpty(keyName))
            parts.Add(keyName);
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Unsubscribe from global hook first
        _hookManager.KeyPressed -= HookOnKeyPressed;

        try
        {
            lock (_sync)
            {
                _started = false;
                _paused = false;
                _captureMode = false;
                _captureCallback = null;
                _onEscapePressed = null;
                _onBackspacePressed = null;
            }
            Console.WriteLine("[DesktopRobot] Disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing DesktopRobotHotkeyService: {ex.Message}");
        }
    }

    private readonly record struct ShortcutDefinition(KeyCode KeyCode, ShortcutModifiers Modifiers);

    [Flags]
    private enum ShortcutModifiers
    {
        None = 0,
        Control = 1,
        Shift = 2,
        Alt = 4,
        Meta = 8
    }
}
