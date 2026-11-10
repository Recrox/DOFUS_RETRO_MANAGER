using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DofusRetroManager.Multi;

namespace DofusRetroManager;

/// <summary>
/// Mini-carte de récolte (onglet Carte) : petite fenêtre posée sur le jeu, centrée sur la map du personnage au premier
/// plan (suivie à chaque changement de map), avec les ressources cochées dans l'onglet Carte sur les maps déjà visitées,
/// et la plus proche hors de la vue. Elle ne fait qu'afficher, comme la superposition (règles 2 et 3).
/// </summary>
public partial class MainWindow
{
    MiniMapWindow? _miniMap;
    /// <summary>Maps visibles autour de celle du personnage (rayon), et échelle de l'image (une map : 40 × 23).</summary>
    const int MiniRadius = 4;
    const double MiniScale = 0.8;
    string _miniKey = "";
    int _miniRetries;
    DateTime _miniRetryAt;

    void InitMiniMap()
    {
        MiniMapEnabled.IsChecked = _organizer.MiniMapEnabled;
        foreach (var k in _organizer.MiniMapResources) _carteShown.Add(k);
        MiniMapEnabled.Checked += MiniMapEnabled_Changed;
        MiniMapEnabled.Unchecked += MiniMapEnabled_Changed;
        MiniMapMove.Click += MiniMapMove_Click;
        _overlayTimer.Tick += (_, _) =>
        {
            try { UpdateMiniMap(); }
            catch (Exception ex) { _miniKey = "?"; AddLog("Mini-carte : " + ex.Message); }
        };
        Closed += (_, _) => _miniMap?.Close();
    }

    void MiniMapEnabled_Changed(object sender, RoutedEventArgs e)
    {
        _organizer.MiniMapEnabled = MiniMapEnabled.IsChecked == true;
        _organizer.Save();
        if (!_organizer.MiniMapEnabled && _miniMap is { Locked: false }) MiniMapMove_Click(sender, e);
        UpdateMiniMap();
    }

    /// <summary>Ressources cochées dans l'onglet Carte (filtres « ressource ») : celles de la mini-carte.</summary>
    void SetMiniMapResources(IEnumerable<int> keys)
    {
        _organizer.MiniMapResources = keys.Where(IsResourceFilter).Distinct().ToList();
        _organizer.Save();
        _miniKey = "";
        UpdateMiniMap();
    }

    void MiniMapMove_Click(object sender, RoutedEventArgs e)
    {
        var w = EnsureMiniMap();
        w.Locked = !w.Locked;
        MiniMapMove.Content = w.Locked ? "Déplacer" : "Verrouiller";
        if (w.Locked)
        {
            _organizer.MiniMapLeft = w.Left;
            _organizer.MiniMapTop = w.Top;
            _organizer.Save();
        }
        _miniKey = "";
        UpdateMiniMap();
    }

    MiniMapWindow EnsureMiniMap()
    {
        if (_miniMap != null) return _miniMap;
        var area = SystemParameters.WorkArea;
        _miniMap = new MiniMapWindow { Opacity = _organizer.OverlayOpacity };
        bool onScreen = _organizer.MiniMapLeft is { } l && _organizer.MiniMapTop is { } t
            && l >= SystemParameters.VirtualScreenLeft - 50 && l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50
            && t >= SystemParameters.VirtualScreenTop - 20 && t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50;
        _miniMap.Left = onScreen ? _organizer.MiniMapLeft!.Value : area.Left + 20;
        _miniMap.Top = onScreen ? _organizer.MiniMapTop!.Value : area.Top + 90;
        return _miniMap;
    }

    /// <summary>Appelé avec la superposition (4 fois par seconde) : visibilité, puis image seulement si la map ou les ressources ont changé.</summary>
    void UpdateMiniMap()
    {
        bool moving = _miniMap is { Locked: false };
        bool want = moving || (_organizer.MiniMapEnabled && IsGameForeground());
        if (!want) { if (_miniMap is { IsVisible: true }) _miniMap.Hide(); return; }
        if (_data == null || _harvest == null) return;

        var fgWindow = _windows.FirstOrDefault(w => w.Handle == GameWindows.Foreground);
        var me = (fgWindow != null ? CharacterOf(fgWindow) : null) ?? _hunt?.Shown;
        Data.MapInfo? map = me != null && int.TryParse(me.MapId, out var id) ? _data.Maps.GetValueOrDefault(id) : null;
        var wanted = _organizer.MiniMapResources.Select(k => k - ResourceFilterBase).ToHashSet();
        var key = $"{map?.Id}|{string.Join(",", wanted)}|{_harvest.Maps.Count}|{_carteTiles.Count}";
        var w = EnsureMiniMap();
        // Tuiles ou icônes pas encore chargées : nouvel essai toutes les 2 s, 10 fois au plus
        bool retry = _miniRetries is > 0 and <= 10 && DateTime.Now >= _miniRetryAt;
        if (key != _miniKey || retry)
        {
            if (key != _miniKey) _miniRetries = 0;
            _miniKey = key;
            if (map == null) w.Show(null, "Mini-carte", "Changez de map en jeu.");
            else
            {
                var (image, info, pending) = RenderMiniMap(map, wanted);
                _miniRetries = pending ? _miniRetries + 1 : 0;
                _miniRetryAt = DateTime.Now.AddSeconds(2);
                w.Show(image, $"[{map.X},{map.Y}] {map.Area}", info);
            }
        }
        if (!w.IsVisible) w.Show();
    }

