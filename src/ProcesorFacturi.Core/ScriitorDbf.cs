using System.Globalization;
using System.Text;

namespace ProcesorFacturi.Core;

/// <summary>
/// Scriitor DBF (dBase III), cod propriu cu <see cref="BinaryWriter"/>, fără driver ODBC/OLE DB
/// (§14.3). Structura e cea documentată integral în §10.3, verificată octet-cu-octet împotriva
/// fișierului de referință IN_22072026_22072026_AF.DBF.
/// </summary>
public static class ScriitorDbf
{
    private readonly record struct CampDbf(string Nume, char Tip, byte Lungime, byte Zecimale);

    private static readonly CampDbf[] Campuri =
    {
        new("NR_NIR", 'C', 16, 0),
        new("NR_INTRARE", 'C', 16, 0),
        new("GESTIUNE", 'C', 4, 0),
        new("DEN_GEST", 'C', 36, 0),
        new("COD", 'C', 5, 0),
        new("DATA", 'D', 8, 0),
        new("SCADENT", 'D', 8, 0),
        new("TIP", 'C', 1, 0),
        new("TVAI", 'N', 1, 0),
        new("COD_ART", 'C', 16, 0),
        new("DEN_ART", 'C', 60, 0),
        new("UM", 'C', 5, 0),
        new("CANTITATE", 'N', 14, 3),
        new("DEN_TIP", 'C', 36, 0),
        new("TVA_ART", 'N', 2, 0),
        new("VALOARE", 'N', 15, 2),
        new("TVA", 'N', 15, 2),
        new("CONT", 'C', 20, 0),
        new("PRET_VANZ", 'N', 15, 2),
        new("GRUPA", 'C', 16, 0),
    };

    public const int LungimeInregistrare = 310; // 1 (marcaj ștergere) + 309 (date) — confirmat pe fișierul de referință
    public const int LungimeHeader = 673;        // 32 + 20*32 + 1

    /// <summary>Convertește ProcTVA (text sursă) în TVA_ART (întreg pe 2 poziții); eroare dacă are zecimale nenule (§10.3.1).</summary>
    public static int ConvertesteTvaArt(string procTvaText, string numarFactura)
    {
        var valoare = XmlUtils.ParseDecimal(procTvaText);
        var rotunjit = Math.Round(valoare, 0, MidpointRounding.AwayFromZero);
        if (valoare != rotunjit)
        {
            throw new FacturaEroareException(numarFactura,
                $"<ProcTVA> = {procTvaText} are zecimale nenule — nu încape în TVA_ART (întreg pe 2 poziții).");
        }
        return (int)rotunjit;
    }

    public static void Scrie(string calePath, IReadOnlyList<InregistrareDbf> inregistrari, Jurnal jurnal)
    {
        if (File.Exists(calePath)) File.Delete(calePath);

        var director = Path.GetDirectoryName(calePath);
        if (!string.IsNullOrEmpty(director)) Directory.CreateDirectory(director);

        using var flux = new FileStream(calePath, FileMode.CreateNew, FileAccess.Write);
        using var writer = new BinaryWriter(flux);

        ScrieHeader(writer, inregistrari.Count);
        ScrieDescriptoriCampuri(writer);
        writer.Write((byte)0x0D); // terminator antet

        foreach (var inregistrare in inregistrari)
            ScrieInregistrare(writer, inregistrare, jurnal);

        writer.Write((byte)0x1A); // terminator fișier
    }

    private static void ScrieHeader(BinaryWriter writer, int numarInregistrari)
    {
        var acum = DateTime.Now;
        writer.Write((byte)0x03);                     // versiune dBase III, fără memo
        writer.Write((byte)(acum.Year - 2000));
        writer.Write((byte)acum.Month);
        writer.Write((byte)acum.Day);
        writer.Write((uint)numarInregistrari);
        writer.Write((ushort)LungimeHeader);
        writer.Write((ushort)LungimeInregistrare);
        writer.Write(new byte[20]);                    // rezervat + flag-uri + language driver 0x00
    }

    private static void ScrieDescriptoriCampuri(BinaryWriter writer)
    {
        foreach (var camp in Campuri)
        {
            var numeBytes = new byte[11];
            var numeAscii = Encoding.ASCII.GetBytes(camp.Nume);
            Array.Copy(numeAscii, numeBytes, Math.Min(numeAscii.Length, 11));
            writer.Write(numeBytes);

            writer.Write((byte)camp.Tip);
            writer.Write(new byte[4]);                  // rezervat (deplasament în înregistrare)
            writer.Write(camp.Lungime);
            writer.Write(camp.Zecimale);
            writer.Write(new byte[14]);                 // rezervat
        }
    }

