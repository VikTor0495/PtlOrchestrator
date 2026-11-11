-- PTL Orchestrator - schema PostgreSQL (generato da EF Core: dotnet ef migrations script --idempotent --project src/PtlOrchestrator.Api)
-- Idempotente: rieseguibile senza effetti se lo schema è già aggiornato. Versione EF: 10.0.13.
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE TABLE operation_events (
        id uuid NOT NULL,
        occurred_at timestamp with time zone NOT NULL,
        received_at timestamp with time zone NOT NULL,
        station_id character varying(64) NOT NULL,
        session_id uuid,
        operator_id uuid,
        type character varying(40) NOT NULL,
        barcode character varying(128),
        cart_id character varying(64),
        basket_id character varying(64),
        details character varying(1000),
        CONSTRAINT pk_operation_events PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE TABLE operators (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        badge_code character varying(64),
        pin_hash character varying(200),
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_operators PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE TABLE stations (
        id character varying(64) NOT NULL,
        first_seen_at timestamp with time zone NOT NULL,
        last_seen_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_stations PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE TABLE work_sessions (
        id uuid NOT NULL,
        operator_id uuid NOT NULL,
        station_id character varying(64) NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone,
        CONSTRAINT pk_work_sessions PRIMARY KEY (id),
        CONSTRAINT fk_work_sessions_operators_operator_id FOREIGN KEY (operator_id) REFERENCES operators (id) ON DELETE RESTRICT,
        CONSTRAINT fk_work_sessions_stations_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_operation_events_occurred_at ON operation_events (occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_operation_events_operator_id_occurred_at ON operation_events (operator_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_operation_events_session_id ON operation_events (session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_operators_badge_code ON operators (badge_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_operators_updated_at ON operators (updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_work_sessions_operator_id_started_at ON work_sessions (operator_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_work_sessions_started_at ON work_sessions (started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    CREATE INDEX ix_work_sessions_station_id ON work_sessions (station_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013201821_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261013201821_InitialCreate', '10.0.13');
    END IF;
END $EF$;
COMMIT;

