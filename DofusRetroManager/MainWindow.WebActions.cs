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
    // --- Actions de la page ---

    /// <summary>Action en cours confirmée par la page : les gestionnaires sautent leur propre confirmation (MessageBox).</summary>
    bool _webConfirmed;

    Dictionary<string, WebControl>? _webControls;

    /// <summary>Contrôle (sans affichage) par son nom : les champs WebControl de la fenêtre (MainWindow.Controls.cs).</summary>
    WebControl? Control(string name)
    {
        _webControls ??= GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => typeof(WebControl).IsAssignableFrom(f.FieldType))
            .ToDictionary(f => f.Name, f => (WebControl)f.GetValue(this)!);
        return _webControls.GetValueOrDefault(name);
    }

    /// <summary>Lignes d'un tableau, dans l'ordre envoyé à la page.</summary>
    static IList? RowsOf(WebList list) => list.ItemsSource as IList ?? list.ItemsSource?.Cast<object>().ToList();

    void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var m = doc.RootElement;
            var name = m.TryGetProperty("name", out var n) ? n.GetString() : null;
            var el = name != null ? Control(name) : null;
            int index = m.TryGetProperty("index", out var ix) ? ix.GetInt32() : -1;
            switch (m.GetProperty("type").GetString())
            {
                case "watch":
                    // La page ne suit que l'onglet affiché : nouvelle liste à chaque changement d'onglet
                    _webWatched = m.GetProperty("names").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
                    _webForce = true;
                    break;
                case "theme":
                    ThemeManager.Apply(m.GetProperty("value").GetString() ?? ThemeManager.Crepuscule);
                    break;
                case "style":
                    ThemeManager.ApplyStyle(m.GetProperty("value").GetString() ?? ThemeManager.Classique);
                    break;
                case "set":
                    var value = m.GetProperty("value");
                    switch (el)
                    {
                        case WebTextBox tb: tb.Text = value.GetString() ?? ""; break;
                        case WebCombo cb: cb.SelectedIndex = value.GetInt32(); break;
                        case WebTabs tc: tc.SelectedIndex = value.GetInt32(); break;
                        case WebToggle tg: tg.IsChecked = value.GetBoolean(); break;
                        case WebSlider range: range.Value = value.GetDouble(); break;
                    }
                    break;
                case "click":
                    if (el is WebButton { IsEnabled: true } button)
                    {
                        // Déjà confirmé dans la page (ex. « Oublier l'objet » de l'onglet FM) : pas de seconde confirmation Windows
                        _webConfirmed = m.TryGetProperty("confirmed", out var c) && c.GetBoolean();
                        try { button.RaiseClick(); }
                        finally { _webConfirmed = false; }
                    }
                    break;
                case "show":
                    // Lien vers un objet : ingrédient d'une recette -> HDV, objet du brisage -> sa recette
                    if (m.TryGetProperty("id", out var shownId) && shownId.TryGetInt32(out var shownItem))
                    {
                        var shownTab = m.TryGetProperty("tab", out var tab) ? tab.GetString() : null;
                        if (shownTab == "recipe")
                        {
                            if (!ShowRecipeOf(shownItem)) AddLog($"{_data?.GetItem(shownItem).Name} : pas de recette.");
                        }
                        else if (shownTab == "pda") ShowInPda(shownItem); // id : le monstre (onglet Ocre)
                        else ShowInHdv(shownItem);
                    }
                    break;
                case "select":
                    if (el is WebList grid && RowsOf(grid) is { } rows && index >= 0 && index < rows.Count) grid.SelectedItem = rows[index];
                    break;
                case "stat":
                    // Seuil du Brisage : field = on (case), min ou max
                    if (_statFilters.ElementAtOrDefault(index) is { } filter)
                    {
                        var v = m.GetProperty("value");
                        switch (m.GetProperty("field").GetString())
                        {
                            case "on": filter.Box.IsChecked = v.GetBoolean(); break;
                            case "min": filter.Min.Text = v.GetString() ?? ""; break;
                            case "max": filter.Max.Text = v.GetString() ?? ""; break;
                        }
                    }
                    break;
                case "reorder":
                    // Glisser-déposer de l'onglet Switcheur : ligne index déposée sur la ligne to
                    int to = m.GetProperty("to").GetInt32();
                    if (el == OrganizerGrid && OrganizerGrid.ItemsSource is IList<OrganizerRow> organizerRows
                        && index >= 0 && index < organizerRows.Count && to >= 0 && to < organizerRows.Count)
                        MoveInOrder(organizerRows[index], organizerRows[to]);
                    break;
                case "toggle":
                    if (el is WebPanel panel && panel.Children.OfType<ToggleButton>().ElementAtOrDefault(index) is { } toggle)
                        toggle.IsChecked = toggle.IsChecked != true;
                    break;
                case "farm-del":
                    // Objectif de farm arrêté (index : identifiant de l'objet, 0 : tous)
                    _hunt?.RemoveFarm(index);
                    break;
                case "trade-del":
                    // « Mes échanges » : ligne oubliée (rapprochement erroné)
                    ForgetTrade(index);
                    break;
                case "trade-sold":
                    // « Mes échanges » : offre vendue hors ligne, validée à la main par le joueur
                    if (_trades != null && index >= 0 && index < _trades.Trades.Count) _trades.MarkSold(_trades.Trades[index]);
                    break;
                case "trade-clear":
                    // « Mes échanges » : vider les lignes des filtres choisis (personnage, période, type)
                    ForgetTrades(m.GetProperty("list").EnumerateArray().Select(x => x.GetInt32()).ToList());
                    break;
                case "fm-open":
                    // Onglet FM, liste des objets : l'objet cliqué devient l'objet affiché
                    _fm?.Select(m.GetProperty("uid").GetInt64());
                    RefreshFm();
                    break;
                case "fm-forget":
                    // Onglet FM, liste des objets : objets cochés retirés avec leur historique
                    _fm?.Forget(m.GetProperty("list").EnumerateArray().Select(x => x.GetInt64()).ToList());
                    break;
                case "cell":
                    // Case cliquée sur la carte de la page : zone, points d'intérêt, ressources
                    ShowCarteCellInfo(m.GetProperty("sa").GetInt32(), m.GetProperty("x").GetInt32(), m.GetProperty("y").GetInt32());
                    break;
                case "minimap-res":
                    // Ressources cochées dans l'onglet Carte : celles de la mini-carte en jeu
                    SetMiniMapResources(m.GetProperty("keys").EnumerateArray().Select(x => x.GetInt32()));
                    break;
                case "row":
                    var handler = m.GetProperty("handler").GetString();
                    if (handler != null && WebRowHandlers.Contains(handler) && el is WebList rowList && RowsOf(rowList) is { } list
                        && index >= 0 && index < list.Count)
                        GetType().GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)?
                            .Invoke(this, [new FrameworkElement { DataContext = list[index] }, new RoutedEventArgs()]);
                    break;
            }
            PushWebState();
        }
        catch (Exception ex) { AddLog($"Interface HTML : action ignorée ({ex.Message})."); }
    }
}