    /// <summary>Image de la mini-carte : tuiles autour de la map, ressources choisies, cadre du personnage ; texte : la plus proche.</summary>
    (ImageSource? Image, string Info, bool Pending) RenderMiniMap(Data.MapInfo map, HashSet<int> wanted)
    {
        bool pending = false;
        int size = 2 * MiniRadius + 1;
        double cw = Data.WorldMap.CellWidth, ch = Data.WorldMap.CellHeight;
        var view = new Rect((map.X - MiniRadius) * cw, (map.Y - MiniRadius) * ch, size * cw, size * ch);

        // Ressources choisies sur les maps visitées de la même super-zone : (x, y) -> objets et nombre
        var spots = new Dictionary<(int X, int Y), Dictionary<int, int>>();
        foreach (var (mapId, hm) in _harvest!.Maps)
        {
            if (_data!.Maps.GetValueOrDefault(mapId) is not { } m || m.SuperArea != map.SuperArea) continue;
            foreach (var c in hm.Cells.Where(c => wanted.Contains(c.Resource)))
            {
                var at = spots.TryGetValue((m.X, m.Y), out var d) ? d : spots[(m.X, m.Y)] = new();
                int item = _data.Resources[c.Resource].ItemId;
                at[item] = at.GetValueOrDefault(item) + 1;
            }
        }

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(MiniScale, MiniScale));
            dc.PushTransform(new TranslateTransform(-view.X, -view.Y));
            dc.PushClip(new RectangleGeometry(view));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x24, 0x2c)), null, view);
            // Tuiles lues en arrière-plan (plusieurs secondes) : jamais ici, sur l'interface
            if (!_carteTiles.TryGetValue(map.SuperArea, out var tiles)) { pending = true; PreloadCarte(); tiles = []; }
            foreach (var t in tiles.Where(t => t.Bounds.IntersectsWith(view)))
                dc.DrawDrawing(t.Drawing);
            // Grille légère des maps
            var grid = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
            for (int i = 0; i <= size; i++)
            {
                dc.DrawLine(grid, new Point(view.X + i * cw, view.Y), new Point(view.X + i * cw, view.Bottom));
                dc.DrawLine(grid, new Point(view.X, view.Y + i * ch), new Point(view.Right, view.Y + i * ch));
            }
            // Ressources : icône de l'objet récolté, nombre de cases en petit
            var font = new Typeface("Segoe UI");
            foreach (var ((x, y), items) in spots)
            {
                var cell = Data.WorldMap.Cell(x, y);
                if (!cell.IntersectsWith(view)) continue;
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(70, 52, 211, 153)), null, cell);
                double ix = cell.X + 2;
                foreach (var (item, count) in items.Take(2))
                {
                    if (Data.ItemIcons.Get(item) is { } icon) dc.DrawImage(icon, new Rect(ix, cell.Y + 2, 19, 19));
                    else pending = true;
                    var text = new FormattedText(count.ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        font, 10, Brushes.White, 1.0);
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), null, new Rect(ix + 19 - text.Width - 2, cell.Bottom - 12, text.Width + 2, 11));
                    dc.DrawText(text, new Point(ix + 19 - text.Width - 1, cell.Bottom - 13));
                    ix += 19;
                }
            }
            // Map du personnage
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D)), 2.5), Data.WorldMap.Cell(map.X, map.Y));
            dc.Pop(); dc.Pop(); dc.Pop();
        }
        var bitmap = new RenderTargetBitmap((int)(view.Width * MiniScale), (int)(view.Height * MiniScale), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        // Ressource la plus proche hors de la vue (distance en maps)
        string info = wanted.Count == 0 ? "Cochez des ressources dans l'onglet Carte."
            : spots.Count == 0 ? "Ressources pas encore vues sur une map visitée." : "";
        var far = spots.Keys.Where(p => Math.Abs(p.X - map.X) > MiniRadius || Math.Abs(p.Y - map.Y) > MiniRadius)
            .OrderBy(p => Math.Max(Math.Abs(p.X - map.X), Math.Abs(p.Y - map.Y))).Cast<(int X, int Y)?>().FirstOrDefault();
        if (info.Length == 0 && far is { } f && !spots.Keys.Any(p => Math.Abs(p.X - map.X) <= MiniRadius && Math.Abs(p.Y - map.Y) <= MiniRadius))
            info = $"Plus proche : [{f.X},{f.Y}] · {Direction(f.X - map.X, f.Y - map.Y)}";
        return (bitmap, info, pending);
    }

    /// <summary>Direction approximative d'une map (flèche) et sa distance en maps.</summary>
    static string Direction(int dx, int dy)
    {
        string[] arrows = ["→", "↘", "↓", "↙", "←", "↖", "↑", "↗"];
        int octant = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        return $"{arrows[(octant + 8) % 8]} {Math.Max(Math.Abs(dx), Math.Abs(dy))} map(s)";
    }
}
