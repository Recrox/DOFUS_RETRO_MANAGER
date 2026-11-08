using DofusRetroManager.Data;

namespace DofusRetroManager.Fm;

/// <summary>Chances d'une rune sur une ligne : succès critique, neutre, échec (en %), difficulté D et dépassement du max.</summary>
public sealed record FmRuneChance(int RuneId, string Rune, int Value, double Sc, double Sn, double Ec, double D, bool Over);

/// <summary>
/// Ligne de l'objet pour le calcul des chances : caractéristique, valeur actuelle, jet du modèle (minimum observé s'il
/// est connu, maximum du jeu ; 0 : ligne exotique), poids de la ligne et chances de chaque rune.
/// </summary>
public sealed record FmChanceLine(int Characteristic, string Name, int Value, int? Min, int Max, double Weight, List<FmRuneChance> Runes)
{
    public bool Exo => Max <= 0;
}

/// <summary>
/// Chances de réussite d'une rune, d'après la formule communautaire publiée pour Dofus (DoFocus), vérifiée sur ses
/// exemples ; pour Retro, c'est une estimation (poids des stats de l'onglet FM, runes de Retro) :
///   D = 5 + 45·[jet ≥ min + 80 % (max − min), sans dépasser le max] + (65 + 4W)·[dépassement du max]
///       + 12·max(0, ligne / rune − 14) − 20·max(0, 1,1 − q)·[niveau &lt; 170] + 60·q¹²
///   jet : valeur de la ligne après la rune ; W : poids qui dépasse le max ; ligne / rune : valeur de la ligne ÷ valeur de
///   la rune ; q : poids actuel de l'objet ÷ poids de ses jets maximum.
///   SC = max(2, (100 − D) / 2) ; SN = min(50, 100 − SC), ou si D &gt; 100 : max(15, 50 − 0,375 (D − 100)),
///   en dépassement : max(5, 40 − 0,375 (D − 100)) ; EC = le reste.
///   Ligne en over (ou exotique) qui pèserait plus de 101 en tout (jet × poids par point) : 0 %, la rune échoue toujours.
/// </summary>
public static class FmChances
{
    /// <summary>Poids maximum d'une ligne au-delà de son max (over) ou exotique : au-dessus, la rune ne passe jamais.</summary>
    public const double MaxOverWeight = 101;

    public static List<FmChanceLine> Compute(GameData data, FmTracker fm, ItemInfo item, IReadOnlyDictionary<int, int> current,
        Func<int, int?> minOf)
    {
        double PointWeight(int c) => ItemStats.All.FirstOrDefault(s => s.Characteristic == c) is { } s ? fm.Weight(s.Effects[0]) : 0;

        // Lignes : celles du modèle (positives), puis les exotiques de l'objet, dans l'ordre des caractéristiques
        var chars = item.MaxStats.Where(kv => kv.Value > 0).Select(kv => kv.Key)
            .Concat(current.Where(kv => kv.Value > 0).Select(kv => kv.Key))
            .Distinct().OrderBy(ItemStats.Order).ToList();

        double maxWeight = item.MaxStats.Where(kv => kv.Value > 0).Sum(kv => kv.Value * PointWeight(kv.Key));
        double nowWeight = current.Where(kv => kv.Value > 0).Sum(kv => kv.Value * PointWeight(kv.Key));
        double q = maxWeight > 0 ? nowWeight / maxWeight : 0;

        var lines = new List<FmChanceLine>();
        foreach (var c in chars)
        {
            int value = Math.Max(0, current.GetValueOrDefault(c)), max = Math.Max(0, item.MaxStats.GetValueOrDefault(c));
            int? min = max > 0 ? minOf(c) : null;
            double w = PointWeight(c);
            var runes = RuneBreaking.RunesOf(c).Select(r =>
            {
                var (sc, sn, ec, d, over) = Chance(value, r.Value, min, max, w, q, item.Level);
                return new FmRuneChance(RuneBreaking.RuneId(data, r.Rune) ?? 0, r.Rune, r.Value, sc, sn, ec, d, over);
            }).ToList();
            lines.Add(new FmChanceLine(c, ItemStats.Name(c), value, min, max, value * w, runes));
        }
        return lines;
    }

    static (double Sc, double Sn, double Ec, double D, bool Over) Chance(int value, int rune, int? min, int max, double w, double q, int level)
    {
        int after = value + rune;
        bool over = after > max;
        // Retro : une ligne au-delà de son max (ou exotique) ne peut pas peser plus de 101 en tout (ex. 4 CC = 120) : rune impossible
        if (over && after * w > MaxOverWeight) return (0, 0, 100, double.PositiveInfinity, true);
        // Minimum du jet inconnu (jamais vu à l'HDV ni dans une banque) : seuil à 80 % du maximum
        double high = min is { } lo && lo < max ? lo + 0.8 * (max - lo) : 0.8 * max;
        double d = 5;
        if (!over && after >= high) d += 45;
        if (over) d += 65 + 4 * (after - max) * w;
        d += 12 * Math.Max(0, (double)value / rune - 14);
        if (level < 170) d -= 20 * Math.Max(0, 1.1 - q);
        d += 60 * Math.Pow(q, 12);

        double sc = Math.Clamp((100 - d) / 2, 2, 100);
        double sn = over ? Math.Clamp(40 - 0.375 * (d - 100), 5, 50)
            : d > 100 ? Math.Max(15, 50 - 0.375 * (d - 100))
            : 50;
        sn = Math.Min(sn, 100 - sc);
        return (sc, sn, 100 - sc - sn, d, over);
    }
}
