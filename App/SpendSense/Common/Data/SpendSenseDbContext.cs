
using Microsoft.EntityFrameworkCore;
using SpendSense.Common.Models;
using SpendSense.Common.Models.Enums;

namespace SpendSense.Common.Data;

public class SpendSenseDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<MonthlyBudget> MonthlyBudgets { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Goal> Goals { get; set; }
    public DbSet<RecurringTransaction> RecurringTransactions { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TransactionTag> TransactionTags { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<AlertNotification> AlertNotifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransactionTag>()
            .HasOne(tt => tt.Transaction)
            .WithMany(tt => tt.TransactionTags)
            .HasForeignKey(tt => tt.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TransactionTag>()
            .HasOne(tt => tt.Tag)
            .WithMany(tt => tt.TransactionTags)
            .HasForeignKey(tt => tt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Currency>()
            .HasData(new Currency
            {
                Id = 1,
                Code = "GBP",
                ExchangeRate = 1,
                Symbol = "£",
                IsDefault = true,
                Name = "Great British Pound"
            });

        // Every transaction needs an account, so a fresh install starts with one. The AddAccounts
        // migration assigns all pre-existing transactions to it.
        modelBuilder.Entity<Account>()
            .HasData(new Account
            {
                Id = 1,
                Name = "Main account",
                Type = AccountTypeEnum.Current,
                CurrencyId = 1,
                IncludeInAvailable = true,
                IsDefault = true
            });

        modelBuilder.Entity<Account>()
            .HasOne(a => a.Currency)
            .WithMany(c => c.Accounts)
            .HasForeignKey(a => a.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Pots: an account inside another. Restrict, so a parent with pots can't be deleted.
        modelBuilder.Entity<Account>()
            .HasOne(a => a.ParentAccount)
            .WithMany(a => a.Pots)
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two relationships between Transaction and Account: the source and the destination.
        // Restrict, so an account in use can't be deleted (archive it instead).
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.ToAccount)
            .WithMany(a => a.IncomingTransfers)
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecurringTransaction>()
            .HasOne(r => r.Account)
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecurringTransaction>()
            .HasOne(r => r.ToAccount)
            .WithMany()
            .HasForeignKey(r => r.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MonthlyBudget>()
            .HasIndex(mb=> new {mb.Year, mb.Month, mb.CategoryId, mb.CurrencyId})
            .IsUnique();

        modelBuilder.Entity<Account>()
            .HasIndex(a=>a.Name)
            .IsUnique();

        modelBuilder.Entity<Category>()
            .HasIndex(c=>c.Name)
            .IsUnique();

        modelBuilder.Entity<Tag>()
            .HasIndex(t=>t.Name)
            .IsUnique();

        modelBuilder.Entity<Currency>()
            .HasIndex(c=>c.Code)
            .IsUnique();
        
        modelBuilder.Entity<Category>()
            .Property(p=>p.Type)
            .HasConversion(v=> v.ToString(), v=> Enum.Parse<TransactionTypeEnum>(v));
        modelBuilder.Entity<Goal>()
            .Property(p=>p.Priority)
            .HasConversion(v=>v.ToString(), v=>Enum.Parse<PriorityEnum>(v));
        modelBuilder.Entity<Goal>()
            .Property(p=>p.Status)
            .HasConversion(v=>v.ToString(), v=>Enum.Parse<StatusEnum>(v));
        modelBuilder.Entity<Transaction>()
            .Property(p=>p.TransactionType)
            .HasConversion(v=>v.ToString(), v=>Enum.Parse<TransactionTypeEnum>(v));
        modelBuilder.Entity<RecurringTransaction>()
            .Property(p=>p.Frequency)
            .HasConversion(v=>v.ToString(), v=>Enum.Parse<FrequencyEnum>(v));
        modelBuilder.Entity<RecurringTransaction>()
            .Property(p=>p.TransactionType)
            .HasConversion(v=>v.ToString(), v=>Enum.Parse<TransactionTypeEnum>(v));
        modelBuilder.Entity<Account>()
            .Property(p=>p.Type)
            .HasConversion(v=>v.ToString(), v=>Enum.Parse<AccountTypeEnum>(v));
    }
}