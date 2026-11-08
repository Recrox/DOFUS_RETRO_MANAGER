using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;

namespace DofusRetroManager.Data;

/// <summary>
/// Icônes des objets, rendues depuis les fichiers du client installé
/// (clips/items/&lt;type&gt;/&lt;gfx&gt;.swf). Lecture seule : le jeu n'est jamais modifié.
/// </summary>
public static class ItemIcons
{
    static readonly string ItemsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "clips", "items");

    static readonly Dictionary<int, ImageSource?> Cache = new();

    public static GameData? Data { get; set; }

    /// <summary>Appelé par la page (fil de rendu des images) et par l'interface : le cache est protégé par un verrou.</summary>
    public static ImageSource? Get(int itemId)
    {
        lock (Cache) return Build(itemId);
    }

    static ImageSource? Build(int itemId)
    {
        if (Cache.TryGetValue(itemId, out var img)) return img;
        img = null;
        if (Data?.Items.TryGetValue(itemId, out var it) == true)
        {
            var path = Path.Combine(ItemsDir, it.TypeId.ToString(), it.Gfx + ".swf");
            try
            {
                if (File.Exists(path) && SwfRenderer.Render(File.ReadAllBytes(path)) is { } drawing)
                {
                    var di = new DrawingImage(drawing);
                    di.Freeze();
                    img = di;
                }
            }
            catch { } // icône illisible : on affiche simplement rien
        }
        Cache[itemId] = img;
        return img;
    }
}

/// <summary>
/// Illustrations des monstres, comme dans le jeu : clips/artworks/big/&lt;g&gt;.swf du client installé (g : champ du
/// fichier de langue « monsters »). Lecture seule : le jeu n'est jamais modifié.
/// </summary>
public static class MonsterIcons
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "clips", "artworks", "big");

    static readonly Dictionary<int, ImageSource?> Cache = new();

    /// <summary>Par identifiant de monstre ; appelé par la page (fil de rendu des images) et par les bulles : cache sous verrou.</summary>
    public static ImageSource? Get(int monsterId)
    {
        lock (Cache) return Build(monsterId);
    }

    static ImageSource? Build(int monsterId)
    {
        if (Cache.TryGetValue(monsterId, out var img)) return img;
        img = null;
        if (ItemIcons.Data?.Monsters.TryGetValue(monsterId, out var m) == true && m.Gfx > 0)
        {
            var path = Path.Combine(Dir, m.Gfx + ".swf");
            try
            {
                if (File.Exists(path) && SwfRenderer.Render(File.ReadAllBytes(path)) is { } drawing)
                {
                    var di = new DrawingImage(drawing);
                    di.Freeze();
                    img = di;
                }
            }
            catch { } // illustration illisible : le nom du monstre reste au survol
        }
        return Cache[monsterId] = img;
    }
}

/// <summary>
/// Icônes des challenges de combat, comme dans le jeu : clips/challenges/&lt;g&gt;.swf du client installé (g : champ du
/// fichier de langue « fightChallenge »). Lecture seule : le jeu n'est jamais modifié.
/// </summary>
public static class ChallengeIcons
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "clips", "challenges");

    static readonly Dictionary<int, ImageSource?> Cache = new();

    public static ImageSource? Get(int gfx)
    {
        lock (Cache) return Build(gfx);
    }

    static ImageSource? Build(int gfx)
    {
        if (Cache.TryGetValue(gfx, out var img)) return img;
        img = null;
        var path = Path.Combine(Dir, gfx + ".swf");
        try
        {
            if (File.Exists(path) && SwfRenderer.Render(File.ReadAllBytes(path)) is { } drawing)
            {
                var di = new DrawingImage(drawing);
                di.Freeze();
                img = di;
            }
        }
        catch { } // icône illisible : rien
        return Cache[gfx] = img;
    }
}

/// <summary>
/// Icônes des effets d'objet, comme dans l'infobulle d'un objet en jeu : symboles « Icon_&lt;caractéristique&gt; » de
/// clips/effectsicons.swf du client installé (11 : vitalité, 13 : chance…). Lecture seule : le jeu n'est jamais modifié.
/// </summary>
public static class EffectIcons
{
    static readonly string SwfPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "clips", "effectsicons.swf");

    static readonly Dictionary<int, ImageSource?> Cache = new();
    static SwfRenderer? _swf;

    /// <summary>Icône de la caractéristique, null si le client n'en a pas (l'appelant prend alors une autre image).</summary>
    public static ImageSource? Get(int characteristic)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(characteristic, out var img)) return img;
            img = null;
            try
            {
                _swf ??= File.Exists(SwfPath) ? SwfRenderer.Load(File.ReadAllBytes(SwfPath)) : null;
                if (_swf?.RenderSymbol("Icon_" + characteristic) is { } drawing)
                {
                    var di = new DrawingImage(drawing);
                    di.Freeze();
                    img = di;
                }
            }
            catch { }
            return Cache[characteristic] = img;
        }
    }
}

