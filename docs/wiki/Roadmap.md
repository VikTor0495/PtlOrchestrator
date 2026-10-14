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
- [ ] 1.5 `SerialBarcodeSource` (`IBarcodeSource` su `System.IO.Ports`, riconnessione) + test su astrazione della porta
- [ ] 1.6 Composition root Station: `Host` + `AddPtlCore` + Serilog + registrazioni Station, validazione `StationSettings`

## Fase 2 — GUI WPF (MVVM, CommunityToolkit.Mvvm)
- [ ] 2.1 `MainWindow` + `MainViewModel`: stato connessione PTL/server, coda eventi pendenti/quarantena
- [ ] 2.2 Schermata login (badge/PIN) e turno operatore
- [ ] 2.3 Vista lavoro: ultima scansione/esito (`IScanActivity`), carrelli e ceste con colori/stato
- [ ] 2.4 Gestione errori e messaggi per l'operatore (sistema occupato, PTL in errore, offline)
- [ ] 2.5 Pagina diagnostica (log recenti, forza sync, stato outbox)

## Fase 3 — Server completo
- [ ] 3.1 Verificare idempotenza ingest per `EventId` con test
- [ ] 3.2 Endpoint registrazione/heartbeat postazioni (tabella `stations`)
- [ ] 3.3 Report turni: export CSV
- [ ] 3.4 Dockerfile + compose (Api + PostgreSQL), health check

## Fase 4 — Chiusura
- [ ] 4.1 Migrare/dismettere la Console su `AddPtlCore` (o rimuoverla)
- [ ] 4.2 Test di integrazione Station↔Api end-to-end
- [ ] 4.3 Installer/publish Station (self-contained)
- [ ] 4.4 Aggiornare README/readme.txt, merge su `master`
