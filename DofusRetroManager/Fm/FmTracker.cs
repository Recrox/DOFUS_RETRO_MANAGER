using System.Globalization;
using System.IO;
using System.Text.Json;
using DofusRetroManager.Data;

namespace DofusRetroManager.Fm;

/// <summary>Variation d'une stat. Stat : identifiant de l'effet « bonus » de la stat (ex. 118 = Force).</summary>
public sealed record FmStatChange(int Stat, int Delta);

public sealed class FmEvent
{
    public DateTime Time { get; set; }
    public int? RuneTemplateId { get; set; }
    public string Outcome { get; set; } = "";
    public List<FmStatChange> Changes { get; set; } = new();
    public double WeightGained { get; set; }
    public double WeightLost { get; set; }
    /// <summary>Poids de la rune passée (0 si inconnue : le poids gagné en tient lieu).</summary>
    public double RuneWeight { get; set; }
    public double ReliquatBefore { get; set; }
    public double ReliquatAfter { get; set; }
}

public sealed class FmItem
{
    public long Uid { get; set; }
    public int TemplateId { get; set; }
    public string Effects { get; set; } = "";
    public double Reliquat { get; set; }
    public DateTime LastUsed { get; set; }
    public List<FmEvent> History { get; set; } = new();
}

/// <summary>
/// Suivi de la forgemagie : à chaque rune passée sur un objet (changement de ses caractéristiques
/// pendant un atelier), calcule le poids gagné/perdu, classe le résultat et estime le reliquat.
///
/// Modèle de reliquat (estimation, corrigeable à la main) :
///   succès critique (gain sans perte)  : reliquat inchangé
///   succès neutre (gain et perte)      : reliquat += poids perdu − poids de la rune (minimum 0)
///   échec (avec ou sans perte)         : reliquat += poids perdu − poids de la rune (minimum 0)
///   échec sans effet                   : inchangé
/// </summary>
public sealed class FmTracker
{
    public const string SuccesCritique = "Succès critique", SuccesNeutre = "Succès neutre",
        Echec = "Échec", EchecSansEffet = "Échec sans effet";

    const int RuneTypeId = 78;
    /// <summary>Écart maximal entre la consommation d'une rune et la nouvelle version de l'objet.</summary>
    static readonly TimeSpan RuneWindow = TimeSpan.FromSeconds(3);

    readonly GameData _data;
    /// <summary>Poids par point de Retro, fixes (affichés dans l'onglet FM, non modifiables).</summary>
    static Dictionary<int, double> _weights => DefaultWeights;
    /// <summary>Effet -> stat (effet bonus de référence). Un malus pointe vers son bonus (-Force -> +Force).</summary>
    readonly Dictionary<int, int> _statOfEffect = new();
    /// <summary>Stat -> effet malus correspondant, pour afficher « -3 en force ».</summary>
    readonly Dictionary<int, int> _malusOfStat = new();

    /// <summary>Runes consommées pas encore rattachées à un passage (plusieurs en cas de « Fusionner tout »).</summary>
    readonly Queue<(int Template, DateTime Time)> _pendingRunes = new();

    public Dictionary<long, FmItem> Items { get; private set; } = new();
    public long? CurrentUid { get; private set; }

    public event Action? Changed;
    public event Action<string>? Log;

    static string FmPath => Path.Combine(GameData.AppDir, "fm.json");

    public FmTracker(GameData data)
    {
        _data = data;

        // Un bonus et son malus partagent la caractéristique ET le libellé (« +#1 en force » / « -#1 en force »).
        // La caractéristique seule ne suffit pas : Vitalité et « Vole PDV » ont la même.
        foreach (var group in data.Effects.Values.GroupBy(e => (e.Characteristic, Suffix(e.Description))))
        {
            var bonus = group.Where(e => !e.IsMalus).OrderByDescending(e => _weights.ContainsKey(e.Id)).ThenBy(e => e.Id).FirstOrDefault();
            var malus = group.Where(e => e.IsMalus).OrderBy(e => e.Id).FirstOrDefault();
            int stat = bonus?.Id ?? malus!.Id;
            foreach (var e in group) _statOfEffect[e.Id] = e.Characteristic > 0 ? stat : e.Id;
            if (malus != null && bonus != null && group.Key.Characteristic > 0) _malusOfStat[stat] = malus.Id;
        }
        Load();
    }

    /// <summary>Libellé sans la valeur : « +#1{~1~2 à }#2 en force » -> « en force ».</summary>
    static string Suffix(string description) =>
        System.Text.RegularExpressions.Regex.Replace(description, @"^[+-]?#1\{[^}]*\}#2\s*", "").Trim();

