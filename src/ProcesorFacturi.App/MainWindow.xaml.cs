using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using ProcesorFacturi.App.ViewModels;
using ProcesorFacturi.Core;

namespace ProcesorFacturi.App;

public partial class MainWindow : Window
{
    private readonly string _caleConfig;
    private ConfigApp _config;

    private readonly ObservableCollection<FisierSursaVm> _fisiereSursa = new();
    private readonly ObservableCollection<string> _fisiereLucru = new();
    private readonly ObservableCollection<IntrareJurnal> _jurnalEntries = new();

    private List<string> _inAnteToate = new();
    private List<string> _ieAnteToate = new();

    public MainWindow()
    {
        InitializeComponent();

        _caleConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
        _config = ConfigApp.Incarca(_caleConfig);

        TxtFolderSursa.Text = _config.FolderSursa;
        TxtFolderDestinatie.Text = _config.FolderDestinatie;
        ChkGenereazaXlsx.IsChecked = _config.GenereazaRaportXlsx;

        DgFisiereSursa.ItemsSource = _fisiereSursa;
        DgFisiereLucru.ItemsSource = _fisiereLucru;
        LstJurnal.ItemsSource = _jurnalEntries;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ReimprospateazaSursa();
        ReimprospateazaLucru();
        ReincarcaRegistre();
        ReincarcaGrupe();
    }

    // ===================== Foldere / opțiuni =====================

