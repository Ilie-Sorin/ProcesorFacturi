# ProcesorFacturi

Aplicație C# / WPF pentru procesarea fișierelor XML de facturi (Intrări/Ieșiri) exportate din ERP, cu agregare de linii, generare DBF pentru SAGA, raport XLSX și registre anti-duplicare. Specificația completă e în [`Specificatie_Completa_v2.2.md`](Specificatie_Completa_v2.2.md).

## Cerințe

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (folosit doar ca instrument de build — aplicația compilată țintește `net48`, deci **nu** are nevoie de runtime .NET distribuit pe stația unde rulează, doar de .NET Framework 4.8, deja prezent pe Windows 10/11)

Nu e nevoie de Visual Studio — proiectul e SDK-style și se compilă direct cu `dotnet`.

## Build

Din rădăcina repo-ului:

```bash
dotnet build
```

Compilează toate cele trei proiecte din `ProcesorFacturi.sln`:

| Proiect | Rol |
|---|---|
| `src/ProcesorFacturi.Core` | logica de business (fără dependență de WPF) |
| `src/ProcesorFacturi.Tests` | teste xUnit |
| `src/ProcesorFacturi.App` | interfața WPF |

## Teste

```bash
dotnet test src/ProcesorFacturi.Tests/ProcesorFacturi.Tests.csproj
```

Include testul de comparație octet-cu-octet al scriitorului DBF împotriva unui fișier de referință și teste de integrare capăt-la-capăt pe XML-uri reale — acestea din urmă se sar automat (`return` timpuriu) dacă fișierele externe la care fac referință (în afara repo-ului) nu sunt prezente pe mașina curentă.

## Rulare

```bash
dotnet run --project src/ProcesorFacturi.App
```

sau, după `dotnet build`, direct executabilul:

```
src\ProcesorFacturi.App\bin\Debug\net48\ProcesorFacturi.App.exe
```

Pentru build de producție:

```bash
dotnet build -c Release
```

Executabilul rezultă în `src\ProcesorFacturi.App\bin\Release\net48\`, alături de DLL-urile lui (fără single-file, fără ILMerge — vezi §14.4 din specificație pentru motiv).

## Configurare

La prima pornire, aplicația creează `config.json` lângă executabil, cu căile implicite din §3.1 (folder sursă, folder destinație, folder arhivă, folder registre, cale `Grupe.xlsx`) și opțiunea „Generează raport XLSX”. Căile se pot schimba din interfață (butoanele „...” de lângă câmpurile de folder) sau editând direct `config.json`.

`Grupe.xlsx`, `InAnte.txt` și `IeAnte.txt` din rădăcina repo-ului sunt datele operaționale implicite (mapare de validare, respectiv registrele anti-duplicare) — `Arhiva/` și `Preluate/` sunt generate la rulare și nu sunt urmărite în git (`.gitignore`).