    private static void ScrieInregistrare(BinaryWriter writer, InregistrareDbf r, Jurnal jurnal)
    {
        var context = r.NrIntrare.Length > 0 ? r.NrIntrare : r.NrNir;

        writer.Write((byte)0x20); // marcaj ștergere: nu e ștearsă

        foreach (var camp in Campuri)
        {
            byte[] octeti = camp.Nume switch
            {
                "NR_NIR" => FormateazaC(r.NrNir, camp.Lungime, camp.Nume, context, jurnal),
                "NR_INTRARE" => FormateazaC(r.NrIntrare, camp.Lungime, camp.Nume, context, jurnal),
                "GESTIUNE" => FormateazaC(r.Gestiune, camp.Lungime, camp.Nume, context, jurnal),
                "DEN_GEST" => FormateazaC(r.DenGest, camp.Lungime, camp.Nume, context, jurnal),
                "COD" => FormateazaC(r.Cod, camp.Lungime, camp.Nume, context, jurnal),
                "DATA" => FormateazaD(r.Data),
                "SCADENT" => FormateazaD(r.Scadent),
                "TIP" => FormateazaC(r.Tip, camp.Lungime, camp.Nume, context, jurnal),
                "TVAI" => FormateazaN(r.Tvai, camp.Lungime, camp.Zecimale, camp.Nume, context),
                "COD_ART" => FormateazaC(r.CodArt, camp.Lungime, camp.Nume, context, jurnal),
                "DEN_ART" => FormateazaC(r.DenArt, camp.Lungime, camp.Nume, context, jurnal),
                "UM" => FormateazaC(r.Um, camp.Lungime, camp.Nume, context, jurnal),
                "CANTITATE" => FormateazaN(r.Cantitate, camp.Lungime, camp.Zecimale, camp.Nume, context),
                "DEN_TIP" => FormateazaC(r.DenTip, camp.Lungime, camp.Nume, context, jurnal),
                "TVA_ART" => FormateazaN(r.TvaArt, camp.Lungime, camp.Zecimale, camp.Nume, context),
                "VALOARE" => FormateazaN(r.Valoare, camp.Lungime, camp.Zecimale, camp.Nume, context),
                "TVA" => FormateazaN(r.Tva, camp.Lungime, camp.Zecimale, camp.Nume, context),
                "CONT" => FormateazaC(r.Cont, camp.Lungime, camp.Nume, context, jurnal),
                "PRET_VANZ" => FormateazaN(r.PretVanz, camp.Lungime, camp.Zecimale, camp.Nume, context),
                "GRUPA" => FormateazaC(r.Grupa, camp.Lungime, camp.Nume, context, jurnal),
                _ => throw new InvalidOperationException($"Câmp DBF necunoscut: {camp.Nume}")
            };
            writer.Write(octeti);
        }
    }

    private static byte[] FormateazaC(string valoare, int lungime, string numeCamp, string context, Jurnal jurnal)
    {
        var text = Transliterare.EliminaDiacritice(valoare ?? "");
        if (text.Length > lungime)
        {
            jurnal.Avertizare(CategorieJurnal.Dbf,
                $"Câmpul {numeCamp} („{text}”) depășește {lungime} caractere — trunchiat.", context);
            text = text.Substring(0, lungime);
        }
        var rezultat = new byte[lungime];
        var octetiText = Encoding.ASCII.GetBytes(text);
        Array.Copy(octetiText, rezultat, octetiText.Length);
        for (var i = octetiText.Length; i < lungime; i++) rezultat[i] = (byte)' ';
        return rezultat;
    }

    private static byte[] FormateazaN(decimal valoare, int lungime, int zecimale, string numeCamp, string context)
    {
        var text = valoare.ToString("F" + zecimale, CultureInfo.InvariantCulture);
        if (text.Length > lungime)
        {
            throw new FacturaEroareException(context,
                $"Valoarea câmpului {numeCamp} ({text}) nu încape în {lungime} poziții.");
        }
        var octetiText = Encoding.ASCII.GetBytes(text.PadLeft(lungime, ' '));
        return octetiText;
    }

    private static byte[] FormateazaD(DateTime? data)
    {
        if (data is null) return Encoding.ASCII.GetBytes(new string(' ', 8));
        return Encoding.ASCII.GetBytes(data.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
    }
}
