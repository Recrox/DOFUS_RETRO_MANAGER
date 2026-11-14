using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DofusRetroManager.Data;

/// <summary>
/// Vue réelle d'une map (sol et décors, comme à l'écran en jeu), dessinée en vignette depuis les fichiers du client
/// installé : la map (data/maps, déchiffrée avec la clé reçue en y entrant, d'où : maps visitées seulement) et ses
/// graphismes (clips/gfx/g*.swf pour le sol, o*.swf pour les décors). Lecture seule : rien n'est modifié dans le client
/// (règle 2). Les vignettes sont gardées sur ce PC (dossier « cartes ») et jamais partagées (règle 5).
/// Le dessin est lent (bibliothèques de plusieurs Mo) : il se fait sur un fil à part, les bibliothèques sont libérées
/// après une minute sans demande.
/// </summary>
public static class MapRenderer
{
    const double CellW = 53, CellHalfW = 26.5, CellHalfH = 13.5, LevelH = 20;
    /// <summary>Taille des vignettes (même rapport qu'une map de la carte du monde, 40 × 23).</summary>
    public const int ThumbW = 160, ThumbH = 92;

    static readonly string GfxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "clips", "gfx");
    static readonly string CacheDir = Path.Combine(GameData.AppDir, "cartes");

    /// <summary>Vue d'une map : vignette, et map extérieure ou non (égouts, grottes, donjons, maisons).</summary>
    public sealed record MapView(BitmapSource Image, bool Outdoor);

    /// <summary>Vignettes prêtes (null : map illisible), par « id_date ».</summary>
    static readonly ConcurrentDictionary<string, MapView?> Ready = new();
    static readonly ConcurrentDictionary<string, bool> Queued = new();
    static readonly BlockingCollection<(int Id, string Date, string Key)> Queue = new();
    static Thread? _worker;
    static int _version;

    /// <summary>Augmente à chaque vignette terminée : la mini-carte se redessine.</summary>
    public static int Version => Volatile.Read(ref _version);

    /// <summary>
    /// Vue d'une map visitée si elle est prête ; sinon null (map illisible, ou demandée : elle sera
    /// dessinée en arrière-plan, puis <see cref="Version"/> augmentera).
    /// </summary>
    public static MapView? TryGet(int mapId, string date, string key)
    {
        var name = $"{mapId}_{date}";
        if (Ready.TryGetValue(name, out var img)) return img;
        if (key.Length > 0 && Queued.TryAdd(name, true))
        {
            Queue.Add((mapId, date, key));
            EnsureWorker();
        }
        return null;
    }

    static void EnsureWorker()
    {
        if (_worker != null) return;
        _worker = new Thread(Work) { IsBackground = true, Name = "Vues des maps", Priority = ThreadPriority.BelowNormal };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }

    // --- Fil de dessin ---

    static Dictionary<int, SwfRenderer>? _ground, _objects;
    static readonly Dictionary<(bool Obj, int Id, int Frame), Drawing?> Symbols = new();

    static void Work()
    {
        while (true)
        {
            if (!Queue.TryTake(out var job, TimeSpan.FromMinutes(1)))
            {
                // Plus de demande : bibliothèques libérées (plusieurs dizaines de Mo)
                _ground = _objects = null;
                Symbols.Clear();
                job = Queue.Take();
            }
            var name = $"{job.Id}_{job.Date}";
            MapView? img = null;
            try { img = LoadCached(name) ?? RenderAndSave(job.Id, job.Date, job.Key, name); }
            catch { } // map illisible : rien n'est dessiné
            if (Ready.Count > 1200) Ready.Clear(); // ~60 Ko par vignette
            Ready[name] = img;
            Queued.TryRemove(name, out _);
            Interlocked.Increment(ref _version);
        }
    }

    /// <summary>Vignette gardée sur le disque : « id_date.png » (extérieure) ou « id_date.in.png » (intérieure).</summary>
    static MapView? LoadCached(string name)
    {
        bool outdoor = true;
        var path = Path.Combine(CacheDir, name + ".png");
        if (!File.Exists(path)) { outdoor = false; path = Path.Combine(CacheDir, name + ".in.png"); }
        if (!File.Exists(path)) return null;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return new MapView(bmp, outdoor);
    }

    static MapView? RenderAndSave(int id, string date, string key, string name)
    {
        if (MapDecoder.Load(id, date, key) is not { } map) return null;
        var img = Render(map);
        if (img == null) return null;
        try
        {
            Directory.CreateDirectory(CacheDir);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using var fs = File.Create(Path.Combine(CacheDir, name + (map.Outdoor ? ".png" : ".in.png")));
            enc.Save(fs);
        }
        catch { } // pas de cache : elle sera redessinée au prochain lancement
        return new MapView(img, map.Outdoor);
    }

    /// <summary>Vue de la map, comme le jeu la construit : sol, décors du calque 1, puis du calque 2, case par case.</summary>
    public static BitmapSource? Render(MapDecoder.MapCells map)
    {
        LoadLibraries();
        var view = new Rect(0, 0, (map.Width - 1) * CellW, (map.Height - 1) * 2 * CellHalfH);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(ThumbW / view.Width, ThumbH / view.Height));
            dc.PushClip(new RectangleGeometry(view));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)), null, view);
            var places = Places(map);
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < map.Cells.Length; i++)
                    DrawCell(dc, map.Cells[i], layer, places[i]);
            dc.Pop(); dc.Pop();
        }
        var bmp = new RenderTargetBitmap(ThumbW, ThumbH, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Position de chaque case : lignes alternées de largeur W et W − 1 (décalées d'une demi-case), hauteur selon le niveau.</summary>
    static Point[] Places(MapDecoder.MapCells map)
    {
        var p = new Point[map.Cells.Length];
        int width = map.Width, x = -1, row = 0;
        double offset = 0;
        for (int i = 0; i < p.Length; i++)
        {
            if (x == width - 1)
            {
                x = 0; row++;
                if (offset == 0) { offset = CellHalfW; width--; }
                else { offset = 0; width++; }
            }
            else x++;
            int level = map.Cells[i][1] & 15;
            p[i] = new Point(x * CellW + offset, row * CellHalfH - LevelH * (level - 7));
        }
        return p;
    }

    static void DrawCell(DrawingContext dc, int[] v, int layer, Point at)
    {
        int num, rot = 0, frame = 1;
        bool flip;
        if (layer == 0)
        {
            num = ((v[0] & 24) << 6) + ((v[2] & 7) << 6) + v[3];
            int slope = (v[4] & 60) >> 2;
            if (slope > 1) frame = slope; else rot = (v[1] & 48) >> 4;
            flip = (v[4] & 2) != 0;
        }
        else if (layer == 1)
        {
            num = ((v[0] & 4) << 11) + ((v[4] & 1) << 12) + (v[5] << 6) + v[6];
            rot = (v[7] & 48) >> 4;
            flip = (v[7] & 8) != 0;
        }
        else
        {
            num = ((v[0] & 2) << 12) + ((v[7] & 1) << 12) + (v[8] << 6) + v[9];
            flip = (v[7] & 4) != 0;
        }
        if (num <= 0 || Symbol(layer > 0, num, frame) is not { } d) return;
        // Comme le jeu : rotation par quart de tour, déformée pour garder la forme isométrique ; miroir horizontal
        double sx = 1, sy = 1;
        if (rot % 2 == 1) { sx = 0.5185; sy = 1.9286; }
        var m = Matrix.Identity;
        m.Scale(flip ? -sx : sx, sy);
        m.Rotate(rot * 90);
        m.Translate(at.X, at.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawDrawing(d);
        dc.Pop();
    }

    static Drawing? Symbol(bool obj, int id, int frame)
    {
        if (Symbols.TryGetValue((obj, id, frame), out var d)) return d;
        d = null;
        try
        {
            if ((obj ? _objects : _ground)!.TryGetValue(id, out var swf))
                d = swf.RenderSymbol(id.ToString(), frame) ?? (frame > 1 ? swf.RenderSymbol(id.ToString()) : null);
        }
        catch { } // graphisme illisible : case vide
        return Symbols[(obj, id, frame)] = d;
    }

    static void LoadLibraries()
    {
        if (_ground != null && _objects != null) return;
        _ground = Library("g");
        _objects = Library("o");
    }

    /// <summary>Bibliothèques g*.swf ou o*.swf : numéro de graphisme -> fichier qui l'exporte.</summary>
    static Dictionary<int, SwfRenderer> Library(string prefix)
    {
        var lib = new Dictionary<int, SwfRenderer>();
        if (!Directory.Exists(GfxDir)) return lib;
        foreach (var file in Directory.GetFiles(GfxDir, prefix + "*.swf"))
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(file)[1..], out _)) continue;
            try
            {
                var swf = SwfRenderer.Load(File.ReadAllBytes(file));
                foreach (var name in swf.Exports.Keys)
                    if (int.TryParse(name, out var id)) lib.TryAdd(id, swf);
            }
            catch { } // bibliothèque illisible : ses graphismes manqueront
        }
        return lib;
    }
}
