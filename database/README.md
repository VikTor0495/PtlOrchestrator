# Database PTL Orchestrator

Schema del server centrale (PostgreSQL). Va installato sul DB del server del cliente insieme all'Api.

## Contenuto
- `postgres/001_schema_ptl.sql`: installazione completa (migration `20261013201821_InitialCreate`).
  Tabelle: `operators`, `stations`, `work_sessions`, `operation_events`, più `__EFMigrationsHistory`.
- `postgres/002_carrelli_e_assegnazioni.sql`: passo successivo (migration `20261111201752_AddCartAssignments`).
  Tabelle: `carts` (carrelli con IP e porta del controller), `cart_baskets` (cestelli, univoci su tutto il sistema),
  `cart_assignments` (chi occupa un carrello; le righe chiuse restano come storico).

## Installazione su un server nuovo
Prerequisito: PostgreSQL 16 o superiore, un database e un ruolo dedicati.

```sql
CREATE ROLE ptl LOGIN PASSWORD '<password>';
CREATE DATABASE ptl OWNER ptl ENCODING 'UTF8';
```

Poi, come ruolo `ptl` (o superuser) sul database `ptl`, in quest'ordine:
```bash
psql -h <host> -U ptl -d ptl -v ON_ERROR_STOP=1 -f postgres/001_schema_ptl.sql
psql -h <host> -U ptl -d ptl -v ON_ERROR_STOP=1 -f postgres/002_carrelli_e_assegnazioni.sql
```

## Aggiornamento di un database già installato
Su un database alla sola migration 001 basta eseguire `002_carrelli_e_assegnazioni.sql`.
Gli script sono idempotenti: rieseguirli non altera uno schema già aggiornato.

Il ruolo dell'Api deve poter leggere e scrivere le tre nuove tabelle (`GRANT SELECT, INSERT, UPDATE, DELETE`).
Se l'Api parte con `Database:MigrateOnStartup=true` può applicare lei le migration, ma il ruolo deve avere il privilegio `CREATE` sullo schema.

## Regole
- Le DDL si generano sempre dalle migration EF, non si scrivono a mano:
  `dotnet ef migrations script --idempotent --project src/PtlOrchestrator.Api --output database/postgres/<file>.sql`
  Per un passo tra due migration: `dotnet ef migrations script <da> <a> --idempotent ...`
- Ogni nuova migration produce un nuovo file numerato (`003_...`), mai una modifica di uno già installato.
- L'Api con `Database:MigrateOnStartup=true` applica le migration da sola; lo script serve per installare il DB senza avviare l'Api, o per il DBA del cliente.
