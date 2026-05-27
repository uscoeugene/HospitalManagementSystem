using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePresciptionModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItems_InventoryItems_InventoryItemId",
                table: "PrescriptionItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "PrescriptionItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Dosage",
                table: "PrescriptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "PrescriptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                table: "PrescriptionItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSubstituted",
                table: "PrescriptionItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MedicationName",
                table: "PrescriptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortageReason",
                table: "PrescriptionItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubstituteMedicationName",
                table: "PrescriptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "DispenseLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicationName",
                table: "DispenseLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "DispenseLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_InventoryItems_InventoryItemId",
                table: "PrescriptionItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItems_InventoryItems_InventoryItemId",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "Dosage",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "IsSubstituted",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "MedicationName",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "ShortageReason",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "SubstituteMedicationName",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "DispenseLogs");

            migrationBuilder.DropColumn(
                name: "MedicationName",
                table: "DispenseLogs");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "DispenseLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "PrescriptionItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_InventoryItems_InventoryItemId",
                table: "PrescriptionItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
