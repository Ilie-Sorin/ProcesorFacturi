using System.Text;

namespace ProcesorFacturi.Core;

/// <summary>
/// Eliminarea diacriticelor înainte de scrierea în DBF (§10.3.1) — fișierul nu are code page
/// setat, deci orice octet peste 127 e ambiguu la citire.
/// </summary>
public static class Transliterare
{
    private static readonly Dictionary<char, char> Harta = new()
    {
        ['ă'] = 'a', ['â'] = 'a', ['î'] = 'i', ['ș'] = 's', ['ş'] = 's', ['ț'] = 't', ['ţ'] = 't',
        ['Ă'] = 'A', ['Â'] = 'A', ['Î'] = 'I', ['Ș'] = 'S', ['Ş'] = 'S', ['Ț'] = 'T', ['Ţ'] = 'T',
    };

    public static string EliminaDiacritice(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (Harta.TryGetValue(c, out var inlocuit))
            {
                sb.Append(inlocuit);
                continue;
            }

            if (c <= 127)
            {
                sb.Append(c);
                continue;
            }

            // Fallback pentru alte caractere accentuate neacoperite explicit (ex. nume străine):
            // descompunere Unicode (NFD) și păstrarea doar a literei de bază ASCII.
            var descompus = c.ToString().Normalize(NormalizationForm.FormD);
            foreach (var ch in descompus)
            {
                if (ch <= 127) sb.Append(ch);
            }
        }
        return sb.ToString();
    }
}
