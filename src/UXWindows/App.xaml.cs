using System.Windows;
using Forms = System.Windows.Forms;

namespace UXWindows;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private UtilityPanel? _panel;
    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Une seule instance à la fois.
        _singleInstance = new Mutex(true, "UXWindows.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        _panel = new UtilityPanel(ScreenHelper.GetUtilityScreen());
        _panel.Show();

        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Afficher / masquer le panneau", null, (_, _) => TogglePanel());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "UX-windows",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => TogglePanel();
    }

    private void TogglePanel()
    {
        if (_panel is null) return;
        if (_panel.IsVisible) _panel.Hide();
        else _panel.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
