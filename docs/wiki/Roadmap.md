# Roadmap

Un passo = un commit + push. Spuntare qui il passo nello stesso commit.

## Fatto
- [x] Test sulla baseline, porting .NET 10, struttura a progetti
- [x] Contracts + registro eventi operazioni
- [x] Core: login operatore, `ScanDispatcher`, `AddPtlCore`
- [x] Api: ASP.NET Core + EF Core + PostgreSQL, auth API key, ingest, operatori, report turni
- [x] Station: storage SQLite (cache operatori, outbox) e `StationSettings`
- [x] Wiki + `CLAUDE.md`

## Fase 1 — Station headless (servizi senza GUI)
- [x] 1.1 `HttpCentralClient`: invio eventi (`/api/events`) e snapshot operatori, con `X-Api-Key`, timeout, test con handler finto
- [x] 1.2 `EventSyncService` (BackgroundService): svuota l'outbox a batch; temporanei → `RecordFailure`, 4xx → `Quarantine`
- [x] 1.3 `OperatorSyncService`: sync incrementale (`since`) nella cache operatori
- [x] 1.4 `SqliteOperatorCache` implementa già `IOperatorDirectory` (login offline) + test
- [x] 1.5 `SerialBarcodeSource` (`IBarcodeSource` su `System.IO.Ports`, riconnessione) + test su astrazione della porta
- [x] 1.6 Composition root Station: `Host` + `AddPtlCore` + Serilog + registrazioni Station, validazione `StationSettings`
- [x] 1.7 `PtlLifecycleService` (Core): connessione PTL, limiti barcode e azzeramento all'avvio; report, azzeramento e disconnessione allo spegnimento (era nel vecchio Worker)

## Fase 2 — GUI WPF (MVVM, CommunityToolkit.Mvvm)
- [x] 2.1 `MainWindow` + `MainViewModel`: stato connessione PTL/server, coda eventi pendenti/quarantena
- [x] 2.2 Schermata login (badge/PIN) e turno operatore
- [x] 2.3 Vista lavoro: ultima scansione/esito (`IScanActivity`), carrelli e ceste con colori/stato
- [x] 2.4 Gestione errori e messaggi per l'operatore (sistema occupato, PTL in errore, offline)
- [x] 2.5 Finestra Diagnostica: stato coda/sync/PTL, sincronizza ora, scrivi report CSV, azzera carrelli (ex comando `reset`). Log recenti: vedi `logs/` accanto all'eseguibile

## Fase 3 — Server completo
- [x] 3.1 Idempotenza ingest per `EventId` (già implementata e coperta da `EventIngestTests`)
- [x] 3.2 `GET /api/stations` con ultima attività (heartbeat implicito dall'invio eventi; nessun endpoint dedicato)
- [x] 3.3 Export CSV dei turni (`GET /api/sessions/export`)
- [x] 3.4 Dockerfile + compose (Api + PostgreSQL), health check `/health` — **da provare**, Docker assente sul PC di sviluppo

## Fase 4 — Chiusura
- [ ] 4.1 Migrare/dismettere la Console su `AddPtlCore` (o rimuoverla)
- [ ] 4.2 Test di integrazione Station↔Api end-to-end
- [ ] 4.3 Installer/publish Station (self-contained)
- [ ] 4.4 Aggiornare README/readme.txt, merge su `master`
