using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DofusRetroManager.Data;

namespace DofusRetroManager;

public sealed record BrisageRow(int ItemId, string Name, int Level, string Type, string Stats, string Where,
    int Owned, int? Craftable, string Missing, double? CraftCost, double? HdvPrice, int Score,
    List<RuneYield> Runes, string RunesText, double? RunesValue, double? Reference, double? Gain)
{
    /// <summary>Caractéristiques à afficher en icônes (rune de la caractéristique + valeur ou fourchette, détail au survol).</summary>
    public List<StatChip> Chips { get; init; } = [];
    public int Index { get; init; }
    public int GainSign => Gain is { } g ? Math.Sign(g) : 0;
    /// <summary>Gain rapporté au prix de référence (prix HDV de l'objet, ou coût de fabrication), en fraction (0,25 = +25 %).</summary>
    public double? GainPercent => Reference > 0 ? Gain / Reference : null;
    /// <summary>Colonne « Faisable » : « ×3 », sinon les ingrédients manquants.</summary>
    public bool CanCraft => Craftable > 0;
    public string CraftText => Craftable > 0 ? $"×{Craftable}" : Missing;
}

/// <summary>
/// Caractéristique d'un objet, en compact : icône de la rune (sinon l'abréviation), valeur (« 400 », « 351–400 »,
/// « -20 »), texte complet au survol. On : caractéristique filtrée ; Neg : malus.
/// </summary>
public sealed record StatChip(int? Rune, string Short, string Value, string Tip, bool On, bool Neg)
{
    /// <summary>Caractéristique (icône d'effet du détail, comme en FM), libellé complet et maximum du jeu (objet possédé).</summary>
    public int Char { get; init; }
    public string Text { get; init; } = "";
    public int Max { get; init; }
}

/// <summary>Rune du détail : quantité estimée, prix unitaire HDV et total.</summary>
public sealed record RuneRow(int ItemId, string Name, string Quantity, double? UnitPrice, double? Total);

/// <summary>
/// Onglet Brisage : cherche les objets dont les caractéristiques atteignent des seuils (ex. 104 en vitalité
/// pour générer une rune), parmi ses objets, les recettes faisables avec sa banque ou tout le jeu.
/// Simple affichage : le brisage se fait toujours à la main, en jeu (REGLES.md, règle 3).
/// </summary>
public partial class MainWindow
{
    sealed record StatFilter(StatDef Stat, string Short, int? RuneId, ToggleButton Box, TextBox Min, TextBox Max);

    readonly List<StatFilter> _statFilters = new();

    /// <summary>Abréviation des caractéristiques du Brisage (nom complet en infobulle), comme sur les runes.</summary>
    static readonly Dictionary<int, string> StatShort = new()
    {
        [11] = "Vi", [12] = "Sa", [10] = "Fo", [15] = "Int", [13] = "Cha", [14] = "Agi", [1] = "PA", [23] = "PM",
        [19] = "PO", [26] = "Invo", [18] = "CC", [17] = "% Do", [80] = "Do", [49] = "Soins", [44] = "Ini", [48] = "PP",
        [37] = "% Neu", [33] = "% Ter", [34] = "% Feu", [35] = "% Eau", [36] = "% Air",
        [87] = "Ré Neu", [83] = "Ré Ter", [84] = "Ré Feu", [85] = "Ré Eau", [86] = "Ré Air",
        [70] = "Pi", [69] = "% Pi", [76] = "% DoF", [50] = "Renvoi", [27] = "Esq PA", [28] = "Esq PM", [96] = "Pods",
    };

    readonly ItemRolls _rolls = new();
    readonly Dictionary<int, int?> _statRunes = new();

    int? StatRune(int characteristic) => _statRunes.TryGetValue(characteristic, out var r) ? r
        : _statRunes[characteristic] = _data == null ? null : RuneBreaking.SmallestRuneId(_data, characteristic);

