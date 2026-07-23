namespace ProcesorFacturi.Core;

/// <summary>Rezultatul procesării unui singur fișier de lucru (folosit de GUI, §13).</summary>
public sealed class RezultatFisier
{
    public string NumeFisierSursa { get; init; } = "";
    public bool Succes { get; init; }
    public bool AreOutput { get; init; }
    public string? CaleXmlGenerat { get; init; }
    public string? CaleRaportGenerat { get; init; }
    public string? CaleDbfGenerat { get; init; }
    public string? CaleArhiva { get; init; }
    public string? MesajEroare { get; init; }
}

/// <summary>
/// Orchestrează fluxul complet pentru un fișier de lucru: detectare tip → procesare →
/// generare fișiere în destinație → actualizare registru → arhivare (§3, §6, §7, §9, §11, §12).
/// </summary>
public static class ServiciuProcesare
{
    public static RezultatFisier ProceseazaFisier(string caleFisierLucru, ConfigApp config, MapareGrupe grupe, Jurnal jurnal)
    {
        var numeFisier = Path.GetFileName(caleFisierLucru);
        try
        {
            var document = ScriitorXml.Incarca(caleFisierLucru);
            var tip = DetectorTip.Detecteaza(document);

            var caleRegistru = tip == TipFisier.Intrari ? config.CaleInAnte : config.CaleIeAnte;
            var registru = Registru.Deschide(caleRegistru, jurnal);

            var rezultat = tip == TipFisier.Intrari
                ? ProcesorIntrari.Proceseaza(document, grupe, registru, jurnal)
                : ProcesorIesiri.Proceseaza(document, grupe, registru, jurnal);

            string? caleXmlFinal = null, caleRaportFinal = null, caleDbfFinal = null;

            if (rezultat.AreOutput)
            {
                var (dataMin, dataMax) = SurseFisiere.IntervalDate(rezultat.DocumentRezultat!);
                var numeXml = SurseFisiere.NumeFisierIesireXml(dataMin, dataMax, tip);
                caleXmlFinal = SurseFisiere.CaleDestinatieCuSufixIncrementat(config.FolderDestinatie, numeXml, jurnal);
                ScriitorXml.Salveaza(rezultat.DocumentRezultat!, caleXmlFinal);

                if (config.GenereazaRaportXlsx)
                {
                    var numeRaport = SurseFisiere.NumeFisierRaport(Path.GetFileName(caleXmlFinal));
                    caleRaportFinal = SurseFisiere.CaleDestinatieCuSufixIncrementat(config.FolderDestinatie, numeRaport, jurnal);
                    ScriitorXlsx.Scrie(caleRaportFinal, tip, rezultat.RanduriRaport, jurnal);
                }

                if (tip == TipFisier.Intrari)
                {
                    var numeDbf = SurseFisiere.NumeFisierDbf(dataMin, dataMax);
                    caleDbfFinal = SurseFisiere.CaleDestinatieCuSufixIncrementat(config.FolderDestinatie, numeDbf, jurnal);
                    ScriitorDbf.Scrie(caleDbfFinal, rezultat.InregistrariDbf!, jurnal);
                }

                var comentariu = Registru.FormateazaComentariuLot(DateTime.Now, numeFisier, rezultat.CheiRegistruNoi.Count);
                registru.AdaugaLot(rezultat.CheiRegistruNoi, comentariu);
                registru.Salveaza(jurnal);
            }

            var caleArhiva = SurseFisiere.Arhiveaza(caleFisierLucru, config.FolderArhiva, jurnal);

            return new RezultatFisier
            {
                NumeFisierSursa = numeFisier,
                Succes = true,
                AreOutput = rezultat.AreOutput,
                CaleXmlGenerat = caleXmlFinal,
                CaleRaportGenerat = caleRaportFinal,
                CaleDbfGenerat = caleDbfFinal,
                CaleArhiva = caleArhiva,
            };
        }
        catch (Exception ex)
        {
            jurnal.Eroare(CategorieJurnal.General,
                $"Procesarea fișierului {numeFisier} a eșuat: {ex.Message}. Fișierul rămâne nefinalizat în folderul de lucru.");
            return new RezultatFisier { NumeFisierSursa = numeFisier, Succes = false, MesajEroare = ex.Message };
        }
    }
}
