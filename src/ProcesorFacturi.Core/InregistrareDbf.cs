namespace ProcesorFacturi.Core;

/// <summary>O înregistrare din DBF-ul de Intrări (§10.3) — o linie consolidată (post-agregare), nu o linie XML brută.</summary>
public sealed class InregistrareDbf
{
    public string NrNir { get; set; } = "";
    public string NrIntrare { get; set; } = "";
    public string Gestiune { get; set; } = "";
    public string DenGest { get; set; } = "";
    public string Cod { get; set; } = "";
    public DateTime? Data { get; set; }
    public DateTime? Scadent { get; set; }
    public string Tip { get; set; } = "";
    public int Tvai { get; set; }
    public string CodArt { get; set; } = "";
    public string DenArt { get; set; } = "";
    public string Um { get; set; } = "";
    public decimal Cantitate { get; set; }
    public string DenTip { get; set; } = "";
    public int TvaArt { get; set; }
    public decimal Valoare { get; set; }
    public decimal Tva { get; set; }
    public string Cont { get; set; } = "";
    public decimal PretVanz { get; set; }
    public string Grupa { get; set; } = "";
}
