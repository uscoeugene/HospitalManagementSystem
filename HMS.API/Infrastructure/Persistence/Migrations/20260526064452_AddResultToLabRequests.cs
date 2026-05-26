using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResultToLabRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbnormalFlag",
                table: "LabRequestItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceRange",
                table: "LabRequestItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultAttachmentUrl",
                table: "LabRequestItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultNotes",
                table: "LabRequestItems",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultStatus",
                table: "LabRequestItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResultUnit",
                table: "LabRequestItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultValue",
                table: "LabRequestItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResultedAt",
                table: "LabRequestItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultedByUserId",
                table: "LabRequestItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                table: "LabRequestItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "LabRequestItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_ResultStatus",
                table: "LabRequestItems",
                column: "ResultStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabRequestItems_ResultStatus",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "AbnormalFlag",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ReferenceRange",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultAttachmentUrl",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultNotes",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultStatus",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultUnit",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultValue",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultedAt",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "ResultedByUserId",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "LabRequestItems");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "LabRequestItems");
        }
    }
}
