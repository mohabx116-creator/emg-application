using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalFundingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "PaymentDate",
                table: "advance_payments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "historical_funding_lines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    Sheet = table.Column<string>(type: "text", nullable: false),
                    Cell = table.Column<string>(type: "text", nullable: false),
                    SourceRow = table.Column<int>(type: "integer", nullable: false),
                    Stream = table.Column<string>(type: "text", nullable: false),
                    Basis = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Narrative = table.Column<string>(type: "text", nullable: false),
                    SourceJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_funding_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_funding_lines_historical_import_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historical_funding_lines_BatchId_Sheet_Cell_Stream",
                table: "historical_funding_lines",
                columns: new[] { "BatchId", "Sheet", "Cell", "Stream" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM historical_funding_lines)
                       OR EXISTS (SELECT 1 FROM advance_payments WHERE "PaymentDate" IS NULL)
                    THEN RAISE EXCEPTION 'Restore the recovery backup before reversing populated historical funding schedules';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "historical_funding_lines");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaymentDate",
                table: "advance_payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
