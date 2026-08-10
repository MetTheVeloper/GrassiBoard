using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace GrassiBoard.Services;

internal sealed record HotkeyRegistration(string Gesture, string Description, Action Action);

internal sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint ModNoRepeat = 0x4000U;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<int, Action> _actions = [];
    private readonly LowLevelKeyboardProc _keyboardProc;
    private nint _window;
    private nint _keyboardHook;
    private ParsedHotkey? _pushToTalk;
    private Action<bool>? _pushToTalkChanged;
    private bool _pushToTalkHeld;
    private int _nextId = 1200;

    public GlobalHotkeyService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _keyboardProc = KeyboardHook;
    }

    public void Attach(nint window) => _window = window;

    public string Refresh(
        IEnumerable<HotkeyRegistration> registrations,
        string pushToTalkGesture,
        Action<bool> pushToTalkChanged)
    {
        UnregisterAll();
        var parsed = new List<(HotkeyRegistration Registration, ParsedHotkey Hotkey)>();
        var messages = new List<string>();
        foreach (HotkeyRegistration registration in registrations.Where(item => !string.IsNullOrWhiteSpace(item.Gesture)))
        {
            if (!TryParse(registration.Gesture, out ParsedHotkey hotkey, out string error))
            {
                messages.Add($"{registration.Description}: {error}");
                continue;
            }
            parsed.Add((registration, hotkey));
        }

        ParsedHotkey? ptt = null;
        if (!string.IsNullOrWhiteSpace(pushToTalkGesture))
        {
            if (TryParse(pushToTalkGesture, out ParsedHotkey parsedPtt, out string pttError))
            {
                ptt = parsedPtt;
            }
            else
            {
                messages.Add($"Push-to-Talk: {pttError}");
            }
        }

        var counts = parsed.Select(item => item.Hotkey.Canonical)
            .Concat(ptt is null ? [] : [ptt.Value.Canonical])
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach ((HotkeyRegistration registration, ParsedHotkey hotkey) in parsed)
        {
            if (counts[hotkey.Canonical] > 1)
            {
                messages.Add($"{hotkey.Canonical}: assigned more than once");
                continue;
            }
            int id = _nextId++;
            if (_window == nint.Zero || !RegisterHotKey(_window, id, hotkey.Modifiers | ModNoRepeat, hotkey.VirtualKey))
            {
                messages.Add($"{registration.Description}: Windows rejected {hotkey.Canonical}");
                continue;
            }
            _actions.Add(id, registration.Action);
        }

        if (ptt is not null && counts[ptt.Value.Canonical] == 1)
        {
            _pushToTalk = ptt;
            _pushToTalkChanged = pushToTalkChanged;
            _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0U);
            if (_keyboardHook == nint.Zero)
            {
                messages.Add("Push-to-Talk: Windows keyboard hook registration failed");
                _pushToTalk = null;
            }
            else
            {
                // A configured PTT starts closed and opens only while its key is held.
                _pushToTalkChanged?.Invoke(false);
            }
        }

        return messages.Count == 0
            ? $"{_actions.Count + (_pushToTalk is null ? 0 : 1)} global hotkeys active"
            : string.Join(Environment.NewLine, messages.Distinct());
    }

    public bool HandleMessage(int message, nint wParam)
    {
        if (message != WmHotkey || !_actions.TryGetValue(wParam.ToInt32(), out Action? action))
        {
            return false;
        }
        _dispatcher.BeginInvoke(action, DispatcherPriority.Input);
        return true;
    }

    private nint KeyboardHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && _pushToTalk is ParsedHotkey hotkey)
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            int message = wParam.ToInt32();
            bool down = message is WmKeyDown or WmSysKeyDown;
            bool up = message is WmKeyUp or WmSysKeyUp;
            if (data.VirtualKey == hotkey.VirtualKey)
            {
                if (down && !_pushToTalkHeld && RequiredModifiersDown(hotkey.Modifiers))
                {
                    _pushToTalkHeld = true;
                    _dispatcher.BeginInvoke(() => _pushToTalkChanged?.Invoke(true), DispatcherPriority.Send);
                }
                else if (up && _pushToTalkHeld)
                {
                    _pushToTalkHeld = false;
                    _dispatcher.BeginInvoke(() => _pushToTalkChanged?.Invoke(false), DispatcherPriority.Send);
                }
            }
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private static bool RequiredModifiersDown(uint modifiers)
    {
        Modifier required = (Modifier)modifiers;
        return (!required.HasFlag(Modifier.Alt) || IsDown(0x12)) &&
            (!required.HasFlag(Modifier.Control) || IsDown(0x11)) &&
            (!required.HasFlag(Modifier.Shift) || IsDown(0x10)) &&
            (!required.HasFlag(Modifier.Windows) || IsDown(0x5B) || IsDown(0x5C));
    }

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    internal static bool TryParse(string value, out ParsedHotkey hotkey, out string error)
    {
        hotkey = default;
        error = string.Empty;
        string[] tokens = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            error = "enter a key combination";
            return false;
        }

        Modifier modifiers = 0U;
        string? keyToken = null;
        foreach (string token in tokens)
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": modifiers |= Modifier.Control; break;
                case "ALT": modifiers |= Modifier.Alt; break;
                case "SHIFT": modifiers |= Modifier.Shift; break;
                case "WIN": case "WINDOWS": modifiers |= Modifier.Windows; break;
                default:
                    if (keyToken is not null)
                    {
                        error = "use exactly one non-modifier key";
                        return false;
                    }
                    keyToken = token;
                    break;
            }
        }
        if (keyToken is null)
        {
            error = "a non-modifier key is required";
            return false;
        }
        try
        {
            var converter = new KeyConverter();
            if (converter.ConvertFromInvariantString(keyToken) is not Key key || key == Key.None)
            {
                error = $"unknown key '{keyToken}'";
                return false;
            }
            uint virtualKey = checked((uint)KeyInterop.VirtualKeyFromKey(key));
            string canonical = string.Join("+", new[]
            {
                modifiers.HasFlag(Modifier.Control) ? "Ctrl" : null,
                modifiers.HasFlag(Modifier.Alt) ? "Alt" : null,
                modifiers.HasFlag(Modifier.Shift) ? "Shift" : null,
                modifiers.HasFlag(Modifier.Windows) ? "Win" : null,
                key.ToString()
            }.Where(item => item is not null));
            hotkey = new ParsedHotkey((uint)modifiers, virtualKey, canonical);
            return true;
        }
        catch (NotSupportedException)
        {
            error = $"unknown key '{keyToken}'";
            return false;
        }
    }

    private void UnregisterAll()
    {
        if (_window != nint.Zero)
        {
            foreach (int id in _actions.Keys)
            {
                _ = UnregisterHotKey(_window, id);
            }
        }
        _actions.Clear();
        if (_keyboardHook != nint.Zero)
        {
            _ = UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = nint.Zero;
        }
        if (_pushToTalkChanged is not null)
        {
            // Removing PTT returns to the normal always-open microphone mode.
            _pushToTalkChanged.Invoke(true);
        }
        _pushToTalkHeld = false;
        _pushToTalk = null;
        _pushToTalkChanged = null;
    }

    public void Dispose() => UnregisterAll();

    [Flags]
    private enum Modifier : uint { Alt = 0x0001U, Control = 0x0002U, Shift = 0x0004U, Windows = 0x0008U }
    internal readonly record struct ParsedHotkey(uint Modifiers, uint VirtualKey, string Canonical);
    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, nint module, uint threadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
