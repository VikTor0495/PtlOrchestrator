# Comandi principali

Tutti i comandi si lanciano dalla cartella radice del repository (`PtlOrchestrator`), in PowerShell o terminale. Serve il **.NET SDK 10**.

Progetti da conoscere:

| Progetto | Tipo | Eseguibile |
|---|---|---|
| `src/PtlOrchestrator.Api` | Web API (ASP.NET Core) | sì |
| `src/PtlOrchestrator.Station` | App WPF, **solo Windows** | sì |
| `src/PtlOrchestrator.Console` | App console (legacy) | sì |
| `src/PtlOrchestrator.Core` | Libreria (logica PTL) | no |
| `src/PtlOrchestrator.Contracts` | Libreria (DTO condivisi) | no |

## 1. Restore e build

```powershell
dotnet restore PtlOrchestrator.sln                     # scarica i pacchetti NuGet
dotnet build PtlOrchestrator.sln                       # build di tutta la soluzione (Debug)
dotnet build PtlOrchestrator.sln -c Release            # build in Release
```

Build di un singolo progetto:

```powershell
dotnet build src/PtlOrchestrator.Api
dotnet build src/PtlOrchestrator.Station
dotnet build src/PtlOrchestrator.Console
```

## 2. Test

Prima di ogni commit i test devono passare:

```powershell
dotnet test PtlOrchestrator.sln
```

Test di un singolo progetto di test:

```powershell
dotnet test tests/PtlOrchestrator.Api.Tests
dotnet test tests/PtlOrchestrator.Core.Tests
dotnet test tests/PtlOrchestrator.Station.Tests
dotnet test tests/PtlOrchestrator.Contracts.Tests
```

Un singolo test per nome:

```powershell
dotnet test tests/PtlOrchestrator.Core.Tests --filter "FullyQualifiedName~NomeDelTest"
```

## 3. Lanciare in Debug

### Api

L'Api richiede la connection string a PostgreSQL e le chiavi API. Non vanno nel file di configurazione: si passano come variabili d'ambiente (la sintassi `__` equivale a `:`).

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"              # usa appsettings.Development.json
$env:ConnectionStrings__Ptl = "Host=localhost;Port=5432;Database=ptl;Username=ptl;Password=..."
$env:ApiKeys__StationKey    = "chiave-station-almeno-16-caratteri"
$env:ApiKeys__AdminKey      = "chiave-admin-almeno-16-caratteri"

dotnet run --project src/PtlOrchestrator.Api                    # build Debug + avvio
dotnet watch --project src/PtlOrchestrator.Api run              # riavvio automatico alle modifiche
```

PostgreSQL in locale (senza installarlo) si può avviare con Docker: `docker compose up -d db`.

### Station (solo Windows)

```powershell
dotnet run --project src/PtlOrchestrator.Station
```

Per il funzionamento reale servono `appsettings.json` compilato (`Central:BaseUrl`, `Central:ApiKey`, `SerialScanner:PortName`, `Lightstep:*`, `Carts`) e il file `prodotti_config.csv`, che non è nel repository (vedi [`docs/wiki/Setup-e-Build.md`](docs/wiki/Setup-e-Build.md)). Senza hardware (pistola seriale, controller Lightstep) l'app parte ma le funzioni PTL non rispondono.

### Console (legacy)

```powershell
dotnet run --project src/PtlOrchestrator.Console
```

### Debugger con breakpoint

- **Visual Studio**: apri `PtlOrchestrator.sln`, imposta il progetto di avvio (clic destro sul progetto → *Imposta come progetto di avvio*), poi `F5` (Debug) o `Ctrl+F5` (senza debugger).
- **VS Code / Rider**: usa la configurazione di avvio del progetto desiderato con configurazione `Debug`.
- Da terminale non c'è un debugger: `dotnet run` gira in Debug ma senza breakpoint; per il debugger usa l'IDE.

## 4. Pubblicazione (EXE singolo)

```powershell
dotnet publish src/PtlOrchestrator.Station -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish src/PtlOrchestrator.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

L'output è nella cartella `publish/` sotto `src/<progetto>/bin/Release/`: `net10.0-windows/win-x64/publish/` per la Station, `net10.0/win-x64/publish/` per la Console.

## 5. Docker (Api + PostgreSQL)

```powershell
copy .env.example .env             # poi compila POSTGRES_PASSWORD, STATION_KEY, ADMIN_KEY
docker compose up -d --build       # avvia Api e database
docker compose logs -f api         # log dell'Api
docker compose down                # ferma i container (i dati restano nel volume ptl-data)
```

Nota: `Dockerfile` e `docker-compose.yml` non sono ancora stati provati (vedi wiki).

## 6. Migrazioni EF Core

```powershell
dotnet tool restore                                                  # installa dotnet-ef (dotnet-tools.json)
dotnet ef migrations add <NomeMigrazione> --project src/PtlOrchestrator.Api
```

Le migrazioni vengono applicate all'avvio dell'Api quando `Database:MigrateOnStartup` è `true`.

## Riferimenti

- Setup completo e primo avvio: [`docs/wiki/Setup-e-Build.md`](docs/wiki/Setup-e-Build.md)
- Configurazione: [`docs/wiki/Configurazione.md`](docs/wiki/Configurazione.md)
- Architettura: [`docs/wiki/Architettura.md`](docs/wiki/Architettura.md)
