using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PtlOrchestrator.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operation_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    station_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    barcode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cart_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    basket_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operation_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operators",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    badge_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    pin_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operators", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_sessions_operators_operator_id",
                        column: x => x.operator_id,
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_sessions_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operation_events_occurred_at",
                table: "operation_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_operation_events_operator_id_occurred_at",
                table: "operation_events",
                columns: new[] { "operator_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_operation_events_session_id",
                table: "operation_events",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_operators_badge_code",
                table: "operators",
                column: "badge_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operators_updated_at",
                table: "operators",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_work_sessions_operator_id_started_at",
                table: "work_sessions",
                columns: new[] { "operator_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_work_sessions_started_at",
                table: "work_sessions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_work_sessions_station_id",
                table: "work_sessions",
                column: "station_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operation_events");

            migrationBuilder.DropTable(
                name: "work_sessions");

            migrationBuilder.DropTable(
                name: "operators");

            migrationBuilder.DropTable(
                name: "stations");
        }
    }
}
