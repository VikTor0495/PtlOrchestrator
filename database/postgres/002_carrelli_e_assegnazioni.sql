-- PTL Orchestrator - schema PostgreSQL, passo 2: catalogo carrelli e assegnazioni (migration 20261111201752_AddCartAssignments)
-- Da applicare su un database gia' alla migration 001. Idempotente.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    CREATE TABLE carts (
        code character varying(16) NOT NULL,
        controller_ip character varying(45) NOT NULL,
        controller_port integer NOT NULL,
        is_active boolean NOT NULL,
        CONSTRAINT pk_carts PRIMARY KEY (code)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    CREATE TABLE cart_assignments (
        id uuid NOT NULL,
        cart_code character varying(16) NOT NULL,
        station_id character varying(64) NOT NULL,
        session_id uuid,
        assigned_at timestamp with time zone NOT NULL,
        released_at timestamp with time zone,
        CONSTRAINT pk_cart_assignments PRIMARY KEY (id),
        CONSTRAINT fk_cart_assignments_carts_cart_code FOREIGN KEY (cart_code) REFERENCES carts (code) ON DELETE RESTRICT,
        CONSTRAINT fk_cart_assignments_stations_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    CREATE TABLE cart_baskets (
        cart_code character varying(16) NOT NULL,
        basket_id character varying(64) NOT NULL,
        CONSTRAINT pk_cart_baskets PRIMARY KEY (cart_code, basket_id),
        CONSTRAINT fk_cart_baskets_carts_cart_code FOREIGN KEY (cart_code) REFERENCES carts (code) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    CREATE INDEX ix_cart_assignments_station_id ON cart_assignments (station_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    CREATE UNIQUE INDEX ux_cart_assignments_open ON cart_assignments (cart_code) WHERE released_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    CREATE UNIQUE INDEX ix_cart_baskets_basket_id ON cart_baskets (basket_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111201752_AddCartAssignments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111201752_AddCartAssignments', '10.0.13');
    END IF;
END $EF$;
COMMIT;

