namespace DofusRetroManager.Data;

/// <summary>Rune obtenue au brisage : objet (pour son prix HDV), nom et quantité estimée.</summary>
public sealed record RuneYield(int ItemId, string Name, double Quantity);

/// <summary>
/// Estimation des runes obtenues en brisant un objet. Aucune formule officielle : on applique celle du projet Cyberia
/// (github.com/Lounek09/Cyberia, RuneCalculator ; d'après le sujet JOL « Les taux de brisage »), pour chaque
/// caractéristique positive :
///   part extractible = min(2/3, 1,5 × niveau² / poids^1,25 + (jet − 1) / poids × max(0, 2/3 − …))
///   points extraits = jet × part × multiplicateur (aléatoire, 90 % à 110 %)
/// puis répartis en lots Ra (4 simples + 2 Pa + 1 Ra), Pa (2 simples + 1 Pa), et runes simples.
/// Points apportés par chaque rune : ceux du fichier « itemstats » du jeu (Rune Vi 3, Pa Vi 10, Ra Vi 30…).
/// </summary>
public static class RuneBreaking
{
    sealed record Line(double PointWeight, (string Rune, int Value)[] Runes);

    /// <summary>Caractéristique (voir ItemStats) -> poids par point et runes, de la plus petite à la plus grosse.</summary>
    static readonly Dictionary<int, Line> Table = new()
    {
        [11] = new(0.25, [("Rune Vi", 3), ("Rune Pa Vi", 10), ("Rune Ra Vi", 30)]),
        [12] = new(3, [("Rune Sa", 1), ("Rune Pa Sa", 3), ("Rune Ra Sa", 10)]),
        [10] = new(1, [("Rune Fo", 1), ("Rune Pa Fo", 3), ("Rune Ra Fo", 10)]),
        [15] = new(1, [("Rune Ine", 1), ("Rune Pa Ine", 3), ("Rune Ra Ine", 10)]),
        [13] = new(1, [("Rune Cha", 1), ("Rune Pa Cha", 3), ("Rune Ra Cha", 10)]),
        [14] = new(1, [("Rune Age", 1), ("Rune Pa Age", 3), ("Rune Ra Age", 10)]),
        [1] = new(100, [("Rune Ga Pa", 1)]),
        [23] = new(90, [("Rune Ga Pme", 1)]),
        [19] = new(51, [("Rune Po", 1)]),
        [26] = new(30, [("Rune Invo", 1)]),
        [18] = new(30, [("Rune Cri", 1)]),
        [17] = new(2, [("Rune Do Per", 1), ("Rune Pa Do Per", 3), ("Rune Ra Do Per", 10)]),
        [80] = new(20, [("Rune Do", 1)]),
        [49] = new(20, [("Rune So", 1)]),
        [44] = new(0.1, [("Rune Ini", 10), ("Rune Pa Ini", 30), ("Rune Ra Ini", 100)]),
        [48] = new(3, [("Rune Prospe", 1), ("Rune Pa Prospe", 3)]),
        [70] = new(15, [("Rune Pi", 1), ("Rune Pa Pi", 3)]),
        [69] = new(2, [("Rune Pi Per", 1), ("Rune Pa Pi Per", 3), ("Rune Ra Pi Per", 10)]),
        [50] = new(30, [("Rune Do Ren", 1)]),
        [33] = new(4, [("Rune Ré Per Terre", 1)]),
        [34] = new(4, [("Rune Ré Per Feu", 1)]),
        [35] = new(4, [("Rune Ré Per Eau", 1)]),
        [36] = new(4, [("Rune Ré Per Air", 1)]),
        [37] = new(4, [("Rune Ré Per Neutre", 1)]),
        [83] = new(5, [("Rune Ré Terre", 1)]),
        [84] = new(5, [("Rune Ré Feu", 1)]),
        [85] = new(5, [("Rune Ré Eau", 1)]),
        [86] = new(5, [("Rune Ré Air", 1)]),
        [87] = new(5, [("Rune Ré Neutre", 1)]),
        [96] = new(0.25, [("Rune Pod", 10), ("Rune Pa Pod", 30), ("Rune Ra Pod", 100)]),
    };

    /// <summary>Runes de forgemagie (type 78) par nom, construit une fois par chargement des données.</summary>
    static (GameData Data, Dictionary<string, int> Ids)? _runeIds;

    static Dictionary<string, int> RuneIds(GameData data)
    {
        if (_runeIds is not { } cache || cache.Data != data)
            _runeIds = cache = (data, data.Items.Values.Where(i => i.TypeId == 78)
                .GroupBy(i => i.Name).ToDictionary(g => g.Key, g => g.First().Id));
        return cache.Ids;
    }

