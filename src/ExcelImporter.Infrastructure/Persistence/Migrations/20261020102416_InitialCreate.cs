using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelImporter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConfigKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportTemplate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConnectionId = table.Column<int>(type: "int", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SheetName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HeaderRow = table.Column<int>(type: "int", nullable: false),
                    ColumnsFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConstantsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportTemplate_ConnectionConfig_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "ConnectionConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportJob",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ConnectionId = table.Column<int>(type: "int", nullable: false),
                    TargetTable = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ImportedRows = table.Column<int>(type: "int", nullable: false),
                    SkippedRows = table.Column<int>(type: "int", nullable: false),
                    ErrorRows = table.Column<int>(type: "int", nullable: false),
                    ErrorReportPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJob", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportJob_ConnectionConfig_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "ConnectionConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportJob_ImportTemplate_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ImportTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ImportError",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportError", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportError_ImportJob_JobId",
                        column: x => x.JobId,
                        principalTable: "ImportJob",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionConfig_Name",
                table: "ConnectionConfig",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportError_JobId",
                table: "ImportError",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJob_ConnectionId",
                table: "ImportJob",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJob_StartedAt",
                table: "ImportJob",
                column: "StartedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ImportJob_TemplateId",
                table: "ImportJob",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportTemplate_ConnectionId",
                table: "ImportTemplate",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportTemplate_Name",
                table: "ImportTemplate",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportError");

            migrationBuilder.DropTable(
                name: "ImportJob");

            migrationBuilder.DropTable(
                name: "ImportTemplate");

            migrationBuilder.DropTable(
                name: "ConnectionConfig");
        }
    }
}
