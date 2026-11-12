using System.Text.Json;
using DofusRetroManager.Elevage;

namespace DofusRetroManager;

/// <summary>Onglet Élevage : vos dragodindes (étable, enclos, monture équipée), lues par MountTracker.</summary>
public partial class MainWindow
{
    /// <summary>Type d'échange de l'étable et de l'enclos (ECK16|étable~enclos).</summary>
    const int PaddockExchangeType = 16;

    MountTracker? _mounts;

    void InitElevage()
    {
        _mounts = new MountTracker();
        _tracker.MountMessage += (character, msg) => Ui(() => _mounts.OnMessage(character, msg));
        _tracker.ExchangeChanged += (character, type, payload) =>
        {
            if (type == PaddockExchangeType) Ui(() => _mounts.OnOpen(character, payload));
        };
        _mounts.Changed += () => _webForce = true;
    }

    string ElevageJson()
    {
        if (_mounts == null || _data == null) return "null";
        object Ride(int model) => _data.Rides.TryGetValue(model, out var r) ? new { n = r.Name, c = r.Colors } : new { n = $"Dragodinde #{model}", c = new[] { 0, 0, 0 } };
        object M(Mount m, string where) => new
        {
            m.Id, where, m.Name, m.Female, m.Level, m.Xp, m.XpMin, m.XpMax, m.Mountable, m.Pods, m.Wild,
            m.Stamina, m.StaminaMax, m.Maturity, m.MaturityMax, m.Energy, m.EnergyMax,
            m.Serenity, m.SerenityMin, m.SerenityMax, m.Love, m.LoveMax, m.Fecundation, m.Fecundable,
            m.Tiredness, m.TirednessMax, m.Reproductions, m.ReproductionsMax,
            ride = Ride(m.Model),
            caps = m.Capacities.Select(c => _data.RideCapacities.TryGetValue(c, out var cap) ? new { n = cap.Name, d = cap.Description } : new { n = $"Capacité {c}", d = "" }),
            anc = m.Ancestors.Where(a => a > 0).Select(a => _data.Rides.TryGetValue(a, out var r) ? r.Name : $"#{a}"),
        };
        return JsonSerializer.Serialize(_mounts.Sets.Where(kv => kv.Value.Stable.Count + kv.Value.Paddock.Count > 0 || kv.Value.Equipped != null)
            .OrderBy(kv => kv.Key).Select(kv => new
            {
                c = kv.Key, g = _hunt?.LookOf(kv.Key)?.Gfx ?? 0, seen = kv.Value.StableSeen == default ? null : kv.Value.StableSeen.ToString("dd/MM HH:mm"),
                mounts = (kv.Value.Equipped is { } e ? new[] { M(e, "eq") } : [])
                    .Concat(kv.Value.Paddock.Select(m => M(m, "enclos"))).Concat(kv.Value.Stable.Select(m => M(m, "etable"))),
            }));
    }
}
