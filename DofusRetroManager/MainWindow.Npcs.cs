using System.Windows.Media;
using DofusRetroManager.Data;
using DofusRetroManager.Harvest;

namespace DofusRetroManager;

/// <summary>
/// PNJ sur la carte : ceux vus sur les maps visitées, application lancée (intérieurs de maisons et souterrains
/// compris : une map intérieure a les coordonnées de la map extérieure). Nom lu dans le fichier de langue « npc »,
/// maison de la map dans « houses ». Simple affichage, rien n'est partagé (règle 4).
/// </summary>
public partial class MainWindow
{
    NpcTracker? _npcs;
    /// <summary>Filtre « PNJ » de la carte.</summary>
    const int NpcFilter = -2;
    List<MapHint> _carteNpcHints = [];

    static readonly ImageSource NpcIcon = CreateNpcIcon();

    /// <summary>Icône d'un PNJ, dessinée par l'application : une bulle de dialogue.</summary>
    static ImageSource CreateNpcIcon()
    {
        var bubble = Geometry.Parse("M3,4 H21 V16 H11 L6,21 V16 H3 Z");
        var dots = Geometry.Parse("M7,9 H9 V11 H7 Z M11,9 H13 V11 H11 Z M15,9 H17 V11 H15 Z");
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xF2, 0xD0, 0x6B)),
            new Pen(new SolidColorBrush(Color.FromRgb(0x4A, 0x2A, 0x10)), 1.6) { LineJoin = PenLineJoin.Round }, bubble));
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x4A, 0x2A, 0x10)), null, dots));
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    void InitNpcs(GameData data)
    {
        _npcs = new NpcTracker();
        _npcs.Changed += RebuildNpcHints;
        _tracker.MapChanged += _npcs.OnMapChanged;
        _tracker.MapSprites += _npcs.OnSprites;
        _sniffer.StreamClosed += stream => Ui(() => _npcs.OnStreamClosed(stream));
        RebuildNpcHints();
    }

    /// <summary>Un point par PNJ et par map : nom, et la maison pour un intérieur (case de la map au survol).</summary>
    void RebuildNpcHints()
    {
        if (_npcs == null || _data == null) return;
        _carteNpcHints = _npcs.Maps.Where(m => _data.Maps.ContainsKey(m.Key))
            .SelectMany(m => m.Value.Keys.Select(npc => new MapHint(
                (_data.Npcs.GetValueOrDefault(npc) is { Length: > 0 } n && n != "???" ? n : $"PNJ {npc}")
                + (_data.HouseOfMap.GetValueOrDefault(m.Key) is { } house ? $" · {house}" : ""),
                NpcFilter, 0, _data.Maps[m.Key])))
            .ToList();
    }

    /// <summary>PNJ pour la page (ajoutés aux points de la carte, filtre « PNJ »).</summary>
    IEnumerable<object> CarteNpcHintsJson() => _carteNpcHints.Where(h => h.Map.SuperArea is 0 or 3).Select(h => (object)new
    {
        n = h.Name, k = h.Category, i = "carte/npc.png", x = h.Map.X, y = h.Map.Y, sa = h.Map.SuperArea, l = h.Map.Label,
    });
}
