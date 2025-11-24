using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerificationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingOrders_ReservedAt",
                table: "PendingOrders",
                column: "ReservedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingOrders");
        }
    }
}
