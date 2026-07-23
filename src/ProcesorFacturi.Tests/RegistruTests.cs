using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class RegistruTests : IDisposable
{
    private readonly string _folderTemp;

    public RegistruTests()
    {
        _folderTemp = Path.Combine(Path.GetTempPath(), "ProcesorFacturiTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_folderTemp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderTemp))
            Directory.Delete(_folderTemp, recursive: true);
    }

    private string Cale(string nume) => Path.Combine(_folderTemp, nume);

    [Fact]
    public void Deschide_FisierInexistent_CreeazaGolSiAvertizeaza()
    {
        var jurnal = new Jurnal();
        var registru = Registru.Deschide(Cale("InAnte.txt"), jurnal);

        Assert.Equal(0, registru.NumarTotal);
        Assert.Contains(jurnal.Intrari, i => i.Nivel == NivelJurnal.Avertizare && i.Mesaj.Contains("nu există"));
    }

    [Fact]
    public void Deschide_CuDuplicateSiLiniiGoale_DeduplicaSiIgnoraGolurile()
    {
        var cale = Cale("InAnte.txt");
        File.WriteAllText(cale, "# comentariu vechi\r\n4057\r\n4058\r\n\r\n4057\r\n   \r\n4058\r\n4059\r\n");

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(cale, jurnal);

        Assert.Equal(3, registru.NumarTotal);
        Assert.True(registru.Contine("4057"));
        Assert.True(registru.Contine(" 4058 "));
        Assert.True(registru.Contine("4059"));
        Assert.Contains(jurnal.Intrari, i => i.Mesaj.Contains("2 duplicate eliminate"));
    }

    [Fact]
    public void Contine_EsteInsensibilLaMajusculeSiSpatii()
    {
        var cale = Cale("IeAnte.txt");
        File.WriteAllText(cale, "BTANCA2 1611280219|21.07.2026\r\n");

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(cale, jurnal);

        Assert.True(registru.Contine("btanca2 1611280219|21.07.2026"));
        Assert.True(registru.Contine("  BTANCA2 1611280219|21.07.2026  "));
    }

    [Fact]
    public void AdaugaLotSiSalveaza_ScrieComentariuDeLotSiValoriCuCrlf()
    {
        var cale = Cale("InAnte.txt");
        File.WriteAllText(cale, "4057\r\n4058\r\n");

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(cale, jurnal);

        var comentariu = Registru.FormateazaComentariuLot(
            new DateTime(2026, 7, 23, 14, 32, 0), "F_13150581_23072026_143012.xml", 2);
        registru.AdaugaLot(new[] { "42927", "42931" }, comentariu);
        registru.Salveaza(jurnal);

        var continut = File.ReadAllText(cale);
        Assert.Contains("# lot 2026-07-23 14:32 — F_13150581_23072026_143012.xml (2 facturi)\r\n42927\r\n42931\r\n", continut);
        Assert.True(File.Exists(cale + ".bak"));
        Assert.False(File.Exists(cale + ".tmp"));
    }

    [Fact]
    public void AdaugaLot_NuDuplicaValoriDejaExistente()
    {
        var cale = Cale("InAnte.txt");
        File.WriteAllText(cale, "4057\r\n");

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(cale, jurnal);
        registru.AdaugaLot(new[] { "4057", "4058" }, "# lot test");
        registru.Salveaza(jurnal);

        var registru2 = Registru.Deschide(cale, jurnal);
        Assert.Equal(2, registru2.NumarTotal);
    }

    [Fact]
    public void AnuleazaUltimulLot_EliminaLiniileDupaUltimulComentariuDeLot()
    {
        var cale = Cale("InAnte.txt");
        File.WriteAllText(cale, "4057\r\n4058\r\n# lot 2026-07-23 14:32 — test.xml (2 facturi)\r\n42927\r\n42931\r\n");

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(cale, jurnal);
        Assert.Equal(4, registru.NumarTotal);

        registru.AnuleazaUltimulLot(jurnal);

        var continutDupa = File.ReadAllText(cale);
        Assert.DoesNotContain("42927", continutDupa);
        Assert.DoesNotContain("42931", continutDupa);
        Assert.Contains("4057", continutDupa);
        Assert.Contains("4058", continutDupa);

        var registruDupa = Registru.Deschide(cale, jurnal);
        Assert.Equal(2, registruDupa.NumarTotal);
    }

    [Fact]
    public void AnuleazaUltimulLot_FaraLinieDeLot_AvertizeazaSiNuModifica()
    {
        var cale = Cale("InAnte.txt");
        File.WriteAllText(cale, "4057\r\n4058\r\n");

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(cale, jurnal);
        registru.AnuleazaUltimulLot(jurnal);

        Assert.Contains(jurnal.Intrari, i => i.Nivel == NivelJurnal.Avertizare && i.Mesaj.Contains("Niciun lot"));
        Assert.Equal("4057\r\n4058\r\n", File.ReadAllText(cale));
    }

    [Fact]
    public void FisierulRealDinProiect_SeIncarcaFaraExceptii()
    {
        var caleReala = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "InAnte.txt");
        caleReala = Path.GetFullPath(caleReala);
        if (!File.Exists(caleReala))
            return; // sări testul dacă fișierul real nu e prezent în acest mediu

        var caleCopie = Cale("InAnte_real.txt");
        File.Copy(caleReala, caleCopie);

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(caleCopie, jurnal);

        // Conținutul exact (inclusiv dacă are sau nu duplicate) e stare vie, mutabilă prin
        // uz normal al aplicației — testul verifică doar că fișierul real se poate încărca
        // fără excepții, nu un anumit conținut fixat la un moment dat.
        Assert.False(jurnal.AreErori);
    }
}
