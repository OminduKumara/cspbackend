using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tmsserver.Migrations
{
    /// <inheritdoc />
    public partial class InitialAzureMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 10, 48, 27, 694, DateTimeKind.Utc).AddTicks(1340));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 10, 48, 27, 694, DateTimeKind.Utc).AddTicks(2180));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 10, 48, 27, 694, DateTimeKind.Utc).AddTicks(2180));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 10, 48, 27, 694, DateTimeKind.Utc).AddTicks(2180));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 9, 19, 26, 468, DateTimeKind.Utc).AddTicks(330));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 9, 19, 26, 468, DateTimeKind.Utc).AddTicks(1150));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 9, 19, 26, 468, DateTimeKind.Utc).AddTicks(1150));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAt",
                value: new DateTime(2026, 3, 5, 9, 19, 26, 468, DateTimeKind.Utc).AddTicks(1150));
        }
    }
}