    // --- Évènements venant du réseau (DofusTracker.CraftItemSeen / CraftItemConsumed / CraftOpened) ---

    /// <summary>Objet en cours de modification dans l'atelier : modèle, uid et caractéristiques connues.</summary>
    (int Template, long Uid, string Effects)? _work;
    /// <summary>Caractéristiques de l'objet au début du passage en cours (avant la rune).</summary>
    string _base = "";
    /// <summary>Dernière version de l'objet vue : un passage sans message de fin (ancien client) est enregistré après ce délai.</summary>
    DateTime _lastSeen;
    /// <summary>Message d'information du passage en cours (117, 183 ou 194), le plus précis s'il y en a plusieurs.</summary>
    int? _passCode;

    public void OnCraftOpened()
    {
        _work = null;
        _base = "";
        _passCode = null;
        _pendingRunes.Clear();
    }

    /// <summary>
    /// Objet vu dans l'atelier. Retro recrée l'objet (nouvel uid) à chaque rune : la fiche suit le nouvel uid. Le passage
    /// lui-même est enregistré à son message de fin (OnCraftResult), qui suit toujours la nouvelle version de l'objet.
    /// </summary>
    public void OnCraftItemSeen(long uid, int templateId, string effects)
    {
        var it = _data.GetItem(templateId);
        if (it.TypeId == RuneTypeId || ParseStats(effects).Count == 0) return;
        _lastSeen = DateTime.Now;

        if (_work is not { } work || work.Template != templateId)
        {
            _work = (templateId, uid, effects); // nouvel objet posé : état de départ
            _base = effects;
            _passCode = null;
            if (!Items.ContainsKey(uid) && SameItem(templateId, effects) is { } same && Items.Remove(same.Uid))
            {
                // Retro donne un nouvel uid à l'objet après une reconnexion : on reprend sa fiche
                same.Uid = uid;
                Items[uid] = same;
            }
            if (Items.TryGetValue(uid, out var known)) { known.Effects = effects; CurrentUid = uid; Changed?.Invoke(); }
            return;
        }
        _work = (templateId, uid, effects);
        // La fiche suit la nouvelle version de l'objet (nouvel uid)
        if (work.Uid != uid && Items.Remove(work.Uid, out var moved))
        {
            moved.Uid = uid;
            Items[uid] = moved;
            if (CurrentUid == work.Uid) CurrentUid = uid;
        }
    }

    /// <summary>
    /// Fiche d'un objet du même modèle : mêmes effets, sinon les plus proches (écart de poids d'au plus 5 % de l'objet :
    /// passages faits sans l'application).
    /// </summary>
    FmItem? SameItem(int templateId, string effects)
    {
        var mine = Items.Values.Where(i => i.TemplateId == templateId).ToList();
        if (mine.FirstOrDefault(i => i.Effects == effects) is { } exact) return exact;
        var now = ParseStats(effects);
        double total = now.Sum(kv => Math.Abs(kv.Value) * Weight(kv.Key));
        double Gap(FmItem i)
        {
            var old = ParseStats(i.Effects);
            return old.Keys.Union(now.Keys).Sum(k => Math.Abs(old.GetValueOrDefault(k) - now.GetValueOrDefault(k)) * Weight(k));
        }
        return mine.Select(i => (Item: i, Gap: Gap(i))).Where(x => x.Gap <= total * 0.05).OrderBy(x => x.Gap)
            .Select(x => x.Item).FirstOrDefault();
    }

    /// <summary>
    /// Message d'information du jeu pendant un passage : 194 = succès neutre (gain avec perte), 117 = échec avec perte,
    /// 183 = échec sans perte visible. Un succès critique n'en envoie aucun.
    /// </summary>
    public void OnCraftInfo(int code)
    {
        if (code is not (117 or 183 or 194)) return;
        static int Priority(int c) => c switch { 194 => 3, 117 => 2, _ => 1 };
        if (_passCode is not { } p || Priority(code) > Priority(p)) _passCode = code;
    }

