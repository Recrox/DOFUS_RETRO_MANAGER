using DofusRetroManager.Data;

namespace DofusRetroManager;

/// <summary>
/// Objet forgemagé (liste des objets de l'onglet FM) : runes passées, résultats, coût des runes (prix HDV au passage,
/// sinon prix actuel ; Unpriced : runes sans prix connu) et tentatives d'exo PA / PM / PO (Ok : réussies).
/// </summary>
public sealed record FmItemRow(long Uid, int TemplateId, string Name, int Level, int Runes, int Sc, int Sn, int Ec,
    double Cost, int Unpriced, int ExoPa, int ExoPaOk, int ExoPm, int ExoPmOk, int ExoPo, int ExoPoOk, int Exo,
    string Last, long LastTicks, bool Current);

public partial class MainWindow
{
    /// <summary>Caractéristiques PA, PM et PO (voir ItemStats).</summary>
    const int CharPa = 1, CharPm = 23, CharPo = 19;

    void RefreshFmItems()
    {
        if (_fm == null || _data == null) return;
        SetRows(FmItemsGrid, _fm.Items.Values.OrderByDescending(i => i.LastUsed).Select(i =>
        {
            var info = _data.GetItem(i.TemplateId);
            double cost = 0;
            int unpriced = 0;
            foreach (var ev in i.History.Where(e => e.RuneTemplateId != null))
            {
                if ((ev.RunePrice ?? UnitPrice(ev.RuneTemplateId!.Value)) is { } p) cost += p;
                else unpriced++;
            }
            // Tentatives d'exo par caractéristique de la rune ; réussie : succès critique ou neutre
            var exo = i.History.Where(e => e.Attempt == "exo" && e.RuneTemplateId != null)
                .Select(e => (Char: RuneBreaking.RuneOf(_data.GetItem(e.RuneTemplateId!.Value).Name)?.Characteristic ?? 0,
                    Ok: e.Outcome is Fm.FmTracker.SuccesCritique or Fm.FmTracker.SuccesNeutre))
                .ToList();
            int N(int c) => exo.Count(x => x.Char == c);
            int Ok(int c) => exo.Count(x => x.Char == c && x.Ok);
            int Count(params string[] o) => i.History.Count(e => o.Contains(e.Outcome));
            return new FmItemRow(i.Uid, i.TemplateId, info.Name, info.Level, i.History.Count,
                Count(Fm.FmTracker.SuccesCritique), Count(Fm.FmTracker.SuccesNeutre), Count(Fm.FmTracker.Echec, Fm.FmTracker.EchecSansEffet),
                Math.Round(cost), unpriced, N(CharPa), Ok(CharPa), N(CharPm), Ok(CharPm), N(CharPo), Ok(CharPo),
                N(CharPa) + N(CharPm) + N(CharPo), i.LastUsed.ToString("dd/MM HH:mm"), i.LastUsed.Ticks, i.Uid == _fm.CurrentUid);
        }).ToList());
    }
}
