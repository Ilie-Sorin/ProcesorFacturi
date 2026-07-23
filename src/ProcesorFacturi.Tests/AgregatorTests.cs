using System.Xml.Linq;
using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class AgregatorTests
{
    private static XElement LinieDrMax(string nrCrt, string activitate, string cantitate, string pret,
        string valoare, string tva, string pretVanzare, string cont = "371.00002", string proctva = "21.00")
        => new(
            "Linie",
            new XElement("LinieNrCrt", nrCrt),
            new XElement("Gestiune", ""),
            new XElement("Activitate", activitate),
            new XElement("CodArticolFurnizor", "Intrare marfa 21%"),
            new XElement("CodArticolClient", ""),
            new XElement("GUID_cod_articol", "Nedefinit"),
            new XElement("UM", "BUC"),
            new XElement("Cantitate", cantitate),
            new XElement("Pret", pret),
            new XElement("Valoare", valoare),
            new XElement("ProcTVA", proctva),
            new XElement("TVA", tva),
            new XElement("Cont", cont),
            new XElement("PretVanzare", pretVanzare)
        );

    private static XElement Factura(string numar, params XElement[] linii)
        => new(
            "Factura",
            new XElement("Antet", new XElement("FacturaNumar", numar)),
            new XElement("Detalii", new XElement("Continut", linii))
        );

    private static List<XElement> Linii(XElement factura)
        => factura.Element("Detalii")!.Element("Continut")!.Elements("Linie").ToList();

    [Fact]
    public void ExemplulDrMax_ConsolideazaDouaLiniiInUna()
    {
        // §8.4 — factura DR.MAX 1611280219 / 21.07.2026
        var factura = Factura("1611280219",
            LinieDrMax("1", "02", "-1", "0.01", "-0.01", "-0.010000", "0.00"),
            LinieDrMax("2", "02", "1", "85.41", "85.41", "17.940000", "162.95"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var linii = Linii(factura);
        Assert.Single(linii);

        var rezultat = linii[0];
        Assert.Equal("1", rezultat.Element("LinieNrCrt")!.Value);
        Assert.Equal("1", rezultat.Element("Cantitate")!.Value);
        Assert.Equal("85.40", rezultat.Element("Pret")!.Value);
        Assert.Equal("85.40", rezultat.Element("Valoare")!.Value);
        Assert.Equal("17.930000", rezultat.Element("TVA")!.Value);
        Assert.Equal("162.95", rezultat.Element("PretVanzare")!.Value);
    }

    [Fact]
    public void GrupCuOSingurLinie_TotAplicaCantitatePmUnu()
    {
        var factura = Factura("100", LinieDrMax("1", "01", "1", "50.00", "50.00", "10.500000", "60.00"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var rezultat = Linii(factura).Single();
        Assert.Equal("1", rezultat.Element("Cantitate")!.Value);
        Assert.Equal("50.00", rezultat.Element("Pret")!.Value);
    }

    [Fact]
    public void ValoareNegativa_StornoCompletDaCantitateMinusUnu()
    {
        var factura = Factura("200", LinieDrMax("1", "01", "-1", "855.10", "-855.10", "-94.060000", "1104.95"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var rezultat = Linii(factura).Single();
        Assert.Equal("-1", rezultat.Element("Cantitate")!.Value);
        Assert.Equal("855.10", rezultat.Element("Pret")!.Value);
        Assert.Equal("1104.95", rezultat.Element("PretVanzare")!.Value);
    }

    [Fact]
    public void NicioLiniePretVanzareNenul_ScrieZeroSiAvertizeaza()
    {
        var factura = Factura("300",
            LinieDrMax("1", "01", "-1", "0.01", "-0.01", "-0.01", "0.00"),
            LinieDrMax("2", "01", "1", "10.00", "10.00", "2.10", "0.00"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var rezultat = Linii(factura).Single();
        Assert.Equal("0.00", rezultat.Element("PretVanzare")!.Value);
        Assert.Contains(jurnal.Intrari, i => i.Nivel == NivelJurnal.Avertizare && i.Categorie == CategorieJurnal.Agregare
            && i.Mesaj.Contains("Nicio linie"));
    }

    [Fact]
    public void MaiMultePretVanzareDiferite_PreiaPrimaSiAvertizeaza()
    {
        var factura = Factura("400",
            LinieDrMax("1", "01", "1", "5.00", "5.00", "1.05", "20.00"),
            LinieDrMax("2", "01", "1", "5.00", "5.00", "1.05", "25.00"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var rezultat = Linii(factura).Single();
        Assert.Equal("20.00", rezultat.Element("PretVanzare")!.Value);
        Assert.Contains(jurnal.Intrari, i => i.Nivel == NivelJurnal.Avertizare
            && i.Mesaj.Contains("valori diferite"));
    }

    [Fact]
    public void ActivitateDiferitaInGrup_PreiaPrimaSiAvertizeaza()
    {
        var factura = Factura("500",
            LinieDrMax("1", "01", "1", "5.00", "5.00", "1.05", "10.00"),
            LinieDrMax("2", "02", "1", "5.00", "5.00", "1.05", "10.00"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var rezultat = Linii(factura).Single();
        Assert.Equal("01", rezultat.Element("Activitate")!.Value);
        Assert.Contains(jurnal.Intrari, i => i.Nivel == NivelJurnal.Avertizare
            && i.Mesaj.Contains("Activitate"));
    }

    [Fact]
    public void ChoieDiferitaDeCont_NuSeAgregaImpreuna()
    {
        var factura = Factura("600",
            LinieDrMax("1", "01", "1", "5.00", "5.00", "1.05", "10.00", cont: "371.00001"),
            LinieDrMax("2", "01", "1", "5.00", "5.00", "1.05", "10.00", cont: "371.00002"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        Assert.Equal(2, Linii(factura).Count);
    }

    [Fact]
    public void ChoieDiferitaDeProcTva_NuSeAgregaImpreuna()
    {
        var factura = Factura("700",
            LinieDrMax("1", "01", "1", "5.00", "5.00", "0.55", "10.00", proctva: "11.00"),
            LinieDrMax("2", "01", "1", "5.00", "5.00", "1.05", "10.00", proctva: "21.00"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        Assert.Equal(2, Linii(factura).Count);
    }

    [Fact]
    public void TreiLiniiInAcelasiGrup_SumeazaToateSiRenumeroteaza()
    {
        var factura = Factura("800",
            LinieDrMax("1", "01", "1", "10.00", "10.00", "2.10", "0.00"),
            LinieDrMax("2", "01", "1", "20.00", "20.00", "4.20", "0.00"),
            LinieDrMax("3", "01", "1", "5.00", "5.00", "1.05", "50.00"));

        var jurnal = new Jurnal();
        Agregator.AgregaLiniiFactura(factura, jurnal);

        var rezultat = Linii(factura).Single();
        Assert.Equal("35.00", rezultat.Element("Valoare")!.Value);
        Assert.Equal("7.35", rezultat.Element("TVA")!.Value);
        Assert.Equal("50.00", rezultat.Element("PretVanzare")!.Value);
    }
}
