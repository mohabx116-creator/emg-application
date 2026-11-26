using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMG.Infrastructure.Db.Migrations;

public partial class AdvancePaymentCurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "Currency", table: "advance_payments", type: "character varying(10)", maxLength: 10,
        nullable: false, defaultValue: "EGP");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "Currency", table: "advance_payments");
}
