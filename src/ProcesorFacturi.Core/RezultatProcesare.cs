using System.Xml.Linq;

namespace ProcesorFacturi.Core;

/// <summary>Un rând pentru raportul XLSX (§10.2) — coloane diferite pentru Intrări/Ieșiri.</summary>
public sealed class RandRaport
{
    public Dictionary<string, string> Valori { get; } = new();
}

/// <summary>Rezultatul procesării unui fișier XML (Intrări sau Ieșiri).</summary>
public sealed class RezultatProcesare
{
    /// <summary>False dacă toate facturile erau deja importate/invalide — nu se generează niciun fișier (§12).</summary>
    public bool AreOutput { get; init; }

    public XDocument? DocumentRezultat { get; init; }
    public TipFisier Tip { get; init; }
    public List<RandRaport> RanduriRaport { get; init; } = new();

    /// <summary>Valorile de adăugat în registru (InAnte/IeAnte) DOAR dacă întreaga procesare reușește.</summary>
    public List<string> CheiRegistruNoi { get; init; } = new();

    /// <summary>Doar Intrări — o înregistrare per linie consolidată.</summary>
    public List<InregistrareDbf>? InregistrariDbf { get; init; }
}
