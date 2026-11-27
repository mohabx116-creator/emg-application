using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalWorkLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historical_work_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    Sheet = table.Column<string>(type: "text", nullable: false),
                    Cell = table.Column<string>(type: "text", nullable: false),
                    SourceRow = table.Column<int>(type: "integer", nullable: false),
                    Family = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Basis = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(22,6)", precision: 22, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    Rate = table.Column<decimal>(type: "numeric", nullable: true),
                    Progress = table.Column<decimal>(type: "numeric", nullable: true),
                    Party = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    ContractorId = table.Column<int>(type: "integer", nullable: true),
                    SourceJson = table.Column<string>(type: "text", nullable: false),
                    OperationalTable = table.Column<string>(type: "text", nullable: true),
                    OperationalId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_work_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_work_lines_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historical_work_lines_historical_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historical_work_lines_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historical_work_lines_BatchId_Sheet_Cell",
                table: "historical_work_lines",
                columns: new[] { "BatchId", "Sheet", "Cell" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_work_lines_ContractorId",
                table: "historical_work_lines",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_historical_work_lines_OperationalTable_OperationalId",
                table: "historical_work_lines",
                columns: new[] { "OperationalTable", "OperationalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_work_lines_SupplierId",
                table: "historical_work_lines",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT FROM historical_work_lines) THEN
                        RAISE EXCEPTION 'Restore the recovery backup before reversing a populated work ledger';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "historical_work_lines");
        }
    }
}
