using ClosedXML.Excel;

namespace ProcesorFacturi.Core;

/// <summary>Un rând din Grupe.xlsx (Nume, Cont, Activitate) — păstrat doar pentru referință vizuală în GUI (§5, §13).</summary>
public sealed class RandGrupa
{
    public string Nume { get; init; } = "";
    public string Cont { get; init; } = "";
    public string Activitate { get; init; } = "";
}

/// <summary>
/// Maparea din Grupe.xlsx (sheet-uri „Intrari”/„Iesiri”). Folosită DOAR pentru validare —
/// aplicația nu completează și nu suprascrie &lt;Cont&gt;/&lt;Activitate&gt; din XML (§5).
/// </summary>
public sealed class MapareGrupe
{
    public List<RandGrupa> Intrari { get; } = new();
    public List<RandGrupa> Iesiri { get; } = new();

    private readonly HashSet<string> _numeIntrariNormalizate = new(StringComparer.Ordinal);
    private readonly HashSet<string> _numeIesiriNormalizate = new(StringComparer.Ordinal);

    public static MapareGrupe Incarca(string caleXlsx)
    {
        if (!File.Exists(caleXlsx))
            throw new ProcesareException($"Grupe.xlsx lipsește: {caleXlsx}");

        var rezultat = new MapareGrupe();
        try
        {
            using var workbook = new XLWorkbook(caleXlsx);
            rezultat.Intrari.AddRange(CitesteSheet(workbook, "Intrari"));
            rezultat.Iesiri.AddRange(CitesteSheet(workbook, "Iesiri"));
        }
        catch (Exception ex) when (ex is not ProcesareException)
        {
            throw new ProcesareException($"Grupe.xlsx nu poate fi citit: {ex.Message}", ex);
        }

        foreach (var rand in rezultat.Intrari)
            rezultat._numeIntrariNormalizate.Add(Normalizeaza(rand.Nume));
        foreach (var rand in rezultat.Iesiri)
            rezultat._numeIesiriNormalizate.Add(Normalizeaza(rand.Nume));

        return rezultat;
    }

    private static IEnumerable<RandGrupa> CitesteSheet(XLWorkbook workbook, string numeSheet)
    {
        if (!workbook.Worksheets.Contains(numeSheet))
            throw new ProcesareException($"Grupe.xlsx nu conține sheet-ul „{numeSheet}”.");

        var sheet = workbook.Worksheet(numeSheet);
        var randuriFolosite = sheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();

        var randuri = new List<RandGrupa>();
        foreach (var rand in randuriFolosite)
        {
            var nume = rand.Cell(1).GetString().Trim();
            if (nume.Length == 0) continue;

            randuri.Add(new RandGrupa
            {
                Nume = nume,
                Cont = rand.Cell(2).GetString().Trim(),
                Activitate = rand.Cell(3).GetString().Trim()
            });
        }
        return randuri;
    }

    public bool ContineIntrari(string nume) => _numeIntrariNormalizate.Contains(Normalizeaza(nume));

    public bool ContineIesiri(string nume) => _numeIesiriNormalizate.Contains(Normalizeaza(nume));

    private static string Normalizeaza(string valoare) => valoare.Trim().ToUpperInvariant();
}
