using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Client.UserInterface.Systems.Chat;

/// <summary>
/// Command which creates a window containing a chatbox
/// </summary>
[UsedImplicitly]
public sealed class ChatWindowCommand : LocalizedCommands
{
    public override string Command => "chatwindow";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var window = new ChatWindow();
        window.OpenCentered();
    }
}

/// <summary>
/// Command which creates a window containing a chatbox configured for admin use
/// </summary>
[UsedImplicitly]
public sealed class AdminChatWindowCommand : LocalizedCommands
{
    private static ChatWindow? _window;

    public override string Command => "achatwindow";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_window == null || _window.Disposed || !_window.IsOpen)
        {
            _window = new ChatWindow();
            _window.ConfigureForAdminChat();
            _window.OnClose += () => _window = null;
            _window.OpenCentered();
            return;
        }

        _window.MoveToFront();
    }
}
