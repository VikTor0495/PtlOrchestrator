using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PtlOrchestrator.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    controller_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    controller_port = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carts", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "cart_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    station_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_cart_assignments_carts_cart_code",
                        column: x => x.cart_code,
                        principalTable: "carts",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cart_assignments_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cart_baskets",
                columns: table => new
                {
                    cart_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    basket_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_baskets", x => new { x.cart_code, x.basket_id });
                    table.ForeignKey(
                        name: "fk_cart_baskets_carts_cart_code",
                        column: x => x.cart_code,
                        principalTable: "carts",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cart_assignments_station_id",
                table: "cart_assignments",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ux_cart_assignments_open",
                table: "cart_assignments",
                column: "cart_code",
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cart_baskets_basket_id",
                table: "cart_baskets",
                column: "basket_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_assignments");

            migrationBuilder.DropTable(
                name: "cart_baskets");

            migrationBuilder.DropTable(
                name: "carts");
        }
    }
}
