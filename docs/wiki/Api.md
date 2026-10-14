# API centrale

Autenticazione: vedi `src/PtlOrchestrator.Api/Security/ApiKeyAuthenticationHandler.cs`. Policy `Station` e `Admin`.

| Metodo | Percorso | Policy | Note |
|---|---|---|---|
| POST | `/api/events` | Station | Body `IngestEventsRequest { Events[] }`. 400 ValidationProblem se non valido. Risposta `IngestEventsResponse`. |
| GET | `/api/operators/snapshot?since=` | Station | `OperatorsSnapshotResponse { Operators, ServerTime }`; `since` = delta dall'ultimo sync. |
| GET | `/api/operators` | Admin | Elenco completo. |
| POST | `/api/operators` | Admin | `SaveOperatorRequest` → 201. |
| PUT | `/api/operators/{id}` | Admin | 404 se assente, 409 su conflitto. |
| GET | `/api/sessions?from&to&operatorId&take` | Admin | `take` default 100, max 500. |
| GET | `/api/sessions/{id}/events` | Admin | Eventi del turno, 404 se non esiste. |
