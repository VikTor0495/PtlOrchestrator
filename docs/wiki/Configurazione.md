# Configurazione

Guida operativa storica (Console) in `readme.txt` alla radice. Sezioni principali:

## Core (`appsettings.json` di Console/Station)
- `Station:StationId` — identificativo postazione (obbligatorio)
- `Lightstep:ControllerIp` / `ControllerPort` — controller PTL
- `Carts[]` — `CartId` + `Baskets[]` (`BasketId` univoco su **tutti** i carrelli: ogni ID è una lampada fisica)
- `LimitBarcode:FileName` — CSV dei limiti per barcode
- `ProcessTimeout:Minutes` — timeout operazione (> 0)

Validazione all'avvio (`ValidateOnStart`) in `PtlCoreServiceCollectionExtensions`.

## Station (`StationSettings.cs`, esempio in `src/PtlOrchestrator.Station/appsettings.json`)
- sezione `LocalData`: `DatabasePath` (default `data/station.db`)
- sezione `Central`: `BaseUrl`, `ApiKey` (StationKey), `TimeoutSeconds` 15, `EventSyncIntervalSeconds` 5, `OperatorSyncIntervalSeconds` 60, `EventBatchSize` 200
- sezione `SerialScanner`: `PortName`, `BaudRate` 9600, 8N1, `ReconnectSeconds` 5, `MaxCodeLength` 128

## Api
`ConnectionStrings:Ptl`, `ApiKeys:StationKey|AdminKey`, `Database:MigrateOnStartup`.
