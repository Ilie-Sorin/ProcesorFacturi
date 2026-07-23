using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcesorFacturi.Core;

/// <summary>Căi și opțiuni configurabile, persistate în config.json lângă executabil (§3.1, §9.1).</summary>
public sealed class ConfigApp
{
    public string FolderSursa { get; set; } = @"D:\Utile\MAGNET\PlusBackOffice\Tmp";
    public string FolderLucru { get; set; } = @"D:\nftosaga2026\Preluate";
    public string FolderDestinatie { get; set; } = @"D:\De_Importat";
    public string FolderArhiva { get; set; } = @"D:\nftosaga2026\Arhiva";
    public string FolderRegistre { get; set; } = @"D:\nftosaga2026";
    public string CaleGrupeXlsx { get; set; } = @"D:\nftosaga2026\Grupe.xlsx";
    public bool GenereazaRaportXlsx { get; set; } = true;

    [JsonIgnore]
    public string CaleInAnte => Path.Combine(FolderRegistre, "InAnte.txt");

    [JsonIgnore]
    public string CaleIeAnte => Path.Combine(FolderRegistre, "IeAnte.txt");

    public static ConfigApp Implicit() => new();

    public static ConfigApp Incarca(string caleConfig)
    {
        if (!File.Exists(caleConfig))
            return Implicit();

        try
        {
            var json = File.ReadAllText(caleConfig);
            return JsonSerializer.Deserialize<ConfigApp>(json) ?? Implicit();
        }
        catch (JsonException)
        {
            return Implicit();
        }
    }

    public void Salveaza(string caleConfig)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        var director = Path.GetDirectoryName(caleConfig);
        if (!string.IsNullOrEmpty(director))
            Directory.CreateDirectory(director);
        File.WriteAllText(caleConfig, json);
    }
}