    /// <summary>
    /// Fin d'un passage de rune, envoyée par le jeu après la nouvelle version de l'objet : « K;modèle » = succès,
    /// « EF » (ou autre « E… ») = échec. Succès sans message 194 ni perte = succès critique ; avec = succès neutre.
    /// </summary>
    public void OnCraftResult(string data)
    {
        if (_work is not { } work || data.Length == 0) return;
        bool success = data[0] == 'K';
        if (!success && data[0] != 'E') return;
        var changes = Diff(_base, work.Effects);
        int? rune = LastRune();
        // Atelier d'un autre métier (pas de rune, rien de changé) : pas de forgemagie
        if (rune == null && _passCode == null && changes.Count == 0) return;

        bool loss = changes.Any(c => c.Delta < 0);
        string outcome = success
            ? (_passCode == 194 || loss ? SuccesNeutre : SuccesCritique)
            : (_passCode == 117 || loss ? Echec : EchecSansEffet);
        RecordPass(changes, outcome, rune);
    }

    /// <summary>Enregistre le passage en cours : variations depuis le début du passage, résultat et rune.</summary>
    void RecordPass(List<FmStatChange> changes, string outcome, int? rune)
    {
        if (WorkItem() is not { } item) return;
        item.Effects = _work!.Value.Effects;
        _base = item.Effects;
        _passCode = null;
        AddEvent(item, new FmEvent
        {
            Time = DateTime.Now, Changes = changes, Outcome = outcome, RuneTemplateId = rune,
            WeightGained = changes.Where(c => c.Delta > 0).Sum(c => c.Delta * Weight(c.Stat)),
            WeightLost = changes.Where(c => c.Delta < 0).Sum(c => -c.Delta * Weight(c.Stat)),
            RuneWeight = rune is { } r ? RuneBreaking.RuneWeight(_data.GetItem(r).Name) ?? 0 : 0,
        });
    }

    /// <summary>Fiche de l'objet posé dans l'atelier, créée au besoin.</summary>
    FmItem? WorkItem()
    {
        if (_work is not { } work) return null;
        if (!Items.TryGetValue(work.Uid, out var item))
            Items[work.Uid] = item = new FmItem { Uid = work.Uid, TemplateId = work.Template, Effects = work.Effects };
        item.LastUsed = DateTime.Now;
        CurrentUid = work.Uid;
        return item;
    }

