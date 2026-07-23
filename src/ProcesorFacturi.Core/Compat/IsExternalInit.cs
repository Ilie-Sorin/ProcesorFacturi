namespace System.Runtime.CompilerServices;

/// <summary>
/// Shim necesar pentru „init” și „record” pe net48 — tipul e prezent doar din .NET 5+,
/// dar compilatorul (Roslyn din SDK-ul .NET 8) doar caută prezența numelui, indiferent de TFM.
/// </summary>
internal static class IsExternalInit { }