    private void BtnBrowseSursa_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = TxtFolderSursa.Text };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        TxtFolderSursa.Text = dlg.SelectedPath;
        _config.FolderSursa = dlg.SelectedPath;
        SalveazaConfig();
        ReimprospateazaSursa();
    }

    private void BtnBrowseDestinatie_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = TxtFolderDestinatie.Text };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        TxtFolderDestinatie.Text = dlg.SelectedPath;
        _config.FolderDestinatie = dlg.SelectedPath;
        SalveazaConfig();
    }

    private void ChkGenereazaXlsx_Changed(object sender, RoutedEventArgs e)
    {
        _config.GenereazaRaportXlsx = ChkGenereazaXlsx.IsChecked == true;
        SalveazaConfig();
    }

    private void SalveazaConfig()
    {
        try { _config.Salveaza(_caleConfig); }
        catch { /* configurarea e best-effort; nu blochează aplicația */ }
    }

    // ===================== Fișiere sursă (Tmp) =====================

    private void BtnReimprospateazaSursa_Click(object sender, RoutedEventArgs e) => ReimprospateazaSursa();

    private void ReimprospateazaSursa()
    {
        _config.FolderSursa = TxtFolderSursa.Text;
        try
        {
            var lista = SurseFisiere.Scaneaza(_config.FolderSursa);
            _fisiereSursa.Clear();
            foreach (var info in lista) _fisiereSursa.Add(new FisierSursaVm(info));
            TxtStatusSursa.Text = $"{lista.Count} fișiere găsite.";
        }
        catch (ProcesareException ex)
        {
            _fisiereSursa.Clear();
            TxtStatusSursa.Text = ex.Message;
        }
    }

    private void BtnPreiaFisiere_Click(object sender, RoutedEventArgs e)
    {
        var selectate = _fisiereSursa.Where(f => f.Selectat).Select(f => f.Info.CaleCompleta).ToList();
        if (selectate.Count == 0)
        {
            MessageBox.Show(this, "Selectați cel puțin un fișier din listă.", "Preia fișiere",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var jurnalTemp = new Jurnal();
        var rezultate = SurseFisiere.PreiaFisiere(selectate, _config.FolderLucru, jurnalTemp);
        var esuate = rezultate.Where(r => r.Eroare is not null).ToList();
        if (esuate.Count > 0)
        {
            MessageBox.Show(this,
                $"{esuate.Count} fișiere nu au putut fi mutate (posibil blocate de alt proces):{Environment.NewLine}" +
                string.Join(Environment.NewLine, esuate.Select(f => $"- {f.NumeFisier}: {f.Eroare}")),
                "Preia fișiere", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        ReimprospateazaSursa();
        ReimprospateazaLucru();
    }

    // ===================== Fișiere de procesat + procesare =====================

    private void BtnReimprospateazaLucru_Click(object sender, RoutedEventArgs e) => ReimprospateazaLucru();

    private void ReimprospateazaLucru()
    {
        _fisiereLucru.Clear();
        if (!Directory.Exists(_config.FolderLucru)) return;

        foreach (var cale in Directory.EnumerateFiles(_config.FolderLucru, "*.xml").OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            _fisiereLucru.Add(Path.GetFileName(cale));
    }

    private async void BtnProceseaza_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_config.FolderLucru))
        {
            MessageBox.Show(this, "Nu există fișiere preluate de procesat.", "Procesează",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var fisiere = Directory.EnumerateFiles(_config.FolderLucru, "*.xml")
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        if (fisiere.Count == 0)
        {
            MessageBox.Show(this, "Niciun fișier de procesat.", "Procesează", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _config.FolderDestinatie = TxtFolderDestinatie.Text;
        SalveazaConfig();

        BtnProceseaza.IsEnabled = false;
        _jurnalEntries.Clear();
        var jurnal = new Jurnal();

        try
        {
            MapareGrupe grupe;
            try
            {
                grupe = await Task.Run(() => MapareGrupe.Incarca(_config.CaleGrupeXlsx));
            }
            catch (ProcesareException ex)
            {
                MessageBox.Show(this, ex.Message, "Grupe.xlsx", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var rezultate = new List<RezultatFisier>();
            foreach (var cale in fisiere)
            {
                var rezultat = await Task.Run(() => ServiciuProcesare.ProceseazaFisier(cale, _config, grupe, jurnal));
                rezultate.Add(rezultat);

                for (var i = _jurnalEntries.Count; i < jurnal.Intrari.Count; i++)
                    _jurnalEntries.Add(jurnal.Intrari[i]);

                ActualizeazaContoare(jurnal);
            }

            ReimprospateazaSursa();
            ReimprospateazaLucru();
            ReincarcaRegistre();

            var succese = rezultate.Count(r => r.Succes);
            MessageBox.Show(this, $"Procesare finalizată: {succese}/{rezultate.Count} fișiere procesate cu succes.",
                "Procesează", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            BtnProceseaza.IsEnabled = true;
        }
    }

    private void ActualizeazaContoare(Jurnal jurnal)
    {
        var avertizari = jurnal.Numara(NivelJurnal.Avertizare);
        var erori = jurnal.Numara(NivelJurnal.Eroare);
        var duplicate = jurnal.Numara(CategorieJurnal.Duplicat);
        var agregare = jurnal.Numara(CategorieJurnal.Agregare);
        TxtContoareJurnal.Text =
            $"{avertizari} avertizări, {erori} erori — {duplicate} facturi excluse ca duplicat, {agregare} note de agregare.";
    }

    // ===================== Registre =====================

    private void ReincarcaRegistre()
    {
        _inAnteToate = CitesteRegistruSimplu(_config.CaleInAnte);
        _ieAnteToate = CitesteRegistruSimplu(_config.CaleIeAnte);
        AplicaFiltruInAnte();
        AplicaFiltruIeAnte();
    }

    private static List<string> CitesteRegistruSimplu(string cale)
    {
        if (!File.Exists(cale)) return new List<string>();
        return File.ReadAllLines(cale, Encoding.UTF8)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
            .ToList();
    }

    private void TxtCautareInAnte_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => AplicaFiltruInAnte();
    private void TxtCautareIeAnte_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => AplicaFiltruIeAnte();

    private void AplicaFiltruInAnte()
    {
        var filtru = TxtCautareInAnte.Text?.Trim() ?? "";
        var filtrate = filtru.Length == 0
            ? _inAnteToate
            : _inAnteToate.Where(v => v.IndexOf(filtru, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        LstInAnte.ItemsSource = filtrate;
        TxtTotalInAnte.Text = $"{filtrate.Count} / {_inAnteToate.Count} valori";
    }

    private void AplicaFiltruIeAnte()
    {
        var filtru = TxtCautareIeAnte.Text?.Trim() ?? "";
        var filtrate = filtru.Length == 0
            ? _ieAnteToate
            : _ieAnteToate.Where(v => v.IndexOf(filtru, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        LstIeAnte.ItemsSource = filtrate;
        TxtTotalIeAnte.Text = $"{filtrate.Count} / {_ieAnteToate.Count} valori";
    }

    private void BtnAnuleazaLotInAnte_Click(object sender, RoutedEventArgs e) => AnuleazaLot(_config.CaleInAnte);
    private void BtnAnuleazaLotIeAnte_Click(object sender, RoutedEventArgs e) => AnuleazaLot(_config.CaleIeAnte);

    private void AnuleazaLot(string caleRegistru)
    {
        var raspuns = MessageBox.Show(this, $"Sigur anulați ultimul lot din {Path.GetFileName(caleRegistru)}?",
            "Anulează ultimul lot", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (raspuns != MessageBoxResult.Yes) return;

        var jurnal = new Jurnal();
        var registru = Registru.Deschide(caleRegistru, jurnal);
        registru.AnuleazaUltimulLot(jurnal);

        ReincarcaRegistre();
        MessageBox.Show(this, string.Join(Environment.NewLine, jurnal.Intrari.Select(i => i.Mesaj)),
            "Anulează ultimul lot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeschideInAnte_Click(object sender, RoutedEventArgs e) => DeschideInEditor(_config.CaleInAnte);
    private void BtnDeschideIeAnte_Click(object sender, RoutedEventArgs e) => DeschideInEditor(_config.CaleIeAnte);

    private void DeschideInEditor(string cale)
    {
        try
        {
            if (!File.Exists(cale)) File.WriteAllText(cale, "");
            Process.Start(new ProcessStartInfo(cale) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nu s-a putut deschide fișierul: {ex.Message}", "Eroare",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===================== Grupe (referință vizuală) =====================

    private void ReincarcaGrupe()
    {
        try
        {
            var grupe = MapareGrupe.Incarca(_config.CaleGrupeXlsx);
            DgGrupeIntrari.ItemsSource = grupe.Intrari;
            DgGrupeIesiri.ItemsSource = grupe.Iesiri;
        }
        catch (ProcesareException)
        {
            // Grupe.xlsx lipsă/ilizibil la pornire — se reîncearcă automat la Procesează.
        }
    }
}
