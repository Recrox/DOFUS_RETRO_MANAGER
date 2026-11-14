using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DofusRetroManager.Multi;

/// <summary>
/// Mini-carte de récolte posée sur le jeu (onglet Carte) : une image de la carte autour de la map du personnage, avec
/// les ressources choisies. Comme la superposition, c'est une fenêtre de l'application : verrouillée, elle laisse passer
/// les clics vers le jeu et ne prend jamais le focus ; rien n'est injecté ni dessiné dans le client (règles 2 et 3).
/// </summary>
public sealed class MiniMapWindow : Window
{
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_LAYERED = 0x80000;

    readonly Image _map = new() { Stretch = Stretch.None };
    readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _info = new() { FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _hint = new() { FontSize = 10.5, Text = "Glissez, puis ⚙ › « Verrouiller »", Visibility = Visibility.Collapsed };
    bool _locked = true;
    bool _overGear;
    public readonly OverlayGear Gear = new();

    public MiniMapWindow()
    {
        Title = "Dofus Retro Manager — mini-carte";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        SetResourceReference(FontFamilyProperty, "AppFont");
        SetResourceReference(ForegroundProperty, "Text");
        FontSize = 12;
        _info.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "AccentSolid");

        var panel = new StackPanel();
        var head = new DockPanel();
        DockPanel.SetDock(Gear.Button, Dock.Right);
        head.Children.Add(Gear.Button);
        head.Children.Add(_title);
        panel.Children.Add(head);
        panel.Children.Add(Gear.Panel);
        panel.Children.Add(new Border { Child = _map, Margin = new Thickness(0, 4, 0, 4), CornerRadius = new CornerRadius(4), ClipToBounds = true });
        panel.Children.Add(_info);
        panel.Children.Add(_hint);
        var card = new Border { Child = panel, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(8), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        Content = card;

        SourceInitialized += (_, _) => ApplyStyle();
        MouseLeftButtonDown += (_, e) => { if (!_locked && e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    /// <summary>Verrouillée : clics transmis au jeu. Déverrouillée : déplaçable à la souris.</summary>
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            _hint.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            Gear.SetLocked(value);
            Cursor = value ? null : Cursors.SizeAll;
            ApplyStyle();
        }
    }

    public void Show(ImageSource? map, string title, string info)
    {
        _map.Source = map;
        _title.Text = title;
        _info.Text = info;
        _info.Visibility = info.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void ApplyStyle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
        style = _locked && !_overGear ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }

    /// <summary>Appelé régulièrement : verrouillée, seule la molette ⚙ et son panneau reçoivent les clics.</summary>
    public void UpdateHitTest()
    {
        if (!IsVisible || !GetCursorPos(out var p)) return;
        bool over = _locked && Gear.IsOver(new Point(p.X, p.Y));
        if (over == _overGear) return;
        _overGear = over;
        ApplyStyle();
    }

    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
}
