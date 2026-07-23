using System.Globalization;
using System.Xml.Linq;

namespace ProcesorFacturi.Core;

public sealed class FisierSursaInfo
{
    public string CaleCompleta { get; set; } = "";
    public string NumeFisier { get; set; } = "";
    public DateTime? DataOraDinNume { get; set; }
    public long DimensiuneOcteti { get; set; }
    public int NumarFacturi { get; set; }
    public TipFisier? TipDetectat { get; set; }
    public string? EroareDetectare { get; set; }
}

/// <summary>
/// Preluare din folderul sursă (§3.2), arhivare (§3.3), livrare în destinație cu coliziuni (§3.4)
/// și numele fișierelor rezultate (§11).
/// </summary>
public static class SurseFisiere
{
    private const string ModelNume = "F_13150581_*.xml";

    public static List<FisierSursaInfo> Scaneaza(string folderSursa)
    {
        if (!Directory.Exists(folderSursa))
            throw new ProcesareException($"Folderul sursă nu există sau nu este accesibil: {folderSursa}");

        var rezultate = new List<FisierSursaInfo>();
        foreach (var cale in Directory.EnumerateFiles(folderSursa, ModelNume).OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(cale);
            var numarFacturi = 0;
            TipFisier? tip = null;
            string? eroare = null;

            try
            {
                var document = XDocument.Load(cale, LoadOptions.None);
                numarFacturi = document.Root?.Elements("Factura").Count() ?? 0;
                tip = DetectorTip.Detecteaza(document);
            }
            catch (ProcesareException ex)
            {
                eroare = ex.Message;
            }
            catch (Exception ex)
            {
                eroare = $"XML invalid: {ex.Message}";
            }

            rezultate.Add(new FisierSursaInfo
            {
                CaleCompleta = cale,
                NumeFisier = info.Name,
                DataOraDinNume = ExtrageDataOraDinNume(info.Name),
                DimensiuneOcteti = info.Length,
                NumarFacturi = numarFacturi,
                TipDetectat = tip,
                EroareDetectare = eroare,
            });
        }
        return rezultate;
    }

    private static DateTime? ExtrageDataOraDinNume(string numeFisier)
    {
        // F_13150581_ddMMyyyy_HHMMSS.xml
        var faraExtensie = Path.GetFileNameWithoutExtension(numeFisier);
        var parti = faraExtensie.Split('_');
        if (parti.Length < 2) return null;

        var oraText = parti[parti.Length - 1];
        var dataText = parti[parti.Length - 2];

        return DateTime.TryParseExact(dataText + oraText, "ddMMyyyyHHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var rezultat)
            ? rezultat
            : null;
    }

    /// <summary>Mută fișierele selectate din folderul sursă (Tmp) în folderul de lucru al aplicației (§3.2).</summary>
    public static List<(string NumeFisier, string? Eroare)> PreiaFisiere(
        IEnumerable<string> caiSelectate, string folderLucru, Jurnal jurnal)
    {
        Directory.CreateDirectory(folderLucru);

        var rezultate = new List<(string, string?)>();
        foreach (var cale in caiSelectate)
        {
            var nume = Path.GetFileName(cale);
            try
            {
                var destinatie = Path.Combine(folderLucru, nume);
                File.Move(cale, destinatie);
                rezultate.Add((nume, null));
            }
            catch (Exception ex)
            {
                jurnal.Eroare(CategorieJurnal.Fisiere, $"Mutarea fișierului {nume} din Tmp a eșuat: {ex.Message}.");
                rezultate.Add((nume, ex.Message));
            }
        }
        return rezultate;
    }

    /// <summary>Arhivare obligatorie a sursei procesate cu succes, în Arhiva\AAAA-LL\ (§3.3).</summary>
    public static string Arhiveaza(string caleFisierLucru, string folderArhivaBaza, Jurnal jurnal)
    {
        var subfolder = Path.Combine(folderArhivaBaza, DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(subfolder);

        var destinatie = Path.Combine(subfolder, Path.GetFileName(caleFisierLucru));
        File.Move(caleFisierLucru, destinatie);

        jurnal.Info(CategorieJurnal.Fisiere, $"Fișier sursă arhivat: {destinatie}");
        return destinatie;
    }

    /// <summary>Calea finală de scriere în destinație, cu sufix incremental la coliziune de nume (§3.4).</summary>
    public static string CaleDestinatieCuSufixIncrementat(string folderDestinatie, string numeFisier, Jurnal jurnal)
    {
        Directory.CreateDirectory(folderDestinatie);

        var caleInitiala = Path.Combine(folderDestinatie, numeFisier);
        if (!File.Exists(caleInitiala)) return caleInitiala;

        var faraExtensie = Path.GetFileNameWithoutExtension(numeFisier);
        var extensie = Path.GetExtension(numeFisier);

        var contor = 2;
        string caleNoua;
        do
        {
            caleNoua = Path.Combine(folderDestinatie, $"{faraExtensie}_{contor}{extensie}");
            contor++;
        } while (File.Exists(caleNoua));

        jurnal.Avertizare(CategorieJurnal.Fisiere,
            $"Fișier existent în destinație: {numeFisier} — se salvează ca {Path.GetFileName(caleNoua)}.");
        return caleNoua;
    }

    /// <summary>Intervalul (min, max) al &lt;FacturaData&gt; peste facturile rămase după excludere (§11).</summary>
    public static (DateTime Min, DateTime Max) IntervalDate(XDocument document)
    {
        var facturi = document.Root?.Elements("Factura").ToList() ?? new List<XElement>();
        var date = new List<DateTime>();

        foreach (var factura in facturi)
        {
            var text = XmlUtils.TextTrim(factura.Element("Antet") ?? factura, "FacturaData");
            if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                date.Add(data);
        }

        if (date.Count == 0)
            throw new ProcesareException("Nu s-a putut determina intervalul de date (nicio <FacturaData> validă).");

        return (date.Min(), date.Max());
    }

    public static string NumeFisierIesireXml(DateTime dataMin, DateTime dataMax, TipFisier tip)
    {
        var sufix = tip == TipFisier.Intrari ? "IN" : "IE";
        return $"F_13150581_{dataMin:ddMMyyyy}_{dataMax:ddMMyyyy}_{sufix}.xml";
    }

    public static string NumeFisierRaport(string numeXmlRezultat)
        => Path.GetFileNameWithoutExtension(numeXmlRezultat) + "_raport.xlsx";

    public static string NumeFisierDbf(DateTime dataMin, DateTime dataMax)
        => $"IN_{dataMin:dd-MM-yyyy}_{dataMax:dd-MM-yyyy}_AF.DBF";
}
