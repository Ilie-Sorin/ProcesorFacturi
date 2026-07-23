namespace ProcesorFacturi.Core;

/// <summary>Eroare care oprește procesarea întregului fișier (§12).</summary>
public class ProcesareException : Exception
{
    public ProcesareException(string message) : base(message) { }
    public ProcesareException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>XML-ul nu conține elemente &lt;Factura&gt; — avertizare, nu eroare (§12).</summary>
public sealed class FisierFaraFacturiException : ProcesareException
{
    public FisierFaraFacturiException(string message) : base(message) { }
}

/// <summary>Eroare specifică unei singure facturi — factura e exclusă din output, restul lotului continuă.</summary>
public sealed class FacturaEroareException : ProcesareException
{
    public string NumarFactura { get; }

    public FacturaEroareException(string numarFactura, string message) : base(message)
    {
        NumarFactura = numarFactura;
    }
}
