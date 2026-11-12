namespace DofusRetroManager.Hunt;

/// <summary>
/// Historique des sessions de combat (onglet Combat) : chaque session terminée (« Nouvelle session », ou une heure
/// sans combat) est résumée et gardée sans limite dans chasse.json. Seuls les totaux sont gardés : combats, XP, kamas,
/// objets obtenus, par personnage.
/// </summary>
public sealed partial class HuntTracker
{
    /// <summary>Sessions terminées, de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<SessionSummary> Sessions => _store.Sessions;

    /// <summary>Résumé de la session en cours (null sans combat), pour l'afficher en tête de l'historique.</summary>
    public SessionSummary? CurrentSession()
    {
        var fights = _store.Fights.Where(f => f.Time >= _store.SessionStart).ToList();
        return fights.Count == 0 ? null : Summarize(fights, _store.SessionStart, DateTime.Now, SessionDuration.TotalSeconds, false);
    }

    /// <summary>Oublie une session de l'historique (ligne erronée).</summary>
    public void ForgetSession(DateTime start)
    {
        if (_store.Sessions.RemoveAll(s => s.Start == start) == 0) return;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Range la session en cours dans l'historique, avant d'en ouvrir une autre. End : fin de la session.</summary>
    void ArchiveSession(DateTime end)
    {
        var fights = _store.Fights.Where(f => f.Time >= _store.SessionStart).ToList();
        if (fights.Count == 0) return;
        if (end < fights[^1].Time) end = fights[^1].Time;
        var seconds = ((_store.SessionPausedAt is { } p && p < end ? p : end) - _store.SessionStart).TotalSeconds - _store.SessionPausedSeconds;
        _store.Sessions.Add(Summarize(fights, _store.SessionStart, end, seconds, false));
    }

    /// <summary>
    /// Premier lancement avec l'historique : les sessions passées sont reconstituées d'après les combats gardés,
    /// coupées après une heure sans combat (comme une nouvelle session automatique). Les pauses ne sont pas connues.
    /// </summary>
    void RebuildSessions()
    {
        if (_store.Sessions.Count > 0) return;
        var past = _store.Fights.Where(f => f.Time < _store.SessionStart).OrderBy(f => f.Time).ToList();
        var chunk = new List<FightRecord>();
        foreach (var f in past)
        {
            if (chunk.Count > 0 && f.Time - chunk[^1].Time > SessionGap) Flush();
            chunk.Add(f);
        }
        Flush();

        void Flush()
        {
            if (chunk.Count == 0) return;
            var start = chunk[0].Time.AddSeconds(-chunk[0].DurationSeconds);
            _store.Sessions.Add(Summarize(chunk, start, chunk[^1].Time, (chunk[^1].Time - start).TotalSeconds, true));
            chunk = [];
        }
    }

    SessionSummary Summarize(List<FightRecord> fights, DateTime start, DateTime end, double seconds, bool rebuilt)
    {
        var s = new SessionSummary { Start = start, End = end, Seconds = Math.Max(0, seconds), Rebuilt = rebuilt };
        var counted = new List<FightRecord>();
        foreach (var f in fights)
        {
            AddGains(s.Total, f);
            var name = f.Character.Length > 0 ? f.Character : "?";
            if (!s.Characters.TryGetValue(name, out var c)) s.Characters[name] = c = new();
            c.Fights++;
            AddGains(c, f);
            AddMonsters(c, f);
            // Même combat qu'un autre de vos personnages (groupés ou non) : ce combat et ses monstres sont déjà comptés
            if (counted.Any(g => SameFight(g, f))) continue;
            counted.Add(f);
            s.Total.Fights++;
            AddMonsters(s.Total, f);
        }
        return s;

        void AddGains(SessionTotals t, FightRecord f)
        {
            t.Xp += f.Xp;
            t.Kamas += f.Kamas;
            foreach (var d in f.Drops)
            {
                t.Drops[d.ItemId] = t.Drops.GetValueOrDefault(d.ItemId) + d.Quantity;
                // Pierre d'âme pleine : gardée avec les monstres du combat, pour son prix (PDA du plus cher)
                if (SoulKey(d.ItemId, f) is { } key) t.Souls[key] = t.Souls.GetValueOrDefault(key) + d.Quantity;
            }
        }

        static void AddMonsters(SessionTotals t, FightRecord f)
        {
            foreach (var m in f.Monsters) t.Monsters[m.Id] = t.Monsters.GetValueOrDefault(m.Id) + 1;
        }
    }

    /// <summary>Pierre d'âme pleine d'un combat : « modèle:monstre,monstre… » (null si l'objet n'en est pas une ou sans monstres).</summary>
    public string? SoulKey(int itemId, FightRecord f) =>
        f.Monsters.Count == 0 || !Hdv.PdaTracker.StoneTypes.Contains(_data.GetItem(itemId).TypeId) ? null
            : $"{itemId}:{string.Join(",", f.Monsters.Select(m => m.Id).Distinct().Order())}";

    /// <summary>Nombre de combats distincts : un combat fait par plusieurs de vos personnages ne compte qu'une fois.</summary>
    public static int CountFights(IEnumerable<FightRecord> fights)
    {
        var counted = new List<FightRecord>();
        foreach (var f in fights)
            if (!counted.Any(g => SameFight(g, f))) counted.Add(f);
        return counted.Count;
    }

    /// <summary>Deux fins de combat de personnages différents à 15 s près, contre les mêmes monstres : le même combat.</summary>
    static bool SameFight(FightRecord a, FightRecord b) =>
        a.Character != b.Character && (a.Time - b.Time).Duration() <= TimeSpan.FromSeconds(15) && a.Monsters.Count == b.Monsters.Count
        && a.Monsters.OrderBy(m => m.Id).ThenBy(m => m.Level).SequenceEqual(b.Monsters.OrderBy(m => m.Id).ThenBy(m => m.Level));

    /// <summary>
    /// Sessions gardées avant le décompte des monstres : il est refait d'après les combats encore gardés de la session
    /// (les plus anciens ont pu être oubliés : le décompte reste alors vide).
    /// </summary>
    void FillSessionMonsters()
    {
        // Total de combats compté une fois par personnage (avant 1.84) : recompté si les combats sont encore gardés
        foreach (var s in _store.Sessions.Where(s => s.Characters.Count > 1 && s.Total.Fights == s.Characters.Values.Sum(c => c.Fights)))
        {
            var fights = _store.Fights.Where(f => f.Time >= s.Start && f.Time <= s.End).ToList();
            if (fights.Count == s.Total.Fights) s.Total.Fights = CountFights(fights);
        }
        foreach (var s in _store.Sessions.Where(s => s.Total.Fights > 0 && s.Total.Monsters.Count == 0))
        {
            var fights = _store.Fights.Where(f => f.Time >= s.Start && f.Time <= s.End).ToList();
            if (fights.Count != s.Characters.Values.Sum(c => c.Fights)) continue;
            var again = Summarize(fights, s.Start, s.End, s.Seconds, s.Rebuilt);
            s.Total.Monsters = again.Total.Monsters;
            foreach (var (name, c) in s.Characters)
                if (again.Characters.TryGetValue(name, out var a)) c.Monsters = a.Monsters;
        }
        // Sessions d'avant le prix des pierres d'âme pleines : leurs monstres repris des combats encore gardés
        foreach (var s in _store.Sessions.Where(s => s.Total.Souls.Count == 0 && s.Total.Drops.Keys.Any(id => Hdv.PdaTracker.StoneTypes.Contains(_data.GetItem(id).TypeId))))
        {
            var fights = _store.Fights.Where(f => f.Time >= s.Start && f.Time <= s.End).ToList();
            if (fights.Count != s.Characters.Values.Sum(c => c.Fights)) continue;
            var again = Summarize(fights, s.Start, s.End, s.Seconds, s.Rebuilt);
            s.Total.Souls = again.Total.Souls;
            foreach (var (name, c) in s.Characters)
                if (again.Characters.TryGetValue(name, out var a)) c.Souls = a.Souls;
        }
    }
}
