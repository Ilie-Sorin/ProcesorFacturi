using System.ComponentModel;
using System.Runtime.CompilerServices;
using ProcesorFacturi.Core;

namespace ProcesorFacturi.App.ViewModels;

public sealed class FisierSursaVm : INotifyPropertyChanged
{
    private bool _selectat;

    public FisierSursaInfo Info { get; }

    public FisierSursaVm(FisierSursaInfo info) => Info = info;

    public bool Selectat
    {
        get => _selectat;
        set { _selectat = value; OnPropertyChanged(); }
    }

    public string NumeFisier => Info.NumeFisier;
    public string DataOra => Info.DataOraDinNume?.ToString("dd.MM.yyyy HH:mm:ss") ?? "necunoscută";
    public string Dimensiune => $"{Info.DimensiuneOcteti / 1024.0:N0} KB";
    public string NumarFacturi => Info.NumarFacturi.ToString();

    public string TipDetectat => Info.EroareDetectare is not null
        ? $"Eroare: {Info.EroareDetectare}"
        : Info.TipDetectat switch
        {
            TipFisier.Intrari => "Intrări",
            TipFisier.Iesiri => "Ieșiri",
            _ => "necunoscut"
        };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nume = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nume));
}
