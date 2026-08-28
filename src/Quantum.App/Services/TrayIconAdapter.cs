using System.Drawing;
using System.Windows.Forms;

namespace Quantum.App.Services;

/// <summary>
/// Ícone da bandeja. Usa o NotifyIcon do WinForms porque é a API de tray mais
/// estável do Windows — o custo é apenas o assembly, sem janela nem laço extra.
/// </summary>
public sealed class TrayIconAdapter : IDisposable
{
    private const int MaxTooltipLength = 63;

    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconAdapter()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new DarkColors()),
            BackColor = Color.FromArgb(15, 20, 32),
            ForeColor = Color.FromArgb(230, 237, 247),
            ShowImageMargin = false,
        };

        menu.Items.Add(Item("Abrir o Quantum", () => OpenRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(Item("Verificar áudio agora", () => CheckupRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("Sair", () => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Quantum",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.BalloonTipClicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? CheckupRequested;

    public event EventHandler? ExitRequested;

    public void ShowBalloon(string title, string message, bool warning)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = warning ? ToolTipIcon.Warning : ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(8000);
    }

    /// <summary>O Windows corta o tooltip da bandeja em 63 caracteres.</summary>
    public void SetTooltip(string text)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Text = text.Length <= MaxTooltipLength
            ? text
            : text[..(MaxTooltipLength - 1)] + "…";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static ToolStripMenuItem Item(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.FileNotFoundException)
        {
            // Cai no ícone padrão do sistema.
        }

        return SystemIcons.Application;
    }

    /// <summary>Deixa o menu da bandeja escuro, no lugar do cinza padrão do WinForms.</summary>
    private sealed class DarkColors : ProfessionalColorTable
    {
        private static readonly Color Surface = Color.FromArgb(15, 20, 32);
        private static readonly Color Hover = Color.FromArgb(27, 36, 55);
        private static readonly Color Edge = Color.FromArgb(30, 39, 57);

        public override Color ToolStripDropDownBackground => Surface;

        public override Color MenuItemSelected => Hover;

        public override Color MenuItemSelectedGradientBegin => Hover;

        public override Color MenuItemSelectedGradientEnd => Hover;

        public override Color MenuItemBorder => Color.FromArgb(34, 211, 238);

        public override Color MenuBorder => Edge;

        public override Color ImageMarginGradientBegin => Surface;

        public override Color ImageMarginGradientMiddle => Surface;

        public override Color ImageMarginGradientEnd => Surface;

        public override Color SeparatorDark => Edge;

        public override Color SeparatorLight => Edge;
    }
}