    /// <summary>Runes de forgemagie d'une caractéristique et leur valeur, de la plus petite à la plus grosse (vide si aucune).</summary>
    public static IReadOnlyList<(string Rune, int Value)> RunesOf(int characteristic) =>
        Table.TryGetValue(characteristic, out var line) ? line.Runes : [];

    /// <summary>Caractéristique et points d'une rune d'après son nom (ex. Rune Pa Fo : force, 3), null si inconnue.</summary>
    public static (int Characteristic, int Value)? RuneOf(string name) =>
        Table.SelectMany(kv => kv.Value.Runes.Where(r => r.Rune == name).Select(r => ((int, int)?)(kv.Key, r.Value))).FirstOrDefault();

    /// <summary>Poids d'une rune (points apportés × poids par point, ex. Rune Ra Fo : 10), null si inconnue.</summary>
    public static double? RuneWeight(string name) =>
        Table.Values.SelectMany(l => l.Runes.Select(r => (r.Rune, W: r.Value * l.PointWeight))).FirstOrDefault(r => r.Rune == name) is { Rune: not null } x ? x.W : null;

    /// <summary>Identifiant d'une rune de forgemagie d'après son nom (ex. « Rune Vi »), null si absente des données.</summary>
    public static int? RuneId(GameData data, string name) => RuneIds(data).TryGetValue(name, out var id) ? id : null;

    /// <summary>Plus petite rune d'une caractéristique (ex. Rune Vi pour la vitalité), pour son icône ; null si aucune.</summary>
    public static int? SmallestRuneId(GameData data, int characteristic) =>
        Table.TryGetValue(characteristic, out var line) ? RuneId(data, line.Runes[0].Rune) : null;

    /// <summary>Part extractible d'une caractéristique, en % (au plus 2/3) : formule de Cyberia.</summary>
    static double ExtractablePercent(double pointWeight, int level, int value)
    {
        const double max = 200.0 / 3;
        double c = 1.5 * level * (double)level / Math.Pow(pointWeight, 1.25);
        return Math.Min(max, c + (value - 1) / pointWeight * Math.Max(0, max - c));
    }

    /// <summary>
    /// Runes d'une caractéristique pour un multiplicateur donné : (simples, Pa, Ra). Chaque Ra vient avec 4 simples et
    /// 2 Pa (seuil 4 × simple + 2 × Pa + Ra points), chaque Pa avec 2 simples (seuil 2 × simple + Pa) ; le reste donne
    /// des runes simples, la partie décimale étant la chance d'en avoir une de plus.
    /// </summary>
    static (double Ba, double Pa, double Ra) Bundle(Line line, int level, int value, double multiplier)
    {
        var r = line.Runes;
        double amount = value * ExtractablePercent(line.PointWeight, level, value) * multiplier / 100;
        double ba = 0, pa = 0, ra = 0;
        if (r.Length >= 3)
        {
            int t = 4 * r[0].Value + 2 * r[1].Value + r[2].Value;
            if (amount > t) { int n = (int)Math.Floor(amount / t); ba += 4 * n; pa += 2 * n; ra += n; amount -= t * n; }
        }
        if (r.Length >= 2)
        {
            int t = 2 * r[0].Value + r[1].Value;
            if (amount > t) { int n = (int)Math.Floor(amount / t); ba += 2 * n; pa += n; amount -= t * n; }
        }
        return (ba + amount / r[0].Value, pa, ra);
    }

    /// <summary>
    /// Runes estimées pour un objet de ce niveau et ces caractéristiques (les malus ne donnent rien) : moyenne sur le
    /// multiplicateur aléatoire du jeu (90 % à 110 %), d'où des quantités décimales (ex. 0,4 Ra = 40 % de chances).
    /// </summary>
    public static List<RuneYield> Estimate(GameData data, int level, IReadOnlyDictionary<int, int> stats)
    {
        const int steps = 20;
        var result = new List<RuneYield>();
        foreach (var (characteristic, value) in stats.OrderBy(kv => ItemStats.Order(kv.Key)))
        {
            if (value <= 0 || !Table.TryGetValue(characteristic, out var line)) continue;
            double ba = 0, pa = 0, ra = 0;
            for (int i = 0; i < steps; i++)
            {
                var b = Bundle(line, level, value, 0.9 + 0.2 * (i + 0.5) / steps);
                ba += b.Ba / steps; pa += b.Pa / steps; ra += b.Ra / steps;
            }
            // De la plus grosse rune à la plus petite
            foreach (var (qty, i) in new[] { (ra, 2), (pa, 1), (ba, 0) })
                if (i < line.Runes.Length && qty >= 0.005)
                    result.Add(new RuneYield(RuneIds(data).GetValueOrDefault(line.Runes[i].Rune), line.Runes[i].Rune, Math.Round(qty, 2)));
        }
        return result;
    }
}
