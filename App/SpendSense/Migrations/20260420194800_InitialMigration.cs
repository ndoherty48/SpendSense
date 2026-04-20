using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendSense.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PeriodStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<double>(type: "REAL", nullable: true),
                    MonthlyBudgetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_Budgets_MonthlyBudgetId",
                        column: x => x.MonthlyBudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incomes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<double>(type: "REAL", nullable: true),
                    IncomeDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MonthlyBudgetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incomes_Budgets_MonthlyBudgetId",
                        column: x => x.MonthlyBudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Savings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<double>(type: "REAL", nullable: true),
                    MonthlyBudgetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Savings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Savings_Budgets_MonthlyBudgetId",
                        column: x => x.MonthlyBudgetId,
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<double>(type: "REAL", nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ExpenseId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseTransactions_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavingsTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<double>(type: "REAL", nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SavingsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsTransactions_Savings_SavingsId",
                        column: x => x.SavingsId,
                        principalTable: "Savings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_MonthlyBudgetId",
                table: "Expenses",
                column: "MonthlyBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTransactions_ExpenseId",
                table: "ExpenseTransactions",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_MonthlyBudgetId",
                table: "Incomes",
                column: "MonthlyBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_Savings_MonthlyBudgetId",
                table: "Savings",
                column: "MonthlyBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsTransactions_SavingsId",
                table: "SavingsTransactions",
                column: "SavingsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseTransactions");

            migrationBuilder.DropTable(
                name: "Incomes");

            migrationBuilder.DropTable(
                name: "SavingsTransactions");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Savings");

            migrationBuilder.DropTable(
                name: "Budgets");
        }
    }
}
