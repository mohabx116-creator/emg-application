using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AdvanceAllocationAccountCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "advance_allocations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "غير محدد");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "advance_allocations");
        }
    }
}
