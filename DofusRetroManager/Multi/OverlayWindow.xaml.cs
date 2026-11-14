using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DofusRetroManager.Multi;

/// <summary>Contenu de la superposition, recalculé avec l'onglet Combat et la liste des fenêtres.</summary>
public sealed record OverlayModel(string Title, string Stats, string Econome, IReadOnlyList<EconomeSpell> Spells,
    string GroupsTitle, IReadOnlyList<OverlayGroup> Groups, IReadOnlyList<OverlayTurn> Rotation,
    bool ShowEconome, bool ShowGroups, bool ShowRotation, System.Windows.Media.ImageSource? Icon = null,
    string HarvestTitle = "", IReadOnlyList<Harvest.HarvestChip>? Harvest = null, bool ShowHarvest = false,
    string FmTitle = "", IReadOnlyList<OverlayFmRune>? FmHistory = null, IReadOnlyList<OverlayFmLine>? FmChances = null,
    bool ShowFmHistory = false, bool ShowFmChances = false, string FmReliquat = "",
    string FarmTitle = "", double FarmFill = 0, IReadOnlyList<string>? FarmSteps = null, System.Windows.Media.ImageSource? FarmIcon = null)
{
    public bool ShowFm => ShowFmHistory || ShowFmChances;
    public bool ShowFarm => FarmTitle.Length > 0;
    public double FarmWidth => 276 * FarmFill;

    /// <summary>Résumé comparable : la fenêtre n'est redessinée que si quelque chose a changé.</summary>
    public string Key => string.Join("|", Title, Stats, Econome, string.Join(",", Spells.Select(s => $"{s.Id}:{s.State}")),
        GroupsTitle, string.Join(",", Groups), string.Join(",", Rotation), ShowEconome, ShowGroups, ShowRotation, Icon,
        HarvestTitle, string.Join(",", (Harvest ?? []).Select(h => h.Tooltip)), ShowHarvest,
        FmTitle, string.Join(",", FmHistory ?? []), string.Join(",", (FmChances ?? []).Select(l => l.Key)), ShowFmHistory, ShowFmChances, FmReliquat,
        FarmTitle, string.Join(",", FarmSteps ?? []));
}

public sealed record OverlayGroup(string Monsters, string Info, bool Archi);

/// <summary>Rune passée (superposition FM) : rune, résultat (SC, SN, EC, EC0), gains et pertes.</summary>
public sealed record OverlayFmRune(int RuneId, string Code, string Gains, string Losses, IReadOnlyList<OverlayFmChange> Changes)
{
    public System.Windows.Media.ImageSource? Icon => Data.ItemIcons.Get(RuneId);
    public string Badge => Code == "EC0" ? "EC" : Code;
}

/// <summary>Ligne de l'objet (superposition FM) : caractéristique, valeur / max et chance SC de chaque rune.</summary>
public sealed record OverlayFmLine(int Characteristic, string Value, bool Over, IReadOnlyList<OverlayFmChance> Runes)
{
    public System.Windows.Media.ImageSource? Icon => Data.CharacteristicIcons.Get(Characteristic);
    public string Key => $"{Characteristic}:{Value}:{string.Join("/", Runes)}";
}

/// <summary>Gain (vert) ou perte (rouge) d'une rune passée : icône de la caractéristique et valeur, texte au survol.</summary>
public sealed record OverlayFmChange(int Characteristic, string Value, string Text, bool Gain)
{
    public System.Windows.Media.ImageSource? Icon => Data.CharacteristicIcons.Get(Characteristic);
}

/// <summary>Chance de réussite (SC + SN) d'une rune ; Over : moins de 50 % (en rouge).</summary>
public sealed record OverlayFmChance(int RuneId, string Sc, bool Over, string Tip = "")
{
    public System.Windows.Media.ImageSource? Icon => Data.ItemIcons.Get(RuneId);
}

/// <summary>
/// Ligne de l'ordre de jeu. Life : points de vie en pourcentage (null : inconnus). Icon : tête du personnage (classe et sexe).
/// Handle : fenêtre du personnage (clic : elle passe au premier plan). Timer : temps du tour qui s'écoule (« 12 / 30 s »).
/// Blink : c'est son tour (mode split : la ligne clignote au lieu de basculer).
/// </summary>
public sealed record OverlayTurn(int Order, string Name, string Initiative, bool Active, int? Life, System.Windows.Media.ImageSource? Icon = null,
    long Handle = 0, string Timer = "", bool Blink = false)
{
    public bool HasLife => Life != null;
    public double LifeValue => Life ?? 0;
    public string LifeText => Life is { } l ? $"{l} %" : "";
}

/// <summary>
/// Superposition en jeu. Verrouillée, elle laisse passer la souris (les clics vont au jeu) et ne prend jamais le focus ;
/// déverrouillée, on la fait glisser pour la placer.
/// </summary>
public partial class OverlayWindow : Window
{
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_LAYERED = 0x80000;

    bool _locked = true;
    public readonly OverlayGear Gear = new();

    public OverlayWindow()
    {
        InitializeComponent();
        // Molette ⚙ en haut à droite, son panneau en tête de la carte
        var stack = (System.Windows.Controls.StackPanel)Card.Child;
        stack.Children.Insert(0, Gear.Panel);
        var grid = new System.Windows.Controls.Grid();
        Card.Child = grid;
        grid.Children.Add(stack);
        Gear.Button.Margin = new Thickness(0, -6, -8, 0);
        grid.Children.Add(Gear.Button);
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
            MoveHint.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            Gear.SetLocked(value);
            Cursor = value ? null : Cursors.SizeAll;
            ApplyStyle();
        }
    }

    public void Show(OverlayModel model)
    {
        if ((DataContext as OverlayModel)?.Key != model.Key) DataContext = model;
    }

    /// <summary>Clic sur un personnage de l'ordre de jeu : sa fenêtre (handle).</summary>
    public event Action<long>? TurnClicked;

    /// <summary>Verrouillée, la souris est sur l'ordre de jeu : seule cette zone reçoit les clics.</summary>
    bool _overRotation;

    void ApplyStyle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
        style = _locked && !_overRotation ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }

    /// <summary>
    /// Appelé régulièrement : la superposition laisse passer les clics vers le jeu, sauf sur la liste de l'ordre de jeu,
    /// où un clic sur un personnage affiche sa fenêtre, et sur les chances FM (détail au survol). Elle ne prend jamais le focus.
    /// </summary>
    public void UpdateHitTest()
    {
        if (!IsVisible || !GetCursorPos(out var p)) return;
        bool over = false;
        // Ordre de jeu (clic) et chances FM (détail SC / SN / EC au survol)
        bool On(FrameworkElement e)
        {
            if (!e.IsVisible || e.ActualHeight <= 0) return false;
            var local = e.PointFromScreen(new Point(p.X, p.Y));
            return local.X >= 0 && local.Y >= 0 && local.X <= e.ActualWidth && local.Y <= e.ActualHeight;
        }
        if (_locked) over = On(RotationList) || On(FmChancesList) || Gear.IsOver(new Point(p.X, p.Y));
        if (over == _overRotation) return;
        _overRotation = over;
        ApplyStyle();
    }

    void Turn_Click(object sender, MouseButtonEventArgs e)
    {
        if (_locked && (sender as FrameworkElement)?.DataContext is OverlayTurn { Handle: not 0 } t) TurnClicked?.Invoke(t.Handle);
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
}
