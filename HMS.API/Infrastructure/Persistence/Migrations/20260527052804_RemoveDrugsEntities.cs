using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDrugsEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItems_Drugs_DrugId",
                table: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "Drugs");

            migrationBuilder.RenameColumn(
                name: "DrugId",
                table: "Reservations",
                newName: "InventoryItemId");

            migrationBuilder.RenameColumn(
                name: "DrugId",
                table: "PrescriptionItems",
                newName: "InventoryItemId");

            migrationBuilder.RenameIndex(
                name: "IX_PrescriptionItems_DrugId",
                table: "PrescriptionItems",
                newName: "IX_PrescriptionItems_InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_InventoryItemId",
                table: "Reservations",
                column: "InventoryItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_InventoryItems_InventoryItemId",
                table: "PrescriptionItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItems_InventoryItems_InventoryItemId",
                table: "PrescriptionItems");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_InventoryItemId",
                table: "Reservations");

            migrationBuilder.RenameColumn(
                name: "InventoryItemId",
                table: "Reservations",
                newName: "DrugId");

            migrationBuilder.RenameColumn(
                name: "InventoryItemId",
                table: "PrescriptionItems",
                newName: "DrugId");

            migrationBuilder.RenameIndex(
                name: "IX_PrescriptionItems_InventoryItemId",
                table: "PrescriptionItems",
                newName: "IX_PrescriptionItems_DrugId");

            migrationBuilder.CreateTable(
                name: "Drugs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsSynced = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReservedStock = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    SyncVersion = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Code",
                table: "Drugs",
                column: "Code");

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_Drugs_DrugId",
                table: "PrescriptionItems",
                column: "DrugId",
                principalTable: "Drugs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