    void AddEvent(FmItem item, FmEvent ev)
    {
        ev.ReliquatBefore = item.Reliquat;
        ApplyReliquat(item, ev);
        item.History.Add(ev);
        Log?.Invoke($"FM {_data.GetItem(item.TemplateId).Name} : {ev.Outcome}, +{ev.WeightGained:0.##} / -{ev.WeightLost:0.##} poids, reliquat {item.Reliquat:0.##}.");
        Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// Met à jour le reliquat selon le résultat : succès critique, inchangé ; succès neutre ou échec (avec ou sans
    /// perte), reliquat + poids perdu − poids de la rune (minimum 0) : la rune est consommée même sans rien changer.
    /// </summary>
    static void ApplyReliquat(FmItem item, FmEvent ev)
    {
        // Retro ne garde pas de décimale : le poids de la rune est arrondi au-dessus (rune Vi, 0,75 : 1 de reliquat consommé),
        // celui d'une perte au-dessous
        double before = ev.ReliquatBefore;
        double rune = Math.Ceiling((ev.RuneWeight > 0 ? ev.RuneWeight : ev.WeightGained) - 1e-6);
        double lost = Math.Floor(ev.WeightLost + 1e-6);
        item.Reliquat = ev.Outcome switch
        {
            SuccesCritique => before,
            SuccesNeutre or Echec or EchecSansEffet => Math.Max(0, before + lost - rune),
            _ => before,
        };
        ev.ReliquatAfter = item.Reliquat;
    }

    public void OnItemConsumed(long uid, int templateId, int count)
    {
        if (_data.GetItem(templateId).TypeId != RuneTypeId) return;

        // La rune est retirée au début du passage, avant son message de fin (OnCraftResult) qui la reprend
        for (int i = 0; i < Math.Min(count, 100); i++) _pendingRunes.Enqueue((templateId, DateTime.Now));
    }

    /// <summary>
    /// Rune du passage qui se termine : la dernière retirée (une rune par passage). Les autres runes en attente (quantité
    /// mal connue, passage interrompu) sont oubliées : elles ne décalent pas les passages suivants.
    /// </summary>
    int? LastRune()
    {
        DropStaleRunes(DateTime.Now);
        int? rune = _pendingRunes.Count > 0 ? _pendingRunes.Last().Template : null;
        _pendingRunes.Clear();
        return rune;
    }

    /// <summary>Rune consommée autour de ce passage (par défaut : maintenant), s'il y en a une.</summary>
    int? DequeueRune(DateTime? at = null)
    {
        var time = at ?? DateTime.Now;
        DropStaleRunes(time);
        return _pendingRunes.TryPeek(out var r) && (r.Time - time).Duration() <= RuneWindow
            ? _pendingRunes.Dequeue().Template : null;
    }

    void DropStaleRunes(DateTime now)
    {
        while (_pendingRunes.TryPeek(out var r) && now - r.Time > RuneWindow) _pendingRunes.Dequeue();
    }

    /// <summary>
    /// Appelé régulièrement : oublie les runes qui n'ont été suivies d'aucun passage, et enregistre
    /// un résultat annoncé par le jeu dont la nouvelle version de l'objet n'a pas été vue.
    /// </summary>
    public void Tick()
    {
        // Objet modifié sans message de fin de passage (ne devrait pas arriver) : classé d'après ses stats seules
        if (_work is { } work && work.Effects != _base && DateTime.Now - _lastSeen > RuneWindow)
        {
            var changes = Diff(_base, work.Effects);
            bool gain = changes.Any(c => c.Delta > 0), loss = changes.Any(c => c.Delta < 0);
            RecordPass(changes, _passCode == 194 || gain && loss ? SuccesNeutre : gain ? SuccesCritique : Echec, DequeueRune(_lastSeen));
        }
        DropStaleRunes(DateTime.Now);
    }

    // --- Actions de l'utilisateur ---

    public void Select(long uid) { if (Items.ContainsKey(uid)) CurrentUid = uid; }

    public void SetReliquat(long uid, double value)
    {
        if (!Items.TryGetValue(uid, out var item)) return;
        item.Reliquat = Math.Max(0, value);
        Save();
        Changed?.Invoke();
    }

    public void ClearHistory(long uid)
    {
        if (!Items.TryGetValue(uid, out var item)) return;
        item.History.Clear();
        item.Reliquat = 0;
        Save();
        Changed?.Invoke();
    }

    public void Forget(long uid)
    {
        if (!Items.Remove(uid)) return;
        if (CurrentUid == uid) CurrentUid = null;
        Save();
        Changed?.Invoke();
    }

    // --- Caractéristiques ---

    public double Weight(int stat) => _weights.GetValueOrDefault(stat);

    /// <summary>Rang d'affichage de la stat dans l'infobulle du jeu (le plus grand en haut).</summary>
    public int Priority(int stat) => _data.Effects.TryGetValue(stat, out var e) ? e.Priority : 0;

    /// <summary>Caractéristique de la stat (voir ItemStats), 0 si inconnue.</summary>
    public int Characteristic(int stat) => _data.Effects.TryGetValue(stat, out var e) ? e.Characteristic : 0;

    /// <summary>Valeur signée par stat (les malus comptent en négatif).</summary>
    public Dictionary<int, int> ParseStats(string effects)
    {
        var stats = new Dictionary<int, int>();
        foreach (var part in effects.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split('#');
            if (f.Length < 2 || !int.TryParse(f[0], NumberStyles.HexNumber, null, out var id)
                || !int.TryParse(f[1], NumberStyles.HexNumber, null, out var value)
                || !_data.Effects.TryGetValue(id, out var e))
                continue;
            int stat = _statOfEffect.GetValueOrDefault(id, id);
            bool negative = e.IsMalus && stat != id; // malus rattaché à son bonus
            stats[stat] = stats.GetValueOrDefault(stat) + (negative ? -value : value);
        }
        return stats;
    }

    List<FmStatChange> Diff(string before, string after)
    {
        var a = ParseStats(before);
        var b = ParseStats(after);
        return a.Keys.Union(b.Keys)
            .Select(k => new FmStatChange(k, b.GetValueOrDefault(k) - a.GetValueOrDefault(k)))
            .Where(c => c.Delta != 0)
            .OrderByDescending(c => c.Delta)
            .ToList();
    }

    /// <summary>Libellé d'une stat avec sa valeur signée, ex. « +12 en force » ou « -3 en sagesse ».</summary>
    public string Describe(int stat, int value)
    {
        if (value >= 0) return _data.FormatEffect(stat, value);
        if (_malusOfStat.TryGetValue(stat, out var malus)) return _data.FormatEffect(malus, -value);
        // Pas d'effet malus connu (ex. % de résistance) : libellé du bonus, signe « - » à la place du « + »
        var text = _data.FormatEffect(stat, -value).TrimStart();
        return "-" + (text.StartsWith('+') ? text[1..] : text);
    }

    /// <summary>
    /// Lignes de l'objet dans l'ordre du jeu : stat (effet bonus), caractéristique (voir ItemStats,
    /// 0 si inconnue) et valeur signée. Les effets sans valeur (signature…) sont ignorés.
    /// </summary>
    public List<(int Stat, int Characteristic, int Value)> Lines(string effects)
    {
        var stats = ParseStats(effects);
        var order = new List<int>();
        foreach (var part in effects.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part.Split('#')[0], NumberStyles.HexNumber, null, out var id) && _statOfEffect.GetValueOrDefault(id, id) is var s
                && stats.ContainsKey(s) && !order.Contains(s))
                order.Add(s);
        // Ordre du jeu : l'infobulle trie les effets par priorité (champ « p » du fichier des effets), à égalité dans
        // l'ordre de l'objet
        return order.Where(s => stats[s] != 0)
            .OrderByDescending(Priority)
            .Select(s => (s, Characteristic(s), stats[s])).ToList();
    }