    /// <summary>
    /// Caractéristiques en icônes. Objet possédé : sa valeur réelle. Modèle d'objet : le maximum du jeu, précédé du plus
    /// petit jet vu (HDV, banques) quand il est connu : « 351–400 ».
    /// </summary>
    List<StatChip> StatChips(ItemInfo item, IEnumerable<KeyValuePair<int, int>> stats, bool real, ICollection<int> active) =>
        stats.Select(kv =>
        {
            var name = ItemStats.Name(kv.Key);
            var roll = _rolls.Of(item.Id, kv.Key);
            int max = item.MaxStats.GetValueOrDefault(kv.Key);
            // Minimum officiel (fichier « itemstats » du jeu), sinon le plus bas vu (HDV, banques)
            int? official = OfficialMin(item.Id, kv.Key);
            int? min = official ?? roll?.Min;
            string value, tip;
            if (real)
            {
                value = $"{kv.Value:+0;-0}";
                tip = $"{name} : {kv.Value:+0;-0}" + (max > 0 ? $" (jet de l'objet : {(min is { } m0 && m0 < max ? m0 + " à " : "jusqu'à ")}{max})" : "");
            }
            else if (min is { } m && m > 0 && m < kv.Value)
            {
                value = $"{m}–{kv.Value}";
                tip = $"{name} : {m} à {kv.Value}\n" + (official != null ? "Fourchette du fichier du jeu" : $"Maximum : fichier du jeu ; minimum : le plus bas vu sur {roll!.Seen} exemplaire(s) (HDV, banques)");
            }
            else if (official != null)
            {
                value = kv.Value.ToString("+0;-0");
                tip = $"{name} : {kv.Value:+0;-0} (valeur fixe)";
            }
            else
            {
                value = kv.Value.ToString("+0;-0");
                tip = $"{name} : jusqu'à {kv.Value:+0;-0}\nLe jeu ne donne que le maximum ; le minimum s'apprend en consultant cet objet à l'HDV";
            }
            return new StatChip(StatRune(kv.Key), StatShort.GetValueOrDefault(kv.Key, name), value, tip, active.Contains(kv.Key), kv.Value < 0)
                {
                    Char = kv.Key, Max = real && max > 0 ? max : 0,
                    // Détail : fourchette du jet ; minimum jamais vu (offres de l'objet pas encore consultées à l'HDV) : « ? »
                    Text = $"{(!real && min == null && kv.Value > 0 ? $"?–{kv.Value}" : value)} {name}",
                };
        }).ToList();

    /// <summary>Minimum officiel par objet et caractéristique (fichier « itemstats » : 2e champ de chaque effet).</summary>
    readonly Dictionary<int, Dictionary<int, int>> _officialMin = new();

    int? OfficialMin(int item, int characteristic)
    {
        if (_data == null || _fm == null || !_data.ItemEffects.TryGetValue(item, out var effects)) return null;
        if (!_officialMin.TryGetValue(item, out var mins))
            _officialMin[item] = mins = ItemStats.FromParsed(_fm.ParseStats(effects));
        return mins.TryGetValue(characteristic, out var v) ? v : null;
    }

    /// <summary>
    /// Caractéristiques d'un modèle d'objet au jet choisi (BrisageRoll : 0 minimum, 1 moyen, 2 maximum) : fourchette
    /// officielle (« itemstats »), sinon le plus bas vu ; sans minimum connu, le maximum.
    /// </summary>
    IReadOnlyDictionary<int, int> RollStats(ItemInfo item, int? roll = null)
    {
        int mode = roll ?? BrisageRoll.SelectedIndex;
        if (mode == 2) return item.MaxStats;
        return item.MaxStats.ToDictionary(kv => kv.Key, kv =>
        {
            if (kv.Value <= 0) return kv.Value;
            int min = OfficialMin(item.Id, kv.Key) ?? _rolls.Of(item.Id, kv.Key)?.Min ?? kv.Value;
            min = Math.Clamp(min, 1, kv.Value);
            return mode == 0 ? min : (int)Math.Round((min + kv.Value) / 2.0, MidpointRounding.AwayFromZero);
        });
    }

    /// <summary>
    /// Qualité du jet d'un exemplaire (0 à 1) : pour chaque caractéristique à fourchette (minimum officiel &lt; maximum),
    /// (valeur − min) / (max − min), moyenne des caractéristiques ; au-delà du maximum (forgemagie) : plus de 1.
    /// Null si l'objet n'a pas de caractéristique à fourchette ou pas d'effets.
    /// </summary>
    double? RollQuality(ItemInfo item, string effects)
    {
        if (_fm == null || effects.Length == 0 || item.MaxStats.Count == 0) return null;
        var stats = ItemStats.FromParsed(_fm.ParseStats(effects));
        var parts = item.MaxStats.Where(kv => kv.Value > 0)
            .Select(kv => (Max: kv.Value, Min: OfficialMin(item.Id, kv.Key) ?? _rolls.Of(item.Id, kv.Key)?.Min, Value: stats.GetValueOrDefault(kv.Key)))
            .Where(x => x.Min is { } m && m < x.Max)
            .Select(x => (x.Value - x.Min!.Value) / (double)(x.Max - x.Min.Value))
            .ToList();
        return parts.Count > 0 ? Math.Max(0, parts.Average()) : null;
    }

    /// <summary>Objets qui ne se brisent pas (familiers, montiliers, Dofus) : pas de valeur de runes.</summary>
    static bool Breakable(ItemInfo item) =>
        !item.TypeName.Contains("Familier", StringComparison.OrdinalIgnoreCase)
        && !item.TypeName.Contains("Montilier", StringComparison.OrdinalIgnoreCase)
        && !item.TypeName.Equals("Dofus", StringComparison.OrdinalIgnoreCase);

    /// <summary>Valeur des runes d'un modèle d'objet au jet moyen (runes au prix HDV, sans prix : 0) ; null si aucune rune n'a de prix.</summary>
    double? BreakValue(ItemInfo item)
    {
        if (_data == null || item.MaxStats.Count == 0 || !Breakable(item)) return null;
        var priced = RuneBreaking.Estimate(_data, item.Level, RollStats(item, 1)).Where(r => UnitPrice(r.ItemId) != null).ToList();
        return priced.Count > 0 ? priced.Sum(r => r.Quantity * UnitPrice(r.ItemId)!.Value) : null;
    }

    /// <summary>Offres consultées à l'HDV : leurs caractéristiques réelles nourrissent les jets observés.</summary>
    void ObserveHdvRolls(string body)
    {
        if (_data == null || _fm == null) return;
        var parts = body.Split('|');
        if (!int.TryParse(parts[0], out var tpl) || !_data.Items.TryGetValue(tpl, out var item) || item.MaxStats.Count == 0) return;
        foreach (var line in parts.Skip(1))
        {
            var f = line.Split(';');
            if (f.Length >= 2 && long.TryParse(f[0], out var id) && f[1].Length > 0)
                _rolls.Observe(item, -id, ItemStats.FromParsed(_fm.ParseStats(f[1])));
        }
    }

    /// <summary>Objets de vos banques et inventaires : leurs caractéristiques nourrissent aussi les jets observés.</summary>
    void ObserveOwnedRolls()
    {
        if (_data == null || _fm == null) return;
        foreach (var s in _tracker.AllSnapshots())
            foreach (var (uid, it) in s.Bank.Concat(s.Inventory))
                if (it.Effects.Length > 0 && _data.Items.TryGetValue(it.TemplateId, out var item) && item.MaxStats.Count > 0)
                    _rolls.Observe(item, uid, ItemStats.FromParsed(_fm.ParseStats(it.Effects)));
    }

    void InitBrisage(GameData data)
    {
        // Un bouton compact par caractéristique (icône de sa rune + abréviation) ; ses seuils n'apparaissent qu'une fois cochée
        foreach (var stat in ItemStats.All.Where(s => s.Main))
        {
            var shortName = StatShort.GetValueOrDefault(stat.Characteristic, stat.Name);
            var rune = RuneBreaking.SmallestRuneId(data, stat.Characteristic);
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            if (rune is { } r && ItemIcons.Get(r) is { } icon)
                content.Children.Add(new Image { Source = icon, Width = 18, Height = 18, Margin = new Thickness(0, 0, 4, 0) });
            content.Children.Add(new TextBlock { Text = shortName, VerticalAlignment = VerticalAlignment.Center });
            var box = new ToggleButton
            {
                Content = content, Style = (Style)FindResource("PillToggle"), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"{stat.Name} : cliquez, puis indiquez le minimum (et le maximum si besoin).",
            };
            var min = new TextBox { Width = 46, Padding = new Thickness(2, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Center, ToolTip = $"{stat.Name} minimum", Margin = new Thickness(4, 0, 0, 0) };
            var max = new TextBox { Width = 46, Padding = new Thickness(2, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Center, ToolTip = $"{stat.Name} maximum (vide = pas de limite)" };
            var to = new TextBlock { Text = "à", Margin = new Thickness(4, 0, 4, 0) };
            var filter = new StatFilter(stat, shortName, rune, box, min, max);
            void ShowBounds()
            {
                var v = box.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                min.Visibility = to.Visibility = max.Visibility = v;
            }
            box.Checked += (s, e) => { ShowBounds(); if (box.IsKeyboardFocusWithin) min.Focus(); BrisageFilters_Changed(s, e); };
            box.Unchecked += (s, e) => { ShowBounds(); BrisageFilters_Changed(s, e); };
            // Saisir un seuil coche la caractéristique
            foreach (var t in new[] { min, max })
                t.TextChanged += (_, _) =>
                {
                    if (t.Text.Trim().Length > 0 && box.IsChecked != true) box.IsChecked = true;
                    else ApplyBrisageFilters();
                };
            _statFilters.Add(filter);
            ShowBounds();

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 6) };
            panel.Children.Add(box);
            panel.Children.Add(min);
            panel.Children.Add(to);
            panel.Children.Add(max);
            BrisageStats.Children.Add(panel);
        }

        _tracker.HdvOffers += body => Safe(() => ObserveHdvRolls(body));
        _tracker.BankChanged += () => Ui(ObserveOwnedRolls);
        Closed += (_, _) => _rolls.Save();
        ObserveOwnedRolls();

        BrisageTypeFilter.Items.Add(AllTypes);
        foreach (var t in data.Items.Values.Where(i => i.MaxStats.Count > 0).Select(i => i.TypeName).Distinct().Order())
            BrisageTypeFilter.Items.Add(t);
        BrisageTypeFilter.SelectedIndex = 0;
    }

    void BrisageFilters_Changed(object sender, RoutedEventArgs e) => ApplyBrisageFilters();

    /// <summary>« 2 », « 0,4 » : quantité estimée de runes, entière pour les grosses, décimale pour les simples.</summary>
    static string RuneQuantity(double q) => q == Math.Floor(q) ? $"{q:0}" : $"≈{q:0.#}";

    void BrisageGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nouvel objet : on propose de briser tous les exemplaires possédés (au moins 1)
        if (BrisageGrid.SelectedItem is BrisageRow row && row.ItemId != _brisageDetailItem)
        {
            _brisageDetailItem = row.ItemId;
            BrisageBreakCount.Text = Math.Max(1, row.Owned).ToString(); // déclenche ShowBrisageDetail
            BrisageBreakMax.IsEnabled = row.Owned > 0;
        }
        ShowBrisageDetail();
    }

