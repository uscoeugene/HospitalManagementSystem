using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalUsersObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LocalUser",
                table: "LocalUser");

            migrationBuilder.RenameTable(
                name: "LocalUser",
                newName: "LocalUsers");

            migrationBuilder.RenameIndex(
                name: "IX_LocalUser_Username",
                table: "LocalUsers",
                newName: "IX_LocalUsers_Username");

            migrationBuilder.RenameIndex(
                name: "IX_LocalUser_TenantId",
                table: "LocalUsers",
                newName: "IX_LocalUsers_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LocalUsers",
                table: "LocalUsers",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "LocalPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_LocalPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_LocalRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalPermissions_TenantId",
                table: "LocalPermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalRoles_TenantId",
                table: "LocalRoles",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalPermissions");

            migrationBuilder.DropTable(
                name: "LocalRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LocalUsers",
                table: "LocalUsers");

            migrationBuilder.RenameTable(
                name: "LocalUsers",
                newName: "LocalUser");

            migrationBuilder.RenameIndex(
                name: "IX_LocalUsers_Username",
                table: "LocalUser",
                newName: "IX_LocalUser_Username");

            migrationBuilder.RenameIndex(
                name: "IX_LocalUsers_TenantId",
                table: "LocalUser",
                newName: "IX_LocalUser_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LocalUser",
                table: "LocalUser",
                column: "Id");
        }
    }
}
