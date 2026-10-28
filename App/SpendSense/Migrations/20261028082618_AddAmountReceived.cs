using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendSense.Migrations;

/// <inheritdoc />
public partial class AddAmountReceived : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExchangeRate",
            table: "Currencies");

        migrationBuilder.AddColumn<double>(
            name: "AmountReceived",
            table: "Transactions",
            type: "REAL",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "AmountReceived",
            table: "RecurringTransactions",
            type: "REAL",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AmountReceived",
            table: "Transactions");

        migrationBuilder.DropColumn(
            name: "AmountReceived",
            table: "RecurringTransactions");

        migrationBuilder.AddColumn<double>(
            name: "ExchangeRate",
            table: "Currencies",
            type: "REAL",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.UpdateData(
            table: "Currencies",
            keyColumn: "Id",
            keyValue: 1,
            column: "ExchangeRate",
            value: 1.0);
    }
}