    int? _brisageDetailItem;

    void BrisageBreakCount_Changed(object sender, TextChangedEventArgs e) => ShowBrisageDetail();

    void BrisageBreakMax_Click(object sender, RoutedEventArgs e)
    {
        if (BrisageGrid.SelectedItem is BrisageRow { Owned: > 0 } row) BrisageBreakCount.Text = row.Owned.ToString();
    }

    /// <summary>Runes de N exemplaires de l'objet sélectionné : quantité, prix unitaire HDV, total, puis comparaison.</summary>
    void ShowBrisageDetail()
    {
        // Pendant le chargement de la fenêtre (TextChanged du champ « Quantité à briser »), le panneau n'est pas encore créé
        if (!_ready) return;
        if (_data == null || BrisageGrid.SelectedItem is not BrisageRow row)
        {
            BrisageDetailTitle.Text = "Sélectionnez un objet";
            BrisageGoRecipe.Visibility = Visibility.Collapsed;
            BrisageRunes.ItemsSource = null;
            BrisageDetailTotal.Text = "";
            return;
        }
        int n = int.TryParse(BrisageBreakCount.Text, out var c) && c > 0 ? Math.Min(c, 100_000) : 1;
        BrisageDetailTitle.Text = row.Name;
        BrisageGoRecipe.Visibility = _rows.Any(r => r.Recipe.ResultId == row.ItemId) ? Visibility.Visible : Visibility.Collapsed;
        BrisageDetailSub.Text = $"Niveau {row.Level} · {row.Type}" + (row.Owned > 0 ? $" · {row.Owned} possédé(s)" : "");
        BrisageRunes.ItemsSource = row.Runes.Select(r =>
        {
            var unit = UnitPrice(r.ItemId);
            return new RuneRow(r.ItemId, r.Name, RuneQuantity(Math.Round(r.Quantity * n, 2)), unit, unit * r.Quantity * n);
        }).OrderByDescending(r => r.Total ?? -1).ToList(); // la plus rentable en haut, sans prix en bas

        int unpriced = row.Runes.Count(r => UnitPrice(r.ItemId) == null);
        double? gain = row.Gain * n;
        var lines = new List<string>();
        if (row.Runes.Count == 0) lines.Add("Aucune caractéristique ne donne de rune.");
        else
        {
            string each = n > 1 ? $" pour {n} objets" : "";
            lines.Add($"Valeur des runes{each} : {Kamas(row.RunesValue * n)}"
                + (unpriced > 0 ? $" ({unpriced} rune(s) sans prix HDV, comptée(s) 0)" : ""));
            var label = BrisageSource.SelectedIndex == 0 ? "Prix HDV de l'objet" : row.CraftCost != null ? "Coût de fabrication" : "Prix HDV de l'objet";
            lines.Add(row.Reference is { } reference ? $"{label}{each} : {Kamas(reference * n)}" : $"{label} : inconnu");
            if (gain is { } g)
                lines.Add($"Gain du brisage{each} : {(g >= 0 ? "+" : "")}{Kamas(g)}");
        }
        BrisageDetailTotal.Text = string.Join("\n", lines);
        BrisageDetailTotal.SetResourceReference(TextBlock.ForegroundProperty, gain is < 0 ? "Missing" : gain is > 0 ? "Ok" : "Text");
    }

