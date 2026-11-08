using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DofusRetroManager.Data;
using DofusRetroManager.Net;

namespace DofusRetroManager;

public sealed record FmItemOption(long Uid, string Label)
{
    public override string ToString() => Label; // texte affiché dans la liste déroulante
}

/// <summary>Passage de rune. Code : SC, SN, EC (échec avec perte) ou EC0 (échec sans perte visible).</summary>
public sealed record FmHistoryRow(int Index, string Time, int RuneId, string Rune, string Outcome, string Code,
    string Gains, string Losses, string Weight, string Reliquat, IReadOnlyList<FmChange> GainList, IReadOnlyList<FmChange> LossList);

/// <summary>Gain ou perte d'un passage : caractéristique (icône), valeur signée (« +3 ») et texte complet (survol).</summary>
public sealed record FmChange(int Char, string Value, string Text);

/// <summary>Ligne de l'objet, comme dans le jeu : texte (« +366 en initiative »), caractéristique (icône), malus, jet du modèle.</summary>
public sealed record FmStatLine(int Char, string Text, bool Neg, string Range, int Max);

/// <summary>Chances d'une rune, pour la page : pourcentages arrondis et explication au survol.</summary>
public sealed record FmRuneCell(int RuneId, string Rune, int Value, double Sc, double Sn, double Ec, bool Over, string Tip);

/// <summary>Ligne du mode « Chances » : caractéristique, jet, valeur actuelle (et sa place dans le jet), poids et runes.</summary>
public sealed record FmChanceRow(int Char, string Name, int Value, string Min, int Max, double Fill, string Weight, bool Exo,
    IReadOnlyList<FmRuneCell> Runes);

/// <summary>Ligne de l'écran « Poids des stats » (onglet FM) : rune, stat, poids par point et valeur par défaut.</summary>
public sealed record FmWeightRow(int Stat, int RuneId, string Rune, string Label, string Weight);

public partial class MainWindow
{
    // --- Forgemagie ---

    Fm.FmTracker? _fm;
    bool _fmUpdating;

    void InitFm(GameData data)
    {
        _fm = new Fm.FmTracker(data);
        _fm.Log += AddLog;
        _fm.Changed += RefreshFm;
        _tracker.CraftOpened += _fm.OnCraftOpened;
        _tracker.CraftItemSeen += _fm.OnCraftItemSeen;
        _tracker.CraftInfo += _fm.OnCraftInfo;
        _tracker.CraftResult += _fm.OnCraftResult;
        _tracker.CraftItemConsumed += (uid, tpl, count) => _fm.OnItemConsumed(uid, tpl, count);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => _fm.Tick();
        timer.Start();
        RefreshFm();
    }

    void RefreshFm()
    {
        if (_fm == null || _data == null) return;
        RefreshFmWeights();
        _fmUpdating = true;
        // Liste courte : le nom et le nombre de runes ; plusieurs exemplaires du même objet : la date les distingue
        var all = _fm.Items.Values.OrderByDescending(i => i.LastUsed).ToList();
        var options = all
            .Select(i => new FmItemOption(i.Uid, $"{_data.GetItem(i.TemplateId).Name} · {i.History.Count} rune(s)"
                + (all.Count(o => o.TemplateId == i.TemplateId) > 1 ? $" · {i.LastUsed:dd/MM HH:mm}" : "")))
            .ToList();
        FmItemCombo.ItemsSource = options;
        FmItemCombo.SelectedItem = options.FirstOrDefault(o => o.Uid == _fm.CurrentUid);
        _fmUpdating = false;

        var item = _fm.CurrentUid is { } uid ? _fm.Items.GetValueOrDefault(uid) : null;
        FmEmpty.Visibility = item == null || item.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (item == null)
        {
            FmItemName.Text = "Aucun objet";
            FmItemSub.Text = FmItemTpl.Text = FmChancesNote.Text = "";
            FmReliquat.Text = "0";
            SetRows(FmStats, Array.Empty<FmStatLine>());
            SetRows(FmChances, Array.Empty<FmChanceRow>());
            SetRows(FmHistoryGrid, Array.Empty<FmHistoryRow>());
            return;
        }

        var info = _data.GetItem(item.TemplateId);
        FmItemName.Text = info.Name;
        FmItemSub.Text = $"Niv. {info.Level} · {info.TypeName}";
        FmItemTpl.Text = item.TemplateId.ToString();
        FmReliquat.Text = item.Reliquat.ToString("0.##");
        // Lignes dans l'ordre du jeu, avec le jet du modèle au survol (minimum observé à l'HDV ou dans une banque)
        SetRows(FmStats, _fm.Lines(item.Effects).Select(l =>
        {
            int max = info.MaxStats.GetValueOrDefault(l.Characteristic);
            var roll = _rolls.Of(info.Id, l.Characteristic);
            var range = max > 0 ? $"Jet : {(roll != null && roll.Min < max ? roll.Min + " à " : "jusqu'à ")}{max}" : l.Value > 0 ? "Ligne exotique" : "";
            return new FmStatLine(l.Characteristic, _fm.Describe(l.Stat, l.Value), l.Value < 0, range, l.Value > 0 ? max : 0);
        }).ToList());
        RefreshFmChances(info, item);

        string Changes(IEnumerable<Fm.FmStatChange> changes) =>
            string.Join(", ", changes.Select(c => _fm.Describe(c.Stat, c.Delta)));
        List<FmChange> Chips(IEnumerable<Fm.FmStatChange> changes) =>
            changes.Select(c => new FmChange(_fm.Characteristic(c.Stat), c.Delta.ToString("+0;-0"), _fm.Describe(c.Stat, c.Delta))).ToList();

        SetRows(FmHistoryGrid, item.History
            .Select((ev, i) => new FmHistoryRow(
                i + 1,
                ev.Time.ToString("HH:mm:ss"),
                ev.RuneTemplateId ?? 0,
                ev.RuneTemplateId is { } r ? _data.GetItem(r).Name : "?",
                ev.Outcome,
                ev.Outcome switch
                {
                    Fm.FmTracker.SuccesCritique => "SC",
                    Fm.FmTracker.SuccesNeutre => "SN",
                    Fm.FmTracker.EchecSansEffet => "EC0",
                    _ => "EC",
                },
                Changes(ev.Changes.Where(c => c.Delta > 0)),
                Changes(ev.Changes.Where(c => c.Delta < 0)),
                $"+{ev.WeightGained:0.##} / -{ev.WeightLost:0.##}",
                ev.ReliquatAfter.ToString("0.##"),
                Chips(ev.Changes.Where(c => c.Delta > 0)),
                Chips(ev.Changes.Where(c => c.Delta < 0))))
            .Reverse() // le plus récent en haut
            .ToList());
    }

