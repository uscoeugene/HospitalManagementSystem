using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSerilogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Logs table only if it does not already exist to avoid conflicts when table pre-exists
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Logs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Logs] (
        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [TimeStamp] datetime2 NOT NULL,
        [Level] nvarchar(128) NULL,
        [Message] nvarchar(max) NULL,
        [Exception] nvarchar(max) NULL,
        [Properties] nvarchar(max) NULL,
        [LogEvent] nvarchar(max) NULL
    );
    CREATE INDEX [IX_Logs_Level] ON [dbo].[Logs]([Level]);
    CREATE INDEX [IX_Logs_TimeStamp] ON [dbo].[Logs]([TimeStamp]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop Logs table only if it exists
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Logs]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[Logs];
END
");
        }
    }
}
