using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class SupplierWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "supplier_supplies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "supplier_supplies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "مسودة");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "supplier_payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "supplier_payments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "مسودة");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "supplier_supplies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "supplier_supplies");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "supplier_payments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "supplier_payments");
        }
    }
}
