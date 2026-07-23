using ClosedXML.Excel;

namespace ProcesorFacturi.Core;

/// <summary>Raportul XLSX (§10.2) — sheet „Raport” cu datele, plus sheet „Jurnal” (§0.2 pct. 2, decis: inclus).</summary>
public static class ScriitorXlsx
{
    private static readonly string[] ColoaneIntrari =
    {
        "FurnizorNume", "FurnizorCIF", "FacturaNumar", "FacturaData", "FacturaScadenta",
        "FacturaInformatiiSuplimentare", "Cod", "LinieNrCrt", "Activitate", "Cantitate",
        "Pret", "Valoare", "ProcTVA", "TVA", "Cont", "PretVanzare", "Descriere",
    };

    private static readonly string[] ColoaneIesiri =
    {
        "ClientNume", "ClientCIF", "ClientJudet", "ClientTara", "ClientLocalitate",
        "FacturaNumar", "FacturaData", "FacturaScadenta", "FacturaIndexSPV", "Cod",
        "LinieNrCrt", "Activitate", "Cantitate", "Pret", "Valoare", "ProcTVA", "TVA",
        "Cont", "PretVanzare", "Descriere",
    };

    public static void Scrie(string calePath, TipFisier tip, IReadOnlyList<RandRaport> randuri, Jurnal jurnal)
    {
        var coloane = tip == TipFisier.Intrari ? ColoaneIntrari : ColoaneIesiri;

        using var workbook = new XLWorkbook();
        var foaieDate = workbook.Worksheets.Add("Raport");
        ScrieAntetEvidentiat(foaieDate, coloane);

        for (var r = 0; r < randuri.Count; r++)
        {
            var rand = randuri[r];
            for (var c = 0; c < coloane.Length; c++)
                foaieDate.Cell(r + 2, c + 1).Value = rand.Valori.TryGetValue(coloane[c], out var v) ? v : "";
        }
        AutoDimensioneaza(foaieDate, coloane.Length);

        var foaieJurnal = workbook.Worksheets.Add("Jurnal");
        ScrieJurnal(foaieJurnal, jurnal);

        var director = Path.GetDirectoryName(calePath);
        if (!string.IsNullOrEmpty(director)) Directory.CreateDirectory(director);
        workbook.SaveAs(calePath);
    }

    private static void ScrieAntetEvidentiat(IXLWorksheet foaie, string[] coloane)
    {
        for (var c = 0; c < coloane.Length; c++)
        {
            var celula = foaie.Cell(1, c + 1);
            celula.Value = coloane[c];
            celula.Style.Font.Bold = true;
            celula.Style.Font.FontColor = XLColor.White;
            celula.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            celula.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void AutoDimensioneaza(IXLWorksheet foaie, int numarColoane)
    {
        for (var c = 1; c <= numarColoane; c++)
        {
            foaie.Column(c).AdjustToContents();
            if (foaie.Column(c).Width > 40) foaie.Column(c).Width = 40;
        }
    }

    private static void ScrieJurnal(IXLWorksheet foaie, Jurnal jurnal)
    {
        string[] antet = { "Timp", "Nivel", "Categorie", "Factura", "Mesaj" };
        for (var c = 0; c < antet.Length; c++)
        {
            var celula = foaie.Cell(1, c + 1);
            celula.Value = antet[c];
            celula.Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var intrare in jurnal.Intrari)
        {
            foaie.Cell(r, 1).Value = intrare.Timestamp.ToString("HH:mm:ss");
            foaie.Cell(r, 2).Value = intrare.Nivel.ToString();
            foaie.Cell(r, 3).Value = intrare.Categorie;
            foaie.Cell(r, 4).Value = intrare.FacturaNumar ?? "";
            foaie.Cell(r, 5).Value = intrare.Mesaj;
            r++;
        }
        AutoDimensioneaza(foaie, antet.Length);
    }
}
