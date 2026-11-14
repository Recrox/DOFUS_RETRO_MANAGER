using System.IO;
using System.Text.Json;
using DofusRetroManager.Data;
using DofusRetroManager.Hdv;

namespace DofusRetroManager;

/// <summary>
/// Onglet Patrimoine : valeur de tous vos biens connus. Objets des banques et des inventaires de vos personnages
/// (chaque objet compté une fois, par son identifiant : une banque partagée par le compte n'est pas comptée deux fois),
/// kamas en banque (une fois par banque), offres en vente à l'HDV (« Mes échanges »). Objets au prix HDV, sinon au prix
/// marchand (cartes TTG) ; sans prix : comptés 0. Une valeur par jour est gardée dans patrimoine.json (courbe).
/// Rien ne sort de ce PC.
/// </summary>
public partial class MainWindow
{
    sealed class PatrimoineDay
    {
        public string D { get; set; } = "";
        public long T { get; set; }
        public long O { get; set; }
        public long K { get; set; }
        public long V { get; set; }
    }

    static string PatrimoinePath => Path.Combine(GameData.AppDir, "patrimoine.json");
    List<PatrimoineDay>? _patrimoineDays;
    DateTime _patrimoineSaved;

    List<PatrimoineDay> PatrimoineDays()
    {
        if (_patrimoineDays != null) return _patrimoineDays;
        try { _patrimoineDays = File.Exists(PatrimoinePath) ? JsonSerializer.Deserialize<List<PatrimoineDay>>(File.ReadAllText(PatrimoinePath)) : null; }
        catch { }
        return _patrimoineDays ??= new();
    }

    /// <summary>
    /// Même rangement (banque du compte, ou coffre de maison ouvert par deux personnages) : au moins 80 % des sortes
    /// d'objets en commun, des deux côtés. Quantités ignorées : un dépôt ou un retrait entre les deux ouvertures ne doit
    /// pas le faire compter deux fois.
    /// </summary>
    static bool SameBank(Dictionary<int, int> a, Dictionary<int, int> b)
    {
        int common = a.Keys.Count(b.ContainsKey);
        return common >= 0.8 * Math.Max(a.Count, b.Count);
    }

    /// <summary>État de l'onglet (envoyé à la page sous « _patrimoine ») ; la valeur du jour est retenue au passage.</summary>
    string PatrimoineJson()
    {
        if (_data == null) return "null";
        // Objets : chaque identifiant une fois (le premier vu : le personnage en jeu d'abord, puis les fenêtres ouvertes, puis les sauvegardes)
        var seen = new HashSet<long>();
        var perChar = new Dictionary<string, (double Bank, double Inv, long Kamas)>();
        var byItem = new Dictionary<int, (int Qty, double Value, bool Priced)>();
        var banks = new HashSet<string>();
        int unpriced = 0;
        long kamas = 0;
        double objects = 0;
        var snapshots = _tracker.AllSnapshots().Where(s => s.Character.Length > 0).ToList();
        // Banque du compte : le jeu renumérote ses objets à chaque ouverture, donc deux personnages du même compte ont
        // des banques aux identifiants différents mais au même contenu. Banques au contenu presque identique (80 % des
        // objets avec la même quantité) : une seule, la plus récente, est comptée.
        var counted = new List<Dictionary<int, int>>();
        var bankOf = new HashSet<Net.Snapshot>();
        foreach (var s in snapshots.Where(s => s.Bank.Count > 0).OrderByDescending(s => s.BankUpdatedAt))
        {
            var content = s.Bank.Values.GroupBy(i => i.TemplateId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            if (counted.Any(o => SameBank(o, content))) continue;
            counted.Add(content);
            bankOf.Add(s);
        }
        foreach (var s in snapshots)
        {
            var c = perChar.GetValueOrDefault(s.Character);
            bool hasBank = bankOf.Contains(s);
            if (hasBank) { banks.Add(s.Character); c.Kamas += s.BankKamas; kamas += s.BankKamas; }
            foreach (var (uid, it, inBank) in (hasBank ? s.Bank.Select(kv => (kv.Key, kv.Value, true)) : [])
                .Concat(s.Inventory.Select(kv => (kv.Key, kv.Value, false))))
            {
                if (!seen.Add(uid)) continue;
                var price = DropPrice(it.TemplateId);
                double v = (price ?? 0) * it.Quantity;
                if (price == null) unpriced++;
                if (inBank) c.Bank += v; else c.Inv += v;
                objects += v;
                var cur = byItem.GetValueOrDefault(it.TemplateId);
                byItem[it.TemplateId] = (cur.Qty + it.Quantity, cur.Value + v, price != null);
            }
            perChar[s.Character] = c;
        }
        // Offres en vente à l'HDV (elles ne sont plus dans l'inventaire) : à leur prix de vente
        long sales = _trades?.Trades.Where(t => t.Kind == Trade.Listing && t.State == Trade.OnSale).Sum(t => t.Price) ?? 0;
        int salesCount = _trades?.Trades.Count(t => t.Kind == Trade.Listing && t.State == Trade.OnSale) ?? 0;
        long total = (long)objects + kamas + sales;

        // Valeur du jour (courbe) : enregistrée au plus une fois par minute
        var days = PatrimoineDays();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (total > 0)
        {
            var day = days.FirstOrDefault(d => d.D == today);
            if (day == null) days.Add(day = new PatrimoineDay { D = today });
            day.T = total; day.O = (long)objects; day.K = kamas; day.V = sales;
            if (DateTime.Now - _patrimoineSaved > TimeSpan.FromMinutes(1))
            {
                _patrimoineSaved = DateTime.Now;
                try { File.WriteAllText(PatrimoinePath, JsonSerializer.Serialize(days)); } catch { }
            }
        }

        return JsonSerializer.Serialize(new
        {
            total, objects = (long)objects, kamas, sales, salesCount, unpriced, banks = banks.Count,
            chars = perChar.Select(kv => new
            {
                n = kv.Key, g = _hunt?.LookOf(kv.Key)?.Gfx ?? 0, b = (long)kv.Value.Bank, i = (long)kv.Value.Inv, k = kv.Value.Kamas,
            }).OrderByDescending(x => x.b + x.i + x.k),
            top = byItem.Where(kv => kv.Value.Priced).OrderByDescending(kv => kv.Value.Value).Take(40)
                .Select(kv => new { id = kv.Key, n = _data.GetItem(kv.Key).Name, q = kv.Value.Qty, v = (long)kv.Value.Value }),
            hist = days.OrderBy(d => d.D).Select(d => new object[] { d.D, d.T, d.O, d.K, d.V }),
        });
    }
}
