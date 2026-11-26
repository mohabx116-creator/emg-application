using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalOperationalReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentSource",
                table: "supplier_payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "مكتب");

            migrationBuilder.AddColumn<int>(
                name: "ContractorId",
                table: "site_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartyType",
                table: "site_movements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "بدون طرف");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "site_movements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "EGP");

            migrationBuilder.AddColumn<string>(
                name: "FundingSource",
                table: "site_movements",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "site_movements",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCategory",
                table: "site_movements",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "historical_opening_balances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    StreamKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_opening_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historical_opening_balances_historical_import_batches_Batch~",
                        column: x => x.BatchId,
                        principalTable: "historical_import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_historical_opening_balances_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_work_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Classification = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_work_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_work_categories_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_movements_ContractorId",
                table: "site_movements",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_historical_opening_balances_BatchId",
                table: "historical_opening_balances",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_historical_opening_balances_ProjectId_BatchId_StreamKey_Cur~",
                table: "historical_opening_balances",
                columns: new[] { "ProjectId", "BatchId", "StreamKey", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_work_categories_ProjectId_Name",
                table: "project_work_categories",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_site_movements_contractors_ContractorId",
                table: "site_movements",
                column: "ContractorId",
                principalTable: "contractors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_site_movements_contractors_ContractorId",
                table: "site_movements");

            migrationBuilder.DropTable(
                name: "historical_opening_balances");

            migrationBuilder.DropTable(
                name: "project_work_categories");

            migrationBuilder.DropIndex(
                name: "IX_site_movements_ContractorId",
                table: "site_movements");

            migrationBuilder.DropColumn(
                name: "PaymentSource",
                table: "supplier_payments");

            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "site_movements");

            migrationBuilder.DropColumn(
                name: "CounterpartyType",
                table: "site_movements");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "site_movements");

            migrationBuilder.DropColumn(
                name: "FundingSource",
                table: "site_movements");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "site_movements");

            migrationBuilder.DropColumn(
                name: "WorkCategory",
                table: "site_movements");
        }
    }
}
