using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DofusRetroManager.Multi;

/// <summary>
/// Molette ⚙ des fenêtres posées sur le jeu (superposition, mini-carte) : un clic ouvre un petit panneau pour
/// déplacer / verrouiller la fenêtre et régler son opacité, sans retourner dans l'application. Ce n'est que de
/// l'affichage : rien n'est envoyé au jeu.
/// </summary>
public sealed class OverlayGear
{
    public readonly Border Button;
    public readonly StackPanel Panel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4), Visibility = Visibility.Collapsed };
    readonly TextBlock _lock = new();
    readonly Slider _opacity = new() { Minimum = 40, Maximum = 100, Width = 90, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
    readonly TextBlock _value = new() { Width = 34, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
    bool _setting;

    public event Action? LockToggled;
    public event Action<double>? OpacityChanged;

    public OverlayGear()
    {
        var icon = new TextBlock { Text = "⚙", FontSize = 13, Margin = new Thickness(3, -1, 3, 0) };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
        Button = new Border
        {
            Child = icon, Background = Brushes.Transparent, Cursor = Cursors.Hand, CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Réglages : déplacer, opacité",
        };
        Button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Panel.Visibility = Panel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
        };

        var lockChip = new Border { Child = _lock, Padding = new Thickness(6, 1, 6, 1), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
        lockChip.SetResourceReference(Border.BackgroundProperty, "HoverBg");
        lockChip.MouseLeftButtonDown += (_, e) => { e.Handled = true; LockToggled?.Invoke(); };
        _lock.FontSize = 11;
        var eye = new TextBlock { Text = "◐", FontSize = 12, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Opacité" };
        _opacity.ValueChanged += (_, _) =>
        {
            _value.Text = $"{_opacity.Value:0} %";
            if (!_setting) OpacityChanged?.Invoke(_opacity.Value / 100);
        };
        Panel.Children.Add(lockChip);
        Panel.Children.Add(eye);
        Panel.Children.Add(_opacity);
        Panel.Children.Add(_value);
        SetLocked(true);
    }

    /// <summary>Boutons − / + (zoom de la mini-carte) : step = −1 ou +1.</summary>
    public void AddZoom(Action<int> step)
    {
        foreach (var (text, delta, tip) in new[] { ("−", -1, "Zoom arrière"), ("+", 1, "Zoom avant") })
        {
            var label = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Bold };
            var chip = new Border { Child = label, Padding = new Thickness(7, 0, 7, 1), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 4, 0), ToolTip = tip };
            chip.SetResourceReference(Border.BackgroundProperty, "HoverBg");
            chip.MouseLeftButtonDown += (_, e) => { e.Handled = true; step(delta); };
            Panel.Children.Insert(1 + (delta > 0 ? 1 : 0), chip);
        }
    }

    public void SetLocked(bool locked)
    {
        _lock.Text = locked ? "✥ Déplacer" : "🔒 Verrouiller";
        Panel.ToolTip = locked ? null : "Faites glisser la fenêtre, puis « Verrouiller »";
    }

    public void SetOpacity(double opacity)
    {
        _setting = true;
        _opacity.Value = opacity * 100;
        _value.Text = $"{_opacity.Value:0} %";
        _setting = false;
    }

    /// <summary>La souris (coordonnées écran) est sur la molette ou son panneau : ces zones reçoivent les clics, même verrouillée.</summary>
    public bool IsOver(Point screen) => On(Button, screen) || On(Panel, screen);

    static bool On(FrameworkElement e, Point screen)
    {
        if (!e.IsVisible || e.ActualHeight <= 0) return false;
        var local = e.PointFromScreen(screen);
        return local.X >= 0 && local.Y >= 0 && local.X <= e.ActualWidth && local.Y <= e.ActualHeight;
    }
}
