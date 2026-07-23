using System.Xml.Linq;
using static ProcesorFacturi.Core.XmlUtils;

namespace ProcesorFacturi.Core;

/// <summary>
/// Agregarea liniilor pe cotă de TVA — doar Intrări (§8). Nu se aplică la Ieșiri.
/// </summary>
public static class Agregator
{
    /// <summary>
    /// Consolidează &lt;Linie&gt;-urile din &lt;Detalii&gt;&lt;Continut&gt; ale unei singure facturi.
    /// Mutează elementul &lt;Factura&gt; primit, înlocuind liniile originale cu cele agregate.
    /// </summary>
    public static void AgregaLiniiFactura(XElement factura, Jurnal jurnal)
    {
        var numarFactura = TextTrim(factura.Element("Antet") ?? factura, "FacturaNumar");
        if (numarFactura.Length == 0) numarFactura = "necunoscut";

        var detalii = factura.Element("Detalii");
        var continut = detalii?.Element("Continut");
        var parinteLinii = continut ?? detalii;
        if (parinteLinii is null) return;

        var linii = parinteLinii.Elements("Linie").ToList();
        if (linii.Count == 0) return;

        var (valoareInainte, tvaInainte) = SumaTotaluri(linii);

        var grupuri = linii
            .GroupBy(CheieGrupare, GrupeCheieComparer.Instanta)
            .ToList();

        var rezultate = new List<XElement>();
        var nrCrt = 1;
        foreach (var grup in grupuri)
        {
            rezultate.Add(Consolideaza(grup.ToList(), nrCrt, jurnal, numarFactura));
            nrCrt++;
        }

        var (valoareDupa, tvaDupa) = SumaTotaluri(rezultate);
        if (Math.Abs(valoareDupa - valoareInainte) > 0.01m || Math.Abs(tvaDupa - tvaInainte) > 0.01m)
        {
            throw new FacturaEroareException(numarFactura,
                $"Totalul facturii după agregare diferă de cel dinainte " +
                $"(Valoare: {valoareInainte} → {valoareDupa}, TVA: {tvaInainte} → {tvaDupa}).");
        }

        parinteLinii.Elements("Linie").Remove();
        parinteLinii.Add(rezultate);
    }

    private readonly record struct CheieAgregare(string Descriere, string Cont, string ProcTva);

    /// <summary>
    /// Cheia de grupare (§8.2): descrierea (CodArticolFurnizor, cu fallback pe Explicatii dacă
    /// exportul o folosește ca purtător al descrierii), Cont, ProcTVA — comparație exactă pe
    /// șiruri trimmed.
    /// </summary>
    private static CheieAgregare CheieGrupare(XElement linie)
    {
        var descriere = TextTrim(linie, "CodArticolFurnizor");
        if (descriere.Length == 0)
        {
            var explicatii = TextTrim(linie, "Explicatii");
            if (explicatii.Length > 0) descriere = explicatii;
        }

        return new CheieAgregare(descriere, TextTrim(linie, "Cont"), TextTrim(linie, "ProcTVA"));
    }

    private sealed class GrupeCheieComparer : IEqualityComparer<CheieAgregare>
    {
        public static readonly GrupeCheieComparer Instanta = new();

        public bool Equals(CheieAgregare x, CheieAgregare y)
            => x.Descriere == y.Descriere && x.Cont == y.Cont && x.ProcTva == y.ProcTva;