    /// <summary>
    /// Mode « Chances » : chaque ligne de l'objet (celles du modèle, puis les exotiques) et, pour chacune de ses runes,
    /// les chances de succès critique, neutre et d'échec (formule de FmChances), le détail au survol.
    /// </summary>
    void RefreshFmChances(Data.ItemInfo info, Fm.FmItem item)
    {
        if (_fm == null || _data == null) return;
        var current = ItemStats.FromParsed(_fm.ParseStats(item.Effects));
        // Même ordre que l'objet dans le jeu ; les lignes du modèle absentes de l'objet à la fin
        var order = _fm.Lines(item.Effects).Select(l => l.Characteristic).ToList();
        var lines = Fm.FmChances.Compute(_data, _fm, info, current, c => _rolls.Of(info.Id, c)?.Min)
            .OrderBy(l => order.IndexOf(l.Characteristic) is var i && i >= 0 ? i : int.MaxValue)
            .ThenByDescending(l => ItemStats.All.FirstOrDefault(s => s.Characteristic == l.Characteristic) is { } s ? _fm.Priority(s.Effects[0]) : 0)
            .ToList();
        static string Pct(double v) => v < 10 ? v.ToString("0.#") : v.ToString("0");
        SetRows(FmChances, lines.Select(l => new FmChanceRow(l.Characteristic, l.Name, l.Value, l.Min?.ToString() ?? "?", l.Max,
            l.Max > 0 ? Math.Clamp((double)l.Value / l.Max, 0, 1.2) : 0, Num(l.Weight), l.Exo,
            l.Runes.Select(r => new FmRuneCell(r.RuneId, r.Rune, r.Value, Math.Round(r.Sc, 1), Math.Round(r.Sn, 1), Math.Round(r.Ec, 1), r.Over,
                $"{r.Rune} (+{r.Value}) : {l.Value} → {l.Value + r.Value}{(r.Over ? (l.Max > 0 ? $" (au-dessus du max {l.Max})" : " (ligne exotique)") : "")}\n"
                + $"SC {Pct(r.Sc)} % · SN {Pct(r.Sn)} % · EC {Pct(r.Ec)} %\nDifficulté D = {r.D:0}")).ToList())).ToList());
        FmChancesNote.Text = $"Niveau {info.Level} · poids de l'objet {Num(lines.Sum(l => l.Weight))}";
    }

    void FmItemCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_fmUpdating || _fm == null || FmItemCombo.SelectedItem is not FmItemOption o) return;
        _fm.Select(o.Uid);
        RefreshFm();
    }

    void FmSetReliquat_Click(object sender, RoutedEventArgs e)
    {
        if (_fm?.CurrentUid is not { } uid) return;
        var text = FmReliquatInput.Text.Trim().Replace(',', '.');
        if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            AddLog("Reliquat invalide : saisissez un nombre.");
            return;
        }
        _fm.SetReliquat(uid, value);
        FmReliquatInput.Text = "";
    }

    void FmClear_Click(object sender, RoutedEventArgs e)
    {
        if (_fm?.CurrentUid is not { } uid) return;
        if (_webConfirmed || MessageBox.Show(this, "Effacer l'historique de cet objet et remettre son reliquat à 0 ?", "Forgemagie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _fm.ClearHistory(uid);
    }

    void FmForget_Click(object sender, RoutedEventArgs e)
    {
        if (_fm?.CurrentUid is not { } uid) return;
        if (_webConfirmed || MessageBox.Show(this, "Retirer cet objet et tout son historique de la liste ?", "Forgemagie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _fm.Forget(uid);
    }

    // --- Poids des stats : affichés à la place de l'historique (lecture seule) ---

    void FmWeights_Click(object sender, RoutedEventArgs e)
    {
        bool show = FmWeightsPanel.Visibility != Visibility.Visible;
        FmWeightsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        FmWeights.Content = show ? "Retour à l'historique" : "Poids des stats";
    }

    static string Num(double v) => v.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture);

    void RefreshFmWeights()
    {
        if (_fm == null || _data == null) return;
        SetRows(FmWeightsGrid, _fm.WeightList().Select(w => new FmWeightRow(w.Stat, w.RuneId,
            w.Rune.Length > 0 ? w.Rune : "—", _data.FormatEffect(w.Stat, 1), Num(w.Weight)
            )).ToList());
    }
}
