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
