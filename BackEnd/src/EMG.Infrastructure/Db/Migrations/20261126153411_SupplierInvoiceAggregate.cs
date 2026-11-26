using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class SupplierInvoiceAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InvoiceId",
                table: "supplier_supplies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceId",
                table: "supplier_payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SiteMovementId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_site_movements_SiteMovementId",
                        column: x => x.SiteMovementId,
                        principalTable: "site_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    SupplierSupplyId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_invoice_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_supplier_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "supplier_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_supplier_supplies_SupplierSupplyId",
                        column: x => x.SupplierSupplyId,
                        principalTable: "supplier_supplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_supplies_InvoiceId",
                table: "supplier_supplies",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_payments_InvoiceId",
                table: "supplier_payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_InvoiceId",
                table: "supplier_invoice_lines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_SupplierSupplyId",
                table: "supplier_invoice_lines",
                column: "SupplierSupplyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_ProjectId_InvoiceNumber",
                table: "supplier_invoices",
                columns: new[] { "ProjectId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_SiteMovementId",
                table: "supplier_invoices",
                column: "SiteMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_SupplierId_ProjectId_InvoiceDate",
                table: "supplier_invoices",
                columns: new[] { "SupplierId", "ProjectId", "InvoiceDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payments_supplier_invoices_InvoiceId",
                table: "supplier_payments",
                column: "InvoiceId",
                principalTable: "supplier_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_supplies_supplier_invoices_InvoiceId",
                table: "supplier_supplies",
                column: "InvoiceId",
                principalTable: "supplier_invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_supplier_payments_supplier_invoices_InvoiceId",
                table: "supplier_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_supplies_supplier_invoices_InvoiceId",
                table: "supplier_supplies");

            migrationBuilder.DropTable(
                name: "supplier_invoice_lines");

            migrationBuilder.DropTable(
                name: "supplier_invoices");

            migrationBuilder.DropIndex(
                name: "IX_supplier_supplies_InvoiceId",
                table: "supplier_supplies");

            migrationBuilder.DropIndex(
                name: "IX_supplier_payments_InvoiceId",
                table: "supplier_payments");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "supplier_supplies");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "supplier_payments");
        }
    }
}
