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
- [x] 5.11 Console dismessa, GUI con solo il login prima dell'accesso (carrelli, impostazioni e diagnostica compaiono dopo), input manuale dei codici sempre disponibile
- [x] 5.12 Simulatore PTL (`tools/ptl-simulator`): modalità `--headless`, operazioni `state` e `press` sulla porta di controllo, risposta in frame (la DLL la richiede), dialetto Station tollerante (`--strict` per rifiutarlo)

### Da fare alla prossima sessione
- [ ] **Commit e push** del lavoro non ancora committato (Console rimossa, GUI login-first, `UseWindowsForms` nella Station, simulatore). Prima `dotnet test PtlOrchestrator.sln`: chiudere Api, Station e simulatore, che bloccano i DLL
- [ ] **Test manuale** (non ancora fatto): Api + simulatore + Station; login `OP001`, check-in carrello `10` (127.0.0.1:15003), scansione prodotto, lampada nel simulatore, click per CONFIRM, rimozione carrello, chiusura sventagliamento, logout
- [ ] **Test d'integrazione DLL ↔ simulatore** (`PtlSimulatorIntegrationTests`, oggi in Skip): la DLL Lightstep carica `System.Windows.Forms` dentro `Connect`, quindi vanno spostati in `Station.Tests` (net10.0-windows) e va capito perché `SendCommand` si blocca nel test host (da console funziona dopo la risposta in frame)
- [ ] **Formato dei comandi**: `Pp505Builder` scrive i blocchi `m1/m2/m3` in forma abbreviata, diversa dai 3 byte dei manuali. Il simulatore li accetta come "dialetto Station" ma il controller reale potrebbe rifiutarli. Verificare con Reflexer/controller. Verificare anche il formato dell'evento `t` (pulsante)
- [ ] **Licenza Lightstep**: `Lightstep:LicenzeKey` va inserita nelle impostazioni della Station (non è nel repo); il simulatore accetta anche la chiave vuota
- [ ] **Pulizia `ptl_test`**: carrelli di prova 1, 2, 3, 10 e relative assegnazioni; **cambiare la password** del ruolo `ptl_test` (è su GitHub in `docs/wiki/Setup-e-Build.md`) e togliere la riga dalla wiki
- [x] 5.8 Icona dell'eseguibile Station (`assets/PTL-Orchestrator.ico`, in `ApplicationIcon` e nella finestra)

## Fase 4 — Chiusura
- [x] 4.1 Console dismessa (decisione dell'utente): progetto rimosso dalla soluzione e dal repo; resta nella storia git. Il docker compose contiene solo PostgreSQL e Api
- [x] 4.2 Contratto client Station ↔ Api provato end-to-end (`StationClientContractTests`: invio, duplicati, rifiuti, chiave errata, sync incrementale operatori). Manca un test con la GUI e la porta seriale reali: richiede hardware
- [x] 4.3 Publish self-contained della Station provato (EXE singolo, parte e crea data/ e logs/); vedi Setup e Build
- [x] 4.4 README aggiornato con rimando alla wiki. **Merge su `master`: da fare a validazione sul campo conclusa**

## Da validare sul campo (non verificabile da software; rinviate dall'utente al collaudo, restano aperte)
- [ ] Postazione con PTL Lightstep reale: connessione, accensione lampade, conferma pulsante, reset
- [ ] Pistola barcode su porta seriale reale (`SerialScanner`): terminatori CR/LF, riconnessione a cavo staccato
- [ ] Aspetto e usabilità della GUI su monitor/touch reale (testata solo l'avvio e la logica dei ViewModel)
- [ ] `docker compose up` (non provato: Docker assente sul PC di sviluppo)
- [ ] Server su PostgreSQL reale (i test usano SQLite in memoria)

## Idee future (non pianificate)
- Interfaccia di amministrazione per operatori e report (oggi solo via API con chiave Admin)
- Logout automatico per inattività
- Visualizzazione dei log recenti nella finestra Diagnostica
