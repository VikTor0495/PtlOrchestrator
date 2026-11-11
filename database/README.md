# Database PTL Orchestrator

Schema del server centrale (PostgreSQL). Va installato sul DB del server del cliente insieme all'Api.

## Contenuto
- `postgres/001_schema_ptl.sql`: schema iniziale (migration `20261013201821_InitialCreate`).
  Tabelle: `operators`, `stations`, `work_sessions`, `operation_events`, più `__EFMigrationsHistory`.

## Installazione su un server nuovo
Prerequisito: PostgreSQL 16 o superiore, un database e un ruolo dedicati.

```sql
CREATE ROLE ptl LOGIN PASSWORD '<password>';
CREATE DATABASE ptl OWNER ptl ENCODING 'UTF8';
```

Poi, come ruolo `ptl` (o superuser) sul database `ptl`:
```bash
psql -h <host> -U ptl -d ptl -v ON_ERROR_STOP=1 -f postgres/001_schema_ptl.sql
```

Lo script è idempotente: rieseguirlo non altera uno schema già aggiornato.

## Regole
- Le DDL si generano sempre dalle migration EF, non si scrivono a mano:
  `dotnet ef migrations script --idempotent --project src/PtlOrchestrator.Api --output database/postgres/<file>.sql`
- Ogni nuova migration produce un nuovo file numerato (`002_...`), mai una modifica di uno già installato.
- L'Api con `Database:MigrateOnStartup=true` applica le migration da sola; lo script serve per installare il DB senza avviare l'Api, o per il DBA del cliente.
