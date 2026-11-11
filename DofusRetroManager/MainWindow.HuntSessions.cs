using System.Windows;
using System.Windows.Controls;
using DofusRetroManager.Hunt;

namespace DofusRetroManager;

/// <summary>Onglet Combat : historique des sessions et détail d'une session (personnages, monstres, objets, combats).</summary>
public partial class MainWindow
{
    /// <summary>
    /// Historique des sessions (tous les personnages), la session en cours en tête : durée, combats, XP, kamas, drops au
    /// prix HDV actuel (comme la session en cours), total et rythme par heure ; le détail par personnage au survol.
    /// </summary>
    void ShowSessionHistory()
    {
        if (_hunt == null) return;
        double Drops(SessionTotals t) => t.Drops.Sum(d => d.Value * (UnitPrice(d.Key) ?? 0));
        var current = _hunt.CurrentSession();
        var list = _hunt.Sessions.Select(s => (s, now: false)).ToList();
        if (current != null) list.Add((current, true));
        _sessionRowsUpdating = true;
        SetRows(HuntSessionGrid, list.AsEnumerable().Reverse().Select(x =>
        {
            var (s, now) = x;
            double drops = Drops(s.Total), total = s.Total.Kamas + drops, minutes = s.Seconds / 60;
            var hours = s.Seconds >= 60 ? s.Seconds / 3600 : (double?)null;
            var tip = string.Join("\n", s.Characters.OrderByDescending(c => c.Value.Xp).Select(c =>
                $"{c.Key} : {c.Value.Fights} combat(s) · {c.Value.Xp:N0} XP · {c.Value.Kamas:N0} kamas · drops {Kamas(Drops(c.Value))}"))
                + (s.Rebuilt ? "\nReconstituée d'après les combats gardés (pauses inconnues)." : "");
            return new SessionRow(
                now ? "En cours" : s.Start.ToString("dd'/'MM HH:mm"), s.Start,
                minutes >= 60 ? $"{(int)(minutes / 60)} h {(int)(minutes % 60):00}" : $"{minutes:0} min", minutes,
                s.Total.Fights, s.Characters.Count, s.Total.Xp, s.Total.Kamas, drops, total,
                hours is { } h ? total / h : null, hours is { } h2 ? s.Total.Xp / h2 : null, tip, now, s.Rebuilt)
            { Kills = s.Total.Monsters.Count > 0 ? s.Total.Monsters.Values.Sum() : null };
        }).ToList());
        // Session dont le détail est ouvert : reste sélectionnée, et son détail suit (session en cours : nouveaux combats)
        HuntSessionGrid.SelectedItem = (HuntSessionGrid.ItemsSource as IEnumerable<SessionRow>)?.FirstOrDefault(r => r.StartSort == _sessionDetail);
        _sessionRowsUpdating = false;
        ShowSessionDetail();
    }

    /// <summary>Session dont le détail est affiché (clic sur une ligne de l'historique), retrouvée par son début.</summary>
    DateTime? _sessionDetail;
    bool _sessionRowsUpdating;

