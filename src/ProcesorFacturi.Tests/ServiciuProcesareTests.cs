using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class ServiciuProcesareTests : IDisposable
{
    private readonly string _folderTemp;
    private const string CaleXmlIntrari = @"D:\nftosaga\20260722\F_13150581_22072026_225954.xml";
    private static readonly string CaleGrupe = @"D:\nftosaga2026\Grupe.xlsx";

    public ServiciuProcesareTests()
    {
        _folderTemp = Path.Combine(Path.GetTempPath(), "ProcesorFacturiServiciu_" + Guid.NewGuid());
        Directory.CreateDirectory(_folderTemp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderTemp))
            Directory.Delete(_folderTemp, recursive: true);
    }

    private ConfigApp CreeazaConfig()
    {
        var folderLucru = Path.Combine(_folderTemp, "Lucru");
        var folderDestinatie = Path.Combine(_folderTemp, "Destinatie");
        var folderArhiva = Path.Combine(_folderTemp, "Arhiva");
        var folderRegistre = Path.Combine(_folderTemp, "Registre");
        Directory.CreateDirectory(folderLucru);
        Directory.CreateDirectory(folderRegistre);

        return new ConfigApp
        {
            FolderLucru = folderLucru,
            FolderDestinatie = folderDestinatie,
            FolderArhiva = folderArhiva,
            FolderRegistre = folderRegistre,
            GenereazaRaportXlsx = true,
        };
    }

    [Fact]
    public void ProceseazaFisier_GenereazaFisiereleInDestinatieSiArhiveaza()
    {
        if (!File.Exists(CaleXmlIntrari) || !File.Exists(CaleGrupe)) return;

        var config = CreeazaConfig();
        var caleLucru = Path.Combine(config.FolderLucru, Path.GetFileName(CaleXmlIntrari));
        File.Copy(CaleXmlIntrari, caleLucru);

        var grupe = MapareGrupe.Incarca(CaleGrupe);
        var jurnal = new Jurnal();

        var rezultat = ServiciuProcesare.ProceseazaFisier(caleLucru, config, grupe, jurnal);

        Assert.True(rezultat.Succes);
        Assert.True(rezultat.AreOutput);
        Assert.NotNull(rezultat.CaleXmlGenerat);
        Assert.True(File.Exists(rezultat.CaleXmlGenerat));
        Assert.NotNull(rezultat.CaleRaportGenerat);
        Assert.True(File.Exists(rezultat.CaleRaportGenerat));
        Assert.NotNull(rezultat.CaleDbfGenerat);
        Assert.True(File.Exists(rezultat.CaleDbfGenerat));
        Assert.NotNull(rezultat.CaleArhiva);
        Assert.True(File.Exists(rezultat.CaleArhiva));

        Assert.False(File.Exists(caleLucru)); // mutat in arhiva
        Assert.True(File.Exists(config.CaleInAnte));
    }

    [Fact]
    public void ProceseazaFisier_ADouaOaraCuAcelasiSursa_NuGenereazaOutputSiArhiveazaTotusi()
    {
        if (!File.Exists(CaleXmlIntrari) || !File.Exists(CaleGrupe)) return;

        var config = CreeazaConfig();
        var grupe = MapareGrupe.Incarca(CaleGrupe);

        var caleLucru1 = Path.Combine(config.FolderLucru, "prima.xml");
        File.Copy(CaleXmlIntrari, caleLucru1);
        var jurnal1 = new Jurnal();
        ServiciuProcesare.ProceseazaFisier(caleLucru1, config, grupe, jurnal1);

        var caleLucru2 = Path.Combine(config.FolderLucru, "adoua.xml");
        File.Copy(CaleXmlIntrari, caleLucru2);
        var jurnal2 = new Jurnal();
        var rezultat2 = ServiciuProcesare.ProceseazaFisier(caleLucru2, config, grupe, jurnal2);

        Assert.True(rezultat2.Succes);
        Assert.False(File.Exists(caleLucru2)); // arhivat, chiar daca fara output
        Assert.True(File.Exists(rezultat2.CaleArhiva));
    }
}
