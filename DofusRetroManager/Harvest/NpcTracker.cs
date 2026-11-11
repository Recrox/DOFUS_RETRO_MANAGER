using System.IO;
using System.Text.Json;
using DofusRetroManager.Data;

namespace DofusRetroManager.Harvest;

/// <summary>
/// PNJ vus sur les maps visitées (onglet Carte) : leur position n'est pas dans les fichiers du jeu, seul le serveur
/// l'envoie à l'entrée sur la map (GM, sprites de type -4). On n'en garde, dans pnj.json, que le modèle du PNJ, la map
/// et la case : ce sont des données du jeu, pas du joueur. Elles restent sur ce PC, ne sont jamais partagées ni
/// journalisées (règle 4). Les autres sprites (joueurs, marchands, monstres) sont ignorés ici.
/// </summary>
public sealed class NpcTracker
{
    static string DataPath => Path.Combine(GameData.AppDir, "pnj.json");

    /// <summary>Map -> PNJ (modèle) -> case.</summary>
    Dictionary<int, Dictionary<int, int>> _maps = new();
    readonly Dictionary<string, int> _streamMap = new();

    public event Action? Changed;

    /// <summary>Connexion du message en cours de traitement (à renseigner avant chaque message).</summary>
    public string Stream { get; set; } = "";

    public IReadOnlyDictionary<int, Dictionary<int, int>> Maps => _maps;

    public NpcTracker() => Load();

    public void OnMapChanged(string mapId)
    {
        if (int.TryParse(mapId, out var id)) _streamMap[Stream] = id;
        else _streamMap.Remove(Stream);
    }

    public void OnStreamClosed(string stream) => _streamMap.Remove(stream);

    /// <summary>Sprites de la map : "+case;dir;bonus;id;modèle;-4;…" pour un PNJ (le champ du nom porte son modèle).</summary>
    public void OnSprites(string body)
    {
        if (!_streamMap.TryGetValue(Stream, out var map)) return;
        bool changed = false;
        foreach (var s in body.Split('|'))
        {
            if (s.Length < 2 || s[0] != '+') continue;
            var f = s[1..].Split(';');
            if (f.Length < 6 || f[5] != "-4" || !int.TryParse(f[0], out var cell) || !int.TryParse(f[4], out var npc)) continue;
            if (!_maps.TryGetValue(map, out var npcs)) _maps[map] = npcs = new();
            if (npcs.TryGetValue(npc, out var old) && old == cell) continue;
            npcs[npc] = cell;
            changed = true;
        }
        if (!changed) return;
        Save();
        Changed?.Invoke();
    }

    void Load()
    {
        try
        {
            if (File.Exists(DataPath)) _maps = JsonSerializer.Deserialize<Dictionary<int, Dictionary<int, int>>>(File.ReadAllText(DataPath)) ?? new();
        }
        catch { _maps = new(); }
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(GameData.AppDir);
            File.WriteAllText(DataPath, JsonSerializer.Serialize(_maps));
        }
        catch { }
    }
}
