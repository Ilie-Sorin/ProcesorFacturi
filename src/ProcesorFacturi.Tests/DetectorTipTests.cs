using System.Xml.Linq;
using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class DetectorTipTests
{
    private static XDocument Document(params (string cif,string)[] facturi)
    {
        var root = new XElement("Facturi",
            facturi.Select(f => new XElement("Factura",
                new XElement("Antet", new XElement("FurnizorCIF", f.cif)))));
        return new XDocument(root);
    }

    [Fact]
    public void CifDiferitDeAncafarm_EsteIntrari()
    {
        var doc = Document(("9378655", ""));
        Assert.Equal(TipFisier.Intrari, DetectorTip.Detecteaza(doc));
    }

    [Fact]
    public void CifAncafarm_EsteIesiri()
    {
        var doc = Document(("13150581", ""));
        Assert.Equal(TipFisier.Iesiri, DetectorTip.Detecteaza(doc));
    }

    [Fact]
    public void TipuriAmestecate_ArunacaProcesareException()
    {
        var doc = Document(("13150581", ""), ("9378655", ""));
        Assert.Throws<ProcesareException>(() => DetectorTip.Detecteaza(doc));
    }

    [Fact]
    public void CifLipsa_ArunacaProcesareException()
    {
        var doc = Document(("", ""));
        Assert.Throws<ProcesareException>(() => DetectorTip.Detecteaza(doc));
    }

    [Fact]
    public void FaraFacturi_ArunacaFisierFaraFacturiException()
    {
        var doc = new XDocument(new XElement("Facturi"));
        Assert.Throws<FisierFaraFacturiException>(() => DetectorTip.Detecteaza(doc));
    }
}
