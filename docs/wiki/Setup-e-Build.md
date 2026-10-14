# Setup e Build

## Prerequisiti
- Windows 11, **.NET SDK 10**
- PostgreSQL (per eseguire l'Api)
- Git con accesso a `github.com/VikTor0495/PtlOrchestrator`

## Primo avvio su un nuovo PC
```powershell
git clone https://github.com/VikTor0495/PtlOrchestrator.git
cd PtlOrchestrator
git checkout feature/evolutiva-gui-db
dotnet restore
dotnet test PtlOrchestrator.sln
```
Poi apri Claude Code nella cartella: leggerà `CLAUDE.md` e questa wiki.

## Eseguire
- **Api**: impostare `ConnectionStrings:Ptl`, `ApiKeys:StationKey`, `ApiKeys:AdminKey` (user-secrets o variabili d'ambiente, non nel file) e `dotnet run --project src/PtlOrchestrator.Api`.
- **Console (legacy)**: `dotnet run --project src/PtlOrchestrator.Console`.
- **Station**: `dotnet run --project src/PtlOrchestrator.Station` (solo Windows).

## Pubblicare la Console (EXE singolo)
```powershell
dotnet publish src/PtlOrchestrator.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Migrazioni EF
```powershell
dotnet tool restore
dotnet ef migrations add <Nome> --project src/PtlOrchestrator.Api
```

## Server con Docker (Api + PostgreSQL)
```powershell
copy .env.example .env      # poi compila password e chiavi (>= 16 caratteri)
docker compose up -d --build
```
L'Api risponde su `http://localhost:5000` (`API_PORT`), applica le migrazioni all'avvio e conserva i dati nel volume `ptl-data`.
> Nota: `Dockerfile` e `docker-compose.yml` sono stati scritti ma **non ancora provati** (Docker non era installato sul PC di sviluppo). Al primo uso verificare il build.

## Pubblicare la Station (EXE singolo)
```powershell
dotnet publish src/PtlOrchestrator.Station -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Provato: produce `PtlStation.exe` (~144 MB) e parte da solo. Accanto all'exe devono stare:
- `appsettings.json` (viene copiato dal publish): impostare `Central:BaseUrl`, `Central:ApiKey` (chiave **Station** del server), `SerialScanner:PortName`, `Lightstep:*`, `Carts`
- `prodotti_config.csv` (o il nome in `LimitBarcode:FileName`): **non è nel repo**, è un dato di installazione. Senza questo file la postazione resta "sistema occupato" e riprova ogni 5 s (vedi `logs/`)

Alla prima esecuzione vengono creati `data/station.db` (coda eventi e cache operatori: **non cancellare** se ci sono eventi non ancora inviati) e `logs/station-AAAAMMGG.log`.

Prima di usare la postazione: creare almeno un operatore con la chiave **Admin** (`POST /api/operators`), perché il login ha bisogno della cache operatori scaricata dal server.
