using System.Text.Json;
using DofusRetroManager.Data;

namespace DofusRetroManager;

/// <summary>
/// Onglet Métier : monter un métier d'un niveau à un autre au moindre coût. Règles des serveurs 1.29 (émulateurs
/// StarLoco et Ancestra, recoupés avec les guides Retro) :
///   cases disponibles : 2 avant le niveau 10, puis niveau / 20 + 3 (3 au 10, 4 au 20, 5 au 40, 6 au 60, 7 au 80) ;
///   XP d'une fabrication selon le nombre de cases de la recette : 1, 10, 25, 50, 100, 250, 500, 1000 ;
///   une recette ne donne plus d'XP quand elle a moins de (cases disponibles − 3) cases ;
///   XP cumulée par niveau : table des métiers (JobXp).
/// Pour chaque palier de cases, la recette au plus petit coût par XP (ingrédients au prix HDV, sinon fabriqués ;
/// revente du résultat au prix HDV déduite si demandé). Simple affichage : on fabrique toujours à la main (règle 3).
/// </summary>
public partial class MainWindow
{
    static readonly int[] CraftXp = [0, 1, 10, 25, 50, 100, 250, 500, 1000];

    /// <summary>XP cumulée pour atteindre chaque niveau de métier (index 0 = niveau 1).</summary>
    static readonly int[] JobXp = [0, 50, 140, 271, 441, 653, 905, 1199, 1543, 1911, 2330, 2792, 3297, 3840, 4439, 5078, 5762, 6493, 7280, 8097,
        8980, 9898, 10875, 11903, 12985, 14122, 15315, 16564, 17873, 19242, 20672, 22166, 23726, 25353, 27048, 28815, 30656, 32572, 34566, 36641,
        38800, 41044, 43378, 45804, 48325, 50946, 53669, 56498, 59437, 62491, 65664, 68960, 72385, 75943, 79640, 83482, 87475, 91624, 95937, 100421,
        105082, 109930, 114971, 120215, 125671, 131348, 137256, 143407, 149811, 156481, 163429, 170669, 178214, 186080, 194283, 202839, 211765,
        221082, 230808, 240964, 251574, 262660, 274248, 286364, 299037, 312297, 326175, 340705, 355924, 371870, 388582, 406106, 424486, 443772,
        464016, 485274, 507604, 531071, 555541, 581687];

    static int JobSlots(int level) => level < 10 ? 2 : level >= 100 ? 9 : level / 20 + 3;

    /// <summary>XP d'une fabrication à ce niveau (0 : la recette ne rapporte plus, ou demande trop de cases).</summary>
    static int CraftXpAt(int cases, int level) =>
        level >= 100 || cases > JobSlots(level) || cases < JobSlots(level) - 3 ? 0 : CraftXp[Math.Clamp(cases, 0, 8)];

    void InitMetier(GameData data)
    {
        // Métiers ayant des recettes, sauf la forgemagie (son XP vient des runes, pas des cases)
        var jobs = data.Recipes.SelectMany(r => r.Jobs).Distinct().Where(j => j.Length > 1 && j != "Sans métier" && !j.Contains("Forgemage", StringComparison.OrdinalIgnoreCase)).Order().ToList();
        foreach (var j in jobs) MetierJob.Items.Add(j);
        MetierJob.SelectedIndex = jobs.Count > 0 ? 0 : -1;
        foreach (var v in new[] { "Coût brut", "Revente HDV", "Brisage", "Revente ou brisage" }) MetierValue.Items.Add(v);
        MetierValue.SelectedIndex = 3;
    }

    string MetierJson()
    {
        if (_data == null || MetierJob.SelectedItem is not string job) return "null";
        int from = Math.Clamp(int.TryParse(MetierFrom.Text, out var f) ? f : 1, 1, 99);
        int to = Math.Clamp(int.TryParse(MetierTo.Text, out var t) ? t : 100, from + 1, 100);
        int mode = MetierValue.SelectedIndex; // 0 coût brut, 1 revente HDV, 2 brisage, 3 le meilleur des deux
        var craftable = _rows.ToLookup(r => r.Recipe.ResultId);

        // Recettes du métier et leur coût par fabrication (null : un ingrédient sans prix ni recette), moins ce que
        // rapporte l'objet fabriqué : revendu à l'HDV (prix ×1), ou brisé (runes au jet moyen)
        var recipes = _data.Recipes.Where(r => r.Jobs.Contains(job)).Select(r =>
        {
            var cost = RecipeCost(r);
            var item = _data.GetItem(r.ResultId);
            var sell = UnitPrice(r.ResultId);
            var runes = item.MaxStats.Count > 0 ? BreakValue(item) : null;
            double back = mode switch
            {
                1 => sell ?? 0,
                2 => runes ?? 0,
                3 => Math.Max(sell ?? 0, runes ?? 0),
                _ => 0,
            };
            return (Recipe: r, Cases: r.Ingredients.Count, Cost: cost, Net: cost is { } c ? c - back : (double?)null,
                Sell: sell, Runes: runes, Craftable: craftable[r.ResultId].FirstOrDefault()?.Craftable ?? 0);
        }).ToList();

        // Paliers : le nombre de cases change aux niveaux 10, 20, 40, 60, 80
        var bounds = new[] { 1, 10, 20, 40, 60, 80, 100 }.Where(b => b > from && b < to).Prepend(from).Append(to).ToList();
        var steps = new List<object>();
        double totalCost = 0;
        long totalCrafts = 0;
        bool incomplete = false;
        for (int i = 0; i + 1 < bounds.Count; i++)
        {
            int a = bounds[i], b = bounds[i + 1];
            long xp = JobXp[b - 1] - JobXp[a - 1];
            var options = recipes.Where(r => CraftXpAt(r.Cases, a) > 0)
                .Select(r => new
                {
                    r.Recipe.ResultId, r.Cases, Xp = CraftXpAt(r.Cases, a), r.Cost, r.Net, r.Sell, r.Runes, r.Craftable,
                    PerXp = r.Net / CraftXpAt(r.Cases, a),
                    Crafts = (long)Math.Ceiling(xp / (double)CraftXpAt(r.Cases, a)),
                })
                .OrderBy(o => o.PerXp == null).ThenBy(o => o.PerXp).ThenByDescending(o => o.Xp).ToList();
            var best = options.FirstOrDefault(o => o.PerXp != null);
            if (best != null) { totalCost += best.Crafts * best.Net!.Value; totalCrafts += best.Crafts; } else incomplete = true;
            steps.Add(new
            {
                a, b, slots = JobSlots(a), xp,
                opts = options.Take(6).Select(o => new
                {
                    id = o.ResultId, n = _data.GetItem(o.ResultId).Name, c = o.Cases, x = o.Xp, cost = o.Cost, net = o.Net, sell = o.Sell, runes = o.Runes,
                    per = o.PerXp, k = o.Crafts, total = o.Net * o.Crafts, own = o.Craftable,
                }),
                unpriced = options.Count(o => o.PerXp == null),
            });
        }
        return JsonSerializer.Serialize(new { job, from, to, xp = JobXp[to - 1] - JobXp[from - 1], totalCost, totalCrafts, incomplete, mode, steps });
    }
}
