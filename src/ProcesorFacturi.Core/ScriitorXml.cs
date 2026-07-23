using System.Xml.Linq;

namespace ProcesorFacturi.Core;

/// <summary>
/// Încărcare/salvare XML păstrând spațierea, declarația și encoding-ul sursei (§10.1, §14.7).
/// </summary>
public static class ScriitorXml
{
    public static XDocument Incarca(string calePath)
        => XDocument.Load(calePath, LoadOptions.PreserveWhitespace);

    public static void Salveaza(XDocument document, string calePath)
    {
        var director = Path.GetDirectoryName(calePath);
        if (!string.IsNullOrEmpty(director)) Directory.CreateDirectory(director);

        // XDocument.Save reia encoding-ul din document.Declaration (populată la Load din sursă);
        // DisableFormatting păstrează spațierea originală (încărcată cu PreserveWhitespace).
        document.Save(calePath, SaveOptions.DisableFormatting);
    }
}
