using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class ScriitorDbfTests : IDisposable
{
    private readonly string _folderTemp;

    public ScriitorDbfTests()
    {
        _folderTemp = Path.Combine(Path.GetTempPath(), "ProcesorFacturiDbfTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_folderTemp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderTemp))
            Directory.Delete(_folderTemp, recursive: true);
    }

    /// <summary>
    /// Cel mai valoros test din proiect (§14.6): regenerarea din date echivalente celor din
    /// fișierul de referință IN_22072026_22072026_AF.DBF trebuie să fie identică octet cu octet,
    /// cu excepția celor 3 octeți de dată din antet (versiunea, numărul de înregistrări, lungimile
    /// și toți cei 20 de descriptori de câmp trebuie să fie identici).
    /// </summary>
    [Fact]
    public void Scrie_ReproduceFisierulDeReferinta_OctetCuOctet()
    {
        var caleReferinta = @"D:\nftosaga\IN_22072026_22072026_AF.DBF";
        if (!File.Exists(caleReferinta))
            return; // sări testul dacă fișierul de referință nu e prezent în acest mediu

        var inregistrari = new List<InregistrareDbf>
        {
            new()
            {
                NrNir = "5066",
                NrIntrare = "1028284317",
                Cod = "01853",
                Data = new DateTime(2026, 7, 22),
                Scadent = new DateTime(2027, 4, 8),
                Tvai = 0,
                DenArt = "Medicamente cu TVA 11%",
                Um = "BUC",
                Cantitate = -1.000m,
                TvaArt = 11,
                Valoare = -1204.72m,
                Tva = -132.52m,
                Cont = "371.00006",
                PretVanz = 1604.69m,
                Grupa = "06",
            },
            new()
            {
                NrNir = "5058",
                NrIntrare = "202608744",
                Cod = "01076",
                Data = new DateTime(2026, 7, 22),
                Scadent = new DateTime(2027, 5, 2),
                Tvai = 0,
                DenArt = "Medicamente cu TVA 11%",
                Um = "BUC",
                Cantitate = 1.000m,
                TvaArt = 11,
                Valoare = 682.20m,
                Tva = 75.04m,
                Cont = "371.00006",
                PretVanz = 908.60m,
                Grupa = "06",
            },
        };

        var caleGenerata = Path.Combine(_folderTemp, "IN_test_AF.dbf");
        var jurnal = new Jurnal();
        ScriitorDbf.Scrie(caleGenerata, inregistrari, jurnal);

        var referinta = File.ReadAllBytes(caleReferinta);
        var generat = File.ReadAllBytes(caleGenerata);

        Assert.Equal(referinta.Length, generat.Length);

        for (var i = 0; i < referinta.Length; i++)
        {
            if (i is 1 or 2 or 3) continue; // octeții de dată din antet (an/lună/zi ultimei modificări)
            Assert.True(referinta[i] == generat[i],
                $"Diferență la offset {i}: referință=0x{referinta[i]:X2}, generat=0x{generat[i]:X2}");
        }
    }

    [Fact]
    public void Scrie_HeaderSiDimensiuniInregistrare_RespectaStructuraDocumentata()
    {
        var caleGenerata = Path.Combine(_folderTemp, "gol_AF.dbf");
        var jurnal = new Jurnal();
        ScriitorDbf.Scrie(caleGenerata, new List<InregistrareDbf>(), jurnal);

        var octeti = File.ReadAllBytes(caleGenerata);
        Assert.Equal(0x03, octeti[0]);
        Assert.Equal((ushort)ScriitorDbf.LungimeHeader, BitConverter.ToUInt16(octeti, 8));
        Assert.Equal((ushort)ScriitorDbf.LungimeInregistrare, BitConverter.ToUInt16(octeti, 10));
        Assert.Equal(0x0D, octeti[32 + 20 * 32]);
        Assert.Equal(0x1A, octeti[octeti.Length - 1]);
        Assert.Equal(ScriitorDbf.LungimeHeader + 1, octeti.Length); // 0 înregistrări + terminator EOF
    }

    [Fact]
    public void Scrie_TextMaiLungDecatCampul_TrunchiazaSiAvertizeaza()
    {
        var inregistrare = new InregistrareDbf
        {
            DenArt = new string('X', 80), // depășește 60 caractere
            Cont = "371.00001",
        };

        var caleGenerata = Path.Combine(_folderTemp, "trunchiat_AF.dbf");
        var jurnal = new Jurnal();
        ScriitorDbf.Scrie(caleGenerata, new List<InregistrareDbf> { inregistrare }, jurnal);

        Assert.Contains(jurnal.Intrari, i => i.Nivel == NivelJurnal.Avertizare && i.Mesaj.Contains("DEN_ART"));
    }

    [Fact]
    public void Scrie_DiacriticeSuntTransliterate()
    {
        // câmpurile C se scriu ASCII: dacă transliterarea nu ar elimina diacriticele,
        // Encoding.ASCII le-ar înlocui tăcut cu '?' (0x3F) — absența lui '?' din zona de
        // înregistrări confirmă indirect că transliterarea a rulat (testul dedicat pentru
        // conținutul exact e în TransliterareTests).
        var inregistrare = new InregistrareDbf { DenArt = "Mogoşoaia ăâîșț ĂÂÎȘȚ", Cont = "371.00001" };

        var caleGenerata = Path.Combine(_folderTemp, "diacritice_AF.dbf");
        var jurnal = new Jurnal();
        ScriitorDbf.Scrie(caleGenerata, new List<InregistrareDbf> { inregistrare }, jurnal);

        var octeti = File.ReadAllBytes(caleGenerata);
        var zonaInregistrari = octeti.Skip(ScriitorDbf.LungimeHeader).ToArray();
        Assert.DoesNotContain((byte)'?', zonaInregistrari);
        Assert.True(zonaInregistrari.All(b => b < 128));
    }

    [Fact]
    public void ConvertesteTvaArt_ProcTvaCuZecimaleNenule_AruncaEroare()
    {
        Assert.Throws<FacturaEroareException>(() => ScriitorDbf.ConvertesteTvaArt("11.50", "999"));
    }

    [Fact]
    public void ConvertesteTvaArt_ProcTvaIntreg_Converteste()
    {
        Assert.Equal(21, ScriitorDbf.ConvertesteTvaArt("21.00", "999"));
    }
}
