using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class Advances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "advance_payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    PayerName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_advance_payments_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "advance_allocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdvancePaymentId = table.Column<int>(type: "integer", nullable: false),
                    SiteMovementId = table.Column<int>(type: "integer", nullable: true),
                    SupplierPaymentId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_advance_allocations_advance_payments_AdvancePaymentId",
                        column: x => x.AdvancePaymentId,
                        principalTable: "advance_payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_advance_allocations_site_movements_SiteMovementId",
                        column: x => x.SiteMovementId,
                        principalTable: "site_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_advance_allocations_supplier_payments_SupplierPaymentId",
                        column: x => x.SupplierPaymentId,
                        principalTable: "supplier_payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_advance_allocations_AdvancePaymentId",
                table: "advance_allocations",
                column: "AdvancePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_advance_allocations_SiteMovementId",
                table: "advance_allocations",
                column: "SiteMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_advance_allocations_SupplierPaymentId",
                table: "advance_allocations",
                column: "SupplierPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_advance_payments_ProjectId_PaymentDate",
                table: "advance_payments",
                columns: new[] { "ProjectId", "PaymentDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advance_allocations");

            migrationBuilder.DropTable(
                name: "advance_payments");
        }
    }
}
