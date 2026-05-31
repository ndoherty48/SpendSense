using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendSense.Migrations
{
    /// <inheritdoc />
    public partial class FixEmptyTransactionTypeOnRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE RecurringTransactions SET TransactionType = 'Expense' WHERE TransactionType = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
