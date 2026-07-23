using System.Globalization;
using System.Xml.Linq;

namespace ProcesorFacturi.Core;

/// <summary>Utilitare partajate pentru citirea/scrierea tag-urilor &lt;Linie&gt;/&lt;Antet&gt; și conversii numerice.</summary>
public static class XmlUtils
{
    public static string Text(XElement element, string tag)
        => element.Element(tag)?.Value ?? "";

    public static string TextTrim(XElement element, string tag)
        => Text(element, tag).Trim();

    public static void SetTagText(XElement element, string tag, string valoare)
    {
        var copil = element.Element(tag);
        if (copil is null)
            element.Add(new XElement(tag, valoare));
        else
            copil.Value = valoare;
    }

    public static decimal ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        return decimal.Parse(text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    /// <summary>Numărul de zecimale folosite în textul sursă al unui tag (⚠️ §0.2 pct. 1 — se păstrează per tag).</summary>
    public static int NumarZecimale(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var idx = text.IndexOf('.');
        return idx < 0 ? 0 : text.Length - idx - 1;
    }

    public static string FormatDecimal(decimal valoare, int zecimale)
        => valoare.ToString("F" + zecimale, CultureInfo.InvariantCulture);
}
