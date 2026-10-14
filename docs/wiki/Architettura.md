# Architettura

## Progetti (`src/`)
| Progetto | Target | Ruolo |
|---|---|---|
| `PtlOrchestrator.Contracts` | net10.0 | DTO condivisi Station↔Api (`OperationEvent`, `OperatorDto`, ...) e `PinHasher`. |
| `PtlOrchestrator.Core` | net10.0 | Logica di dominio: carrelli/ceste, `CartManager`, builder comandi PP505, connessione Lightstep, login operatore, `ScanDispatcher`, `ScanListener`. Si registra con `AddPtlCore(configuration)`. |
| `PtlOrchestrator.Api` | net10.0 (ASP.NET Core) | Backend centrale: EF Core + PostgreSQL, auth con API key, ingest eventi, anagrafica operatori, report turni. |
| `PtlOrchestrator.Station` | net10.0-windows (WPF) | Applicazione di postazione: SQLite locale (cache operatori + outbox eventi), futura GUI. |
| `PtlOrchestrator.Console` | net10.0 | Vecchio eseguibile (Worker + console). Ancora con `NullOperationEventSink` e registrazioni manuali; da migrare/dismettere. |

Test in `tests/` (un progetto per ciascuno tranne Console), xUnit.

## Flusso di una scansione
```
IBarcodeSource ──► ScanListener ──► ScanDispatcher ──► (login operatore | CartManager)
 (seriale/console)  (canale da 1,       │                         │
                     scarta se occupato)│                         ▼
                                        │               IPtlCommandService ──► controller Lightstep (TCP)
                                        ▼
                               OperationRecorder ──► IOperationEventSink
                                                       │ (Station: OutboxEventSink → SQLite outbox)
                                                       ▼
                                        sync in background ──► POST /api/events ──► PostgreSQL
```
- `ScanListener` mantiene la semantica del vecchio Worker: durante un'operazione le nuove scansioni sono **scartate** ("sistema occupato"), non accodate.
- Gli esiti arrivano alla UI tramite `IScanActivity.ScanProcessed`.

## Punti di estensione che l'host deve fornire
`AddPtlCore` registra tutto tranne:
- `IBarcodeSource` — sorgente codici (seriale in Station, console nella Console)
- `IOperatorDirectory` — anagrafica operatori **locale** (il login deve funzionare senza rete)
- `IOperationEventSink` — destinazione eventi (Station: outbox durevole)

## Offline-first
La postazione non dipende mai dalla rete per lavorare:
1. Login contro la **cache operatori** SQLite, sincronizzata dal server (`GET /api/operators/snapshot?since=`).
2. Eventi scritti nell'**outbox** SQLite; un servizio in background li invia a batch (`POST /api/events`).
3. Errori temporanei → `RecordFailureAsync` e ritenta; rifiuti definitivi (validazione) → `QuarantineAsync`.
4. Consegna *at-least-once*: l'ingest lato server deve essere idempotente per `EventId`.

## Sicurezza
- Due API key: `StationKey` (postazioni: ingest + snapshot) e `AdminKey` (CRUD operatori, report turni).
- Il PIN non viaggia mai in chiaro: solo `PinHash` (formato di `PinHasher`).

## Eventi di operazione (`OperationEventType`)
SessionStarted, SessionEnded, BarcodeAccepted, BarcodeRejected, BasketConfirmed, WrongModulePressed,
OperationTimedOut, PtlFault, CartsReset. Serializzati come stringa.

## Database centrale
Tabelle: operators, stations, work_sessions, operation_events (snake_case). Migrazioni EF in
`src/PtlOrchestrator.Api/Persistence/Migrations`, applicate all'avvio se `Database:MigrateOnStartup=true`.
