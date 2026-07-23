namespace ProcesorFacturi.Core;

public enum NivelJurnal
{
    Info,
    Avertizare,
    Eroare
}

/// <summary>Categorii folosite pentru gruparea avertizărilor/erorilor în GUI și în sheet-ul „Jurnal” din raport.</summary>
public static class CategorieJurnal
{
    public const string Duplicat = "Duplicat";
    public const string Agregare = "Agregare";
    public const string Validare = "Validare";
    public const string Registru = "Registru";
    public const string Dbf = "Dbf";
    public const string Fisiere = "Fisiere";
    public const string General = "General";
}

public sealed class IntrareJurnal
{
    public DateTime Timestamp { get; }
    public NivelJurnal Nivel { get; }
    public string Categorie { get; }
    public string Mesaj { get; }
    public string? FacturaNumar { get; }

    public IntrareJurnal(NivelJurnal nivel, string categorie, string mesaj, string? facturaNumar)
    {
        Timestamp = DateTime.Now;
        Nivel = nivel;
        Categorie = categorie;
        Mesaj = mesaj;
        FacturaNumar = facturaNumar;
    }

    public override string ToString()
    {
        var prefix = Nivel switch
        {
            NivelJurnal.Eroare => "EROARE",
            NivelJurnal.Avertizare => "AVERTIZARE",
            _ => "INFO"
        };
        var factura = string.IsNullOrEmpty(FacturaNumar) ? "" : $" [factura {FacturaNumar}]";
        return $"{Timestamp:HH:mm:ss} {prefix} ({Categorie}){factura}: {Mesaj}";
    }
}

/// <summary>
/// Colector de avertizări/erori pentru o rulare de procesare. Nu scrie nimic direct în UI —
/// e returnat apelantului (§14.5), astfel încât testele să nu ceară o fereastră.
/// </summary>
public sealed class Jurnal
{
    private readonly List<IntrareJurnal> _intrari = new();

    public IReadOnlyList<IntrareJurnal> Intrari => _intrari;

    public void Info(string categorie, string mesaj, string? facturaNumar = null)
        => Adauga(NivelJurnal.Info, categorie, mesaj, facturaNumar);

    public void Avertizare(string categorie, string mesaj, string? facturaNumar = null)
        => Adauga(NivelJurnal.Avertizare, categorie, mesaj, facturaNumar);

    public void Eroare(string categorie, string mesaj, string? facturaNumar = null)
        => Adauga(NivelJurnal.Eroare, categorie, mesaj, facturaNumar);

    private void Adauga(NivelJurnal nivel, string categorie, string mesaj, string? facturaNumar)
        => _intrari.Add(new IntrareJurnal(nivel, categorie, mesaj, facturaNumar));

    public int Numara(NivelJurnal nivel) => _intrari.Count(i => i.Nivel == nivel);

    public int Numara(string categorie) => _intrari.Count(i => i.Categorie == categorie);

    public bool AreErori => _intrari.Any(i => i.Nivel == NivelJurnal.Eroare);
}
