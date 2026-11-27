using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalAccountSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historical_settlements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    Sheet = table.Column<string>(type: "text", nullable: false),
                    Cell = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(22,6)", precision: 22, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Party = table.Column<string>(type: "text", nullable: false),
                    Narrative = table.Column<string>(type: "text", nullable: false),
                    SourceJson = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    ContractorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_settlements_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historical_settlements_historical_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historical_settlements_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historical_settlements_BatchId_Sheet_Cell",
                table: "historical_settlements",
                columns: new[] { "BatchId", "Sheet", "Cell" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_settlements_ContractorId",
                table: "historical_settlements",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_historical_settlements_SupplierId",
                table: "historical_settlements",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT FROM historical_settlements) THEN
                        RAISE EXCEPTION 'Restore the recovery backup before reversing populated settlements';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "historical_settlements");
        }
    }
}
