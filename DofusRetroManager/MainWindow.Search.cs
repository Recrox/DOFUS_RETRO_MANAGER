using System.Text.Json;

namespace DofusRetroManager;

/// <summary>
/// Recherche globale (Ctrl+K dans la page) : pour les objets dont le nom correspond, tout ce que l'application sait
/// d'un coup : prix HDV, quantité possédée (et chez qui), recette (coût, faisable), valeur des runes au jet moyen,
/// monstres qui le font tomber. Résultat envoyé à la page sous « _search ».
/// </summary>
public partial class MainWindow
{
    string _searchJson = "null";
    Dictionary<int, List<int>>? _droppedBy;
    HashSet<int>? _ttgItems;

    void Search(string query)
    {
        query = query.Trim();
        if (_data == null || query.Length < 2) { _searchJson = "null"; _webForce = true; return; }
        // Monstres qui font tomber chaque objet (index construit une fois)
        _ttgItems ??= _data.TtgCards.Values.Select(c => c.ItemId).Concat(_data.TtgFamilies.Values.Select(f => f.ItemId)).Where(id => id > 0).ToHashSet();
        _droppedBy ??=_data.MonsterDrops.SelectMany(kv => kv.Value.Select(d => (d.ItemId, Monster: kv.Key)))
            .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.Select(x => x.Monster).Distinct().ToList());
        var snapshots = _tracker.AllSnapshots().Where(s => s.Character.Length > 0).ToList();
        var craftable = _rows.ToLookup(r => r.Recipe.ResultId);
        var items = _data.Items.Values.Where(i => Contains(i.Name, query))
            .OrderBy(i => i.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(i => _hdv?.Prices.ContainsKey(i.Id) == true ? 0 : 1).ThenBy(i => i.Name.Length).ThenBy(i => i.Name)
            .Take(8).ToList();
        _searchJson = JsonSerializer.Serialize(new
        {
            q = query,
            items = items.Select(i =>
            {
                var price = _hdv?.Prices.GetValueOrDefault(i.Id);
                // Possédé : chaque exemplaire une fois (banque du compte vue par plusieurs personnages)
                var seen = new HashSet<long>();
                var owners = snapshots.Select(s => (s.Character, Qty: s.Bank.Concat(s.Inventory)
                        .Where(kv => kv.Value.TemplateId == i.Id && seen.Add(kv.Key)).Sum(kv => kv.Value.Quantity)))
                    .Where(x => x.Qty > 0).OrderByDescending(x => x.Qty).ToList();
                bool hasRecipe = _recipeByResult.ContainsKey(i.Id);
                // Carte TTG ou paquet : ne se vend pas à l'HDV, prix des marchands (onglet TTG)
                bool card = _ttgItems.Contains(i.Id);
                return new
                {
                    id = i.Id, n = i.Name, l = i.Level, t = i.TypeName, card,
                    shop = card ? BookCardPrice(i.Id) : null,
                    unit = price?.UnitPrice, avg = price?.Average, seen = price != null ? Ago(price.SeenAt) : null,
                    own = owners.Sum(x => x.Qty), who = string.Join(", ", owners.Take(4).Select(x => $"{x.Character} ×{x.Qty}")),
                    recipe = hasRecipe, cost = hasRecipe ? CraftCost(i.Id) : null, craft = craftable[i.Id].FirstOrDefault()?.Craftable ?? 0,
                    runes = i.MaxStats.Count > 0 ? BreakValue(i) : null,
                    drops = (_droppedBy.GetValueOrDefault(i.Id) ?? []).Select(m => _data.GetMonster(m)).OrderBy(m => m.Name).Take(6)
                        .Select(m => new { id = m.Id, n = m.Name }),
                    dropCount = _droppedBy.GetValueOrDefault(i.Id)?.Count ?? 0,
                };
            }),
        });
        _webForce = true;
    }
}