        public int GetHashCode(CheieAgregare obj)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + obj.Descriere.GetHashCode();
                hash = hash * 31 + obj.Cont.GetHashCode();
                hash = hash * 31 + obj.ProcTva.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Consolidează un grup de linii cu aceeași cheie (§8.3). Elementul rezultat pornește ca o
    /// clonă a primei linii — astfel toate tag-urile nemenționate explicit în regulă (Gestiune,
    /// GUID_cod_articol, UM, CodArticolClient etc.) rămân exact ca în prima linie a grupului,
    /// fără a le enumera manual.
    /// </summary>
    private static XElement Consolideaza(List<XElement> grup, int nrCrt, Jurnal jurnal, string numarFactura)
    {
        var prima = grup[0];
        var rezultat = new XElement(prima);

        var valori = grup.Select(l => ParseDecimal(Text(l, "Valoare"))).ToList();
        var tvaValori = grup.Select(l => ParseDecimal(Text(l, "TVA"))).ToList();
        var sumaValoare = valori.Sum();
        var sumaTva = tvaValori.Sum();

        var zecimaleValoare = NumarZecimale(Text(prima, "Valoare"));
        var zecimaleTva = NumarZecimale(Text(prima, "TVA"));
        var zecimalePret = NumarZecimale(Text(prima, "Pret"));

        var cantitate = sumaValoare >= 0 ? "1" : "-1";
        var pret = FormatDecimal(Math.Abs(sumaValoare), zecimalePret);

        var candidatiPretVanzare = grup
            .Select(l => (Element: l, Valoare: ParseDecimal(Text(l, "PretVanzare"))))
            .Where(x => x.Valoare != 0m)
            .ToList();

        decimal pretVanzareValoare = 0m;
        var zecimalePretVanzare = NumarZecimale(Text(prima, "PretVanzare"));

        if (candidatiPretVanzare.Count == 0)
        {
            jurnal.Avertizare(CategorieJurnal.Agregare,
                "Nicio linie din grup cu PretVanzare != 0 — se scrie 0.", numarFactura);
        }
        else
        {
            pretVanzareValoare = candidatiPretVanzare[0].Valoare;
            zecimalePretVanzare = NumarZecimale(Text(candidatiPretVanzare[0].Element, "PretVanzare"));

            var distincte = candidatiPretVanzare.Select(c => c.Valoare).Distinct().ToList();
            if (distincte.Count > 1)
            {
                jurnal.Avertizare(CategorieJurnal.Agregare,
                    $"Mai multe linii cu PretVanzare != 0 și valori diferite ({string.Join(", ", distincte)}) " +
                    "— se preia prima; factura marcată pentru verificare manuală.", numarFactura);
            }
        }

        var activitati = grup.Select(l => TextTrim(l, "Activitate")).Distinct().ToList();
        if (activitati.Count > 1)
        {
            jurnal.Avertizare(CategorieJurnal.Agregare,
                $"<Activitate> diferită în același grup ({string.Join(", ", activitati)}) — se preia din prima linie.",
                numarFactura);
        }

        SetTagText(rezultat, "LinieNrCrt", nrCrt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SetTagText(rezultat, "Cantitate", cantitate);
        SetTagText(rezultat, "Pret", pret);
        SetTagText(rezultat, "Valoare", FormatDecimal(sumaValoare, zecimaleValoare));
        SetTagText(rezultat, "TVA", FormatDecimal(sumaTva, zecimaleTva));
        SetTagText(rezultat, "PretVanzare", FormatDecimal(pretVanzareValoare, zecimalePretVanzare));

        if (grup.Count > 1)
        {
            jurnal.Info(CategorieJurnal.Agregare,
                $"{grup.Count} linii consolidate într-una singură (cheie: {TextTrim(prima, "CodArticolFurnizor")} / " +
                $"{TextTrim(prima, "Cont")} / {TextTrim(prima, "ProcTVA")}%).", numarFactura);
        }

        return rezultat;
    }

    private static (decimal Valoare, decimal Tva) SumaTotaluri(IEnumerable<XElement> linii)
    {
        decimal valoare = 0m, tva = 0m;
        foreach (var linie in linii)
        {
            valoare += ParseDecimal(Text(linie, "Valoare"));
            tva += ParseDecimal(Text(linie, "TVA"));
        }
        return (valoare, tva);
    }
}
