using System.Xml.Linq;
using static ProcesorFacturi.Core.XmlUtils;

namespace ProcesorFacturi.Core;

/// <summary>
/// Procesare Ieșiri (FurnizorCIF = 13150581) — §6. Nu se aplică agregarea (§8);
/// liniile rămân exact ca în sursă.
/// </summary>
public static class ProcesorIesiri
{
    // §6.3 — prefixul se calculează din Activitate, indiferent dacă e codul original (01-04)
    // sau deja remapat (11-14); rezultatul e identic în ambele cazuri.
    private static readonly Dictionary<string, string> HartaPrefix = new()
    {
        ["01"] = "BTANCA1 ", ["11"] = "BTANCA1 ",
        ["02"] = "BTANCA2 ", ["12"] = "BTANCA2 ",
        ["03"] = "BTANCA3 ", ["13"] = "BTANCA3 ",
        ["04"] = "BTANCA4 ", ["14"] = "BTANCA4 ",
    };

    // §6.2
    private static readonly Dictionary<string, string> HartaRemapare = new()
    {
        ["01"] = "11", ["02"] = "12", ["03"] = "13", ["04"] = "14",
    };

    public static RezultatProcesare Proceseaza(XDocument document, MapareGrupe grupe, Registru registruIeAnte, Jurnal jurnal)
    {
        var facturi = document.Root?.Elements("Factura").ToList() ?? new List<XElement>();
        var deExclus = new List<XElement>();
        var randuriRaport = new List<RandRaport>();
        var cheiNoi = new List<string>();
        var numarFacturiProcesate = 0;

        foreach (var factura in facturi)
        {
            var antet = factura.Element("Antet");
            if (antet is null) { deExclus.Add(factura); continue; }

            var numarFactura = TextTrim(antet, "FacturaNumar");
            var facturaData = TextTrim(antet, "FacturaData");

            var linii = ExtrageLinii(factura);
            if (linii.Count == 0)
            {
                jurnal.Eroare(CategorieJurnal.Validare, "Factura nu are nicio linie <Linie>.", numarFactura);
                deExclus.Add(factura);
                continue;
            }

            var activitatePrima = DeterminaActivitatePrima(linii, jurnal, numarFactura);
            if (!HartaPrefix.TryGetValue(activitatePrima, out var prefix))
            {
                jurnal.Eroare(CategorieJurnal.Validare, $"Activitate necunoscută „{activitatePrima}” — factura exclusă.", numarFactura);
                deExclus.Add(factura);
                continue;
            }

            var numarPrefixat = prefix + numarFactura;
            var cheieRegistru = $"{numarPrefixat}|{facturaData}";

            if (registruIeAnte.Contine(cheieRegistru))
            {
                jurnal.Info(CategorieJurnal.Duplicat, $"Factura {numarFactura} exclusă — deja prezentă în IeAnte.txt.", numarFactura);
                deExclus.Add(factura);
                continue;
            }

            var furnizorNume = TextTrim(antet, "FurnizorNume");
            if (furnizorNume.Length > 0 && !grupe.ContineIesiri(furnizorNume))
            {
                jurnal.Avertizare(CategorieJurnal.Validare,
                    $"„{furnizorNume}” negăsit în Grupe.xlsx (sheet Iesiri) — posibil client/depozit nou.", numarFactura);
            }

            foreach (var linie in linii)
            {
                RedenumesteTag(linie, "CodArticolClient", "Descriere");

                var activitateLinie = TextTrim(linie, "Activitate");
                if (HartaRemapare.TryGetValue(activitateLinie, out var remapata))
                    SetTagText(linie, "Activitate", remapata);
            }

            SetTagText(antet, "FacturaNumar", numarPrefixat);

            foreach (var linie in linii)
                randuriRaport.Add(ConstruiesteRandRaport(antet, linie));

            cheiNoi.Add(cheieRegistru);
            numarFacturiProcesate++;
        }

        foreach (var factura in deExclus)
            factura.Remove();

        if (numarFacturiProcesate == 0)
        {
            jurnal.Avertizare(CategorieJurnal.General,
                "Toate facturile din fișier erau deja importate (sau invalide) — nu se generează output.");
            return new RezultatProcesare { AreOutput = false, Tip = TipFisier.Iesiri };
        }

        return new RezultatProcesare
        {
            AreOutput = true,
            DocumentRezultat = document,
            RanduriRaport = randuriRaport,
            CheiRegistruNoi = cheiNoi,
            Tip = TipFisier.Iesiri,
        };
    }

    private static List<XElement> ExtrageLinii(XElement factura)
    {
        var detalii = factura.Element("Detalii");
        return detalii?.Element("Continut")?.Elements("Linie").ToList()
               ?? detalii?.Elements("Linie").ToList()
               ?? new List<XElement>();
    }

    private static string DeterminaActivitatePrima(List<XElement> linii, Jurnal jurnal, string numarFactura)
    {
        var activitati = linii.Select(l => TextTrim(l, "Activitate")).ToList();
        var distincte = activitati.Distinct().ToList();
        if (distincte.Count > 1)
        {
            jurnal.Avertizare(CategorieJurnal.Validare,
                $"Activități diferite pe liniile facturii ({string.Join(", ", distincte)}) — se ia prefixul din prima linie.",
                numarFactura);
        }
        return activitati[0];
    }

    private static void RedenumesteTag(XElement linie, string tagVechi, string tagNou)
    {
        var element = linie.Element(tagVechi);
        if (element is null) return;
        var nou = new XElement(tagNou, element.Value);
        element.ReplaceWith(nou);
    }

    private static RandRaport ConstruiesteRandRaport(XElement antet, XElement linie)
    {
        var rand = new RandRaport();
        rand.Valori["ClientNume"] = TextTrim(antet, "ClientNume");
        rand.Valori["ClientCIF"] = TextTrim(antet, "ClientCIF");
        rand.Valori["ClientJudet"] = TextTrim(antet, "ClientJudet");
        rand.Valori["ClientTara"] = TextTrim(antet, "ClientTara");
        rand.Valori["ClientLocalitate"] = TextTrim(antet, "ClientLocalitate");
        rand.Valori["FacturaNumar"] = TextTrim(antet, "FacturaNumar");
        rand.Valori["FacturaData"] = TextTrim(antet, "FacturaData");
        rand.Valori["FacturaScadenta"] = TextTrim(antet, "FacturaScadenta");
        rand.Valori["FacturaIndexSPV"] = TextTrim(antet, "FacturaIndexSPV");
        rand.Valori["Cod"] = TextTrim(antet, "Cod");
        rand.Valori["LinieNrCrt"] = TextTrim(linie, "LinieNrCrt");
        rand.Valori["Activitate"] = TextTrim(linie, "Activitate");
        rand.Valori["Cantitate"] = TextTrim(linie, "Cantitate");
        rand.Valori["Pret"] = TextTrim(linie, "Pret");
        rand.Valori["Valoare"] = TextTrim(linie, "Valoare");
        rand.Valori["ProcTVA"] = TextTrim(linie, "ProcTVA");
        rand.Valori["TVA"] = TextTrim(linie, "TVA");
        rand.Valori["Cont"] = TextTrim(linie, "Cont");
        rand.Valori["PretVanzare"] = TextTrim(linie, "PretVanzare");
        rand.Valori["Descriere"] = TextTrim(linie, "Descriere");
        return rand;
    }
}