/// <summary>
/// Icône d'une caractéristique comme dans l'infobulle d'un objet en jeu (core.swf / loader.swf du client installé : IconVita,
/// IconWaterBonus, CriticalHit, IconEarth pour le % de résistance, IconEarthAlt pour la résistance fixe…), sinon icône
/// d'effet (clips/effectsicons.swf), sinon la plus petite rune de la caractéristique.
/// </summary>
public static class CharacteristicIcons
{
    static readonly Dictionary<int, string> Game = new()
    {
        [11] = "Vita", [12] = "Wisdom", [10] = "EarthBonus", [15] = "FireBonus", [13] = "WaterBonus", [14] = "AirBonus",
        [44] = "Init", [48] = "PP", [23] = "MP", [18] = "CriticalHit",
        [33] = "Earth", [34] = "Fire", [35] = "Water", [36] = "Air", [37] = "Neutral",
        [83] = "EarthAlt", [84] = "FireAlt", [85] = "WaterAlt", [86] = "AirAlt", [87] = "NeutralAlt",
    };

    /// <summary>Caractéristiques dont l'icône d'effet du jeu (« Icon_&lt;n&gt; ») est la bonne.</summary>
    static readonly HashSet<int> EffectChars = [17, 19, 26, 49, 80];

    public static GameData? Data { get; set; }

    public static ImageSource? Get(int characteristic) =>
        (Game.TryGetValue(characteristic, out var n) ? StatIcons.Get(n) : null)
        ?? (EffectChars.Contains(characteristic) ? EffectIcons.Get(characteristic) : null)
        ?? (Data != null && RuneBreaking.SmallestRuneId(Data, characteristic) is { } rune ? ItemIcons.Get(rune) : null);
}

/// <summary>
/// Icônes des caractéristiques de la fiche du personnage, comme dans le jeu : symboles « Icon&lt;nom&gt; » de
/// modules/core.swf du client installé (Wisdom : sagesse, PP : prospection, Init : initiative, Vita : vitalité).
/// Lecture seule : le jeu n'est jamais modifié.
/// </summary>
public static class StatIcons
{
    static readonly string CorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "modules", "core.swf");

    static readonly Dictionary<string, ImageSource?> Cache = new();
    static readonly string LoaderPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(CorePath))!, "loader.swf");
    static SwfRenderer? _core, _loader;

    public static ImageSource? Get(string name)
    {
        lock (Cache) return Build(name);
    }

    static Drawing? Symbol(SwfRenderer? swf, string name) => swf != null && swf.Exports.ContainsKey(name) ? swf.RenderSymbol(name) : null;

    static ImageSource? Build(string name)
    {
        if (Cache.TryGetValue(name, out var img)) return img;
        img = null;
        try
        {
            _core ??= File.Exists(CorePath) ? SwfRenderer.Load(File.ReadAllBytes(CorePath)) : null;
            // core.swf (fiche du personnage), sinon loader.swf (infobulle des objets : IconEarthAlt, CriticalHit…)
            _loader ??= File.Exists(LoaderPath) ? SwfRenderer.Load(File.ReadAllBytes(LoaderPath)) : null;
            if ((Symbol(_core, "Icon" + name) ?? Symbol(_loader, "Icon" + name) ?? Symbol(_loader, name)) is { } drawing)
            {
                var di = new DrawingImage(drawing);
                di.Freeze();
                img = di;
            }
        }
        catch { } // icône illisible : l'abréviation reste affichée
        return Cache[name] = img;
    }
}

/// <summary>
/// Icônes des sorts, comme dans le jeu, rendues depuis les fichiers du client installé : le fond
/// (clips/spells/icons/back/&lt;b&gt;.swf : « _spellBackground » et « _spellFrame ») puis le symbole
/// (icons/up/&lt;up&gt;.swf : « _spellPrint »), colorés selon le fichier de langue des sorts.
/// Lecture seule : le jeu n'est jamais modifié.
/// </summary>
public static class SpellIcons
{
    static readonly string IconsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ankama", "Retro", "resources", "app", "retroclient", "clips", "spells", "icons");

    static readonly Dictionary<int, ImageSource?> Cache = new();

    static Color Rgb(int c) => Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);

    static Drawing? Load(string dir, int id, Dictionary<string, Color> tints)
    {
        var path = Path.Combine(IconsDir, dir, id + ".swf");
        return File.Exists(path) ? SwfRenderer.Render(File.ReadAllBytes(path), tints) : null;
    }

    public static ImageSource? Get(int spellId, SpellLook? look)
    {
        lock (Cache) return Build(spellId, look);
    }

    static ImageSource? Build(int spellId, SpellLook? look)
    {
        if (Cache.TryGetValue(spellId, out var img)) return img;
        img = null;
        try
        {
            // Sans données de langue : symbole blanc sur fond gris, comme un sort sans élément
            look ??= new SpellLook(1, spellId, 0x6E6E6E, 0xFFFFFF, 0xFFFFFF);
            var group = new DrawingGroup();
            if (Load("back", look.Back, new() { ["_spellBackground"] = Rgb(look.Background), ["_spellFrame"] = Rgb(look.Frame) }) is { } back)
                group.Children.Add(back);
            if (Load("up", look.Up, new() { ["_spellPrint"] = Rgb(look.Print) }) is { } print)
                group.Children.Add(print);
            if (group.Children.Count > 0)
            {
                var di = new DrawingImage(group);
                di.Freeze();
                img = di;
            }
        }
        catch { } // icône illisible : le nom du sort reste affiché
        Cache[spellId] = img;
        return img;
    }
}