    void HuntSessionGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_sessionRowsUpdating) return;
        _sessionDetail = (HuntSessionGrid.SelectedItem as SessionRow)?.StartSort;
        ShowSessionDetail();
    }

    /// <summary>
    /// Détail d'une session (fenêtre de la page) : chaque personnage (combats, XP, kamas, drops, total, gain par heure)
    /// et chaque objet obtenu (quantité, prix unitaire HDV actuel, total, et qui l'a obtenu au survol).
    /// </summary>
    void ShowSessionDetail()
    {
        var s = _hunt == null || _sessionDetail is not { } start ? null
            : _hunt.Sessions.FirstOrDefault(x => x.Start == start) ?? (_hunt.CurrentSession() is { } cur && cur.Start == start ? cur : null);
        if (s == null || _data == null)
        {
            HuntSessionDetailTitle.Text = "";
            SetRows(HuntSessionChars, Array.Empty<SessionCharRow>());
            SetRows(HuntSessionDrops, Array.Empty<HuntDropRow>());
            SetRows(HuntSessionMobs, Array.Empty<MonsterChip>());
            SetRows(HuntSessionFights, Array.Empty<SessionFightRow>());
            return;
        }
        ShowSessionFights(s);
        double hours = Math.Max(s.Seconds, 60) / 3600;
        double Drops(SessionTotals t) => t.Drops.Sum(d => d.Value * (UnitPrice(d.Key) ?? 0));
        SetRows(HuntSessionChars, s.Characters.Select(c =>
        {
            double drops = Drops(c.Value), total = c.Value.Kamas + drops;
            return new SessionCharRow(c.Key, _hunt!.LookOf(c.Key)?.Gfx ?? 0, c.Value.Fights, c.Value.Xp, c.Value.Kamas, drops, total, total / hours, c.Value.Xp / hours)
                { Kills = c.Value.Monsters.Count > 0 ? c.Value.Monsters.Values.Sum() : null };
        }).OrderByDescending(r => r.Total).ToList());
        // Monstres tués : illustration et nombre, du plus tué au moins tué (un combat de plusieurs personnages compte une fois)
        SetRows(HuntSessionMobs, s.Total.Monsters.Select(m => new MonsterChip(m.Key, _data.GetMonster(m.Key).Name, 0, m.Value))
            .OrderByDescending(m => m.Count).ThenBy(m => m.Name).ToList());
        SetRows(HuntSessionDrops, s.Total.Drops.Select(d => new HuntDropRow(d.Key, _data.GetItem(d.Key).Name, d.Value, DropPrice(d.Key))
            {
                Who = string.Join(", ", s.Characters.Where(c => c.Value.Drops.ContainsKey(d.Key)).OrderByDescending(c => c.Value.Drops[d.Key])
                    .Select(c => $"{c.Key} ×{c.Value.Drops[d.Key]}")),
            })
            .OrderByDescending(r => r.Total ?? -1).ThenBy(r => r.Name).ToList());
        // Dates seulement : les totaux (durée, combats, XP, kamas) sont affichés en tuiles d'après la ligne de la session
        HuntSessionDetailTitle.Text = $"{s.Start:dd'/'MM HH:mm} → {s.End:HH:mm}" + (s.Rebuilt ? " · reconstituée (pauses inconnues)" : "");
    }

    /// <summary>
    /// Combats de la session (détail, onglet « Combats »), du plus récent au plus ancien : vos personnages du même combat
    /// réunis (XP, kamas et objets additionnés). Seuls les combats encore gardés sont listés (les plus anciens sont oubliés).
    /// </summary>
    void ShowSessionFights(SessionSummary s)
    {
        var fights = new List<List<FightRecord>>();
        foreach (var f in _hunt!.Fights.Where(f => f.Time >= s.Start && f.Time <= s.End).OrderBy(f => f.Time))
            if (fights.FindLast(g => IsSameFight(g[0], f)) is { } same) same.Add(f);
            else fights.Add([f]);
        SetRows(HuntSessionFights, fights.AsEnumerable().Reverse().Select(g =>
        {
            var f = g[0];
            var drops = g.SelectMany(x => x.Drops).GroupBy(d => d.ItemId)
                .Select(x => new HuntDropRow(x.Key, _data!.GetItem(x.Key).Name, x.Sum(d => d.Quantity), DropPrice(x.Key))
                {
                    Who = string.Join(", ", g.Where(c => c.Drops.Any(d => d.ItemId == x.Key))
                        .Select(c => $"{c.Character} ×{c.Drops.Where(d => d.ItemId == x.Key).Sum(d => d.Quantity)}")),
                })
                .OrderByDescending(d => d.Total ?? -1).ToList();
            return new SessionFightRow(f.Time.ToString("HH:mm"), f.Time, $"{f.DurationSeconds / 60}:{f.DurationSeconds % 60:00}",
                string.Join(", ", g.Select(x => x.Character).Distinct()), g.Count, g.Sum(x => x.Xp), g.Sum(x => x.Kamas), drops.Sum(d => d.Total ?? 0))
            {
                Mobs = f.Monsters.GroupBy(m => m.Id).Select(x => new MonsterChip(x.Key,
                    $"{_data!.GetMonster(x.Key).Name} (niv. {string.Join(", ", x.Select(m => m.Level))})", x.Max(m => m.Level), x.Count())).ToList(),
                Drops = drops,
            };
        }).ToList());
    }

    void HuntSessionForget_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SessionRow { Current: false } row) _hunt?.ForgetSession(row.StartSort);
    }
}