    void BrisageReset_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _ready = false; // une seule mise à jour à la fin
        foreach (var f in _statFilters) { f.Min.Text = ""; f.Max.Text = ""; f.Box.IsChecked = false; }
        _ready = true;
        ApplyBrisageFilters();
    }

    void ApplyBrisageFilters()
    {
        if (!_ready || _data == null || BrisageTypeFilter.SelectedItem == null) return;
        var search = BrisageSearch.Text.Trim();
        var type = BrisageTypeFilter.SelectedItem as string;
        int levelMin = int.TryParse(BrisageLevelMin.Text, out var a) ? a : 0;
        int levelMax = int.TryParse(BrisageLevelMax.Text, out var b) ? b : int.MaxValue;
        var active = _statFilters.Where(f => f.Box.IsChecked == true)
            .Select(f => (f.Stat.Characteristic,
                Min: int.TryParse(f.Min.Text, out var min) ? min : 1,
                Max: int.TryParse(f.Max.Text, out var max) ? max : int.MaxValue))
            .ToList();

        var owned = OwnedQuantities();
        var recipeRows = _rows.ToLookup(r => r.Recipe.ResultId);
        bool mine = BrisageSource.SelectedIndex == 0;

        // Source : objets possédés (stats réelles), ou modèles d'objets (stats maximales)
        IEnumerable<(ItemInfo Item, IReadOnlyDictionary<int, int> Stats, string Where, int Count)> candidates;
        if (BrisageSource.SelectedIndex == 0)
        {
            var s = _tracker.State;
            candidates = s.Bank.Values.Select(i => (Owned: i, Where: "Banque"))
                .Concat(s.Inventory.Values.Select(i => (Owned: i, Where: "Inventaire")))
                .Where(x => x.Owned.Effects.Length > 0 && _fm != null)
                .Select(x => (_data.GetItem(x.Owned.TemplateId),
                    (IReadOnlyDictionary<int, int>)ItemStats.FromParsed(_fm!.ParseStats(x.Owned.Effects)),
                    x.Where, x.Owned.Quantity));
        }
        else
        {
            IEnumerable<ItemInfo> items = BrisageSource.SelectedIndex switch
            {
                1 => _rows.Where(r => r.Craftable > 0).Select(r => _data.GetItem(r.Recipe.ResultId)),
                2 => _rows.Select(r => _data.GetItem(r.Recipe.ResultId)),
                _ => _data.Items.Values,
            };
            candidates = items.DistinctBy(i => i.Id)
                .Select(it => (it, it.MaxStats, "", owned.GetValueOrDefault(it.Id)));
        }

        var list = candidates
            .Where(x => x.Stats.Count > 0)
            .Where(x => type == AllTypes || x.Item.TypeName == type)
            .Where(x => x.Item.Level >= levelMin && x.Item.Level <= levelMax)
            .Where(x => search.Length == 0 || Contains(x.Item.Name, search))
            .Where(x => active.All(f => x.Stats.GetValueOrDefault(f.Characteristic) is var v && v >= f.Min && v <= f.Max))
            .Select(x =>
            {
                // Caractéristiques filtrées en premier, puis les autres
                var ordered = x.Stats
                    .OrderBy(kv => active.Any(f => f.Characteristic == kv.Key) ? 0 : 1)
                    .ThenBy(kv => ItemStats.Order(kv.Key));
                var recipe = recipeRows[x.Item.Id].FirstOrDefault();
                double? craftCost = CraftCost(x.Item.Id), hdvPrice = UnitPrice(x.Item.Id);

                // Runes estimées et leur valeur à l'HDV (runes sans prix comptées 0)
                // Modèle d'objet : runes sur le jet choisi (minimum, moyen ou maximum de la fourchette officielle)
                var runes = RuneBreaking.Estimate(_data, x.Item.Level, mine ? x.Stats : RollStats(x.Item));
                var priced = runes.Where(r => UnitPrice(r.ItemId) != null).ToList();
                double? value = priced.Count > 0 ? priced.Sum(r => r.Quantity * UnitPrice(r.ItemId)!.Value) : null;
                // À comparer : son prix de vente pour un objet possédé, son coût de fabrication sinon
                double? reference = mine ? hdvPrice : craftCost ?? hdvPrice;

                var chips = StatChips(x.Item, ordered, mine, active.Select(f => f.Characteristic).ToList());
                return new BrisageRow(x.Item.Id, x.Item.Name, x.Item.Level, x.Item.TypeName,
                    string.Join(" · ", ordered.Select(kv => ItemStats.Format(kv.Key, kv.Value))),
                    x.Where, x.Count, recipe?.Craftable, recipe?.MissingText ?? "",
                    craftCost, hdvPrice,
                    active.Sum(f => x.Stats.GetValueOrDefault(f.Characteristic)),
                    runes, string.Join(", ", runes.Select(r => $"{RuneQuantity(r.Quantity)} {r.Name[5..]}")),
                    value, reference, value - reference) { Chips = chips };
            })
            .OrderByDescending(r => r.Score).ThenByDescending(r => r.Level).ThenBy(r => r.Name)
            .Take(2000) // « Tous les objets du jeu » sans filtre : la liste reste fluide
            .Select((r, i) => r with { Index = i + 1 })
            .ToList();

        var selected = (BrisageGrid.SelectedItem as BrisageRow)?.ItemId;
        SetRows(BrisageGrid, list);
        BrisageGrid.SelectedItem = list.FirstOrDefault(r => r.ItemId == selected);
        BrisageGainColumn.Header = mine ? "Gain vs vente" : "Gain vs craft";
        BrisageCount.Text = list.Count == 2000 ? "2000 objets affichés (affinez les filtres)" : $"{list.Count} objet(s)";
        BrisageEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var bank = _tracker.State.Bank.Values;
        BrisageEmpty.Text = BrisageSource.SelectedIndex != 0 ? "Aucun objet ne correspond à ces critères."
            : bank.Count == 0 ? "Aucune banque reçue : ouvrez votre banque en jeu, ou choisissez une autre source."
            // Banque sauvegardée par une ancienne version, sans les caractéristiques des objets
            : bank.All(i => i.Effects.Length == 0)
                ? "Les caractéristiques de vos objets ne sont pas connues : rouvrez votre banque en jeu (capture active) pour les relever."
            : "Aucun objet ne correspond à ces critères.";
    }
}
