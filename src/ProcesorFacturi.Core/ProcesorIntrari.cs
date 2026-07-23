using System.Globalization;
using System.Xml.Linq;
using static ProcesorFacturi.Core.XmlUtils;

namespace ProcesorFacturi.Core;

/// <summary>Procesare Intrări (FurnizorCIF ≠ 13150581) — §7: excludere InAnte, agregare, DBF, ANCAFARM.</summary>
public static class ProcesorIntrari
{
    public static RezultatProcesare Proceseaza(XDocument document, MapareGrupe grupe, Registru registruInAnte, Jurnal jurnal)
    {
        var facturi = document.Root?.Elements("Factura").ToList() ?? new List<XElement>();
        var deExclus = new List<XElement>();
        var randuriRaport = new List<RandRaport>();
        var inregistrariDbf = new List<InregistrareDbf>();
        var cheiNoi = new List<string>();
        var numarFacturiProcesate = 0;

        foreach (var factura in facturi)
        {
            var antet = factura.Element("Antet");
            if (antet is null) { deExclus.Add(factura); continue; }

            var numarFactura = TextTrim(antet, "FacturaNumar");
            var nrNir = TextTrim(antet, "FacturaInformatiiSuplimentare");

            var adaugaInRegistru = false;

            if (nrNir.Length == 0)
            {
                jurnal.Avertizare(CategorieJurnal.Validare,
                    "Factură fără <FacturaInformatiiSuplimentare> (sau valoare goală) — rămâne în output, nu poate fi verificată ca duplicat.",
                    numarFactura);
            }
            else if (registruInAnte.Contine(nrNir))
            {
                jurnal.Info(CategorieJurnal.Duplicat, $"Factura {numarFactura} exclusă — NR_NIR {nrNir} deja în InAnte.txt.", numarFactura);
                deExclus.Add(factura);
                continue;
            }
            else
            {
                adaugaInRegistru = true;
            }

            var clientNume = TextTrim(antet, "ClientNume");
            if (clientNume.Length > 0 && !grupe.ContineIntrari(clientNume))
            {
                jurnal.Avertizare(CategorieJurnal.Validare,
                    $"„{clientNume}” negăsit în Grupe.xlsx (sheet Intrari) — posibil client/depozit nou.", numarFactura);
            }

            try
            {
                Agregator.AgregaLiniiFactura(factura, jurnal);
            }
            catch (FacturaEroareException ex)
            {
                jurnal.Eroare(CategorieJurnal.Agregare, ex.Message, numarFactura);
                deExclus.Add(factura);
                continue;
            }

            var linii = ExtrageLinii(factura);

            List<InregistrareDbf> inregistrariFactura;
            try
            {
                inregistrariFactura = ConstruiesteInregistrariDbf(antet, linii, numarFactura, jurnal);
            }
            catch (FacturaEroareException ex)
            {
                jurnal.Eroare(CategorieJurnal.Dbf, ex.Message, numarFactura);
                deExclus.Add(factura);
                continue;
            }

            foreach (var linie in linii)
                RedenumesteTag(linie, "CodArticolFurnizor", "Descriere");

            SetTagText(antet, "ClientNume", "ANCAFARM");

            foreach (var linie in linii)
                randuriRaport.Add(ConstruiesteRandRaport(antet, linie));

            inregistrariDbf.AddRange(inregistrariFactura);

            if (adaugaInRegistru) cheiNoi.Add(nrNir);
            numarFacturiProcesate++;
        }

        foreach (var factura in deExclus)
            factura.Remove();

        if (numarFacturiProcesate == 0)
        {
            jurnal.Avertizare(CategorieJurnal.General,
                "Toate facturile din fișier erau deja importate (sau invalide) — nu se generează output.");
            return new RezultatProcesare { AreOutput = false, Tip = TipFisier.Intrari };
        }

        return new RezultatProcesare
        {
            AreOutput = true,
            DocumentRezultat = document,
            RanduriRaport = randuriRaport,
            CheiRegistruNoi = cheiNoi,
            InregistrariDbf = inregistrariDbf,
            Tip = TipFisier.Intrari,
        };
    }

    private static List<XElement> ExtrageLinii(XElement factura)
    {
        var detalii = factura.Element("Detalii");
        return detalii?.Element("Continut")?.Elements("Linie").ToList()
               ?? detalii?.Elements("Linie").ToList()
               ?? new List<XElement>();
    }

    private static List<InregistrareDbf> ConstruiesteInregistrariDbf(
        XElement antet, List<XElement> linii, string numarFactura, Jurnal jurnal)
    {
        var cod = TextTrim(antet, "Cod");
        if (cod.Length == 0)
        {
            jurnal.Avertizare(CategorieJurnal.Validare, "<Antet><Cod> lipsă sau gol — COD rămâne gol în DBF.", numarFactura);
        }

        var tvai = string.Equals(TextTrim(antet, "FacturaTVAIncasare"), "da", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var data = ParseDataXml(TextTrim(antet, "FacturaData"));
        var scadent = ParseDataXml(TextTrim(antet, "FacturaScadenta"));
        var nrNir = TextTrim(antet, "FacturaInformatiiSuplimentare");
        var nrIntrare = TextTrim(antet, "FacturaNumar");

        var rezultat = new List<InregistrareDbf>();
        foreach (var linie in linii)
        {
            var tvaArt = ScriitorDbf.ConvertesteTvaArt(TextTrim(linie, "ProcTVA"), numarFactura);

            rezultat.Add(new InregistrareDbf
            {
                NrNir = nrNir,
                NrIntrare = nrIntrare,
                Cod = cod,
                Data = data,
                Scadent = scadent,
                Tvai = tvai,
                DenArt = TextTrim(linie, "CodArticolFurnizor"),
                Um = TextTrim(linie, "UM"),
                Cantitate = ParseDecimal(Text(linie, "Cantitate")),
                TvaArt = tvaArt,
                Valoare = ParseDecimal(Text(linie, "Valoare")),
                Tva = ParseDecimal(Text(linie, "TVA")),
                Cont = TextTrim(linie, "Cont"),
                PretVanz = ParseDecimal(Text(linie, "PretVanzare")),
                Grupa = TextTrim(linie, "Activitate"),
            });
        }
        return rezultat;
    }

    private static DateTime? ParseDataXml(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;
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
        rand.Valori["FurnizorNume"] = TextTrim(antet, "FurnizorNume");
        rand.Valori["FurnizorCIF"] = TextTrim(antet, "FurnizorCIF");
        rand.Valori["FacturaNumar"] = TextTrim(antet, "FacturaNumar");
        rand.Valori["FacturaData"] = TextTrim(antet, "FacturaData");
        rand.Valori["FacturaScadenta"] = TextTrim(antet, "FacturaScadenta");
        rand.Valori["FacturaInformatiiSuplimentare"] = TextTrim(antet, "FacturaInformatiiSuplimentare");
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
