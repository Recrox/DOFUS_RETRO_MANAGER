using System.IO;
using System.Text.Json;
using DofusRetroManager.Data;

namespace DofusRetroManager.Elevage;

/// <summary>
/// Dragodinde, telle que décrite par le serveur (champs séparés par « : », sous-champs par « , ») :
///   id : modèle : ancêtres (14 modèles) : capacités : nom : sexe : xp,min,max : niveau : montable : pods : sauvage :
///   endurance,max : maturité,max : énergie,max : sérénité,min,max : amour,max : fécondation : fécondable : effets :
///   fatigue,max : reproductions,max
/// (formats de l'émulateur StarLoco et de la bibliothèque kralamoure/retroproto). Le nom est celui de la monture.
/// </summary>
public sealed class Mount
{
    public long Id { get; set; }
    public int Model { get; set; }
    public List<int> Ancestors { get; set; } = new();
    public List<int> Capacities { get; set; } = new();
    public string Name { get; set; } = "";
    public bool Female { get; set; }
    public long Xp { get; set; }
    public long XpMin { get; set; }
    public long XpMax { get; set; }
    public int Level { get; set; }
    public bool Mountable { get; set; }
    public int Pods { get; set; }
    public bool Wild { get; set; }
    public int Stamina { get; set; }
    public int StaminaMax { get; set; }
    public int Maturity { get; set; }
    public int MaturityMax { get; set; }
    public int Energy { get; set; }
    public int EnergyMax { get; set; }
    public int Serenity { get; set; }
    public int SerenityMin { get; set; }
    public int SerenityMax { get; set; }
    public int Love { get; set; }
    public int LoveMax { get; set; }
    /// <summary>Heures depuis la fécondation (-1 : pas fécondée).</summary>
    public int Fecundation { get; set; } = -1;
    public bool Fecundable { get; set; }
    public string Effects { get; set; } = "";
    public int Tiredness { get; set; }
    public int TirednessMax { get; set; }
    public int Reproductions { get; set; }
    public int ReproductionsMax { get; set; }

    static long L(string[] f, int i, int j = 0) =>
        i < f.Length && f[i].Split(',') is var p && j < p.Length && long.TryParse(p[j], out var v) ? v : 0;

    public static Mount? Parse(string s)
    {
        var f = s.Split(':');
        if (f.Length < 21 || !long.TryParse(f[0], out var id)) return null;
        static List<int> Ints(string x) => x.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => int.TryParse(v, out var n) ? n : 0).ToList();
        return new Mount
        {
            Id = id, Model = (int)L(f, 1), Ancestors = Ints(f[2]), Capacities = Ints(f[3]).Where(c => c > 0).ToList(),
            Name = f[4], Female = L(f, 5) == 1,
            Xp = L(f, 6), XpMin = L(f, 6, 1), XpMax = L(f, 6, 2), Level = (int)L(f, 7), Mountable = L(f, 8) == 1,
            Pods = (int)L(f, 9), Wild = L(f, 10) == 1,
            Stamina = (int)L(f, 11), StaminaMax = (int)L(f, 11, 1), Maturity = (int)L(f, 12), MaturityMax = (int)L(f, 12, 1),
            Energy = (int)L(f, 13), EnergyMax = (int)L(f, 13, 1),
            Serenity = (int)L(f, 14), SerenityMin = (int)L(f, 14, 1), SerenityMax = (int)L(f, 14, 2),
            Love = (int)L(f, 15), LoveMax = (int)L(f, 15, 1), Fecundation = int.TryParse(f[16], out var fec) ? fec : -1,
            Fecundable = L(f, 17) == 1, Effects = f[18],
            Tiredness = (int)L(f, 19), TirednessMax = (int)L(f, 19, 1), Reproductions = (int)L(f, 20), ReproductionsMax = (int)L(f, 20, 1),
        };
    }
}

/// <summary>Dragodindes d'un personnage : étable, enclos, monture équipée, et date du dernier relevé.</summary>
public sealed class MountSet
{
    public List<Mount> Stable { get; set; } = new();
    public List<Mount> Paddock { get; set; } = new();
    public Mount? Equipped { get; set; }
    public DateTime StableSeen { get; set; }
}

/// <summary>
/// Onglet Élevage : dragodindes de vos personnages, lues passivement (règle 4) à l'ouverture de l'étable ou de
/// l'enclos (ECK16|étable~enclos), à chaque mouvement (Ee+/Ee~/Ee-, Ef+/Ef-), et pour la monture équipée (Re+/Re-/Rd).
/// Seules les caractéristiques des montures sont gardées (elevage.json) ; aucun nom de joueur n'est lu. Rien ne sort du PC.
/// </summary>
public sealed class MountTracker
{
    static string DataPath => Path.Combine(GameData.AppDir, "elevage.json");
    public Dictionary<string, MountSet> Sets { get; private set; } = new();
    public event Action? Changed;

    public MountTracker()
    {
        try { if (File.Exists(DataPath)) Sets = JsonSerializer.Deserialize<Dictionary<string, MountSet>>(File.ReadAllText(DataPath)) ?? new(); }
        catch { Sets = new(); }
    }

    MountSet Of(string character) => Sets.TryGetValue(character, out var s) ? s : Sets[character] = new MountSet();

    /// <summary>Étable et enclos à leur ouverture (contenu de ECK16 : « étable~enclos », montures séparées par « ; »).</summary>
    public void OnOpen(string character, string payload)
    {
        if (character.Length == 0) return;
        var parts = payload.Split('~');
        var set = Of(character);
        set.Stable = ParseList(parts[0]);
        set.Paddock = parts.Length > 1 ? ParseList(parts[1]) : new();
        set.StableSeen = DateTime.Now;
        Save();
    }

    static List<Mount> ParseList(string s) => s.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Mount.Parse).OfType<Mount>().ToList();

    public void OnMessage(string character, string msg)
    {
        if (character.Length == 0) return;
        var set = Of(character);
        var body = msg[3..];
        switch (msg[..3])
        {
            case "Ee+" or "Ee~": Upsert(set.Stable, body); set.Paddock.RemoveAll(m => Mount.Parse(body)?.Id == m.Id); break;
            case "Ee-": if (long.TryParse(body, out var gone)) set.Stable.RemoveAll(m => m.Id == gone); break;
            case "Ef+": Upsert(set.Paddock, body); set.Stable.RemoveAll(m => Mount.Parse(body)?.Id == m.Id); break;
            case "Ef-": if (long.TryParse(body, out var back)) set.Paddock.RemoveAll(m => m.Id == back); break;
            case "Re+": set.Equipped = Mount.Parse(body) ?? set.Equipped; break;
            case "Re-": set.Equipped = null; break;
            default:
                // Rd : fiche d'une monture consultée : mise à jour si elle est connue
                if (msg.StartsWith("Rd") && Mount.Parse(msg[2..]) is { } m)
                {
                    if (set.Equipped?.Id == m.Id) set.Equipped = m;
                    Replace(set.Stable, m);
                    Replace(set.Paddock, m);
                }
                else return;
                break;
        }
        Save();
    }

    static void Upsert(List<Mount> list, string body)
    {
        if (Mount.Parse(body) is not { } m) return;
        if (!Replace(list, m)) list.Add(m);
    }

    static bool Replace(List<Mount> list, Mount m)
    {
        int i = list.FindIndex(x => x.Id == m.Id);
        if (i >= 0) list[i] = m;
        return i >= 0;
    }

    void Save()
    {
        try { File.WriteAllText(DataPath, JsonSerializer.Serialize(Sets)); } catch { }
        Changed?.Invoke();
    }
}
