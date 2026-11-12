using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DofusRetroManager.Data;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DofusRetroManager;

public partial class MainWindow
{
    // --- Fichiers servis à la page ---

    void OnWebResource(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var path = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath.TrimStart('/'));
        // Icônes et tuiles de la carte : dessinées depuis les fichiers du jeu (près de 100 ms chacune la première fois),
        // par le fil de rendu des images, pour que l'interface et les raccourcis ne se figent pas pendant ce temps
        if (path.StartsWith("icon/") || path.StartsWith("carte/"))
        {
            var deferral = e.GetDeferral();
            RenderQueue.Add(() =>
            {
                var file = ReadWebFile(path);
                Dispatcher.BeginInvoke(() =>
                {
                    try { e.Response = WebResponse(file.Bytes, file.Type); }
                    finally { deferral.Complete(); }
                });
            });
            return;
        }
        var (bytes, type) = ReadWebFile(path);
        e.Response = WebResponse(bytes, type);
    }

    (byte[]? Bytes, string Type) ReadWebFile(string path)
    {
        try { return WebFile(path); }
        catch (Exception ex) { Ui(() => AddLog($"Interface HTML : {path} illisible ({ex.Message}).")); return (null, "text/plain"); }
    }

    CoreWebView2WebResourceResponse WebResponse(byte[]? bytes, string type) =>
        _web!.CoreWebView2.Environment.CreateWebResourceResponse(
            bytes == null ? null : new MemoryStream(bytes), bytes == null ? 404 : 200, bytes == null ? "Not Found" : "OK",
            $"Content-Type: {type}\r\nCache-Control: no-cache");

    /// <summary>
    /// Fil de rendu des images de la page (une file, un seul fil STA, comme l'exige le rendu WPF). Les images produites
    /// sont figées (Freeze) et les caches d'icônes protégés par un verrou : l'interface peut les réutiliser.
    /// </summary>
    static readonly BlockingCollection<Action> RenderQueue = StartRenderThread();

    static BlockingCollection<Action> StartRenderThread()
    {
        var queue = new BlockingCollection<Action>();
        var thread = new Thread(() =>
        {
            foreach (var job in queue.GetConsumingEnumerable())
                try { job(); } catch { } // une image illisible ne bloque pas les suivantes
        })
        { IsBackground = true, Name = "Rendu des images" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return queue;
    }

    (byte[]? Bytes, string Type) WebFile(string path)
    {
        var icon = Regex.Match(path, @"^icon/(item|spell|head|chall|monster)/(\d+)\.png$");
        if (icon.Success)
        {
            if (!_webIcons.TryGetValue(path, out var png))
            {
                int id = int.Parse(icon.Groups[2].Value);
                _webIcons[path] = png = Png(icon.Groups[1].Value switch
                {
                    "item" => ItemIcons.Get(id),
                    "head" => CharacterIcons.Get(id), // tête du personnage : classe × 10 + sexe
                    "chall" => ChallengeIcons.Get(id), // challenge : son icône (champ g du fichier de langue)
                    // monstre : son illustration (champ g du fichier de langue), marges transparentes rognées, en plus grand
                    "monster" => Trim(MonsterIcons.Get(id)),
                    _ => SpellIcons.Get(id, _data?.SpellLooks.GetValueOrDefault(id)),
                }, icon.Groups[1].Value == "monster" ? 128 : 64);
            }
            return (png, "image/png");
        }
        // Caractéristiques de la fiche du personnage (sagesse, prospection…), lues dans l'interface du client
        // Kama : symbole des kamas ; Group : icône « Amis » du bandeau du jeu (deux personnages), pour le groupe
        var stat = Regex.Match(path, @"^icon/stat/(Wisdom|PP|Init|Vita|Kama|Group)\.png$");
        if (stat.Success)
        {
            if (!_webIcons.TryGetValue(path, out var png))
                _webIcons[path] = png = Png(StatIcons.Get(stat.Groups[1].Value switch
                {
                    "Kama" => "KamaSymbol",
                    "Group" => "UI_BannerFriendsIcon",
                    var n => n,
                }), 64);
            return (png, "image/png");
        }
        // Effets d'objet (onglet FM) : icône de l'infobulle de l'objet en jeu (voir CharacteristicIcons)
        var effect = Regex.Match(path, @"^icon/effect/(\d+)\.png$");
        if (effect.Success)
        {
            if (!_webIcons.TryGetValue(path, out var png))
            {
                CharacteristicIcons.Data ??= _data;
                _webIcons[path] = png = Png(CharacteristicIcons.Get(int.Parse(effect.Groups[1].Value)), 64);
            }
            return (png, "image/png");
        }
        // Plus petite rune d'une caractéristique (exos PA / PM / PO de l'onglet FM)
        var rune = Regex.Match(path, @"^icon/rune/(\d+)\.png$");
        if (rune.Success)
        {
            if (_data == null) return (null, "image/png"); // données du jeu pas encore chargées : rien en cache
            if (!_webIcons.TryGetValue(path, out var png))
                _webIcons[path] = png = RuneBreaking.SmallestRuneId(_data, int.Parse(rune.Groups[1].Value)) is { } id
                    ? Png(ItemIcons.Get(id), 64) : null;
            return (png, "image/png");
        }
        // Carte : tuiles et icônes rendues depuis les fichiers du client installé (rien n'est livré avec l'application)
        if (path.StartsWith("carte/")) return (CarteWebFile(path), "image/png");
        if (Regex.IsMatch(path, @"^fonts/PlusJakartaSans-\w+\.ttf$"))
        {
            using var font = Application.GetResourceStream(new Uri("pack://application:,,,/Fonts/" + path[6..]))?.Stream;
            return (font == null ? null : ReadAll(font), "font/ttf");
        }
        // Journal des changements (bouton « Nouveautés ») : un script, la page n'ayant pas le droit de faire de requêtes
        if (path == "changelog.js")
            return (Encoding.UTF8.GetBytes("window.CHANGELOG = " + JsonSerializer.Serialize(new
            {
                current = UpdateChecker.CurrentText,
                entries = Changelog.Entries.Select(e => new { version = e.Version, date = e.Date, items = e.Items }),
            }) + ";"), "text/javascript; charset=utf-8");
        // Styles et scripts de la page, regroupés en un fichier chacun : deux requêtes au lieu d'une par fichier source
        // (chaque requête passe par le fil de l'interface, occupé au démarrage)
        if (path == "bundle.css") return (_webCss ??= Bundle(WebCssFiles), "text/css; charset=utf-8");
        if (path == "bundle.js") return (_webJs ??= Bundle(WebJsFiles), "text/javascript; charset=utf-8");
        // Page : Web/index.html, logo… intégrés à l'exe
        using var res = typeof(MainWindow).Assembly.GetManifestResourceStream("web/" + path);
        var mime = System.IO.Path.GetExtension(path) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };
        return (res == null ? null : ReadAll(res), mime);
    }

    /// <summary>
    /// Fichiers de la page (Web/), dans leur ordre de chargement : couleurs et thèmes, styles communs, puis un fichier par onglet ;
    /// pour les scripts, le cœur (état, utilitaires), un fichier par onglet, puis le rendu et les événements (app.js).
    /// Un nouveau fichier de Web/ s'ajoute ici.
    /// </summary>
    static readonly string[] WebCssFiles =
        ["theme", "app", "recettes", "hdv", "combat", "brisage", "ttg", "fm", "switcheur", "carte", "patrimoine", "metier", "elevage"];
    static readonly string[] WebJsFiles =
        ["core", "banque", "recettes", "hdv", "combat", "brisage", "ttg", "pda", "ocre", "fm", "switcheur", "carte", "patrimoine", "metier", "elevage", "app"];
    static byte[]? _webCss, _webJs;

    static byte[] Bundle(string[] names)
    {
        var ext = names == WebCssFiles ? ".css" : ".js";
        using var ms = new MemoryStream();
        foreach (var name in names)
        {
            using var res = typeof(MainWindow).Assembly.GetManifestResourceStream("web/" + name + ext)
                ?? throw new FileNotFoundException("Fichier de la page absent de l'exe : " + name + ext);
            res.CopyTo(ms);
            ms.WriteByte((byte)'\n');
        }
        return ms.ToArray();
    }

    static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Icône vectorielle du jeu rendue en PNG carré (proportions gardées, centrée).</summary>
    /// <summary>Image sans ses marges transparentes (illustrations de monstres : le dessin occupe toute la vignette).</summary>
    static ImageSource? Trim(ImageSource? img)
    {
        if (img == null || img.Width <= 0 || img.Height <= 0) return img;
        int w = (int)Math.Ceiling(img.Width), h = (int)Math.Ceiling(img.Height);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawImage(img, new Rect(0, 0, img.Width, img.Height));
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var px = new byte[w * h * 4];
        bitmap.CopyPixels(px, w * 4, 0);
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] > 8)
                {
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
        if (x1 < 0) return img;
        var cropped = new CroppedBitmap(bitmap, new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
        cropped.Freeze();
        return cropped;
    }

    static byte[]? Png(ImageSource? img, int size)
    {
        if (img == null || img.Width <= 0 || img.Height <= 0) return null;
        double scale = size / Math.Max(img.Width, img.Height);
        double w = img.Width * scale, h = img.Height * scale;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawImage(img, new Rect((size - w) / 2, (size - h) / 2, w, h));
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
