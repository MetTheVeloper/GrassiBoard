using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GrassiBoard.Services;

internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _muteItem;
    private readonly Icon? _applicationIcon;

    public TrayService(
        Dispatcher dispatcher,
        Action show,
        Action toggleMute,
        Action stopAll,
        Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("Show GrassiBoard");
        _muteItem = new Forms.ToolStripMenuItem("Mute Microphone");
        var stopItem = new Forms.ToolStripMenuItem("Stop All");
        var exitItem = new Forms.ToolStripMenuItem("Exit");
        showItem.Click += (_, _) => dispatcher.BeginInvoke(show);
        _muteItem.Click += (_, _) => dispatcher.BeginInvoke(toggleMute);
        stopItem.Click += (_, _) => dispatcher.BeginInvoke(stopAll);
        exitItem.Click += (_, _) => dispatcher.BeginInvoke(exit);
        menu.Items.AddRange([showItem, _muteItem, stopItem, new Forms.ToolStripSeparator(), exitItem]);

        _applicationIcon = TryLoadApplicationIcon();
        _icon = new Forms.NotifyIcon
        {
            Text = "GrassiBoard",
            Icon = _applicationIcon ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => dispatcher.BeginInvoke(show);
    }

    public void SetMuted(bool muted) => _muteItem.Text = muted ? "Unmute Microphone" : "Mute Microphone";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _applicationIcon?.Dispose();
    }

    private static Icon? TryLoadApplicationIcon()
    {
        try
        {
            string? executablePath = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Icon.ExtractAssociatedIcon(executablePath);
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or IOException)
        {
            return null;
        }
    }
}
