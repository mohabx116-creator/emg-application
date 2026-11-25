using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class WorkAccountCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "steel_records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "الحديد");

            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "finishing_records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "غير محدد");

            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "excavation_records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "العرب");

            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "concrete_records",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "المقاول");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "steel_records");

            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "finishing_records");

            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "excavation_records");

            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "concrete_records");
        }
    }
}
