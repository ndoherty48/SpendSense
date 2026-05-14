
using Microsoft.EntityFrameworkCore;

using SpendSense.Common.Models;

namespace SpendSense.Common.Data;

public class SpendSenseDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<MonthlyBudget> MonthlyBudgets { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Currency> Currencies { get; set; }
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

        modelBuilder.Entity<MonthlyBudget>()
            .HasIndex(mb=> new {mb.Year, mb.Month, mb.CategoryId, mb.CurrencyId})
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
    }
}