    // --- Poids des caractéristiques (affichés dans l'onglet FM, mode « Poids ») ---

    /// <summary>Poids par point de Dofus Retro (ceux du Brisage), indexés par effet « bonus ». Valeurs communautaires, à ajuster si besoin.</summary>
    static readonly Dictionary<int, double> DefaultWeights = new()
    {
        [111] = 100,  // PA
        [128] = 90,   // PM
        [117] = 51,   // Portée
        [182] = 30,   // Invocations
        [118] = 1,    // Force
        [119] = 1,    // Agilité
        [123] = 1,    // Chance
        [126] = 1,    // Intelligence
        [125] = 0.25, // Vitalité
        [124] = 3,    // Sagesse
        [174] = 0.1,  // Initiative
        [176] = 3,    // Prospection
        [158] = 0.25, // Pods
        [112] = 20,   // Dommages
        [138] = 2,    // % dommages
        [178] = 20,   // Soins
        [115] = 30,   // Coups critiques
        [225] = 15,   // Dommages aux pièges
        [226] = 2,    // % dommages aux pièges
        [220] = 30,   // Renvoi de dommages
        [210] = 4, [211] = 4, [212] = 4, [213] = 4, [214] = 4, // % résistances
        [240] = 5, [241] = 5, [242] = 5, [243] = 5, [244] = 5, // résistances fixes
    };


    /// <summary>Rune de forgemagie de chaque stat (la plus petite), pour son icône dans l'écran « Poids des stats ».</summary>
    static readonly Dictionary<int, string> RuneOfStat = new()
    {
        [111] = "Rune Ga Pa", [128] = "Rune Ga Pme", [117] = "Rune Po", [182] = "Rune Invo",
        [118] = "Rune Fo", [119] = "Rune Age", [123] = "Rune Cha", [126] = "Rune Ine", [125] = "Rune Vi", [124] = "Rune Sa",
        [174] = "Rune Ini", [176] = "Rune Prospe", [158] = "Rune Pod", [112] = "Rune Do", [138] = "Rune Do Per",
        [178] = "Rune So", [115] = "Rune Cri", [225] = "Rune Pi", [226] = "Rune Pi Per", [220] = "Rune Do Ren",
        [210] = "Rune Ré Per Terre", [211] = "Rune Ré Per Eau", [212] = "Rune Ré Per Air", [213] = "Rune Ré Per Feu", [214] = "Rune Ré Per Neutre",
        [240] = "Rune Ré Terre", [241] = "Rune Ré Eau", [242] = "Rune Ré Air", [243] = "Rune Ré Feu", [244] = "Rune Ré Neutre",
    };

    /// <summary>Stat de l'écran « Poids des stats » : rune (0 si inconnue) et poids par point.</summary>
    public sealed record WeightInfo(int Stat, int RuneId, string Rune, double Weight);

    /// <summary>Stats pondérées, dans l'ordre de la table.</summary>
    public List<WeightInfo> WeightList() => DefaultWeights.Keys
        .Select(stat =>
        {
            var rune = RuneOfStat.GetValueOrDefault(stat, "");
            return new WeightInfo(stat, rune.Length > 0 ? RuneBreaking.RuneId(_data, rune) ?? 0 : 0, rune, Weight(stat));
        })
        .ToList();


    // --- Persistance ---

    void Load()
    {
        try
        {
            if (File.Exists(FmPath))
                Items = JsonSerializer.Deserialize<Dictionary<long, FmItem>>(File.ReadAllText(FmPath)) ?? new();
        }
        catch { Items = new(); }
        CurrentUid = Items.Values.OrderByDescending(i => i.LastUsed).FirstOrDefault()?.Uid;
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(GameData.AppDir);
            File.WriteAllText(FmPath, JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log?.Invoke("Sauvegarde FM impossible : " + ex.Message); }
    }
}
