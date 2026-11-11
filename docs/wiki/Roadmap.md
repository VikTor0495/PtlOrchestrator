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

## Fase 5 — Flusso operatore (in corso)
- [x] 5.1 Login con codice operatore digitato a mano (stesso percorso del badge; PIN e lista operatori tolti dalla schermata)
- [x] 5.2 Check-in carrelli: il turno parte con zero carrelli attivi. Il carrello si identifica con il suo numero seriale (`1`, `2`…); il check-in lo occupa sul server (`POST /api/carts/{codice}/checkin`), che lo censisce con IP, porta e cestelli e rifiuta un carrello già occupato da un'altra postazione (`CartCheckedIn` nel log)
- [x] 5.3 Check-in durante lo sventagliamento: stessa scansione/input, con turno attivo (test `ScanDispatcherTests`)
- [x] 5.4 Chiusura: pulsante "Chiudi sventagliamento" (CSV + azzeramento, carrelli restano del turno). Automatica quando un prodotto non trova più spazio
- [x] 5.5 Logout: rifiutato se ci sono prodotti nei carrelli; altrimenti libera i carrelli sul server e azzera l'interfaccia
- [x] 5.6 Più controller: ogni carrello ha il proprio IP:porta (dal server); pool di connessioni per endpoint; comandi e attesa pulsanti instradati al controller del carrello. Carrello vuoto rimovibile dalla GUI (`CartReleased`)
- [x] 5.7 Impostazioni nella GUI, salvate in SQLite locale (`app_settings`) con storico (`app_settings_history`); valgono dal riavvio
- [x] 5.9 Identificativo postazione: `Station:StationId` se configurato, altrimenti derivato dal `MachineGuid` di Windows (stabile tra reinstallazioni dell'app); la postazione si registra sul server al primo contatto
- [x] 5.10 Server: tabelle `carts`, `cart_baskets`, `cart_assignments` (indice univoco parziale sui carrelli aperti), migration `AddCartAssignments`, DDL `database/postgres/002_carrelli_e_assegnazioni.sql`. Verificato su Postgres: 10 postazioni in simultanea sullo stesso carrello → una sola 200, nove 409
- [x] 5.8 Icona dell'eseguibile Station (`assets/PTL-Orchestrator.ico`, in `ApplicationIcon` e nella finestra)

## Fase 4 — Chiusura
- [ ] 4.1 **Decisione aperta**: la Console resta com'è (storica, senza operatori né DB). Migrarla su `AddPtlCore` ne cambierebbe il comportamento (login obbligatorio). Proposta: dismetterla dopo la validazione sul campo della Station
- [x] 4.2 Contratto client Station ↔ Api provato end-to-end (`StationClientContractTests`: invio, duplicati, rifiuti, chiave errata, sync incrementale operatori). Manca un test con la GUI e la porta seriale reali: richiede hardware
- [x] 4.3 Publish self-contained della Station provato (EXE singolo, parte e crea data/ e logs/); vedi Setup e Build
- [x] 4.4 README aggiornato con rimando alla wiki. **Merge su `master`: da fare a validazione sul campo conclusa**

## Da validare sul campo (non verificabile da software)
- [ ] Postazione con PTL Lightstep reale: connessione, accensione lampade, conferma pulsante, reset
- [ ] Pistola barcode su porta seriale reale (`SerialScanner`): terminatori CR/LF, riconnessione a cavo staccato
- [ ] Aspetto e usabilità della GUI su monitor/touch reale (testata solo l'avvio e la logica dei ViewModel)
- [ ] `docker compose up` (non provato: Docker assente sul PC di sviluppo)
- [ ] Server su PostgreSQL reale (i test usano SQLite in memoria)

## Idee future (non pianificate)
- Interfaccia di amministrazione per operatori e report (oggi solo via API con chiave Admin)
- Logout automatico per inattività
- Visualizzazione dei log recenti nella finestra Diagnostica
