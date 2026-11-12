using System.Text.Json;
using System.Windows;
using DofusRetroManager.Hunt;
using DofusRetroManager.Multi;

namespace DofusRetroManager;

/// <summary>
/// Objectif de farm (onglet Combat) : un objet et une quantité voulue. À chaque fin de combat, les quantités de l'objet
/// obtenues par vos personnages s'ajoutent (décompte dans l'onglet et la superposition) ; une bulle et un son préviennent
/// quand la quantité est atteinte. Seuls les objets de vos fins de combat sont lus (règle 4).
/// </summary>
public partial class MainWindow
{
    void InitFarm()
    {
        FarmItem.TextChanged += (_, _) => SuggestFarmItems();
        FarmStart.Click += FarmStart_Click;
        FarmReset.Click += (_, _) => { _hunt?.RemoveFarm(0); FarmStatus.Text = ""; RefreshFarm(); };
        _hunt!.Changed += RefreshFarm;
        _hunt.FarmReached += OnFarmReached;
        RefreshFarm();
    }

    void SuggestFarmItems()
    {
        var text = FarmItem.Text.Trim();
        if (_data == null || text.Length < 2) { FarmSuggest.Text = ""; return; }
        FarmSuggest.Text = string.Join("|", _data.Items.Values.Where(i => Contains(i.Name, text)).Select(i => i.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(n => n.Length).ThenBy(n => n).Take(12));
    }

    void FarmStart_Click(object sender, RoutedEventArgs e)
    {
        if (_data == null || _hunt == null) return;
        var name = FarmItem.Text.Trim();
        var exact = _data.Items.Values.Where(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var found = exact.Count > 0 ? exact : name.Length < 3 ? [] : _data.Items.Values.Where(i => Contains(i.Name, name)).ToList();
        if (found.Select(i => i.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
        {
            FarmStatus.Text = found.Count == 0 ? $"Aucun objet « {name} »." : "Plusieurs objets : précisez le nom.";
            return;
        }
        int target = int.TryParse(FarmTarget.Text, out var t) && t > 0 ? t : 50;
        _hunt.AddFarm(new FarmGoal { ItemId = found[0].Id, Target = target });
        FarmItem.Text = "";
        FarmStatus.Text = "";
    }

    void OnFarmReached(FarmGoal goal, HuntCharacter c)
    {
        var name = _data?.GetItem(goal.ItemId).Name ?? "";
        AddLog($"Objectif de farm atteint : {goal.Count} × {name}.");
        try { ArchiSound.PlayNotice(_hunt?.SoundVolume ?? 70); } catch { }
        NoticeToast.Show("Objectif atteint", $"🎯 {goal.Count} / {goal.Target} {name}\n{goal.Fights} combat(s)", null,
            WindowOf(c) ?? default, icon: Data.ItemIcons.Get(goal.ItemId));
    }

    void RefreshFarm()
    {
        var goals = _hunt?.Farms ?? [];
        FarmState.Text = goals.Count == 0 || _data == null ? "" : JsonSerializer.Serialize(goals.Select(goal => new
        {
            id = goal.ItemId, n = _data.GetItem(goal.ItemId).Name, c = goal.Count, t = goal.Target, f = goal.Fights,
            s = goal.Steps.TakeLast(8).Reverse().Select(s => new { n = s.Number, g = s.Got, t = s.Total }),
        }));
        UpdateOverlay();
    }

    /// <summary>Objectif montré par la superposition : le premier pas encore atteint (sinon le dernier).</summary>
    FarmGoal? OverlayFarm() => _hunt?.Farms is { Count: > 0 } l ? l.FirstOrDefault(g => g.Count < g.Target) ?? l[^1] : null;

    /// <summary>Décompte pour la superposition : titre (« +n » : autres objectifs), remplissage (0 à 1) et derniers combats.</summary>
    (string Title, double Fill, List<string> Steps) FarmOverlay()
    {
        if (OverlayFarm() is not { } g || _data == null) return ("", 0, []);
        int others = _hunt!.Farms.Count - 1;
        return ($"{g.Count} / {g.Target}" + (others > 0 ? $"  (+{others})" : ""), Math.Min(1, g.Count / (double)Math.Max(1, g.Target)),
            g.Steps.TakeLast(3).Reverse().Select(s => $"Combat {s.Number} · +{s.Got} → {s.Total}").ToList());
    }
}
