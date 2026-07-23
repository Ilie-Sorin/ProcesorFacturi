using System.Xml.Linq;

namespace ProcesorFacturi.Core;

public enum TipFisier
{
    Intrari,
    Iesiri
}

/// <summary>Determinarea tipului (Intrări/Ieșiri) din &lt;FurnizorCIF&gt;, nu din numele fișierului (§2).</summary>
public static class DetectorTip
{
    public const string CifAncafarm = "13150581";

    public static TipFisier Detecteaza(XDocument document)
    {
        var facturi = document.Root?.Elements("Factura").ToList() ?? new List<XElement>();
        if (facturi.Count == 0)
            throw new FisierFaraFacturiException("XML-ul nu conține elemente <Factura>.");

        TipFisier? tip = null;
        foreach (var factura in facturi)
        {
            var cif = XmlUtils.TextTrim(factura.Element("Antet") ?? factura, "FurnizorCIF");
            if (string.IsNullOrEmpty(cif))
                throw new ProcesareException("<FurnizorCIF> lipsă sau gol într-o factură din fișier.");

            var tipCurent = string.Equals(cif, CifAncafarm, StringComparison.Ordinal)
                ? TipFisier.Iesiri
                : TipFisier.Intrari;

            tip ??= tipCurent;
            if (tip != tipCurent)
                throw new ProcesareException("Fișierul conține atât facturi de Intrări, cât și de Ieșiri (tip amestecat) — procesarea nu începe.");
        }

        return tip!.Value;
    }
}
