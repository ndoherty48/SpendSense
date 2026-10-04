using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendSense.Migrations
{
    /// <inheritdoc />
    public partial class AddAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ToAccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "RecurringTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ToAccountId",
                table: "RecurringTransactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    CurrencyId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpeningBalance = table.Column<double>(type: "REAL", nullable: false),
                    CreditLimit = table.Column<double>(type: "REAL", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                    IncludeInAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Seed the default account, then backfill. This is the HasData row for Main account, written
            // as SQL so it still works if currency 1 was deleted (it falls back to the default currency).
            migrationBuilder.Sql("""
                INSERT INTO Accounts (Id, Name, Type, CurrencyId, OpeningBalance, CreditLimit, Color, IncludeInAvailable, IsDefault, IsArchived, SortOrder, CreatedAt, UpdatedAt)
                VALUES (1, 'Main account', 'Current',
                        COALESCE((SELECT Id FROM Currencies WHERE Id = 1),
                                 (SELECT Id FROM Currencies WHERE IsDefault = 1 ORDER BY Id LIMIT 1),
                                 (SELECT MIN(Id) FROM Currencies)),
                        0.0, NULL, NULL, 1, 1, 0, 0, '0001-01-01 00:00:00', '0001-01-01 00:00:00');
                """);

            // An account has one currency, so rows in any other currency get their own
            // "Main account (EUR)" rather than being silently re-denominated.
            migrationBuilder.Sql("""
                INSERT INTO Accounts (Name, Type, CurrencyId, OpeningBalance, IncludeInAvailable, IsDefault, IsArchived, SortOrder, CreatedAt, UpdatedAt)
                SELECT 'Main account (' || c.Code || ')', 'Current', c.Id, 0.0, 1, 0, 0, 0, datetime('now'), datetime('now')
                FROM Currencies c
                WHERE c.Id <> (SELECT CurrencyId FROM Accounts WHERE Id = 1)
                  AND (c.Id IN (SELECT CurrencyId FROM Transactions) OR c.Id IN (SELECT CurrencyId FROM RecurringTransactions));
                """);

            // Lowest-Id account in the row's currency: Main account, or the per-currency one above.
            migrationBuilder.Sql("""
                UPDATE Transactions
                SET AccountId = COALESCE((SELECT a.Id FROM Accounts a WHERE a.CurrencyId = Transactions.CurrencyId ORDER BY a.Id LIMIT 1), 1);
                """);
            migrationBuilder.Sql("""
                UPDATE RecurringTransactions
                SET AccountId = COALESCE((SELECT a.Id FROM Accounts a WHERE a.CurrencyId = RecurringTransactions.CurrencyId ORDER BY a.Id LIMIT 1), 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ToAccountId",
                table: "Transactions",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_AccountId",
                table: "RecurringTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_ToAccountId",
                table: "RecurringTransactions",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CurrencyId",
                table: "Accounts",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Name",
                table: "Accounts",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Accounts_AccountId",
                table: "RecurringTransactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Accounts_ToAccountId",
                table: "RecurringTransactions",
                column: "ToAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_ToAccountId",
                table: "Transactions",
                column: "ToAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite rebuilds Transactions/RecurringTransactions (to drop their FKs) after every other
            // operation, so dropping Accounts would otherwise fail on rows that still reference it. The
            // rebuild step turns foreign keys back on when it finishes.
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Accounts_AccountId",
                table: "RecurringTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Accounts_ToAccountId",
                table: "RecurringTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_ToAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ToAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_AccountId",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_ToAccountId",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "RecurringTransactions");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "RecurringTransactions");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
