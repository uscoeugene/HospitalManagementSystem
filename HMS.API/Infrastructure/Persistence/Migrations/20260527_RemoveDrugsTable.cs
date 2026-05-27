using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Persistence.Migrations
{
    public partial class RemoveDrugsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // drop FK from PrescriptionItems to Drugs if exists
            try
            {
                migrationBuilder.DropForeignKey(
                    name: "FK_PrescriptionItems_Drugs_DrugId",
                    table: "PrescriptionItems");
            }
            catch { }

            // drop index on DrugId
            try
            {
                migrationBuilder.DropIndex(
                    name: "IX_PrescriptionItems_DrugId",
                    table: "PrescriptionItems");
            }
            catch { }

            // drop DrugId column from PrescriptionItems if present
            try
            {
                migrationBuilder.DropColumn(
                    name: "DrugId",
                    table: "PrescriptionItems");
            }
            catch { }

            // finally drop the Drugs table
            try
            {
                migrationBuilder.DropTable(
                    name: "Drugs");
            }
            catch { }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // recreate Drugs table
            migrationBuilder.CreateTable(
                name: "Drugs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    ReservedStock = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsSynced = table.Column<bool>(type: "bit", nullable: false),
                    SyncVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_Code",
                table: "Drugs",
                column: "Code");

            // add DrugId column back to PrescriptionItems
            migrationBuilder.AddColumn<Guid>(
                name: "DrugId",
                table: "PrescriptionItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_DrugId",
                table: "PrescriptionItems",
                column: "DrugId");

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
