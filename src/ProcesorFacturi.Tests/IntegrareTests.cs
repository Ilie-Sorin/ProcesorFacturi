using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class IntegrareTests : IDisposable
{
    private readonly string _folderTemp;

    public IntegrareTests()
    {
        _folderTemp = Path.Combine(Path.GetTempPath(), "ProcesorFacturiIntegrare_" + Guid.NewGuid());
        Directory.CreateDirectory(_folderTemp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderTemp))
            Directory.Delete(_folderTemp, recursive: true);
    }

    private static readonly string CaleGrupe = @"D:\nftosaga2026\Grupe.xlsx";
    private const string CaleXmlIntrari = @"D:\nftosaga\20260722\F_13150581_22072026_225954.xml";
    private const string CaleXmlIesiri = @"D:\nftosaga\20260722\F_13150581_22072026_230119.xml";

    [Fact]
    public void FisierIntrariReal_SeProceseazaCapatLaCapatFaraExceptii()
    {
        if (!File.Exists(CaleXmlIntrari) || !File.Exists(CaleGrupe)) return;

        var document = ScriitorXml.Incarca(CaleXmlIntrari);
        Assert.Equal(TipFisier.Intrari, DetectorTip.Detecteaza(document));

        var grupe = MapareGrupe.Incarca(CaleGrupe);

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(Path.Combine(_folderTemp, "InAnte.txt"), jurnal);

        var rezultat = ProcesorIntrari.Proceseaza(document, grupe, registru, jurnal);

        Assert.True(rezultat.AreOutput);
        Assert.NotEmpty(rezultat.RanduriRaport);
        Assert.NotNull(rezultat.InregistrariDbf);
        Assert.NotEmpty(rezultat.InregistrariDbf!);
        Assert.False(jurnal.AreErori);

        // fiecare rand de raport trebuie sa aiba ClientNume inlocuit (Antet-ul original are ANCAFARM*)
        foreach (var factura in rezultat.DocumentRezultat!.Root!.Elements("Factura"))
        {
            var clientNume = factura.Element("Antet")?.Element("ClientNume")?.Value;
            Assert.Equal("ANCAFARM", clientNume);
        }

        var caleXmlIesire = Path.Combine(_folderTemp, "iesire_IN.xml");
        ScriitorXml.Salveaza(rezultat.DocumentRezultat!, caleXmlIesire);
        Assert.True(File.Exists(caleXmlIesire));

        var caleXlsx = Path.Combine(_folderTemp, "raport_IN.xlsx");
        ScriitorXlsx.Scrie(caleXlsx, TipFisier.Intrari, rezultat.RanduriRaport, jurnal);
        Assert.True(File.Exists(caleXlsx));

        var caleDbf = Path.Combine(_folderTemp, "IN_test_AF.dbf");
        ScriitorDbf.Scrie(caleDbf, rezultat.InregistrariDbf!, jurnal);
        Assert.True(File.Exists(caleDbf));

        registru.AdaugaLot(rezultat.CheiRegistruNoi, Registru.FormateazaComentariuLot(DateTime.Now, "test.xml", rezultat.CheiRegistruNoi.Count));
        registru.Salveaza(jurnal);
    }

    [Fact]
    public void FisierIesiriReal_SeProceseazaCapatLaCapatFaraExceptii()
    {
        if (!File.Exists(CaleXmlIesiri) || !File.Exists(CaleGrupe)) return;

        var document = ScriitorXml.Incarca(CaleXmlIesiri);
        Assert.Equal(TipFisier.Iesiri, DetectorTip.Detecteaza(document));

        var grupe = MapareGrupe.Incarca(CaleGrupe);

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(Path.Combine(_folderTemp, "IeAnte.txt"), jurnal);

        var rezultat = ProcesorIesiri.Proceseaza(document, grupe, registru, jurnal);

        Assert.True(rezultat.AreOutput);
        Assert.NotEmpty(rezultat.RanduriRaport);
        Assert.Null(rezultat.InregistrariDbf); // fără DBF la Ieșiri (§6.4)
        Assert.False(jurnal.AreErori);

        // FacturaNumar trebuie sa aiba deja prefixul BTANCAx
        foreach (var factura in rezultat.DocumentRezultat!.Root!.Elements("Factura"))
        {
            var numar = factura.Element("Antet")?.Element("FacturaNumar")?.Value ?? "";
            Assert.StartsWith("BTANCA", numar);
        }

        var caleXmlIesire = Path.Combine(_folderTemp, "iesire_IE.xml");
        ScriitorXml.Salveaza(rezultat.DocumentRezultat!, caleXmlIesire);
        Assert.True(File.Exists(caleXmlIesire));

        var caleXlsx = Path.Combine(_folderTemp, "raport_IE.xlsx");
        ScriitorXlsx.Scrie(caleXlsx, TipFisier.Iesiri, rezultat.RanduriRaport, jurnal);
        Assert.True(File.Exists(caleXlsx));

        registru.AdaugaLot(rezultat.CheiRegistruNoi, Registru.FormateazaComentariuLot(DateTime.Now, "test.xml", rezultat.CheiRegistruNoi.Count));
        registru.Salveaza(jurnal);
    }

    [Fact]
    public void RulareADoua_ExcludeToateFacturileDejaImportate()
    {
        if (!File.Exists(CaleXmlIntrari) || !File.Exists(CaleGrupe)) return;

        var grupe = MapareGrupe.Incarca(CaleGrupe);
        var caleRegistru = Path.Combine(_folderTemp, "InAnte.txt");

        // prima rulare: proceseaza normal si salveaza registrul
        var document1 = ScriitorXml.Incarca(CaleXmlIntrari);
        var jurnal1 = new Jurnal();
        var registru1 = Registru.Deschide(caleRegistru, jurnal1);
        var rezultat1 = ProcesorIntrari.Proceseaza(document1, grupe, registru1, jurnal1);
        registru1.AdaugaLot(rezultat1.CheiRegistruNoi, Registru.FormateazaComentariuLot(DateTime.Now, "test.xml", rezultat1.CheiRegistruNoi.Count));
        registru1.Salveaza(jurnal1);

        // a doua rulare pe ACELASI fisier sursa: toate facturile cu NR_NIR ar trebui excluse
        var document2 = ScriitorXml.Incarca(CaleXmlIntrari);
        var jurnal2 = new Jurnal();
        var registru2 = Registru.Deschide(caleRegistru, jurnal2);
        var rezultat2 = ProcesorIntrari.Proceseaza(document2, grupe, registru2, jurnal2);

        // fisierul poate contine facturi fara FacturaInformatiiSuplimentare (raman mereu), deci
        // verificam ca NR_NIR-urile deja adaugate nu se mai regasesc ca duplicate NOI de adaugat.
        Assert.Empty(rezultat2.CheiRegistruNoi.Intersect(rezultat1.CheiRegistruNoi));
    }
}
