using System.Text;

namespace ProcesorFacturi.Core;

/// <summary>
/// InAnte.txt / IeAnte.txt: citire, deduplicare, adăugare, backup, scriere atomică,
/// anulare lot (§4.1–§4.4). Tratează valorile ca șiruri opace — construirea cheii compuse
/// pentru IeAnte (§4.2) e responsabilitatea apelantului.
/// </summary>
public sealed class Registru
{
    private readonly string _cale;
    private readonly List<string> _valori = new();
    private readonly HashSet<string> _valoriNormalizate = new(StringComparer.OrdinalIgnoreCase);

    private int? _pozitieUltimulLot;
    private string? _comentariuUltimulLot;

    public IReadOnlyList<string> Valori => _valori;
    public int NumarTotal => _valori.Count;

    private Registru(string cale) => _cale = cale;

    public static Registru Deschide(string cale, Jurnal jurnal)
    {
        var registru = new Registru(cale);

        if (!File.Exists(cale))
        {
            jurnal.Avertizare(CategorieJurnal.Registru,
                $"Registrul {Path.GetFileName(cale)} nu există — se creează gol.");
            return registru;
        }

        var linii = File.ReadAllLines(cale, Encoding.UTF8);
        var eliminateDuplicate = 0;

        foreach (var linieRaw in linii)
        {
            var linie = linieRaw.Trim();
            if (linie.Length == 0) continue;
            if (linie.StartsWith("#", StringComparison.Ordinal)) continue;

            var normalizata = Normalizeaza(linie);
            if (!registru._valoriNormalizate.Add(normalizata))
            {
                eliminateDuplicate++;
                continue;
            }
            registru._valori.Add(linie);
        }

        if (eliminateDuplicate > 0)
        {
            jurnal.Avertizare(CategorieJurnal.Registru,
                $"{eliminateDuplicate} duplicate eliminate din {Path.GetFileName(cale)} (păstrată prima apariție).");
        }

        return registru;
    }

    public static string Normalizeaza(string valoare) => valoare.Trim();

    public bool Contine(string valoare) => _valoriNormalizate.Contains(Normalizeaza(valoare));

    public static string FormateazaComentariuLot(DateTime cand, string numeFisierSursa, int numarFacturi)
        => $"# lot {cand:yyyy-MM-dd HH:mm} — {numeFisierSursa} ({numarFacturi} facturi)";

    /// <summary>Adaugă valorile unui lot nou (§4.3 pct. 4), ignorând valorile deja prezente.</summary>
    public void AdaugaLot(IEnumerable<string> valoriNoi, string comentariuLot)
    {
        _pozitieUltimulLot = _valori.Count;
        _comentariuUltimulLot = comentariuLot;

        foreach (var valoareRaw in valoriNoi)
        {
            var valoare = valoareRaw.Trim();
            if (valoare.Length == 0) continue;

            var normalizata = Normalizeaza(valoare);
            if (!_valoriNormalizate.Add(normalizata)) continue;
            _valori.Add(valoare);
        }
    }

    /// <summary>Backup + scriere atomică (fișier temporar + înlocuire), UTF-8, CRLF (§4.3 pct. 3/5/6).</summary>
    public void Salveaza(Jurnal jurnal)
    {
        try
        {
            if (File.Exists(_cale))
                File.Copy(_cale, _cale + ".bak", overwrite: true);

            var caleTemp = _cale + ".tmp";
            using (var writer = new StreamWriter(caleTemp, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.NewLine = "\r\n";
                var idx = 0;
                foreach (var valoare in _valori)
                {
                    if (_pozitieUltimulLot.HasValue && idx == _pozitieUltimulLot.Value && _comentariuUltimulLot is not null)
                        writer.WriteLine(_comentariuUltimulLot);

                    writer.WriteLine(valoare);
                    idx++;
                }

                if (_pozitieUltimulLot.HasValue && _pozitieUltimulLot.Value == _valori.Count && _comentariuUltimulLot is not null)
                    writer.WriteLine(_comentariuUltimulLot);
            }

            if (File.Exists(_cale)) File.Delete(_cale);
            File.Move(caleTemp, _cale);
        }
        catch (Exception ex)
        {
            RestaureazaDinBackup(jurnal);
            throw new ProcesareException(
                $"Eroare la scrierea registrului {Path.GetFileName(_cale)}: {ex.Message}. " +
                "Fișierele generate rămân, dar registrul nu a fost actualizat.", ex);
        }
    }

    private void RestaureazaDinBackup(Jurnal jurnal)
    {
        var caleBackup = _cale + ".bak";
        if (!File.Exists(caleBackup)) return;

        File.Copy(caleBackup, _cale, overwrite: true);
        jurnal.Avertizare(CategorieJurnal.Registru,
            $"Registrul {Path.GetFileName(_cale)} a fost restaurat din backup după o eroare de scriere.");
    }

    /// <summary>Elimină liniile adăugate la ultima procesare, identificate prin linia-comentariu de lot (§4.4).</summary>
    public void AnuleazaUltimulLot(Jurnal jurnal)
    {
        if (!File.Exists(_cale))
        {
            jurnal.Avertizare(CategorieJurnal.Registru, $"Nu există registrul {Path.GetFileName(_cale)} de anulat.");
            return;
        }

        var liniiOriginale = File.ReadAllLines(_cale, Encoding.UTF8).ToList();
        var ultimaPozitieLot = -1;
        for (var i = liniiOriginale.Count - 1; i >= 0; i--)
        {
            if (liniiOriginale[i].TrimStart().StartsWith("# lot", StringComparison.OrdinalIgnoreCase))
            {
                ultimaPozitieLot = i;
                break;
            }
        }

        if (ultimaPozitieLot < 0)
        {
            jurnal.Avertizare(CategorieJurnal.Registru,
                $"Niciun lot de anulat în {Path.GetFileName(_cale)} (nicio linie „# lot” găsită).");
            return;
        }

        var pastrate = liniiOriginale.Take(ultimaPozitieLot).ToList();

        File.Copy(_cale, _cale + ".bak", overwrite: true);
        var caleTemp = _cale + ".tmp";
        using (var writer = new StreamWriter(caleTemp, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.NewLine = "\r\n";
            foreach (var linie in pastrate) writer.WriteLine(linie);
        }
        if (File.Exists(_cale)) File.Delete(_cale);
        File.Move(caleTemp, _cale);

        _valori.Clear();
        _valoriNormalizate.Clear();
        foreach (var linie in pastrate)
        {
            var curata = linie.Trim();
            if (curata.Length == 0 || curata.StartsWith("#", StringComparison.Ordinal)) continue;
            if (_valoriNormalizate.Add(Normalizeaza(curata))) _valori.Add(curata);
        }
        _pozitieUltimulLot = null;
        _comentariuUltimulLot = null;

        jurnal.Info(CategorieJurnal.Registru,
            $"Ultimul lot a fost anulat din {Path.GetFileName(_cale)} " +
            $"({liniiOriginale.Count - pastrate.Count} linii eliminate).");
    }
}
