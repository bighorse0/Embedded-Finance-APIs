using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace CoreBanking;

public class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountAuditLog> AccountAuditLogs => Set<AccountAuditLog>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionAuditLog> TransactionAuditLogs => Set<TransactionAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Add further configuration as needed
    }
} 