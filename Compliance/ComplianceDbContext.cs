using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Compliance;

public class ComplianceDbContext : DbContext
{
    public ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : base(options) { }

    public DbSet<KYCProfile> KYCProfiles => Set<KYCProfile>();
    public DbSet<KYCAuditLog> KYCAuditLogs => Set<KYCAuditLog>();
    public DbSet<AMLAlert> AMLAlerts => Set<AMLAlert>();
    public DbSet<AMLAlertAuditLog> AMLAlertAuditLogs => Set<AMLAlertAuditLog>();
    public DbSet<RegulatoryReport> RegulatoryReports => Set<RegulatoryReport>();
    public DbSet<RegulatoryReportAuditLog> RegulatoryReportAuditLogs => Set<RegulatoryReportAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Add further configuration as needed
    }
} 